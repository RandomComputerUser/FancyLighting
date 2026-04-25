using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;
using Vec4 = System.Numerics.Vector4;

namespace FancyLighting.LightingEngines;

public sealed class FancyLightingEngine4X : FancyLightingEngineVecDecay
{
    private const int GlobalIlluminationPassCount = 3;

    private readonly record struct LightSpread(
        int DistanceToTop,
        int DistanceToRight,
        Vec4 LightFromLeft,
        Vec4 LightFromBottom,
        Vec4 TopFromLeftX,
        Vec4 TopFromLeftY,
        Vec4 TopFromLeftZ,
        Vec4 TopFromLeftW,
        Vec4 TopFromBottomX,
        Vec4 TopFromBottomY,
        Vec4 TopFromBottomZ,
        Vec4 TopFromBottomW,
        Vec4 RightFromLeftX,
        Vec4 RightFromLeftY,
        Vec4 RightFromLeftZ,
        Vec4 RightFromLeftW,
        Vec4 RightFromBottomX,
        Vec4 RightFromBottomY,
        Vec4 RightFromBottomZ,
        Vec4 RightFromBottomW
    );

    private readonly record struct DistanceCache(double Top, double Right);

    private readonly LightSpread[] _lightSpread;

    private bool _countTemporal;

    public FancyLightingEngine4X()
    {
        ComputeLightSpread(out _lightSpread);
        InitializeDecayArrays();
        ComputeCircles();
    }

    private void ComputeLightSpread(out LightSpread[] values)
    {
        values = new LightSpread[(MaxLightRange + 1) * (MaxLightRange + 1)];
        var distances = new DistanceCache[MaxLightRange + 1];

        for (var row = 0; row <= MaxLightRange; ++row)
        {
            var index = row;
            ref var value = ref values[index];
            value = CalculateTileLightSpread(row, 0, 0.0, 0.0);
            distances[row] = new(
                row + 1.0,
                row + (value.DistanceToRight / (double)DistanceTicks)
            );
        }

        for (var col = 1; col <= MaxLightRange; ++col)
        {
            var index = (MaxLightRange + 1) * col;
            ref var value = ref values[index];
            value = CalculateTileLightSpread(0, col, 0.0, 0.0);
            distances[0] = new(
                col + (value.DistanceToTop / (double)DistanceTicks),
                col + 1.0
            );

            for (var row = 1; row <= MaxLightRange; ++row)
            {
                ++index;
                var distance = MathUtils.Hypot(col, row);
                value = ref values[index];
                value = CalculateTileLightSpread(
                    row,
                    col,
                    distances[row].Right - distance,
                    distances[row - 1].Top - distance
                );

                distances[row] = new(
                    (value.DistanceToTop / (double)DistanceTicks)
                        + (
                            (
                                Vec4.Dot(value.TopFromLeftX, Vec4.One)
                                + Vec4.Dot(value.TopFromLeftY, Vec4.One)
                                + Vec4.Dot(value.TopFromLeftZ, Vec4.One)
                                + Vec4.Dot(value.TopFromLeftW, Vec4.One)
                            )
                            / 4.0
                            * distances[row].Right
                        )
                        + (
                            (
                                Vec4.Dot(value.TopFromBottomX, Vec4.One)
                                + Vec4.Dot(value.TopFromBottomY, Vec4.One)
                                + Vec4.Dot(value.TopFromBottomZ, Vec4.One)
                                + Vec4.Dot(value.TopFromBottomW, Vec4.One)
                            )
                            / 4.0
                            * distances[row - 1].Top
                        ),
                    (value.DistanceToRight / (double)DistanceTicks)
                        + (
                            (
                                Vec4.Dot(value.RightFromLeftX, Vec4.One)
                                + Vec4.Dot(value.RightFromLeftY, Vec4.One)
                                + Vec4.Dot(value.RightFromLeftZ, Vec4.One)
                                + Vec4.Dot(value.RightFromLeftW, Vec4.One)
                            )
                            / 4.0
                            * distances[row].Right
                        )
                        + (
                            (
                                Vec4.Dot(value.RightFromBottomX, Vec4.One)
                                + Vec4.Dot(value.RightFromBottomY, Vec4.One)
                                + Vec4.Dot(value.RightFromBottomZ, Vec4.One)
                                + Vec4.Dot(value.RightFromBottomW, Vec4.One)
                            )
                            / 4.0
                            * distances[row - 1].Top
                        )
                );
            }
        }
    }

