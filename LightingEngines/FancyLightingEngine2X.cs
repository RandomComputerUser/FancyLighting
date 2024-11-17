﻿using System;
using FancyLighting.Config;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;
using Vec4 = System.Numerics.Vector4;

namespace FancyLighting.LightingEngines;

internal sealed class FancyLightingEngine2X : FancyLightingEngineBase
{
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

    public FancyLightingEngine2X()
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
        CalculateSubTileLightSpread(in x, in y, ref lightFrom, ref area, row, col);

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

        ArrayUtils.MakeAtLeastSize(ref _lightMask, length);

        UpdateLightMasks(lightMasks, width, height);
        InitializeTaskVariables(length);

        _countTemporal = LightingConfig.Instance.FancyLightingEngineUseTemporal;
        RunLightingPass(
            colors,
            colors,
            length,
            _countTemporal,
            (Vec3[] lightMap, ref int temporalData, int begin, int end) =>
            {
                for (var i = begin; i < end; ++i)
                {
                    ProcessLight(lightMap, colors, ref temporalData, i, width, height);
                }
            }
        );

        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            SimulateGlobalIllumination(colors, colors, width, height);
        }
    }

    private void ProcessLight(
        Vec3[] lightMap,
        Vector3[] colors,
        ref int temporalData,
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

        color *= _lightMask[index][DistanceTicks];

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
            var workingLights = (Span<Vec2>)stackalloc Vec2[lightRange + 1];

            if (doUpperLeft)
            {
                ProcessQuadrant(
                    lightMap,
                    ref workingLights,
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
                    ref workingLights,
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
                    ref workingLights,
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
                    ref workingLights,
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
        scoped ref Span<Vec2> workingLights,
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
        var lightMask = _lightMask;
        var solidDecay = _lightSolidDecay;
        var lightLoss = _lightLossExitingSolid;
        var lightSpread = _lightSpread;

        {
            workingLights[0] = new(1f);
            var i = index + verticalChange;
            var value = 1f;
            var prevMask = lightMask[i];
            workingLights[1] = new(prevMask[lightSpread[1].DistanceToRight]);
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

                workingLights[y] = new(value * mask[lightSpread[y].DistanceToRight]);
            }
        }

        for (var x = 1; x <= horizontalDistance; ++x)
        {
            var i = index + (horizontalChange * x);
            var j = (MaxLightRange + 1) * x;

            var mask = lightMask[i];

            Vec2 verticalLight;
            {
                ref var horizontalLight = ref workingLights[0];

                if (
                    x > 1
                    && mask != solidDecay
                    && lightMask[i - horizontalChange] == solidDecay
                )
                {
                    horizontalLight *= lightLoss;
                }

                verticalLight = horizontalLight * mask[lightSpread[j].DistanceToTop];
                horizontalLight *= mask[DistanceTicks];
            }

            var edge = Math.Min(verticalDistance, circle[x]);
            var prevMask = mask;
            for (var y = 1; y <= edge; ++y)
            {
                ref var horizontalLight = ref workingLights[y];

                mask = lightMask[i += verticalChange];
                if (mask != solidDecay)
                {
                    if (prevMask == solidDecay)
                    {
                        verticalLight *= lightLoss;
                    }

                    if (lightMask[i - horizontalChange] == solidDecay)
                    {
                        horizontalLight *= lightLoss;
                    }
                }

                prevMask = mask;

                ref var spread = ref lightSpread[++j];

                SetLight(
                    ref lightMap[i],
                    Vec4.Dot(
                        new Vec4(horizontalLight, verticalLight.X, verticalLight.Y),
                        spread.LightFrom
                    ) * color
                );

                var topDecay = mask[spread.DistanceToTop];
                var rightDecay = mask[spread.DistanceToRight];
                var outgoingLight =
                    (
                        (
                            (horizontalLight.X * spread.FromLeftX)
                            + (horizontalLight.Y * spread.FromLeftY)
                        )
                        + (
                            (verticalLight.X * spread.FromBottomX)
                            + (verticalLight.Y * spread.FromBottomY)
                        )
                    ) * new Vec4(topDecay, topDecay, rightDecay, rightDecay);
                horizontalLight = new(outgoingLight.Z, outgoingLight.W);
                verticalLight = new(outgoingLight.X, outgoingLight.Y);
            }
        }
    }
}
