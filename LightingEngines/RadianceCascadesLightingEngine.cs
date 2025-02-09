using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

internal sealed class RadianceCascadesLightingEngine : ICustomLightingEngine
{
    private const int CascadeCount = 4;
    private const int BranchingFactor = 4;
    private const float Cascade0RayLength = 3f;

    private const int LengthStepCount = 127;
    private const int UnitLengthIndex = 89;

    private record struct LightInfo(Vec3 LightColor, LightMaskIndex MaskIndex);

    private record struct RayLightInfo(Vec3 Radiance, Vec3 Transparency);

    private record struct RayCastStep(
        sbyte Dx,
        sbyte Dy,
        byte LengthIndex,
        float LightMultiplier
    );

    private record struct RectangleSize(int Width, int Height);

    private static readonly Vec2[][] _bilinearFixOffsets =
    [
        [new(-1.5f, -1.5f), new(-1.5f, 0.5f), new(0.5f, -1.5f), new(0.5f, 0.5f)],
        [new(-1.5f, -0.5f), new(-1.5f, 1.5f), new(0.5f, -0.5f), new(0.5f, 1.5f)],
        [new(-0.5f, -1.5f), new(-0.5f, 0.5f), new(1.5f, -1.5f), new(1.5f, 0.5f)],
        [new(-0.5f, -0.5f), new(-0.5f, 1.5f), new(1.5f, -0.5f), new(1.5f, 1.5f)],
    ];

    private static readonly float[][] _probeMergeWeights =
    [
        [0.25f * 0.25f, 0.25f * 0.75f, 0.75f * 0.25f, 0.75f * 0.75f],
        [0.25f * 0.75f, 0.25f * 0.25f, 0.75f * 0.75f, 0.75f * 0.25f],
        [0.75f * 0.25f, 0.75f * 0.75f, 0.25f * 0.25f, 0.25f * 0.75f],
        [0.75f * 0.75f, 0.75f * 0.25f, 0.25f * 0.75f, 0.25f * 0.25f],
    ];

    // First index: cascade level
    // Second index: grid offset
    // Third index: each ray cast by a probe
    // Fourth index: each step in the ray
    private RayCastStep[][][][] _rayCastInstructions;

    private Vec3[] _lightMaskTransparency;
    private Vec3 _transparencyExitingSolid;

    private Rectangle _lightMapArea;
    private int _width;
    private int _height;

    private LightInfo[] _lights;
    private Vec3[] _lightMap;

    // First index: cascade level
    // Second index: each probe in a cascade
    // Third index: each ray cast by a probe
    private RayLightInfo[][][] _cascades;
    private RectangleSize[] _cascadeSizes;

    public RadianceCascadesLightingEngine()
    {
        GenerateRayCastInstructions();
    }

    private void GenerateRayCastInstructions()
    {
        _rayCastInstructions = new RayCastStep[CascadeCount][][][];
        var steps = new List<RayCastStep>();
        var rayCount = 4;
        var cascadeScale = 1f;
        var rayBegin = 0f;
        var rayEnd = Cascade0RayLength;
        for (var cascadeIndex = 0; cascadeIndex < CascadeCount; ++cascadeIndex)
        {
            if (cascadeIndex == CascadeCount - 1)
            {
                _rayCastInstructions[cascadeIndex] = new RayCastStep[1][][];

                var cascadeInstructions = _rayCastInstructions[cascadeIndex][0] =
                    new RayCastStep[rayCount][];

                for (var rayIndex = 0; rayIndex < rayCount; ++rayIndex)
                {
                    var angle = (rayIndex + 0.5f) / rayCount * MathHelper.TwoPi;
                    var dx = MathF.Cos(angle);
                    var dy = MathF.Sin(angle);

                    var beginX = dx * rayBegin;
                    var beginY = dy * rayBegin;
                    var endX = dx * rayEnd;
                    var endY = dy * rayEnd;

                    steps.Clear();
                    CalculateRaySteps(steps, 0f, 0f, beginX, beginY, endX, endY);
                    cascadeInstructions[rayIndex] = steps.ToArray();
                }

                continue;
            }

            _rayCastInstructions[cascadeIndex] = new RayCastStep[4][][];

            for (var gridOffset = 0; gridOffset < 4; ++gridOffset)
            {
                // Bilinear fix
                var higherProbeOffsets = _bilinearFixOffsets[gridOffset];

                var cascadeInstructions = _rayCastInstructions[cascadeIndex][gridOffset] =
                    new RayCastStep[4 * rayCount][];

                var offset = cascadeIndex == 0 ? 0.5f : 0f;
                var i = 0;
                for (var rayIndex = 0; rayIndex < rayCount; ++rayIndex)
                {
                    var angle = (rayIndex + 0.5f) / rayCount * MathHelper.TwoPi;
                    var dx = MathF.Cos(angle);
                    var dy = MathF.Sin(angle);

                    var beginX = (dx * rayBegin) + offset;
                    var beginY = (dy * rayBegin) + offset;
                    var baseEndX = (dx * rayEnd) + offset;
                    var baseEndY = (dy * rayEnd) + offset;

                    foreach (var probeOffset in higherProbeOffsets)
                    {
                        var endX = baseEndX + (cascadeScale * probeOffset.X);
                        var endY = baseEndY + (cascadeScale * probeOffset.Y);

                        steps.Clear();
                        CalculateRaySteps(
                            steps,
                            offset,
                            offset,
                            beginX,
                            beginY,
                            endX,
                            endY
                        );
                        cascadeInstructions[i++] = steps.ToArray();
                    }
                }
            }

            rayCount *= BranchingFactor;
            cascadeScale *= 2f;
            rayBegin = rayEnd;
            rayEnd *= BranchingFactor;
        }
    }

