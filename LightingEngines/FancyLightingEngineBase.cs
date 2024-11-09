using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

internal abstract class FancyLightingEngineBase : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;
    private long _temporalData = 0;

    protected const int MaxLightRange = 64;
    protected const int DistanceTicks = 256;

    private const float MaxDecayMult = 0.95f;
    private const float LowLightLevel = 0.03f;

    protected float _initialBrightnessCutoff;
    protected float _logBrightnessCutoff;
    protected float _logBasicWorkCutoff;

    protected float _thresholdMult;
    protected float _reciprocalLogSlowestDecay;
    protected float _lightLossExitingSolid;

    private float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    private float[] _lightWaterDecay;
    private float[] _lightHoneyDecay;
    private float[] _lightNonSolidDecay;

    protected float[][] _lightMask;

    private Task[] _tasks;
    private Vec3[][] _workingLightMaps;
    private int[] _workingTemporalData;

    public void Unload() { }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    );

    protected delegate void LightingAction(
        Vec3[] workingLightMap,
        ref int workingTemporalData,
        int begin,
        int end
    );

    protected static void CalculateSubTileLightSpread(
        scoped in Span<double> x,
        scoped in Span<double> y,
        scoped ref Span<double> lightFrom,
        scoped ref Span<double> area,
        int row,
        int col
    )
    {
        var numSections = x.Length;
        var tMult = 0.5 * numSections;
        var leftX = col - 0.5;
        var rightX = col + 0.5;
        var bottomY = row - 0.5;
        var topY = row + 0.5;

        var previousT = 0.0;
        var index = 0;
        for (var i = 0; i < numSections; ++i)
        {
            var x1 = leftX + x[i];
            var y1 = bottomY + y[i];

            var slope = y1 / x1;

            double t;
            var x2 = rightX;
            var y2 = y1 + ((x2 - x1) * slope);
            if (y2 > topY)
            {
                y2 = topY;
                x2 = x1 + ((y2 - y1) / slope);
                t = tMult * (x2 - leftX);
            }
            else
            {
                t = tMult * ((topY - y2) + 1.0);
            }

            area[i] = ((topY - y1) * (x2 - leftX)) - (0.5 * (y2 - y1) * (x2 - x1));

            for (var j = 0; j < numSections; ++j)
            {
                if (j + 1 <= previousT)
                {
                    lightFrom[index++] = 0.0;
                    continue;
                }

                if (j >= t)
                {
                    lightFrom[index++] = 0.0;
                    continue;
                }

                var value = j < previousT ? j + 1 - previousT : 1.0;
                value -= j + 1 > t ? j + 1 - t : 0.0;
                lightFrom[index++] = value;
            }

            previousT = t;
        }
    }

    protected void InitializeDecayArrays()
    {
        _lightAirDecay = new float[DistanceTicks + 2];
        _lightSolidDecay = new float[DistanceTicks + 2];
        _lightWaterDecay = new float[DistanceTicks + 2];
        _lightHoneyDecay = new float[DistanceTicks + 2];
        _lightNonSolidDecay = new float[DistanceTicks + 2];
        for (var exponent = 0; exponent <= DistanceTicks; ++exponent)
        {
            _lightAirDecay[exponent] = 1f;
            _lightSolidDecay[exponent] = 1f;
            _lightWaterDecay[exponent] = 1f;
            _lightHoneyDecay[exponent] = 1f;
            _lightNonSolidDecay[exponent] = 1f;
        }

        // Last element is for diagonal decay (used in GI)
        _lightAirDecay[^1] =
            _lightSolidDecay[^1] =
            _lightWaterDecay[^1] =
            _lightHoneyDecay[^1] =
            _lightNonSolidDecay[^1] =
                MathF.Sqrt(2f);
    }

    protected void ComputeCircles()
    {
        _circles = new int[MaxLightRange + 1][];
        _circles[0] = [0];
        for (var radius = 1; radius <= MaxLightRange; ++radius)
        {
            _circles[radius] = new int[radius + 1];
            _circles[radius][0] = radius;
            var diagonal = radius / Math.Sqrt(2.0);
            for (var x = 1; x <= radius; ++x)
            {
                _circles[radius][x] =
                    x <= diagonal
                        ? (int)Math.Ceiling(Math.Sqrt((radius * radius) - (x * x)))
                        : (int)
                            Math.Floor(
                                Math.Sqrt((radius * radius) - ((x - 1) * (x - 1)))
                            );
            }
        }
    }

    protected void UpdateBrightnessCutoff(
        double temporalMult = 1.0,
        double temporalMin = 0.02,
        double temporalMax = 0.125
    )
    {
        const float BaseCutoff = 0.04f;
        const float CameraModeCutoff = 0.02f;
        const double TemporalDataDivisor = 55555.5;
        const double BaseTemporalMult = 0.02;

        temporalMult *= BaseTemporalMult;

        _initialBrightnessCutoff = LowLightLevel;

        var cutoff =
            FancyLightingMod._inCameraMode ? CameraModeCutoff
            : LightingConfig.Instance.FancyLightingEngineUseTemporal
                ? (float)
                    Math.Clamp(
                        Math.Sqrt(_temporalData / TemporalDataDivisor) * temporalMult,
                        temporalMin,
                        temporalMax
                    )
            : BaseCutoff;

        var basicWorkCutoff = BaseCutoff;

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            GammaConverter.GammaToLinear(ref _initialBrightnessCutoff);
            GammaConverter.GammaToLinear(ref cutoff);
            GammaConverter.GammaToLinear(ref basicWorkCutoff);
        }

        _logBrightnessCutoff = MathF.Log(cutoff);
        _logBasicWorkCutoff = MathF.Log(basicWorkCutoff);
    }

    protected void UpdateDecays(LightMap lightMap)
    {
        var lightAirDecayBaseline = Math.Min(lightMap.LightDecayThroughAir, MaxDecayMult);
        var lightSolidDecayBaseline = Math.Min(
            MathF.Pow(
                lightMap.LightDecayThroughSolid,
                PreferencesConfig.Instance.FancyLightingEngineAbsorptionExponent()
            ),
            MaxDecayMult
        );
        var lightWaterDecayBaseline = Math.Min(
            (0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length())
                + (
                    0.375f
                    * Math.Max(
                        lightMap.LightDecayThroughWater.X,
                        Math.Max(
                            lightMap.LightDecayThroughWater.Y,
                            lightMap.LightDecayThroughWater.Z
                        )
                    )
                ),
            MaxDecayMult
        );
        var lightHoneyDecayBaseline = Math.Min(
            (0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length())
                + (
                    0.375f
                    * Math.Max(
                        lightMap.LightDecayThroughHoney.X,
                        Math.Max(
                            lightMap.LightDecayThroughHoney.Y,
                            lightMap.LightDecayThroughHoney.Z
                        )
                    )
                ),
            MaxDecayMult
        );

        _lightLossExitingSolid =
            PreferencesConfig.Instance.FancyLightingEngineExitMultiplier();
        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            _lightLossExitingSolid *= _lightLossExitingSolid;
        }

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            GammaConverter.GammaToLinear(ref lightAirDecayBaseline);
            GammaConverter.GammaToLinear(ref lightSolidDecayBaseline);
            GammaConverter.GammaToLinear(ref lightWaterDecayBaseline);
            GammaConverter.GammaToLinear(ref lightHoneyDecayBaseline);

            GammaConverter.GammaToLinear(ref _lightLossExitingSolid);
        }

        const float ThresholdMultExponent = 0.41421354f; // sqrt(2) - 1

        var logSlowestDecay = MathF.Log(
            Math.Max(
                Math.Max(lightAirDecayBaseline, lightSolidDecayBaseline),
                Math.Max(lightWaterDecayBaseline, lightHoneyDecayBaseline)
            )
        );
        _thresholdMult = MathF.Exp(ThresholdMultExponent * logSlowestDecay);
        _reciprocalLogSlowestDecay = 1f / logSlowestDecay;

        UpdateDecay(_lightAirDecay, lightAirDecayBaseline);
        UpdateDecay(_lightSolidDecay, lightSolidDecayBaseline);
        UpdateDecay(_lightWaterDecay, lightWaterDecayBaseline);
        UpdateDecay(_lightHoneyDecay, lightHoneyDecayBaseline);

        if (!PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
        {
            Array.Copy(_lightSolidDecay, _lightNonSolidDecay, _lightSolidDecay.Length);
        }
    }

    private static void UpdateDecay(float[] decay, float baseline)
    {
        if (baseline == decay[DistanceTicks])
        {
            return;
        }

        var logBaseline = MathF.Log(baseline);
        const float ExponentMult = 1f / DistanceTicks;
        for (var i = 0; i < decay.Length; ++i)
        {
            decay[i] = MathF.Exp(ExponentMult * i * logBaseline);
        }

        decay[^1] = MathF.Exp(1.5f * logBaseline);
    }

    protected void UpdateLightMasks(LightMaskMode[] lightMasks, int width, int height)
    {
        if (PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
        {
            Parallel.For(
                0,
                width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var endIndex = height * (i + 1);
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        _lightMask[j] = lightMasks[j] switch
                        {
                            LightMaskMode.Solid => _lightSolidDecay,
                            LightMaskMode.Water => _lightWaterDecay,
                            LightMaskMode.Honey => _lightHoneyDecay,
                            _ => _lightAirDecay,
                        };
                    }
                }
            );
        }
        else
        {
            Parallel.For(
                0,
                width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var endIndex = height * (i + 1);
                    var x = i + _lightMapArea.X;
                    var y = _lightMapArea.Y;
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        _lightMask[j] = lightMasks[j] switch
                        {
                            LightMaskMode.Solid => TileUtils.IsNonSolid(x, y)
                                ? _lightNonSolidDecay
                                : _lightSolidDecay,
                            LightMaskMode.Water => _lightWaterDecay,
                            LightMaskMode.Honey => _lightHoneyDecay,
                            _ => _lightAirDecay,
                        };
                        ++y;
                    }
                }
            );
        }
    }

    protected static void ConvertLightColorsToLinear(
        Vector3[] colors,
        int width,
        int height
    ) =>
        Parallel.For(
            0,
            width,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
            },
            (x) =>
            {
                var i = height * x;
                for (var y = 0; y < height; ++y)
                {
                    GammaConverter.GammaToLinear(ref colors[i++]);
                }
            }
        );

    protected void InitializeTaskVariables(int lightMapSize)
    {
        var taskCount = PreferencesConfig.Instance.ThreadCount;

        if (_tasks is null)
        {
            _tasks = new Task[taskCount];
            _workingLightMaps = new Vec3[taskCount][];
            _workingTemporalData = new int[taskCount];

            for (var i = 0; i < taskCount; ++i)
            {
                _workingLightMaps[i] = new Vec3[lightMapSize];
            }
        }
        else if (_tasks.Length != taskCount)
        {
            _tasks = new Task[taskCount];
            _workingTemporalData = new int[taskCount];

            var workingLightMaps = new Vec3[taskCount][];
            var numToCopy = Math.Min(_workingLightMaps.Length, taskCount);

            for (var i = 0; i < numToCopy; ++i)
            {
                if (_workingLightMaps[i].Length >= lightMapSize)
                {
                    workingLightMaps[i] = _workingLightMaps[i];
                }
                else
                {
                    workingLightMaps[i] = new Vec3[lightMapSize];
                }
            }

            for (var i = numToCopy; i < taskCount; ++i)
            {
                workingLightMaps[i] = new Vec3[lightMapSize];
            }

            _workingLightMaps = workingLightMaps;
        }
        else
        {
            for (var i = 0; i < taskCount; ++i)
            {
                ArrayUtils.MakeAtLeastSize(ref _workingLightMaps[i], lightMapSize);
            }
        }
    }

    protected void RunLightingPass(
        Vector3[] initialLightMapValue,
        Vector3[] destination,
        int lightMapSize,
        bool countTemporalData,
        LightingAction lightingAction
    )
    {
        var taskCount = PreferencesConfig.Instance.ThreadCount;

        if (countTemporalData)
        {
            for (var i = 0; i < taskCount; ++i)
            {
                _workingTemporalData[i] = 0;
            }
        }

        if (taskCount <= 1)
        {
            var workingLightMap = _workingLightMaps[0];
            ref var workingTemporalData = ref _workingTemporalData[0];

            CopyVec3Array(initialLightMapValue, workingLightMap, 0, lightMapSize);

            lightingAction(workingLightMap, ref workingTemporalData, 0, lightMapSize);

            CopyVec3Array(workingLightMap, destination, 0, lightMapSize);
            _temporalData = workingTemporalData;

            return;
        }

        const int IndexIncrement = 32;

        var taskIndex = -1;
        var lightIndex = -IndexIncrement;
        for (var i = 0; i < taskCount; ++i)
        {
            _tasks[i] = Task.Factory.StartNew(() =>
            {
                var index = Interlocked.Increment(ref taskIndex);

                var workingLightMap = _workingLightMaps[index];
                ref var workingTemporalData = ref _workingTemporalData[index];

                CopyVec3Array(initialLightMapValue, workingLightMap, 0, lightMapSize);

                while (true)
                {
                    var i = Interlocked.Add(ref lightIndex, IndexIncrement);
                    if (i >= lightMapSize)
                    {
                        break;
                    }

                    lightingAction(
                        workingLightMap,
                        ref workingTemporalData,
                        i,
                        Math.Min(lightMapSize, i + IndexIncrement)
                    );
                }
            });
        }

        Task.WaitAll(_tasks);

        const int ChunkSize = 64;

        Parallel.For(
            0,
            ((lightMapSize - 1) / ChunkSize) + 1,
            new ParallelOptions { MaxDegreeOfParallelism = taskCount },
            (i) =>
            {
                var begin = ChunkSize * i;
                var end = Math.Min(lightMapSize, begin + ChunkSize);

                for (var j = 1; j < _workingLightMaps.Length; ++j)
                {
                    MaxArraysIntoFirst(
                        _workingLightMaps[0],
                        _workingLightMaps[j],
                        begin,
                        end
                    );
                }

                CopyVec3Array(_workingLightMaps[0], destination, begin, end);
            }
        );

        if (countTemporalData)
        {
            _temporalData = 0;
            for (var i = 0; i < taskCount; ++i)
            {
                _temporalData += _workingTemporalData[i];
            }
        }
    }

    private static void MaxArraysIntoFirst(Vec3[] arr1, Vec3[] arr2, int begin, int end)
    {
        for (var i = begin; i < end; ++i)
        {
            ref var value = ref arr1[i];
            value = Vec3.Max(value, arr2[i]);
        }
    }

    private static void CopyVec3Array(
        Vector3[] source,
        Vec3[] destination,
        int begin,
        int end
    )
    {
        for (var i = begin; i < end; ++i)
        {
            ref var sourceValue = ref source[i];
            ref var destinationValue = ref destination[i];
            destinationValue.X = sourceValue.X;
            destinationValue.Y = sourceValue.Y;
            destinationValue.Z = sourceValue.Z;
        }
    }

    private static void CopyVec3Array(
        Vec3[] source,
        Vector3[] destination,
        int begin,
        int end
    )
    {
        for (var i = begin; i < end; ++i)
        {
            ref var sourceValue = ref source[i];
            ref var destinationValue = ref destination[i];
            destinationValue.X = sourceValue.X;
            destinationValue.Y = sourceValue.Y;
            destinationValue.Z = sourceValue.Z;
        }
    }

    protected void SimulateGlobalIllumination(
        Vector3[] source,
        Vector3[] destination,
        int width,
        int height
    )
    {
        var length = width * height;

        var giMult =
            PreferencesConfig.Instance.FancyLightingEngineGlobalIlluminationMultiplier();
        if (LightingConfig.Instance.DoGammaCorrection())
        {
            GammaConverter.GammaToLinear(ref giMult);
        }

        ArrayUtils.MakeAtLeastSize(ref _workingLightMaps[0], length);
        var lights = _workingLightMaps[0];
        CopyVec3Array(source, lights, 0, length);

        var lightMasks = _lightMask;
        var solidDecay = _lightSolidDecay;

        Parallel.For(
            0,
            width,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
            },
            (i) =>
            {
                var endIndex = height * (i + 1);
                for (var j = height * i; j < endIndex; ++j)
                {
                    ref var giLight = ref lights[j];

                    if (lightMasks[j] == solidDecay || lightMasks[j] == solidDecay)
                    {
                        giLight = Vec3.Zero;
                        continue;
                    }

                    ref var light = ref source[j];
                    giLight.X = giMult * light.X;
                    giLight.Y = giMult * light.Y;
                    giLight.Z = giMult * light.Z;
                }
            }
        );

        for (var i = 6; i-- > 0; )
        {
            // down
            Parallel.For(
                0,
                width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var endIndex = height * (i + 1);
                    for (var j = (height * i) + 1; j < endIndex; ++j)
                    {
                        var mask = lightMasks[j - 1];
                        if (lightMasks[j] != solidDecay && mask == solidDecay)
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(value, lights[j - 1] * mask[DistanceTicks]);
                    }
                }
            );
            // up
            Parallel.For(
                0,
                width,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var endIndex = height * i;
                    for (var j = (height * (i + 1)) - 1; --j >= endIndex; )
                    {
                        var mask = lightMasks[j + 1];
                        if (lightMasks[j] != solidDecay && mask == solidDecay)
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(value, lights[j + 1] * mask[DistanceTicks]);
                    }
                }
            );
            // right
            Parallel.For(
                0,
                height,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var endIndex = i + length;
                    for (var j = i + height; j < endIndex; j += height)
                    {
                        var mask = lightMasks[j - height];
                        if (lightMasks[j] != solidDecay && mask == solidDecay)
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(value, lights[j - height] * mask[DistanceTicks]);
                    }
                }
            );
            // left
            Parallel.For(
                0,
                height,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var endIndex = i;
                    for (var j = i + (width * (height - 1)); (j -= height) >= endIndex; )
                    {
                        var mask = lightMasks[j + height];
                        if (lightMasks[j] != solidDecay && mask == solidDecay)
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(value, lights[j + height] * mask[DistanceTicks]);
                    }
                }
            );
            // down right
            Parallel.For(
                0,
                height + width - 1,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var inc = height + 1;
                    var startIndex = i < height ? i : height * (i - height + 1);
                    var endIndex = Math.Min(
                        length,
                        startIndex + (inc * (i < height ? height - i : height))
                    );
                    for (var j = startIndex + inc; j < endIndex; j += inc)
                    {
                        var mask = lightMasks[j - inc];
                        if (
                            lightMasks[j] != solidDecay
                            && (
                                mask == solidDecay
                                || lightMasks[j - 1] == solidDecay
                                || lightMasks[j - height] == solidDecay
                            )
                        )
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(
                            value,
                            lights[j - inc] * mask[DistanceTicks + 1]
                        );
                    }
                }
            );
            // up left
            Parallel.For(
                0,
                height + width - 1,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var inc = -height - 1;
                    var startIndex =
                        (length - 1) - (i < height ? i : height * (i - height + 1));
                    var endIndex = Math.Max(
                        -1,
                        startIndex + (inc * (i < height ? height - i : height))
                    );
                    for (var j = startIndex + inc; j > endIndex; j += inc)
                    {
                        var mask = lightMasks[j - inc];
                        if (
                            lightMasks[j] != solidDecay
                            && (
                                mask == solidDecay
                                || lightMasks[j + 1] == solidDecay
                                || lightMasks[j + height] == solidDecay
                            )
                        )
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(
                            value,
                            lights[j - inc] * mask[DistanceTicks + 1]
                        );
                    }
                }
            );
            // down left
            Parallel.For(
                0,
                height + width - 1,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var inc = -height + 1;
                    var startIndex =
                        ((width - 1) * height)
                        + (i < height ? i : -height * (i - height + 1));
                    var endIndex = Math.Max(
                        -1,
                        startIndex + (inc * (i < height ? height - i : height))
                    );
                    for (var j = startIndex + inc; j > endIndex; j += inc)
                    {
                        var mask = lightMasks[j - inc];
                        if (
                            lightMasks[j] != solidDecay
                            && (
                                mask == solidDecay
                                || lightMasks[j - 1] == solidDecay
                                || lightMasks[j + height] == solidDecay
                            )
                        )
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(
                            value,
                            lights[j - inc] * mask[DistanceTicks + 1]
                        );
                    }
                }
            );
            // up right
            Parallel.For(
                0,
                height + width - 1,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
                },
                (i) =>
                {
                    var inc = height - 1;
                    var startIndex =
                        (height - 1) + (i < height ? -i : height * (i - height + 1));
                    var endIndex = Math.Min(
                        length,
                        startIndex + (inc * (i < height ? height - i : height))
                    );
                    for (var j = startIndex + inc; j < endIndex; j += inc)
                    {
                        var mask = lightMasks[j - inc];
                        if (
                            lightMasks[j] != solidDecay
                            && (
                                mask == solidDecay
                                || lightMasks[j + 1] == solidDecay
                                || lightMasks[j - height] == solidDecay
                            )
                        )
                        {
                            continue;
                        }

                        ref var value = ref lights[j];
                        value = Vec3.Max(
                            value,
                            lights[j - inc] * mask[DistanceTicks + 1]
                        );
                    }
                }
            );
        }

        if (source != destination)
        {
            Array.Copy(source, destination, length);
        }

        Parallel.For(
            0,
            width,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = PreferencesConfig.Instance.ThreadCount,
            },
            (i) =>
            {
                var endIndex = height * (i + 1);
                for (var j = height * i; j < endIndex; ++j)
                {
                    ref var value = ref destination[j];
                    var max = Vec3.Max(new Vec3(value.X, value.Y, value.Z), lights[j]);
                    value.X = max.X;
                    value.Y = max.Y;
                    value.Z = max.Z;
                }
            }
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CalculateLightSourceValues(
        Vector3[] colors,
        Vec3 color,
        int index,
        int width,
        int height,
        out int upDistance,
        out int downDistance,
        out int leftDistance,
        out int rightDistance,
        out bool doUp,
        out bool doDown,
        out bool doLeft,
        out bool doRight
    )
    {
        var (x, y) = Math.DivRem(index, height);

        upDistance = y;
        downDistance = height - 1 - y;
        leftDistance = x;
        rightDistance = width - 1 - x;

        var threshold = _thresholdMult * color;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool LessThanThreshold(int otherIndex)
        {
            ref var otherColorRef = ref colors[otherIndex];
            var otherColor = new Vec3(otherColorRef.X, otherColorRef.Y, otherColorRef.Z);
            otherColor *= _lightMask[otherIndex][DistanceTicks];
            return otherColor.X < threshold.X
                || otherColor.Y < threshold.Y
                || otherColor.Z < threshold.Z;
        }

        doUp = upDistance > 0 && LessThanThreshold(index - 1);
        doDown = downDistance > 0 && LessThanThreshold(index + 1);
        doLeft = leftDistance > 0 && LessThanThreshold(index - height);
        doRight = rightDistance > 0 && LessThanThreshold(index + height);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int CalculateLightRange(Vec3 color) =>
        Math.Clamp(
            (int)
                Math.Ceiling(
                    (
                        _logBrightnessCutoff
                        - MathF.Log(Math.Max(color.X, Math.Max(color.Y, color.Z)))
                    ) * _reciprocalLogSlowestDecay
                ) + 1,
            1,
            MaxLightRange
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void SetLight(ref Vec3 light, Vec3 value) =>
        light = Vec3.Max(light, value);

    protected void SpreadLightLine(
        Vec3[] lightMap,
        Vec3 color,
        int index,
        int distance,
        int indexChange
    )
    {
        // Performance optimization
        var lightMask = _lightMask;
        var solidDecay = _lightSolidDecay;
        var lightLoss = _lightLossExitingSolid;

        index += indexChange;
        SetLight(ref lightMap[index], color);

        // Would multiply by (distance + 1), but we already incremented index once
        var endIndex = index + (distance * indexChange);
        var prevMask = lightMask[index];
        while (true)
        {
            index += indexChange;
            if (index == endIndex)
            {
                break;
            }

            var mask = lightMask[index];
            if (prevMask == solidDecay && mask != solidDecay)
            {
                color *= lightLoss * prevMask[DistanceTicks];
            }
            else
            {
                color *= prevMask[DistanceTicks];
            }

            prevMask = mask;

            SetLight(ref lightMap[index], color);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int CalculateTemporalData(
        Vec3 color,
        bool doUp,
        bool doDown,
        bool doLeft,
        bool doRight,
        bool doUpperLeft,
        bool doUpperRight,
        bool doLowerLeft,
        bool doLowerRight
    )
    {
        var baseWork = Math.Clamp(
            (int)
                Math.Ceiling(
                    (
                        _logBasicWorkCutoff
                        - MathF.Log(Math.Max(color.X, Math.Max(color.Y, color.Z)))
                    ) * _reciprocalLogSlowestDecay
                ) + 1,
            1,
            MaxLightRange
        );

        var approximateWorkDone =
            1
            + (
                ((doUp ? 1 : 0) + (doDown ? 1 : 0) + (doLeft ? 1 : 0) + (doRight ? 1 : 0))
                * baseWork
            )
            + (
                (
                    (doUpperLeft ? 1 : 0)
                    + (doUpperRight ? 1 : 0)
                    + (doLowerLeft ? 1 : 0)
                    + (doLowerRight ? 1 : 0)
                ) * (baseWork * baseWork)
            );

        return approximateWorkDone;
    }
}
