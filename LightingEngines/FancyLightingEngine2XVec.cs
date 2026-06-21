using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;
using Vec4 = System.Numerics.Vector4;

namespace FancyLighting.LightingEngines;

public sealed class FancyLightingEngine2XVec : FancyLightingEngineVecDecay
{
    private const int GlobalIlluminationPassCount = 3;

    private readonly record struct LightSpread(
        int DistanceToTop,
        int DistanceToRight,
        Vec4 LightFrom,
        Vec4 FromLeftX,
        Vec4 FromLeftY,
        Vec4 FromBottomX,
        Vec4 FromBottomY
    );

    private readonly record struct DistanceCache(double Top, double Right);

    private readonly LightSpread[] _lightSpread;

    private bool _countTemporal;

    public FancyLightingEngine2XVec()
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
                                (value.FromLeftX.X + value.FromLeftX.Y)
                                + (value.FromLeftY.X + value.FromLeftY.Y)
                            )
                            / 2.0
                            * distances[row].Right
                        )
                        + (
                            (
                                (value.FromBottomX.X + value.FromBottomX.Y)
                                + (value.FromBottomY.X + value.FromBottomY.Y)
                            )
                            / 2.0
                            * distances[row - 1].Top
                        ),
                    (value.DistanceToRight / (double)DistanceTicks)
                        + (
                            (
                                (value.FromLeftX.Z + value.FromLeftX.W)
                                + (value.FromLeftY.Z + value.FromLeftY.W)
                            )
                            / 2.0
                            * distances[row].Right
                        )
                        + (
                            (
                                (value.FromBottomX.Z + value.FromBottomX.W)
                                + (value.FromBottomY.Z + value.FromBottomY.W)
                            )
                            / 2.0
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
                Vec4.Zero
            );
        }

        var lightFrom = (Span<double>)stackalloc double[4 * 4];
        var area = (Span<double>)stackalloc double[4];

        var x = (Span<double>)[0.0, 0.0, 0.5, 1.0];
        var y = (Span<double>)[0.5, 0.0, 0.0, 0.0];
        CalculateSubTileLightSpread(x, y, lightFrom, area, row, col);

        distanceToTop -=
            (
                (lightFrom[0] + lightFrom[1] + lightFrom[4] + lightFrom[5])
                / 2.0
                * leftDistanceError
            )
            + (
                (lightFrom[8] + lightFrom[9] + lightFrom[12] + lightFrom[13])
                / 2.0
                * bottomDistanceError
            );
        distanceToRight -=
            (
                (lightFrom[2] + lightFrom[3] + lightFrom[6] + lightFrom[7])
                / 2.0
                * leftDistanceError
            )
            + (
                (lightFrom[10] + lightFrom[11] + lightFrom[14] + lightFrom[15])
                / 2.0
                * bottomDistanceError
            );

        return new(
            DoubleToIndex(distanceToTop),
            DoubleToIndex(distanceToRight),
            new(
                (float)(area[1] - area[0]),
                (float)area[0],
                (float)(area[2] - area[1]),
                (float)(area[3] - area[2])
            ),
            new(
                (float)lightFrom[4],
                (float)lightFrom[5],
                (float)lightFrom[7],
                (float)lightFrom[6]
            ),
            new(
                (float)lightFrom[0],
                (float)lightFrom[1],
                (float)lightFrom[3],
                (float)lightFrom[2]
            ),
            new(
                (float)lightFrom[8],
                (float)lightFrom[9],
                (float)lightFrom[11],
                (float)lightFrom[10]
            ),
            new(
                (float)lightFrom[12],
                (float)lightFrom[13],
                (float)lightFrom[15],
                (float)lightFrom[14]
            )
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
                var myColors = colors;
                var myWidth = width;
                var myHeight = height;
                for (var i = begin; i < end; ++i)
                {
                    ProcessLight(
                        lightMap,
                        myColors,
                        ref temporalData,
                        i,
                        myWidth,
                        myHeight
                    );
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
            var workingLights1 = (Span<Vec3>)stackalloc Vec3[lightRange + 1];
            var workingLights2 = (Span<Vec3>)stackalloc Vec3[lightRange + 1];

            if (doUpperLeft)
            {
                ProcessQuadrant(
                    lightMap,
                    workingLights1,
                    workingLights2,
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
                    workingLights1,
                    workingLights2,
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
                    workingLights1,
                    workingLights2,
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
                    workingLights1,
                    workingLights2,
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
        Span<Vec3> workingLights1,
        Span<Vec3> workingLights2,
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
            workingLights1[0] = workingLights2[0] = color;
            var i = index + verticalChange;
            var prevMask = lightMask[i];
            workingLights1[1] = workingLights2[1] =
                color * prevMask[lightSpread[1].DistanceToRight];
            for (var y = 2; y <= verticalDistance; ++y)
            {
                i += verticalChange;

                var mask = lightMask[i];
                if (prevMask == solidDecay && mask != solidDecay)
                {
                    color *= lightLoss * prevMask[DistanceTicks];
                }
                else
                {
                    color *= prevMask[DistanceTicks];
                }

                prevMask = mask;

                workingLights1[y] = workingLights2[y] =
                    color * mask[lightSpread[y].DistanceToRight];
            }
        }

        for (var x = 1; x <= horizontalDistance; ++x)
        {
            var i = index + (horizontalChange * x);
            var j = (MaxLightRange + 1) * x;

            var mask = lightMask[i];

            Vec3 verticalLight1;
            Vec3 verticalLight2;
            {
                ref var horizontalLight1 = ref workingLights1[0];
                ref var horizontalLight2 = ref workingLights2[0];

                if (
                    x > 1
                    && mask != solidDecay
                    && lightMask[i - horizontalChange] == solidDecay
                )
                {
                    horizontalLight1 *= lightLoss;
                }

                verticalLight1 = verticalLight2 =
                    horizontalLight1 * mask[lightSpread[j].DistanceToTop];
                horizontalLight1 *= mask[DistanceTicks];
                horizontalLight2 = horizontalLight1;
            }

            var edge = Math.Min(verticalDistance, circle[x]);
            var prevMask = mask;
            for (var y = 1; y <= edge; ++y)
            {
                ref var horizontalLight1 = ref workingLights1[y];
                ref var horizontalLight2 = ref workingLights2[y];

                mask = lightMask[i += verticalChange];
                if (mask != solidDecay)
                {
                    if (prevMask == solidDecay)
                    {
                        verticalLight1 *= lightLoss;
                        verticalLight2 *= lightLoss;
                    }

                    if (lightMask[i - horizontalChange] == solidDecay)
                    {
                        horizontalLight1 *= lightLoss;
                        horizontalLight2 *= lightLoss;
                    }
                }

                prevMask = mask;

                ref var spread = ref lightSpread[++j];

                SetLight(
                    ref lightMap[i],
                    new Vec3(
                        Vec4.Dot(
                            new(
                                horizontalLight1.X,
                                horizontalLight2.X,
                                verticalLight1.X,
                                verticalLight2.X
                            ),
                            spread.LightFrom
                        ),
                        Vec4.Dot(
                            new(
                                horizontalLight1.Y,
                                horizontalLight2.Y,
                                verticalLight1.Y,
                                verticalLight2.Y
                            ),
                            spread.LightFrom
                        ),
                        Vec4.Dot(
                            new(
                                horizontalLight1.Z,
                                horizontalLight2.Z,
                                verticalLight1.Z,
                                verticalLight2.Z
                            ),
                            spread.LightFrom
                        )
                    )
                );

                var topDecay = mask[spread.DistanceToTop];
                var rightDecay = mask[spread.DistanceToRight];
                var outgoingLightX =
                    (
                        (
                            (horizontalLight1.X * spread.FromLeftX)
                            + (horizontalLight2.X * spread.FromLeftY)
                        )
                        + (
                            (verticalLight1.X * spread.FromBottomX)
                            + (verticalLight2.X * spread.FromBottomY)
                        )
                    ) * new Vec4(topDecay.X, topDecay.X, rightDecay.X, rightDecay.X);
                var outgoingLightY =
                    (
                        (
                            (horizontalLight1.Y * spread.FromLeftX)
                            + (horizontalLight2.Y * spread.FromLeftY)
                        )
                        + (
                            (verticalLight1.Y * spread.FromBottomX)
                            + (verticalLight2.Y * spread.FromBottomY)
                        )
                    ) * new Vec4(topDecay.Y, topDecay.Y, rightDecay.Y, rightDecay.Y);
                var outgoingLightZ =
                    (
                        (
                            (horizontalLight1.Z * spread.FromLeftX)
                            + (horizontalLight2.Z * spread.FromLeftY)
                        )
                        + (
                            (verticalLight1.Z * spread.FromBottomX)
                            + (verticalLight2.Z * spread.FromBottomY)
                        )
                    ) * new Vec4(topDecay.Z, topDecay.Z, rightDecay.Z, rightDecay.Z);
                horizontalLight1 = new(
                    outgoingLightX.Z,
                    outgoingLightY.Z,
                    outgoingLightZ.Z
                );
                horizontalLight2 = new(
                    outgoingLightX.W,
                    outgoingLightY.W,
                    outgoingLightZ.W
                );
                verticalLight1 = new(
                    outgoingLightX.X,
                    outgoingLightY.X,
                    outgoingLightZ.X
                );
                verticalLight2 = new(
                    outgoingLightX.Y,
                    outgoingLightY.Y,
                    outgoingLightZ.Y
                );
            }
        }
    }
}