    private void CalculateRaySteps(
        List<RayCastStep> steps,
        float offsetX,
        float offsetY,
        float beginX,
        float beginY,
        float endX,
        float endY
    )
    {
        var dx = endX - beginX;
        var dy = endY - beginY;
        var lengthError = 0f;
        var x = beginX;
        var y = beginY;
        var prevDistance = MathUtils.HypotF(beginX - offsetX, beginY - offsetY);
        var prevTileX = 0;
        var prevTileY = 0;
        var done = false;
        while (!done)
        {
            var nextX =
                dx > 0 ? MathF.Floor(x + 1)
                : dx < 0 ? MathF.Ceiling(x - 1)
                : x;
            var nextY =
                dy > 0 ? MathF.Floor(y + 1)
                : dy < 0 ? MathF.Ceiling(y - 1)
                : y;

            var doneX = (dx > 0 && nextX >= endX) || (dx < 0 && nextX <= endX);
            var doneY = (dy > 0 && nextY >= endY) || (dy < 0 && nextY <= endY);

            if (doneX || doneY)
            {
                nextX = endX;
                nextY = endY;
                done = true;
            }
            else
            {
                var nextXLerp = (nextX - beginX) / dx;
                var nextYLerp = (nextY - beginY) / dy;

                if (dy == 0 || (dx != 0 && nextXLerp <= nextYLerp))
                {
                    nextY = MathUtils.Lerp(beginY, endY, nextXLerp);
                }
                else
                {
                    nextX = MathUtils.Lerp(beginX, endX, nextYLerp);
                }
            }

            var tileX = (int)MathF.Floor(0.5f * (x + nextX));
            var tileY = (int)MathF.Floor(0.5f * (y + nextY));
            var length = MathUtils.HypotF(nextX - x, nextY - y);
            var scaledLength = (UnitLengthIndex * length) + lengthError;
            var lengthIndex = Math.Clamp(MathF.Round(scaledLength), 0f, LengthStepCount);
            lengthError = scaledLength - lengthIndex;
            var distance = MathUtils.HypotF(nextX - offsetX, nextY - offsetY);
            var lightMultiplier = CalculateLightMultiplier(prevDistance, distance);
            steps.Add(
                new(
                    (sbyte)(tileX - prevTileX),
                    (sbyte)(tileY - prevTileY),
                    (byte)lengthIndex,
                    lightMultiplier
                )
            );
            x = nextX;
            y = nextY;
            prevTileX = tileX;
            prevTileY = tileY;
            prevDistance = distance;
        }
    }

    private static float CalculateLightMultiplier(
        float nearDistance,
        float farDistance
    ) => farDistance - nearDistance;

