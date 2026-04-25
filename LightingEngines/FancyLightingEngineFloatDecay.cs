using System.Runtime.CompilerServices;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

public abstract class FancyLightingEngineFloatDecay : FancyLightingEngineBase
{
    private float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    private float[] _lightWaterDecay;
    private float[] _lightHoneyDecay;
    private float[] _lightNonSolidDecay;

    protected float[][] _lightMasks;

    protected void InitializeDecayArrays()
    {
        _lightAirDecay = new float[DistanceTicks + 2];
        _lightSolidDecay = new float[DistanceTicks + 2];
        _lightWaterDecay = new float[DistanceTicks + 2];
        _lightHoneyDecay = new float[DistanceTicks + 2];
        _lightNonSolidDecay = new float[DistanceTicks + 2];
        for (var exponent = 0; exponent < DistanceTicks + 2; ++exponent)
        {
            _lightAirDecay[exponent] =
                _lightSolidDecay[exponent] =
                _lightWaterDecay[exponent] =
                _lightHoneyDecay[exponent] =
                _lightNonSolidDecay[exponent] =
                    1f;
        }
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

        // Last element is for diagonal decay (used in GI)
        decay[^1] = MathF.Exp(MathF.Sqrt(2) * logBaseline);
    }

    protected void UpdateLightMasks(LightMaskMode[] lightMasks, int width, int height)
    {
        ArrayUtils.MakeAtLeastSize(ref _lightMasks, width * height);

        if (PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
        {
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (i) =>
                {
                    var paramLightMasks = lightMasks;
                    var myLightMasks = _lightMasks;

                    var endIndex = height * (i + 1);
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        myLightMasks[j] = paramLightMasks[j] switch
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
                    var paramLightMasks = lightMasks;
                    var myLightMasks = _lightMasks;

                    var endIndex = height * (i + 1);
                    var x = i + _lightMapArea.X;
                    var y = _lightMapArea.Y;
                    for (var j = height * i; j < endIndex; ++j)
                    {
                        myLightMasks[j] = paramLightMasks[j] switch
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

    protected override void SimulateGlobalIllumination(
        int width,
        int height,
        int passCount
    )
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
                        dst[j] =
                            (lightMasks[j] == solidDecay)
                                ? Vec3.Zero
                                : Vec3.Multiply(giMult, src[j]);
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
                for (var j = beginIndex; j != endIndex; )
                {
                    j += inc;

                    var mask = lightMasks[j];
                    var preventSpread = mask != solidDecay && prevMask == solidDecay;
                    prevMask = mask;

                    dst[j] = light = preventSpread
                        ? src[j]
                        : Vec3.Max(src[j], Vec3.Multiply(prevMask[DistanceTicks], light));
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
                for (var j = beginIndex; j != endIndex; )
                {
                    j += inc;

                    var mask = lightMasks[j];
                    var preventSpread =
                        mask != solidDecay
                        && (
                            prevMask == solidDecay
                            || (
                                lightMasks[j + diff1] == solidDecay
                                && lightMasks[j + diff2] == solidDecay
                            )
                        );
                    prevMask = mask;

                    var srcLight = Vec3.Max(src1[j], src2[j]);
                    light = preventSpread
                        ? srcLight
                        : Vec3.Max(
                            srcLight,
                            Vec3.Multiply(prevMask[DistanceTicks + 1], light)
                        );
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
}