    private static LightSpread CalculateTileLightSpread(
        int row,
        int col,
        double leftDistanceError,
        double bottomDistanceError
    )
    {
        static int DoubleToIndex(double x) =>
            Math.Clamp((int)Math.Round(DistanceTicks * x), 0, DistanceTicks);

        var distance = MathUtils.Hypot(col, row);
        var distanceToTop = MathUtils.Hypot(col, row + 1) - distance;
        var distanceToRight = MathUtils.Hypot(col + 1, row) - distance;

        if (row == 0 && col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero
            );
        }

        if (row == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero
            );
        }

        if (col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero,
                Vec4.Zero
            );
        }

        var lightFrom = (Span<double>)stackalloc double[8 * 8];
        var area = (Span<double>)stackalloc double[8];

        var x = (Span<double>)[0.0, 0.0, 0.0, 0.0, 0.25, 0.5, 0.75, 1.0];
        var y = (Span<double>)[0.75, 0.5, 0.25, 0.0, 0.0, 0.0, 0.0, 0.0];
        CalculateSubTileLightSpread(in x, in y, ref lightFrom, ref area, row, col);

        static double QuadrantSum(scoped in Span<double> lightFrom, int index)
        {
            var result = 0.0;
            var i = index;
            for (var row = 0; row < 4; ++row)
            {
                for (var col = 0; col < 4; ++col)
                {
                    result += lightFrom[i++];
                }

                i += 4;
            }

            return result;
        }

        static Vec4 VectorAt(scoped in Span<double> lightFrom, int index) =>
            new(
                (float)lightFrom[index],
                (float)lightFrom[index + 1],
                (float)lightFrom[index + 2],
                (float)lightFrom[index + 3]
            );

        static Vec4 ReverseVectorAt(scoped in Span<double> lightFrom, int index) =>
            new(
                (float)lightFrom[index + 3],
                (float)lightFrom[index + 2],
                (float)lightFrom[index + 1],
                (float)lightFrom[index]
            );

        distanceToTop -=
            (QuadrantSum(lightFrom, (8 * 0) + 0) / 4.0 * leftDistanceError)
            + (QuadrantSum(lightFrom, (8 * 4) + 0) / 4.0 * bottomDistanceError);
        distanceToRight -=
            (QuadrantSum(lightFrom, (8 * 0) + 4) / 4.0 * leftDistanceError)
            + (QuadrantSum(lightFrom, (8 * 4) + 4) / 4.0 * bottomDistanceError);

        return new(
            DoubleToIndex(distanceToTop),
            DoubleToIndex(distanceToRight),
            new(
                (float)(area[3] - area[2]),
                (float)(area[2] - area[1]),
                (float)(area[1] - area[0]),
                (float)area[0]
            ),
            new(
                (float)(area[4] - area[3]),
                (float)(area[5] - area[4]),
                (float)(area[6] - area[5]),
                (float)(area[7] - area[6])
            ),
            VectorAt(lightFrom, (8 * 3) + 0),
            VectorAt(lightFrom, (8 * 2) + 0),
            VectorAt(lightFrom, (8 * 1) + 0),
            VectorAt(lightFrom, (8 * 0) + 0),
            VectorAt(lightFrom, (8 * 4) + 0),
            VectorAt(lightFrom, (8 * 5) + 0),
            VectorAt(lightFrom, (8 * 6) + 0),
            VectorAt(lightFrom, (8 * 7) + 0),
            ReverseVectorAt(lightFrom, (8 * 3) + 4),
            ReverseVectorAt(lightFrom, (8 * 2) + 4),
            ReverseVectorAt(lightFrom, (8 * 1) + 4),
            ReverseVectorAt(lightFrom, (8 * 0) + 4),
            ReverseVectorAt(lightFrom, (8 * 4) + 4),
            ReverseVectorAt(lightFrom, (8 * 5) + 4),
            ReverseVectorAt(lightFrom, (8 * 6) + 4),
            ReverseVectorAt(lightFrom, (8 * 7) + 4)
        );
    }

    public override void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    )
    {
        UpdateBrightnessCutoff();
        UpdateDecays(lightMap);

        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            ConvertLightColorsToLinear(colors, width, height);
        }

        var length = width * height;
        var doGi = LightingConfig.Instance.SimulateGlobalIllumination;

        UpdateLightMasks(lightMasks, width, height);
        InitializeTaskVariables(length, doGi);

        _countTemporal = LightingConfig.Instance.FancyLightingEngineUseTemporal;
        RunLightingPass(
            colors,
            colors,
            width,
            height,
            doGi ? GlobalIlluminationPassCount : 0,
            _countTemporal,
            (Vec3[] lightMap, ref long temporalData, int begin, int end) =>
            {
                for (var i = begin; i < end; ++i)
                {
                    ProcessLight(lightMap, colors, ref temporalData, i, width, height);
                }
            }
        );
    }

    private void ProcessLight(
        Vec3[] lightMap,
        Vector3[] colors,
        ref long temporalData,
        int index,
        int width,
        int height
    )
    {
        ref var colorRef = ref colors[index];
        var color = new Vec3(colorRef.X, colorRef.Y, colorRef.Z);
        if (
            color.X <= _initialBrightnessCutoff
            && color.Y <= _initialBrightnessCutoff
            && color.Z <= _initialBrightnessCutoff
        )
        {
            return;
        }

        color *= _lightMasks[index][DistanceTicks];

        CalculateLightSourceValues(
            colors,
            color,
            index,
            width,
            height,
            out var upDistance,
            out var downDistance,
            out var leftDistance,
            out var rightDistance,
            out var doUp,
            out var doDown,
            out var doLeft,
            out var doRight
        );

        // We blend by taking the max of each component, so this is a valid check to skip
        if (!(doUp || doDown || doLeft || doRight))
        {
            return;
        }

        var lightRange = CalculateLightRange(color);

        upDistance = Math.Min(upDistance, lightRange);
        downDistance = Math.Min(downDistance, lightRange);
        leftDistance = Math.Min(leftDistance, lightRange);
        rightDistance = Math.Min(rightDistance, lightRange);

        if (doUp)
        {
            SpreadLightLine(lightMap, color, index, upDistance, -1);
        }

        if (doDown)
        {
            SpreadLightLine(lightMap, color, index, downDistance, 1);
        }

        if (doLeft)
        {
            SpreadLightLine(lightMap, color, index, leftDistance, -height);
        }

        if (doRight)
        {
            SpreadLightLine(lightMap, color, index, rightDistance, height);
        }

        // Using && instead of || for culling is sometimes inaccurate, but much faster
        var doUpperLeft = doUp && doLeft;
        var doUpperRight = doUp && doRight;
        var doLowerLeft = doDown && doLeft;
        var doLowerRight = doDown && doRight;

        if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
        {
            var circle = _circles[lightRange];
            var workingLightsX = (Span<Vec4>)stackalloc Vec4[lightRange + 1];
            var workingLightsY = (Span<Vec4>)stackalloc Vec4[lightRange + 1];
            var workingLightsZ = (Span<Vec4>)stackalloc Vec4[lightRange + 1];

            if (doUpperLeft)
            {
                ProcessQuadrant(
                    lightMap,
                    workingLightsX,
                    workingLightsY,
                    workingLightsZ,
                    circle,
                    color,
                    index,
                    upDistance,
                    leftDistance,
                    -1,
                    -height
                );
            }

            if (doUpperRight)
            {
                ProcessQuadrant(
                    lightMap,
                    workingLightsX,
                    workingLightsY,
                    workingLightsZ,
                    circle,
                    color,
                    index,
                    upDistance,
                    rightDistance,
                    -1,
                    height
                );
            }

            if (doLowerLeft)
            {
                ProcessQuadrant(
                    lightMap,
                    workingLightsX,
                    workingLightsY,
                    workingLightsZ,
                    circle,
                    color,
                    index,
                    downDistance,
                    leftDistance,
                    1,
                    -height
                );
            }

            if (doLowerRight)
            {
                ProcessQuadrant(
                    lightMap,
                    workingLightsX,
                    workingLightsY,
                    workingLightsZ,
                    circle,
                    color,
                    index,
                    downDistance,
                    rightDistance,
                    1,
                    height
                );
            }
        }

        if (_countTemporal)
        {
            temporalData += CalculateTemporalData(
                color,
                doUp,
                doDown,
                doLeft,
                doRight,
                doUpperLeft,
                doUpperRight,
                doLowerLeft,
                doLowerRight
            );
        }
    }

    private void ProcessQuadrant(
        Vec3[] lightMap,
        Span<Vec4> workingLightsX,
        Span<Vec4> workingLightsY,
        Span<Vec4> workingLightsZ,
        int[] circle,
        Vec3 color,
        int index,
        int verticalDistance,
        int horizontalDistance,
        int verticalChange,
        int horizontalChange
    )
    {
        // Performance optimization
        var lightMask = _lightMasks;
        var solidDecay = _lightSolidDecay;
        var lightLoss = _lightLossExitingSolid;
        var lightSpread = _lightSpread;

        {
            workingLightsX[0] = workingLightsY[0] = workingLightsZ[0] = Vec4.One;
            var i = index + verticalChange;
            var value = Vec3.One;
            var prevMask = lightMask[i];
            var decay = prevMask[lightSpread[1].DistanceToRight];
            workingLightsX[1] = new(decay.X);
            workingLightsY[1] = new(decay.Y);
            workingLightsZ[1] = new(decay.Z);
            for (var y = 2; y <= verticalDistance; ++y)
            {
                i += verticalChange;

                var mask = lightMask[i];
                if (prevMask == solidDecay && mask != solidDecay)
                {
                    value *= lightLoss * prevMask[DistanceTicks];
                }
                else
                {
                    value *= prevMask[DistanceTicks];
                }

                prevMask = mask;

                decay = value * mask[lightSpread[y].DistanceToRight];
                workingLightsX[y] = new(decay.X);
                workingLightsY[y] = new(decay.Y);
                workingLightsZ[y] = new(decay.Z);
            }
        }

        for (var x = 1; x <= horizontalDistance; ++x)
        {
            var i = index + (horizontalChange * x);
            var j = (MaxLightRange + 1) * x;

            var mask = lightMask[i];

            Vec4 verticalLightX;
            Vec4 verticalLightY;
            Vec4 verticalLightZ;
            {
                ref var horizontalLightX = ref workingLightsX[0];
                ref var horizontalLightY = ref workingLightsY[0];
                ref var horizontalLightZ = ref workingLightsZ[0];

                if (
                    x > 1
                    && mask != solidDecay
                    && lightMask[i - horizontalChange] == solidDecay
                )
                {
                    horizontalLightX *= lightLoss;
                    horizontalLightY *= lightLoss;
                    horizontalLightZ *= lightLoss;
                }

                var decay = mask[lightSpread[j].DistanceToTop];
                verticalLightX = horizontalLightX * decay.X;
                verticalLightY = horizontalLightY * decay.Y;
                verticalLightZ = horizontalLightZ * decay.Z;
                decay = mask[DistanceTicks];
                horizontalLightX *= decay.X;
                horizontalLightY *= decay.Y;
                horizontalLightZ *= decay.Z;
            }

            var edge = Math.Min(verticalDistance, circle[x]);
            var prevMask = mask;
            for (var y = 1; y <= edge; ++y)
            {
                ref var horizontalLightRefX = ref workingLightsX[y];
                ref var horizontalLightRefY = ref workingLightsY[y];
                ref var horizontalLightRefZ = ref workingLightsZ[y];
                var horizontalLightX = horizontalLightRefX;
                var horizontalLightY = horizontalLightRefY;
                var horizontalLightZ = horizontalLightRefZ;

                mask = lightMask[i += verticalChange];
                if (mask != solidDecay)
                {
                    if (prevMask == solidDecay)
                    {
                        verticalLightX *= lightLoss;
                        verticalLightY *= lightLoss;
                        verticalLightZ *= lightLoss;
                    }

                    if (lightMask[i - horizontalChange] == solidDecay)
                    {
                        horizontalLightX *= lightLoss;
                        horizontalLightY *= lightLoss;
                        horizontalLightZ *= lightLoss;
                    }
                }

                prevMask = mask;

                ref var spread = ref lightSpread[++j];

                SetLight(
                    ref lightMap[i],
                    new Vec3(
                        Vec4.Dot(verticalLightX, spread.LightFromBottom)
                            + Vec4.Dot(horizontalLightX, spread.LightFromLeft),
                        Vec4.Dot(verticalLightY, spread.LightFromBottom)
                            + Vec4.Dot(horizontalLightY, spread.LightFromLeft),
                        Vec4.Dot(verticalLightZ, spread.LightFromBottom)
                            + Vec4.Dot(horizontalLightZ, spread.LightFromLeft)
                    ) * color
                );

                // The last time I tested this, it was slightly faster than using
                // Vector4.Transform(Vector4, Matrix4x4)
                var decay = mask[spread.DistanceToRight];
                horizontalLightRefX =
                    (
                        (
                            (
                                (horizontalLightX.X * spread.RightFromLeftX)
                                + (horizontalLightX.Y * spread.RightFromLeftY)
                            )
                            + (
                                (horizontalLightX.Z * spread.RightFromLeftZ)
                                + (horizontalLightX.W * spread.RightFromLeftW)
                            )
                        )
                        + (
                            (
                                (verticalLightX.X * spread.RightFromBottomX)
                                + (verticalLightX.Y * spread.RightFromBottomY)
                            )
                            + (
                                (verticalLightX.Z * spread.RightFromBottomZ)
                                + (verticalLightX.W * spread.RightFromBottomW)
                            )
                        )
                    ) * decay.X;
                horizontalLightRefY =
                    (
                        (
                            (
                                (horizontalLightY.X * spread.RightFromLeftX)
                                + (horizontalLightY.Y * spread.RightFromLeftY)
                            )
                            + (
                                (horizontalLightY.Z * spread.RightFromLeftZ)
                                + (horizontalLightY.W * spread.RightFromLeftW)
                            )
                        )
                        + (
                            (
                                (verticalLightY.X * spread.RightFromBottomX)
                                + (verticalLightY.Y * spread.RightFromBottomY)
                            )
                            + (
                                (verticalLightY.Z * spread.RightFromBottomZ)
                                + (verticalLightY.W * spread.RightFromBottomW)
                            )
                        )
                    ) * decay.Y;
                horizontalLightRefZ =
                    (
                        (
                            (
                                (horizontalLightZ.X * spread.RightFromLeftX)
                                + (horizontalLightZ.Y * spread.RightFromLeftY)
                            )
                            + (
                                (horizontalLightZ.Z * spread.RightFromLeftZ)
                                + (horizontalLightZ.W * spread.RightFromLeftW)
                            )
                        )
                        + (
                            (
                                (verticalLightZ.X * spread.RightFromBottomX)
                                + (verticalLightZ.Y * spread.RightFromBottomY)
                            )
                            + (
                                (verticalLightZ.Z * spread.RightFromBottomZ)
                                + (verticalLightZ.W * spread.RightFromBottomW)
                            )
                        )
                    ) * decay.Z;
                decay = mask[spread.DistanceToTop];
                verticalLightX =
                    (
                        (
                            (
                                (horizontalLightX.X * spread.TopFromLeftX)
                                + (horizontalLightX.Y * spread.TopFromLeftY)
                            )
                            + (
                                (horizontalLightX.Z * spread.TopFromLeftZ)
                                + (horizontalLightX.W * spread.TopFromLeftW)
                            )
                        )
                        + (
                            (
                                (verticalLightX.X * spread.TopFromBottomX)
                                + (verticalLightX.Y * spread.TopFromBottomY)
                            )
                            + (
                                (verticalLightX.Z * spread.TopFromBottomZ)
                                + (verticalLightX.W * spread.TopFromBottomW)
                            )
                        )
                    ) * decay.X;
                verticalLightY =
                    (
                        (
                            (
                                (horizontalLightY.X * spread.TopFromLeftX)
                                + (horizontalLightY.Y * spread.TopFromLeftY)
                            )
                            + (
                                (horizontalLightY.Z * spread.TopFromLeftZ)
                                + (horizontalLightY.W * spread.TopFromLeftW)
                            )
                        )
                        + (
                            (
                                (verticalLightY.X * spread.TopFromBottomX)
                                + (verticalLightY.Y * spread.TopFromBottomY)
                            )
                            + (
                                (verticalLightY.Z * spread.TopFromBottomZ)
                                + (verticalLightY.W * spread.TopFromBottomW)
                            )
                        )
                    ) * decay.Y;
                verticalLightZ =
                    (
                        (
                            (
                                (horizontalLightZ.X * spread.TopFromLeftX)
                                + (horizontalLightZ.Y * spread.TopFromLeftY)
                            )
                            + (
                                (horizontalLightZ.Z * spread.TopFromLeftZ)
                                + (horizontalLightZ.W * spread.TopFromLeftW)
                            )
                        )
                        + (
                            (
                                (verticalLightZ.X * spread.TopFromBottomX)
                                + (verticalLightZ.Y * spread.TopFromBottomY)
                            )
                            + (
                                (verticalLightZ.Z * spread.TopFromBottomZ)
                                + (verticalLightZ.W * spread.TopFromBottomW)
                            )
                        )
                    ) * decay.Z;
            }
        }
    }
}