    public void Unload() { }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    )
    {
        var doGammaCorrection = LightingConfig.Instance.HiDefFeaturesEnabled();

        UpdateLightMasks(lightMap);
        SetLightMapSize(width, height);

        UpdateLights(colors, lightMasks);

        CastRaysAndMergeCascades();

        if (doGammaCorrection)
        {
            Parallel.For(
                0,
                _width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var cascade = _cascades[0];
                    var endIndex = _height * (i + 1);
                    for (var j = _height * i; j < endIndex; ++j)
                    {
                        var probe = cascade[j + 1];
                        colors[j] = (
                            probe[0].Radiance
                            + probe[1].Radiance
                            + probe[2].Radiance
                            + probe[3].Radiance
                        ).ToXnaVector3();
                    }
                }
            );
        }
        else
        {
            Parallel.For(
                0,
                _width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var cascade = _cascades[0];
                    var endIndex = _height * (i + 1);
                    for (var j = _height * i; j < endIndex; ++j)
                    {
                        var probe = cascade[j + 1];
                        colors[j] = (
                            probe[0].Radiance
                            + probe[1].Radiance
                            + probe[2].Radiance
                            + probe[3].Radiance
                        ).ToXnaVector3();
                        ColorUtils.LinearToGamma(ref colors[j]);
                    }
                }
            );
        }
    }

    private void UpdateLightMasks(LightMap lightMap)
    {
        ArrayUtils.MakeSize(ref _lightMaskTransparency, 5 * (LengthStepCount + 1));

        UpdateLightMask(
            LightMaskIndex.Solid,
            new(
                MathF.Pow(
                    lightMap.LightDecayThroughSolid,
                    PreferencesConfig.Instance.FancyLightingEngineAbsorptionExponent()
                )
            )
        );
        if (
            GetLightMaskTransparency(LightMaskIndex.NonSolid, UnitLengthIndex)
            != GetLightMaskTransparency(LightMaskIndex.Solid, UnitLengthIndex)
        )
        {
            Array.Copy(
                _lightMaskTransparency,
                (LengthStepCount + 1) * (byte)LightMaskIndex.Solid,
                _lightMaskTransparency,
                (LengthStepCount + 1) * (byte)LightMaskIndex.NonSolid,
                LengthStepCount + 1
            );
        }

        UpdateLightMask(
            LightMaskIndex.Honey,
            lightMap.LightDecayThroughHoney.ToSystemVector3()
        );
        UpdateLightMask(
            LightMaskIndex.Water,
            lightMap.LightDecayThroughWater.ToSystemVector3()
        );

        var airLightDecay = lightMap.LightDecayThroughAir;
        airLightDecay = airLightDecay >= 0.91f ? 1f : airLightDecay / 0.91f;
        UpdateLightMask(LightMaskIndex.Air, new(airLightDecay));

        _transparencyExitingSolid = new(
            ColorUtils.GammaToLinear(
                PreferencesConfig.Instance.FancyLightingEngineExitMultiplier()
            )
        );
    }

    private void UpdateLightMask(LightMaskIndex maskIndex, Vec3 transparency)
    {
        ColorUtils.GammaToLinear(ref transparency.X);
        ColorUtils.GammaToLinear(ref transparency.Y);
        ColorUtils.GammaToLinear(ref transparency.Z);

        var i = (LengthStepCount + 1) * (byte)maskIndex;
        if (transparency == _lightMaskTransparency[i + UnitLengthIndex])
        {
            return;
        }

        if (transparency.X == transparency.Y && transparency.X == transparency.Z)
        {
            _lightMaskTransparency[i++] = transparency.X == 0f ? Vec3.Zero : Vec3.One;
            for (var lengthIndex = 1; lengthIndex <= LengthStepCount; ++lengthIndex)
            {
                var length = lengthIndex / (float)UnitLengthIndex;
                _lightMaskTransparency[i++] = new(MathF.Pow(transparency.X, length));
            }
        }
        else
        {
            _lightMaskTransparency[i++] = new(
                transparency.X == 0f ? 0f : 1f,
                transparency.Y == 0f ? 0f : 1f,
                transparency.Z == 0f ? 0f : 1f
            );
            for (var lengthIndex = 1; lengthIndex <= LengthStepCount; ++lengthIndex)
            {
                var length = lengthIndex / (float)UnitLengthIndex;
                _lightMaskTransparency[i++] = new(
                    MathF.Pow(transparency.X, length),
                    MathF.Pow(transparency.Y, length),
                    MathF.Pow(transparency.Z, length)
                );
            }
        }
    }

    private void UpdateLights(Vector3[] lightColors, LightMaskMode[] lightMasks)
    {
        if (PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
        {
            Parallel.For(
                0,
                _width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var endIndex = _height * (i + 1);
                    for (var j = _height * i; j < endIndex; ++j)
                    {
                        ref var light = ref _lights[j];
                        ColorUtils.GammaToLinear(ref lightColors[j]);
                        light.LightColor = lightColors[j].ToSystemVector3();
                        light.MaskIndex = lightMasks[j] switch
                        {
                            LightMaskMode.Solid => LightMaskIndex.Solid,
                            LightMaskMode.Honey => LightMaskIndex.Honey,
                            LightMaskMode.Water => LightMaskIndex.Water,
                            _ => LightMaskIndex.Air,
                        };
                    }
                }
            );
        }
        else
        {
            Parallel.For(
                0,
                _width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var endIndex = _height * (i + 1);
                    var x = i + _lightMapArea.X;
                    var y = _lightMapArea.Y;
                    for (var j = _height * i; j < endIndex; ++j)
                    {
                        ref var light = ref _lights[j];
                        ColorUtils.GammaToLinear(ref lightColors[j]);
                        light.LightColor = lightColors[j].ToSystemVector3();
                        light.MaskIndex = lightMasks[j] switch
                        {
                            LightMaskMode.Solid => TileUtils.IsNonSolid(x, y)
                                ? LightMaskIndex.NonSolid
                                : LightMaskIndex.Solid,
                            LightMaskMode.Honey => LightMaskIndex.Honey,
                            LightMaskMode.Water => LightMaskIndex.Water,
                            _ => LightMaskIndex.Air,
                        };
                        ++y;
                    }
                }
            );
        }
    }

    private void SetLightMapSize(int width, int height)
    {
        if (_width == width && _height == height)
        {
            return;
        }

        _width = width;
        _height = height;

        var size = width * height;
        ArrayUtils.MakeAtLeastSize(ref _lights, size);
        ArrayUtils.MakeAtLeastSize(ref _lightMap, size);

        ArrayUtils.MakeSize(ref _cascades, CascadeCount);
        ArrayUtils.MakeSize(ref _cascadeSizes, CascadeCount);
        var probeRayCount = 4;
        for (var cascadeIndex = 0; cascadeIndex < CascadeCount; ++cascadeIndex)
        {
            var cascadeWidth = (width + (1 << cascadeIndex) - 1) >> cascadeIndex;
            var cascadeHeight = (height + (1 << cascadeIndex) - 1) >> cascadeIndex;
            var cascadeSize = new RectangleSize(cascadeWidth, cascadeHeight);

            if (_cascadeSizes[cascadeIndex] == cascadeSize)
            {
                continue;
            }

            _cascadeSizes[cascadeIndex] = cascadeSize;

            var cascadeLength = (cascadeWidth * cascadeHeight) + 1;
            var prevLength = _cascades[cascadeIndex]?.Length ?? 0;

            if (cascadeLength <= prevLength)
            {
                continue;
            }

            ArrayUtils.MakeAtLeastSize(ref _cascades[cascadeIndex], cascadeLength);

            var cascade = _cascades[cascadeIndex];
            for (var probeIndex = 0; probeIndex < cascadeLength; ++probeIndex)
            {
                ArrayUtils.MakeSize(ref cascade[probeIndex], probeRayCount);
            }

            probeRayCount *= BranchingFactor;
        }
    }

    private void CastRaysAndMergeCascades()
    {
        for (var cascadeIndex = CascadeCount; cascadeIndex-- > 0; )
        {
            var cascadeLevel = cascadeIndex;
            var cascade = _cascades[cascadeLevel];
            var instructions = _rayCastInstructions[cascadeLevel];
            var cascadeSize = _cascadeSizes[cascadeLevel];
            var offset = cascadeLevel == 0 ? 0 : 1 << (cascadeLevel - 1);

            if (cascadeIndex == CascadeCount - 1)
            {
                var probeInstructions = instructions[0];

                Parallel.For(
                    0,
                    cascadeSize.Width * cascadeSize.Height,
                    SettingsSystem._parallelOptions,
                    (probeIndex) =>
                    {
                        var probeX = probeIndex / cascadeSize.Height;
                        var probeY = probeIndex % cascadeSize.Height;

                        var x = (probeX << cascadeLevel) + offset;
                        var y = (probeY << cascadeLevel) + offset;

                        var probe = cascade[probeIndex + 1];
                        for (
                            var rayIndex = 0;
                            rayIndex < probeInstructions.Length;
                            ++rayIndex
                        )
                        {
                            probe[rayIndex] = CastRay(x, y, probeInstructions[rayIndex]);
                        }
                    }
                );

                continue;
            }

            var higherCascade = _cascades[cascadeLevel + 1];
            var higherCascadeSize = _cascadeSizes[cascadeLevel + 1];
            Parallel.For(
                0,
                cascadeSize.Width * cascadeSize.Height,
                SettingsSystem._parallelOptions,
                (probeIndex) =>
                {
                    var probeX = probeIndex / cascadeSize.Height;
                    var probeY = probeIndex % cascadeSize.Height;
                    var gridOffset = CalculateGridOffset(probeX, probeY);
                    var probeInstructions = instructions[gridOffset];
                    var weights = _probeMergeWeights[gridOffset];

                    var x = (probeX << cascadeLevel) + offset;
                    var y = (probeY << cascadeLevel) + offset;

                    var higherX = ((probeX + 1) / 2) - 1;
                    var higherY = ((probeY + 1) / 2) - 1;
                    var higherProbes =
                        (Span<RayLightInfo[]>)
                            [
                                higherCascade[
                                    CalculateProbeIndex(
                                        higherCascadeSize,
                                        higherX,
                                        higherY
                                    )
                                ],
                                higherCascade[
                                    CalculateProbeIndex(
                                        higherCascadeSize,
                                        higherX,
                                        higherY + 1
                                    )
                                ],
                                higherCascade[
                                    CalculateProbeIndex(
                                        higherCascadeSize,
                                        higherX + 1,
                                        higherY
                                    )
                                ],
                                higherCascade[
                                    CalculateProbeIndex(
                                        higherCascadeSize,
                                        higherX + 1,
                                        higherY + 1
                                    )
                                ],
                            ];

                    var probe = cascade[probeIndex + 1];
                    var i = 0;
                    var higherRayIndex = 0;
                    for (var rayIndex = 0; rayIndex < probeInstructions.Length; )
                    {
                        var rayLight = new RayLightInfo(Vec3.Zero, Vec3.Zero);

                        for (
                            var higherProbeIndex = 0;
                            higherProbeIndex < higherProbes.Length;
                            ++higherProbeIndex
                        )
                        {
                            var higherProbe = higherProbes[higherProbeIndex];
                            var combinedHigherRayLight = new RayLightInfo(
                                Vec3.Zero,
                                Vec3.Zero
                            );

                            for (
                                var higherRayIndexOffset = 0;
                                higherRayIndexOffset < BranchingFactor;
                                ++higherRayIndexOffset
                            )
                            {
                                var higherRayLight = higherProbe[
                                    higherRayIndex + higherRayIndexOffset
                                ];
                                combinedHigherRayLight.Radiance +=
                                    higherRayLight.Radiance;
                                combinedHigherRayLight.Transparency +=
                                    higherRayLight.Transparency;
                            }

                            combinedHigherRayLight.Radiance *= 1f / BranchingFactor;
                            combinedHigherRayLight.Transparency *= 1f / BranchingFactor;

                            var lowerRayLight = CastRay(
                                x,
                                y,
                                probeInstructions[rayIndex++]
                            );
                            var weight = weights[higherProbeIndex];

                            rayLight.Radiance +=
                                weight
                                * (
                                    lowerRayLight.Radiance
                                    + (
                                        lowerRayLight.Transparency
                                        * combinedHigherRayLight.Radiance
                                    )
                                );
                            rayLight.Transparency +=
                                weight
                                * (
                                    lowerRayLight.Transparency
                                    * combinedHigherRayLight.Transparency
                                );
                        }

                        probe[i++] = rayLight;
                        higherRayIndex += BranchingFactor;
                    }
                }
            );
        }
    }

    private RayLightInfo CastRay(int x, int y, RayCastStep[] rayInstructions)
    {
        var lightColor = Vec3.Zero;
        var transparency = Vec3.One;
        var prevSolid = true;
        foreach (var instruction in rayInstructions)
        {
            x += instruction.Dx;
            y += instruction.Dy;

            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                transparency = Vec3.Zero;
                break;
            }

            var light = _lights[CalculateTileIndex(x, y)];
            lightColor += transparency * instruction.LightMultiplier * light.LightColor;
            transparency *= GetLightMaskTransparency(
                light.MaskIndex,
                instruction.LengthIndex
            );

            var isSolid = light.MaskIndex is LightMaskIndex.Solid;
            if (isSolid && !prevSolid)
            {
                transparency *= _transparencyExitingSolid;
            }

            prevSolid = isSolid;
        }

        return new(lightColor, transparency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateTileIndex(int x, int y) => (_height * x) + y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateGridOffset(int probeX, int probeY) =>
        ((probeX & 1) << 1) | (probeY & 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateProbeIndex(RectangleSize cascadeSize, int x, int y)
    {
        if (x < 0 || x >= cascadeSize.Width || y < 0 || y >= cascadeSize.Height)
        {
            return 0;
        }

        return (cascadeSize.Height * x) + y + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vec3 GetLightMaskTransparency(LightMaskIndex maskIndex, byte lengthIndex) =>
        _lightMaskTransparency[((LengthStepCount + 1) * (byte)maskIndex) + lengthIndex];
}
