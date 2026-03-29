using System.Runtime.CompilerServices;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

internal abstract class FancyLightingEngineBase : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;
    private long _temporalData;

    protected const int MaxLightRange = 64;
    protected const int DistanceTicks = 64;

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

    protected float[][] _lightMasks;

    private Action[] _actions;
    private Vec3[][] _workingLightMaps;

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
        ref long workingTemporalData,
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
            _lightAirDecay[exponent] =
                _lightSolidDecay[exponent] =
                _lightWaterDecay[exponent] =
                _lightHoneyDecay[exponent] =
                _lightNonSolidDecay[exponent] =
                    1f;
        }

        // Last element is for diagonal decay (used in GI)
        _lightAirDecay[^1] =
            _lightSolidDecay[^1] =
            _lightWaterDecay[^1] =
            _lightHoneyDecay[^1] =
            _lightNonSolidDecay[^1] =
                1f;
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

        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            _initialBrightnessCutoff = MathF.Pow(
                _initialBrightnessCutoff,
                PostProcessing.DefaultGamma
            );
            cutoff = MathF.Pow(cutoff, PostProcessing.DefaultGamma);
            basicWorkCutoff = MathF.Pow(basicWorkCutoff, PostProcessing.DefaultGamma);
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
            ColorUtils.ApproximateLightAbsorption(lightMap.LightDecayThroughWater),
            MaxDecayMult
        );
        var lightHoneyDecayBaseline = Math.Min(
            ColorUtils.ApproximateLightAbsorption(lightMap.LightDecayThroughHoney),
            MaxDecayMult
        );

        _lightLossExitingSolid =
            PreferencesConfig.Instance.FancyLightingEngineExitMultiplier();
        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            _lightLossExitingSolid *= _lightLossExitingSolid;
        }

        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            ColorUtils.GammaToLinear(ref lightAirDecayBaseline);
            ColorUtils.GammaToLinear(ref lightSolidDecayBaseline);
            ColorUtils.GammaToLinear(ref lightWaterDecayBaseline);
            ColorUtils.GammaToLinear(ref lightHoneyDecayBaseline);

            ColorUtils.GammaToLinear(ref _lightLossExitingSolid);
        }

        var thresholdMultExponent = MathF.Sqrt(2) - 1;

        var logSlowestDecay = MathF.Log(
            Math.Max(
                Math.Max(lightAirDecayBaseline, lightSolidDecayBaseline),
                Math.Max(lightWaterDecayBaseline, lightHoneyDecayBaseline)
            )
        );
        _thresholdMult = MathF.Exp(thresholdMultExponent * logSlowestDecay);
        _reciprocalLogSlowestDecay = 1f / logSlowestDecay;

        UpdateDecay(_lightAirDecay, lightAirDecayBaseline);
        UpdateDecay(_lightSolidDecay, lightSolidDecayBaseline);
        UpdateDecay(_lightWaterDecay, lightWaterDecayBaseline);
        UpdateDecay(_lightHoneyDecay, lightHoneyDecayBaseline);

        if (
            !PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque
            && _lightNonSolidDecay[DistanceTicks] != _lightSolidDecay[DistanceTicks]
        )
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

        decay[^1] = MathF.Exp(MathF.Sqrt(2) * logBaseline);
    }

    protected void UpdateLightMasks(LightMaskMode[] lightMasks, int width, int height)
    {
        if (PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
        {
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var endIndex = height * (i + 1);
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        _lightMasks[j] = lightMasks[j] switch
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
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var endIndex = height * (i + 1);
                    var x = i + _lightMapArea.X;
                    var y = _lightMapArea.Y;
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        _lightMasks[j] = lightMasks[j] switch
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
            SettingsSystem._parallelOptions,
            (x) =>
            {
                var i = height * x;
                for (var y = 0; y < height; ++y)
                {
                    ColorUtils.GammaToLinear(ref colors[i++]);
                }
            }
        );

    protected void InitializeTaskVariables(int lightMapSize, bool doGi)
    {
        var taskCount = SettingsSystem._parallelOptions.MaxDegreeOfParallelism;
        var arrCount = doGi ? Math.Max(taskCount, 5) : taskCount;

        if (_actions?.Length != taskCount)
        {
            _actions = new Action[taskCount];
        }

        if (_workingLightMaps is null)
        {
            _workingLightMaps = new Vec3[arrCount][];

            for (var i = 0; i < arrCount; ++i)
            {
                _workingLightMaps[i] = new Vec3[lightMapSize];
            }
        }
        else if (_workingLightMaps.Length != arrCount)
        {
            var workingLightMaps = new Vec3[arrCount][];
            var copyCount = Math.Min(_workingLightMaps.Length, arrCount);

            for (var i = 0; i < copyCount; ++i)
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

            for (var i = copyCount; i < arrCount; ++i)
            {
                workingLightMaps[i] = new Vec3[lightMapSize];
            }

            _workingLightMaps = workingLightMaps;
        }
        else
        {
            for (var i = 0; i < arrCount; ++i)
            {
                ArrayUtils.MakeAtLeastSize(ref _workingLightMaps[i], lightMapSize);
            }
        }
    }

    protected void RunLightingPass(
        Vector3[] source,
        Vector3[] destination,
        int width,
        int height,
        int giPassCount,
        bool countTemporalData,
        LightingAction lightingAction
    )
    {
        PerformanceTracker.StartTiming("Fancy Lighting Engine (Direct Lighting)");

        var taskCount = SettingsSystem._parallelOptions.MaxDegreeOfParallelism;
        var length = width * height;
        var doGi = giPassCount > 0;

        if (countTemporalData)
        {
            _temporalData = 0;
        }

        if (taskCount <= 1)
        {
            var workingLightMap = _workingLightMaps[0];

            CopyVec3Array(source, workingLightMap, 0, length);

            lightingAction(workingLightMap, ref _temporalData, 0, length);

            PerformanceTracker.StopTiming("Fancy Lighting Engine (Direct Lighting)");

            if (doGi)
            {
                PerformanceTracker.StartTiming(
                    "Fancy Lighting Engine (Indirect Lighting)"
                );
                SimulateGlobalIllumination(width, height, giPassCount);
                PerformanceTracker.StopTiming(
                    "Fancy Lighting Engine (Indirect Lighting)"
                );
            }

            CopyVec3Array(_workingLightMaps[0], destination, 0, length);

            return;
        }

        const int IndexIncrement = 32;

        var lightIndex = -IndexIncrement;
        for (var i = 0; i < taskCount; ++i)
        {
            var index = i;
            _actions[i] = () =>
            {
                var workingLightMap = _workingLightMaps[index];
                var temporalData = 0L;

                CopyVec3Array(source, workingLightMap, 0, length);

                while (true)
                {
                    var i = Interlocked.Add(ref lightIndex, IndexIncrement);
                    if (i >= length)
                    {
                        break;
                    }

                    lightingAction(
                        workingLightMap,
                        ref temporalData,
                        i,
                        Math.Min(length, i + IndexIncrement)
                    );
                }

                if (countTemporalData)
                {
                    Interlocked.Add(ref _temporalData, temporalData);
                }
            };
        }

        Parallel.Invoke(SettingsSystem._parallelOptions, _actions);

        const int ChunkSize = 64;

        Parallel.For(
            0,
            ((length - 1) / ChunkSize) + 1,
            SettingsSystem._parallelOptions,
            (i) =>
            {
                var begin = ChunkSize * i;
                var end = Math.Min(length, begin + ChunkSize);

                for (var j = 1; j < _workingLightMaps.Length; ++j)
                {
                    MaxArraysIntoFirst(
                        _workingLightMaps[0],
                        _workingLightMaps[j],
                        begin,
                        end
                    );
                }

                if (!doGi)
                {
                    CopyVec3Array(_workingLightMaps[0], destination, begin, end);
                }
            }
        );

        PerformanceTracker.StopTiming("Fancy Lighting Engine (Direct Lighting)");

        if (doGi)
        {
            PerformanceTracker.StartTiming("Fancy Lighting Engine (Indirect Lighting)");
            SimulateGlobalIllumination(width, height, giPassCount);
            PerformanceTracker.StopTiming("Fancy Lighting Engine (Indirect Lighting)");

            Parallel.For(
                0,
                ((length - 1) / ChunkSize) + 1,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var begin = ChunkSize * i;
                    var end = Math.Min(length, begin + ChunkSize);
                    CopyVec3Array(_workingLightMaps[0], destination, begin, end);
                }
            );
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

    private void SimulateGlobalIllumination(int width, int height, int passCount)
    {
        var giMult =
            PreferencesConfig.Instance.FancyLightingEngineGlobalIlluminationMultiplier();
        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            ColorUtils.GammaToLinear(ref giMult);
        }

        var lights = _workingLightMaps[0];
        var leftLights = _workingLightMaps[1];
        var upLights = _workingLightMaps[2];
        var rightLights = _workingLightMaps[3];
        var downLights = _workingLightMaps[4];

        for (var i = passCount; i-- > 0; )
        {
            // get GI lights
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var src = lights;
                    var dst = downLights;
                    var lightMasks = _lightMasks;
                    var solidDecay = _lightSolidDecay;

                    var endIndex = height * (i + 1);
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        if (lightMasks[j] == solidDecay)
                        {
                            dst[j] = Vec3.Zero;
                            continue;
                        }

                        dst[j] = Vec3.Multiply(giMult, src[j]);
                    }
                }
            );

            static void RunAxialSpread(
                Vec3[] src,
                Vec3[] dst,
                float[][] lightMasks,
                float[] solidDecay,
                int beginIndex,
                int endIndex,
                int inc
            )
            {
                var prevMask = lightMasks[beginIndex];
                var light = dst[beginIndex] = src[beginIndex];
                var j = beginIndex;
                while (j != endIndex)
                {
                    j += inc;

                    var mask = lightMasks[j];
                    if (mask != solidDecay && prevMask == solidDecay)
                    {
                        light = src[j];
                    }
                    else
                    {
                        light = Vec3.Max(
                            src[j],
                            Vec3.Multiply(prevMask[DistanceTicks], light)
                        );
                    }

                    prevMask = mask;
                    dst[j] = light;
                }
            }

            static void RunDiagonalSpread(
                Vec3[] src1,
                Vec3[] src2,
                Vec3[] dst,
                float[][] lightMasks,
                float[] solidDecay,
                int beginIndex,
                int endIndex,
                int inc,
                int diff1,
                int diff2
            )
            {
                var prevMask = lightMasks[beginIndex];
                var light = Vec3.Max(src1[beginIndex], src2[beginIndex]);
                var j = beginIndex;
                while (j != endIndex)
                {
                    j += inc;

                    var mask = lightMasks[j];
                    var srcLight = Vec3.Max(src1[j], src2[j]);
                    if (
                        mask != solidDecay
                        && (
                            prevMask == solidDecay
                            || (
                                lightMasks[j + diff1] == solidDecay
                                && lightMasks[j + diff2] == solidDecay
                            )
                        )
                    )
                    {
                        light = srcLight;
                    }
                    else
                    {
                        light = Vec3.Max(
                            srcLight,
                            Vec3.Multiply(prevMask[DistanceTicks + 1], light)
                        );
                    }

                    prevMask = mask;
                    ref var dstLight = ref dst[j];
                    dstLight = Vec3.Max(dstLight, light);
                }
            }

            // left
            Parallel.For(
                0,
                height,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunAxialSpread(
                        downLights,
                        leftLights,
                        _lightMasks,
                        _lightSolidDecay,
                        i + (height * (width - 1)),
                        i,
                        -height
                    );
                }
            );
            // up
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunAxialSpread(
                        downLights,
                        upLights,
                        _lightMasks,
                        _lightSolidDecay,
                        (height * (i + 1)) - 1,
                        height * i,
                        -1
                    );
                }
            );
            // right
            Parallel.For(
                0,
                height,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunAxialSpread(
                        downLights,
                        rightLights,
                        _lightMasks,
                        _lightSolidDecay,
                        i,
                        i + (height * (width - 1)),
                        height
                    );
                }
            );
            // down
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunAxialSpread(
                        downLights,
                        downLights,
                        _lightMasks,
                        _lightSolidDecay,
                        height * i,
                        (height * (i + 1)) - 1,
                        1
                    );
                }
            );

            // up left
            Parallel.For(
                -height + 2,
                width - 1,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunDiagonalSpread(
                        leftLights,
                        upLights,
                        lights,
                        _lightMasks,
                        _lightSolidDecay,
                        Math.Min(
                            (height * (i + height)) - 1,
                            -i + ((height + 1) * (width - 1))
                        ),
                        Math.Max(height * i, -i),
                        -height - 1,
                        1,
                        height
                    );
                }
            );
            // up right
            Parallel.For(
                -height + 2,
                width - 1,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunDiagonalSpread(
                        upLights,
                        rightLights,
                        lights,
                        _lightMasks,
                        _lightSolidDecay,
                        Math.Max((height * (i + 1)) - 1, i + height - 1),
                        Math.Min(height * (i + height - 1), i + ((height - 1) * width)),
                        height - 1,
                        1,
                        -height
                    );
                }
            );
            // down right
            Parallel.For(
                -height + 2,
                width - 1,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunDiagonalSpread(
                        rightLights,
                        downLights,
                        lights,
                        _lightMasks,
                        _lightSolidDecay,
                        Math.Max(height * i, -i),
                        Math.Min(
                            (height * (i + height)) - 1,
                            -i + ((height + 1) * (width - 1))
                        ),
                        height + 1,
                        -1,
                        -height
                    );
                }
            );
            // down left
            Parallel.For(
                -height + 2,
                width - 1,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    RunDiagonalSpread(
                        downLights,
                        leftLights,
                        lights,
                        _lightMasks,
                        _lightSolidDecay,
                        Math.Min(height * (i + height - 1), i + ((height - 1) * width)),
                        Math.Max((height * (i + 1)) - 1, i + height - 1),
                        -height + 1,
                        -1,
                        height
                    );
                }
            );
        }
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
            otherColor *= _lightMasks[otherIndex][DistanceTicks];
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
        var lightMask = _lightMasks;
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
