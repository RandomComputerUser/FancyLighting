using System.Runtime.CompilerServices;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

public abstract class FancyLightingEngineBase : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;
    private long _temporalData;

    protected const int MaxLightRange = 64;
    protected const int DistanceTicks = 64;

    protected const float MaxDecayMult = 0.95f;
    protected const float LowLightLevel = 0.03f;

    protected float _initialBrightnessCutoff;
    protected float _logBrightnessCutoff;
    protected float _logBasicWorkCutoff;

    protected float _thresholdMult;
    protected float _reciprocalLogSlowestDecay;
    protected float _lightLossExitingSolid;

    private Action[] _actions;
    protected Vec3[][] _workingLightMaps;

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
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        Span<double> lightFrom,
        Span<double> area,
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
                var myHeight = height;
                var myColors = colors;

                var i = myHeight * x;
                for (var y = 0; y < myHeight; ++y)
                {
                    ColorUtils.GammaToLinear(ref myColors[i++]);
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

            CopyVec3Array(workingLightMap, destination, 0, length);

            return;
        }

        const int IndexIncrement = 32;

        var lightIndex = -IndexIncrement;
        for (var i = 0; i < taskCount; ++i)
        {
            var index = i;
            _actions[i] = () =>
            {
                var myLength = length;
                var myLightingAction = lightingAction;

                var workingLightMap = _workingLightMaps[index];
                var temporalData = 0L;

                CopyVec3Array(source, workingLightMap, 0, myLength);

                while (true)
                {
                    var i = Interlocked.Add(ref lightIndex, IndexIncrement);
                    if (i >= myLength)
                    {
                        break;
                    }

                    myLightingAction(
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

        if (!doGi)
        {
            return;
        }

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

    protected abstract void SimulateGlobalIllumination(
        int width,
        int height,
        int passCount
    );

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
