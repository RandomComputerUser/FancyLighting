using FancyLighting.Utils.Accessors;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using ReLogic.Content;
using Terraria.Graphics.Light;

namespace FancyLighting;

public sealed class SmoothLighting
{
    private Texture2D _colors;
    private RenderTarget2D _colorsHiRes;

    private readonly Texture2D _ditherNoise;

    private Rectangle _lightMapTileArea;

    private RenderTarget2D _drawTarget;

    private Vector3[] _lights;
    private bool[] _hasLight;
    private Rgba1010102[] _finalLights;
    private HalfVector4[] _finalLightsHiDef;

    internal Vector3[] _whiteLights;
    internal Vector3[] _tmpLights;
    internal Vector3[] _blackLights;

    private bool _smoothLightingLightMapValid;
    private bool _smoothLightingComplete;
    private bool _smoothLightingHiResComplete;

    internal RenderTarget2D _cameraModeTarget1;
    internal RenderTarget2D _cameraModeTarget2;

    internal bool CanDrawSmoothLighting =>
        _smoothLightingComplete && LightingConfig.Instance.SmoothLightingEnabled();

    private Shader _bicubicFilteringShader;
    private Shader _normalsShader;
    private Shader _normalsOverbrightShader;
    private Shader _normalsOverbrightAmbientOcclusionShader;
    private Shader _normalsOverbrightLightOnlyShader;
    private Shader _normalsOverbrightLightOnlyOpaqueShader;
    private Shader _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader;
    private Shader _overbrightShader;
    private Shader _overbrightAmbientOcclusionShader;
    private Shader _overbrightLightOnlyShader;
    private Shader _overbrightLightOnlyOpaqueShader;
    private Shader _overbrightLightOnlyOpaqueAmbientOcclusionShader;
    private Shader _overbrightMaxShader;
    private Shader _inverseOverbrightMaxHiDefShader;
    private Shader _lightOnlyShader;
    private Shader _brightenShader;
    private Shader _glowMaskShader;
    private Shader _enhancedGlowMaskShader;

    /// <summary>
    /// Modify the lighting of a tile.
    /// </summary>
    /// <param name="tile">The affected tile.</param>
    /// <param name="x">The x-coordinate of the tile.</param>
    /// <param name="y">The y-coordinate of the tile.</param>
    /// <param name="lightColor">The light color at the location of the tile, after global brightness is applied.</param>
    /// <remarks>
    /// It is highly recommended to avoid having side effects.
    /// </remarks>
    public delegate void TileLightModifier(
        Tile tile,
        int x,
        int y,
        ref Vector3 lightColor
    );

    /// <summary>
    /// The <see cref="TileLightModifier"/> functions currently associated with each tile type, changing how those tile types are lit.
    /// </summary>
    /// <remarks>
    /// This can be null if there is currently no custom tile lighting.
    /// Custom tile lighting affects only how tiles appear to be lit when using Smooth Lighting; there is no effect on any other part of the game.
    /// Before adding custom tile lighting, it is recommended to test whether a tile appears differently using Smooth Lighting compared to vanilla lighting.
    /// In most cases, custom tile lighting is not needed since Smooth Lighting preserves glow effects.
    /// </remarks>
    public static TileLightModifier[] TileLightModifiers = null;

    /// <summary>
    /// Set custom tile lighting for a particular tile type.
    /// </summary>
    /// <param name="tileType">The affected tile type.</param>
    /// <param name="tileLightModifier">The function that modifies the tile's light color. Can be null to remove custom tile lighting for this tile type.</param>
    /// <returns>Whether any changes were made.</returns>
    /// <remarks>
    /// This function changes the <see cref="TileLightModifiers"/> array.
    /// </remarks>
    public static bool SetCustomTileLighting(
        int tileType,
        TileLightModifier tileLightModifier
    )
    {
        if (TileLightModifiers is null)
        {
            if (tileLightModifier is null)
            {
                return false;
            }

            TileLightModifiers = new TileLightModifier[TileLoader.TileCount];
        }

        ref var activeModifier = ref TileLightModifiers[tileType];
        var changed = !ReferenceEquals(activeModifier, tileLightModifier);
        activeModifier = tileLightModifier;
        return changed;
    }

    /// <summary>
    /// Handle an update to the light map.
    /// </summary>
    /// <param name="lightMapTexture">The texture used to sample the light map.</param>
    /// <param name="samplingTransformation">A transformation matrix that converts world coordinates (in pixels) to normalized coordinates for sampling <paramref name="lightMapTexture"></paramref>.</param>
    /// <param name="lightMapArea">The area of the world covered by the light map, measured in tiles.</param>
    /// <param name="cameraMode">Whether the light map is for a camera mode capture.</param>
    /// <remarks>
    /// The dimensions of <paramref name="lightMapTexture"></paramref> may not match the dimensions of the light map in tiles.
    /// </remarks>
    public delegate void LightMapUpdateHandler(
        Texture2D lightMapTexture,
        Matrix samplingTransformation,
        Rectangle lightMapArea,
        bool cameraMode
    );

    /// <summary>
    /// This event is invoked after the light map is updated.
    /// </summary>
    public static event LightMapUpdateHandler PostUpdateLightMap;

    internal SmoothLighting()
    {
        _lightMapTileArea = new(0, 0, 0, 0);

        _smoothLightingLightMapValid = false;
        _smoothLightingComplete = false;
        _smoothLightingHiResComplete = false;

        _tmpLights = null;

        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _bicubicFilteringShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "BicubicFiltering"
        );

        _normalsShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Normals"
        );
        _normalsOverbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbright",
            true
        );
        _normalsOverbrightAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightAmbientOcclusion",
            true
        );
        _normalsOverbrightLightOnlyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnly",
            true
        );
        _normalsOverbrightLightOnlyOpaqueShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnlyOpaque",
            true
        );
        _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnlyOpaqueAmbientOcclusion",
            true
        );
        _overbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Overbright",
            true
        );
        _overbrightAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightAmbientOcclusion",
            true
        );
        _overbrightLightOnlyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnly",
            true
        );
        _overbrightLightOnlyOpaqueShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnlyOpaque",
            true
        );
        _overbrightLightOnlyOpaqueAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnlyOpaqueAmbientOcclusion",
            true
        );
        _overbrightMaxShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightMax",
            true
        );
        _inverseOverbrightMaxHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "InverseOverbrightMaxHiDef"
        );
        _lightOnlyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "LightOnly"
        );
        _brightenShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Brighten"
        );
        _glowMaskShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "GlowMask"
        );
        _enhancedGlowMaskShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "EnhancedGlowMask"
        );
    }

    internal void Unload()
    {
        TileLightModifiers = null;
        PostUpdateLightMap = null;
        _drawTarget?.Dispose();
        _colors?.Dispose();
        _colorsHiRes?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _bicubicFilteringShader);
        EffectLoader.UnloadEffect(ref _normalsShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightLightOnlyShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightLightOnlyOpaqueShader);
        EffectLoader.UnloadEffect(
            ref _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader
        );
        EffectLoader.UnloadEffect(ref _overbrightShader);
        EffectLoader.UnloadEffect(ref _overbrightAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyOpaqueShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyOpaqueAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _overbrightMaxShader);
        EffectLoader.UnloadEffect(ref _inverseOverbrightMaxHiDefShader);
        EffectLoader.UnloadEffect(ref _lightOnlyShader);
        EffectLoader.UnloadEffect(ref _brightenShader);
        EffectLoader.UnloadEffect(ref _glowMaskShader);
        EffectLoader.UnloadEffect(ref _enhancedGlowMaskShader);
    }

    internal void InvalidateSmoothLighting() => _smoothLightingComplete = false;

    internal void ApplyLightOnlyShader() => _lightOnlyShader.Apply();

    internal void ApplyBrightenShader(float brightness) =>
        _brightenShader.SetParameter("BrightnessMult", brightness).Apply();

    private static void TileShine(ref Vector3 color, Tile tile, float shimmerAlpha)
    {
        var type = tile.TileType;
        var frameX = tile.TileFrameX;
        if (!TileDrawingAccessors.ShouldTileShine(null, type, frameX))
        {
            return;
        }

        Main.shine(ref color, type);
        if (shimmerAlpha > 0f)
        {
            // This code is adapted from vanilla
            // The vanilla Main.shine() function limits each component to a max of 1
            // We don't want that
            var shimmerShine = MainAccessors.shimmerShine(null);
            var inverseShimmerAlpha = 1f - shimmerAlpha;
            color.X *= inverseShimmerAlpha + (shimmerAlpha * shimmerShine.X);
            color.Y *= inverseShimmerAlpha + (shimmerAlpha * shimmerShine.Y);
            color.Z *= inverseShimmerAlpha + (shimmerAlpha * shimmerShine.Z);
        }
    }

    internal void GetAndBlurLightMap(
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    )
    {
        _smoothLightingLightMapValid = false;
        _smoothLightingComplete = false;

        var length = width * height;

        ArrayUtils.MakeAtLeastSize(ref _lights, length);
        ArrayUtils.MakeAtLeastSize(ref _whiteLights, length);
        ArrayUtils.MakeAtLeastSize(ref _blackLights, length);
        ArrayUtils.MakeAtLeastSize(ref _hasLight, length);

        if (width == 0 || height == 0)
        {
            return;
        }

        if (colors.Length < length)
        {
            return;
        }

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var blurLightMap = LightingConfig.Instance.UseLightMapBlurring;
        var doGrayscale = PreferencesConfig.Instance.UseGrayscaleLighting;
        var doToneMap = LightingConfig.Instance.UseLightMapToneMapping();
        var colorProcessingNeeded = doToneMap || doGrayscale;

        if (hiDef && !LightingConfig.Instance.FancyLightingEngineEnabled())
        {
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
                        try
                        {
                            ColorUtils.GammaToLinear(ref myColors[i++]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            );
        }

        var lightEngine = (LightingEngine)
            LightingAccessors._activeEngine(null).AssertNotNull();
        var lightMapTileArea = LightingEngineAccessors._workingProcessedArea(lightEngine);

        if (blurLightMap)
        {
            BlurLightMap(colors, lightMasks, width, height, lightMapTileArea);
        }

        if (colorProcessingNeeded)
        {
            var gammaConversionNeeded = !hiDef;
            var lights = blurLightMap ? _lights : colors;

            if (gammaConversionNeeded)
            {
                Parallel.For(
                    0,
                    width,
                    SettingsSystem._parallelOptions,
                    (x) =>
                    {
                        var myHeight = height;
                        var myLights = lights;

                        var i = myHeight * x;
                        for (var y = 0; y < myHeight; ++y)
                        {
                            try
                            {
                                ColorUtils.GammaToLinear(ref myLights[i++]);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                    }
                );
            }

            if (doGrayscale)
            {
                Parallel.For(
                    0,
                    width,
                    SettingsSystem._parallelOptions,
                    (x) =>
                    {
                        var myHeight = height;
                        var myLights = lights;

                        var i = myHeight * x;
                        for (var y = 0; y < myHeight; ++y)
                        {
                            try
                            {
                                ref var lightColor = ref myLights[i++];
                                var level = ColorUtils.Luma(lightColor);
                                lightColor.X = lightColor.Y = lightColor.Z = level;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                    }
                );
            }

            if (doToneMap)
            {
                Parallel.For(
                    0,
                    width,
                    SettingsSystem._parallelOptions,
                    (x) =>
                    {
                        var myHeight = height;
                        var myLights = lights;

                        var i = myHeight * x;
                        for (var y = 0; y < myHeight; ++y)
                        {
                            try
                            {
                                ToneMapping.ToneMap(ref myLights[i++]);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                    }
                );
            }

            if (gammaConversionNeeded)
            {
                Parallel.For(
                    0,
                    width,
                    SettingsSystem._parallelOptions,
                    (x) =>
                    {
                        var myHeight = height;
                        var myLights = lights;

                        var i = myHeight * x;
                        for (var y = 0; y < myHeight; ++y)
                        {
                            try
                            {
                                ColorUtils.LinearToGamma(ref myLights[i++]);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                    }
                );
            }
        }

        if (hiDef)
        {
            var lights = blurLightMap ? _lights : colors;
            var otherLights = blurLightMap ? colors : _lights;

            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (x) =>
                {
                    var myHeight = height;
                    var myLights = lights;

                    var i = myHeight * x;
                    for (var y = 0; y < myHeight; ++y)
                    {
                        try
                        {
                            ref var lightColor = ref myLights[i++];
                            ColorUtils.LinearToGamma(ref lightColor);
                            Vector3.Multiply(
                                ref lightColor,
                                PostProcessing.HiDefBrightnessScale,
                                out lightColor
                            );
                        }
                        catch (IndexOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            );

            Array.Copy(lights, otherLights, length);
        }
        else
        {
            if (blurLightMap)
            {
                Array.Copy(_lights, colors, length);
            }
            else
            {
                Array.Copy(colors, _lights, length);
            }
        }

        const float AbsoluteLow = 1f / 255f;
        var low = AbsoluteLow / Lighting.GlobalBrightness;

        if (TileLightModifiers is null)
        {
            Parallel.For(
                0,
                width,
                SettingsSystem._parallelOptions,
                (x) =>
                {
                    var lights = _lights;
                    var hasLight = _hasLight;
                    var myLow = low;

                    var i = height * x;
                    var end = i + height;
                    while (i < end)
                    {
                        try
                        {
                            ref var color = ref lights[i];
                            hasLight[i++] =
                                color.X >= myLow || color.Y >= myLow || color.Z >= myLow;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            );
        }
        else
        {
            // Can't be parallel due to thread safety issues
            var brightness = Lighting.GlobalBrightness;
            var tileX = lightMapTileArea.X;
            for (var x = 0; x < width; ++x)
            {
                var tileY = lightMapTileArea.Y;
                var i = height * x;
                var end = i + height;
                while (i < end)
                {
                    try
                    {
                        Vector3.Multiply(ref _lights[i], brightness, out var color);

                        if (
                            0 <= tileX
                            && tileX < Main.tile.Width
                            && 0 <= tileY
                            && tileY < Main.tile.Height
                        )
                        {
                            var tile = Main.tile[tileX, tileY];
                            if (
                                tile.HasTile
                                && TileLightModifiers[tile.TileType]
                                    is { } tileLightModifier
                            )
                            {
                                tileLightModifier(tile, tileX, tileY, ref color);
                            }
                        }

                        _hasLight[i++] =
                            color.X >= AbsoluteLow
                            || color.Y >= AbsoluteLow
                            || color.Z >= AbsoluteLow;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }

                    ++tileY;
                }

                ++tileX;
            }
        }

        Parallel.For(
            1,
            width - 1,
            SettingsSystem._parallelOptions,
            (x) =>
            {
                var myHeight = height;
                var whiteLights = _whiteLights;
                var blackLights = _blackLights;
                var hasLight = _hasLight;

                var i = myHeight * x;
                for (var y = 1; y < myHeight - 1; ++y)
                {
                    try
                    {
                        ref var whiteLight = ref whiteLights[++i];
                        ref var blackLight = ref blackLights[i];

                        if (
                            hasLight[i]
                            || hasLight[i - 1]
                            || hasLight[i + 1]
                            || hasLight[i - height]
                            || hasLight[i - height - 1]
                            || hasLight[i - height + 1]
                            || hasLight[i + height]
                            || hasLight[i + height - 1]
                            || hasLight[i + height + 1]
                        )
                        {
                            whiteLight.X = whiteLight.Y = whiteLight.Z = 1f;
                            blackLight.X = blackLight.Y = blackLight.Z = 1f / 255;
                        }
                        else
                        {
                            whiteLight.X = whiteLight.Y = whiteLight.Z = 0f;
                            blackLight.X = blackLight.Y = blackLight.Z = 0f;
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }
                }
            }
        );

        _lightMapTileArea = lightMapTileArea;

        _smoothLightingLightMapValid = true;
    }

    private void BlurLightMap(
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height,
        Rectangle lightMapTileArea
    )
    {
        if (width < 3 || height < 3)
        {
            Array.Copy(
                colors,
                _lights,
                Math.Min(Math.Min(colors.Length, _lights.Length), width * height)
            );
            return;
        }

        if (LightingConfig.Instance.UseEnhancedBlurring)
        {
            if (PreferencesConfig.Instance.FancyLightingEngineNonSolidOpaque)
            {
                Parallel.For(
                    1,
                    width - 1,
                    SettingsSystem._parallelOptions,
                    (x) =>
                    {
                        var myHeight = height;
                        var myLightMasks = lightMasks;
                        var myColors = colors;
                        var lights = _lights;

                        var i = myHeight * x;
                        for (var y = 1; y < myHeight - 1; ++y)
                        {
                            ++i;

                            try
                            {
                                var mask = myLightMasks[i];

                                var upperLeftMult =
                                    myLightMasks[i - myHeight - 1] == mask ? 1f : 0f;
                                var leftMult =
                                    myLightMasks[i - myHeight] == mask ? 2f : 0f;
                                var lowerLeftMult =
                                    myLightMasks[i - myHeight + 1] == mask ? 1f : 0f;
                                var upperMult = myLightMasks[i - 1] == mask ? 2f : 0f;
                                var middleMult = mask is LightMaskMode.Solid ? 12f : 4f;
                                var lowerMult = myLightMasks[i + 1] == mask ? 2f : 0f;
                                var upperRightMult =
                                    myLightMasks[i + myHeight - 1] == mask ? 1f : 0f;
                                var rightMult =
                                    myLightMasks[i + myHeight] == mask ? 2f : 0f;
                                var lowerRightMult =
                                    myLightMasks[i + myHeight + 1] == mask ? 1f : 0f;

                                var mult =
                                    1f
                                    / (
                                        (upperLeftMult + leftMult + lowerLeftMult)
                                        + (upperMult + middleMult + lowerMult)
                                        + (upperRightMult + rightMult + lowerRightMult)
                                    );

                                ref var light = ref lights[i];

                                ref var upperLeft = ref myColors[i - myHeight - 1];
                                ref var left = ref myColors[i - myHeight];
                                ref var lowerLeft = ref myColors[i - myHeight + 1];
                                ref var upper = ref myColors[i - 1];
                                ref var middle = ref myColors[i];
                                ref var lower = ref myColors[i + 1];
                                ref var upperRight = ref myColors[i + myHeight - 1];
                                ref var right = ref myColors[i + myHeight];
                                ref var lowerRight = ref myColors[i + myHeight + 1];

                                // Faster to do it separately for each component
                                light.X =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.X)
                                            + (leftMult * left.X)
                                            + (lowerLeftMult * lowerLeft.X)
                                        )
                                        + (
                                            (upperMult * upper.X)
                                            + (middleMult * middle.X)
                                            + (lowerMult * lower.X)
                                        )
                                        + (
                                            (upperRightMult * upperRight.X)
                                            + (rightMult * right.X)
                                            + (lowerRightMult * lowerRight.X)
                                        )
                                    ) * mult;

                                light.Y =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Y)
                                            + (leftMult * left.Y)
                                            + (lowerLeftMult * lowerLeft.Y)
                                        )
                                        + (
                                            (upperMult * upper.Y)
                                            + (middleMult * middle.Y)
                                            + (lowerMult * lower.Y)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Y)
                                            + (rightMult * right.Y)
                                            + (lowerRightMult * lowerRight.Y)
                                        )
                                    ) * mult;

                                light.Z =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Z)
                                            + (leftMult * left.Z)
                                            + (lowerLeftMult * lowerLeft.Z)
                                        )
                                        + (
                                            (upperMult * upper.Z)
                                            + (middleMult * middle.Z)
                                            + (lowerMult * lower.Z)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Z)
                                            + (rightMult * right.Z)
                                            + (lowerRightMult * lowerRight.Z)
                                        )
                                    ) * mult;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                    }
                );
            }
            else
            {
                Parallel.For(
                    1,
                    width - 1,
                    SettingsSystem._parallelOptions,
                    (x) =>
                    {
                        var myHeight = height;
                        var myLightMasks = lightMasks;
                        var myColors = colors;
                        var lights = _lights;

                        var tileX = x + lightMapTileArea.X;
                        var tileY = lightMapTileArea.Y;
                        var i = myHeight * x;
                        for (var y = 1; y < myHeight - 1; ++y)
                        {
                            ++i;
                            ++tileY;

                            try
                            {
                                var mask = myLightMasks[i];
                                var isSolid = mask is LightMaskMode.Solid;
                                var isNonSolid = TileUtils.IsNonSolid(tileX, tileY);

                                var upperLeftMult =
                                    myLightMasks[i - myHeight - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX - 1, tileY - 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;
                                var leftMult =
                                    myLightMasks[i - myHeight] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX - 1, tileY)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var lowerLeftMult =
                                    myLightMasks[i - myHeight + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX - 1, tileY + 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;
                                var upperMult =
                                    myLightMasks[i - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX, tileY - 1)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var middleMult = isSolid && !isNonSolid ? 12f : 4f;
                                var lowerMult =
                                    myLightMasks[i + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX, tileY + 1)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var upperRightMult =
                                    myLightMasks[i + myHeight - 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX + 1, tileY - 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;
                                var rightMult =
                                    myLightMasks[i + myHeight] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX + 1, tileY)
                                            == isNonSolid
                                    )
                                        ? 2f
                                        : 0f;
                                var lowerRightMult =
                                    myLightMasks[i + myHeight + 1] == mask
                                    && (
                                        !isSolid
                                        || TileUtils.IsNonSolid(tileX + 1, tileY + 1)
                                            == isNonSolid
                                    )
                                        ? 1f
                                        : 0f;

                                var mult =
                                    1f
                                    / (
                                        (upperLeftMult + leftMult + lowerLeftMult)
                                        + (upperMult + middleMult + lowerMult)
                                        + (upperRightMult + rightMult + lowerRightMult)
                                    );

                                ref var light = ref lights[i];

                                ref var upperLeft = ref myColors[i - myHeight - 1];
                                ref var left = ref myColors[i - myHeight];
                                ref var lowerLeft = ref myColors[i - myHeight + 1];
                                ref var upper = ref myColors[i - 1];
                                ref var middle = ref myColors[i];
                                ref var lower = ref myColors[i + 1];
                                ref var upperRight = ref myColors[i + myHeight - 1];
                                ref var right = ref myColors[i + myHeight];
                                ref var lowerRight = ref myColors[i + myHeight + 1];

                                // Faster to do it separately for each component
                                light.X =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.X)
                                            + (leftMult * left.X)
                                            + (lowerLeftMult * lowerLeft.X)
                                        )
                                        + (
                                            (upperMult * upper.X)
                                            + (middleMult * middle.X)
                                            + (lowerMult * lower.X)
                                        )
                                        + (
                                            (upperRightMult * upperRight.X)
                                            + (rightMult * right.X)
                                            + (lowerRightMult * lowerRight.X)
                                        )
                                    ) * mult;

                                light.Y =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Y)
                                            + (leftMult * left.Y)
                                            + (lowerLeftMult * lowerLeft.Y)
                                        )
                                        + (
                                            (upperMult * upper.Y)
                                            + (middleMult * middle.Y)
                                            + (lowerMult * lower.Y)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Y)
                                            + (rightMult * right.Y)
                                            + (lowerRightMult * lowerRight.Y)
                                        )
                                    ) * mult;

                                light.Z =
                                    (
                                        (
                                            (upperLeftMult * upperLeft.Z)
                                            + (leftMult * left.Z)
                                            + (lowerLeftMult * lowerLeft.Z)
                                        )
                                        + (
                                            (upperMult * upper.Z)
                                            + (middleMult * middle.Z)
                                            + (lowerMult * lower.Z)
                                        )
                                        + (
                                            (upperRightMult * upperRight.Z)
                                            + (rightMult * right.Z)
                                            + (lowerRightMult * lowerRight.Z)
                                        )
                                    ) * mult;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                break;
                            }
                        }
                    }
                );
            }
        }
        else
        {
            Parallel.For(
                1,
                width - 1,
                SettingsSystem._parallelOptions,
                (x) =>
                {
                    var myHeight = height;
                    var myColors = colors;
                    var lights = _lights;

                    var i = myHeight * x;
                    for (var y = 1; y < myHeight - 1; ++y)
                    {
                        ++i;

                        try
                        {
                            ref var light = ref lights[i];

                            ref var upperLeft = ref myColors[i - myHeight - 1];
                            ref var left = ref myColors[i - myHeight];
                            ref var lowerLeft = ref myColors[i - myHeight + 1];
                            ref var upper = ref myColors[i - 1];
                            ref var middle = ref myColors[i];
                            ref var lower = ref myColors[i + 1];
                            ref var upperRight = ref myColors[i + myHeight - 1];
                            ref var right = ref myColors[i + myHeight];
                            ref var lowerRight = ref myColors[i + myHeight + 1];

                            // Faster to do it separately for each component
                            light.X =
                                (
                                    (upperLeft.X + (2f * left.X) + lowerLeft.X)
                                    + (2f * (upper.X + (2f * middle.X) + lower.X))
                                    + (upperRight.X + (2f * right.X) + lowerRight.X)
                                ) * (1f / 16f);

                            light.Y =
                                (
                                    (upperLeft.Y + (2f * left.Y) + lowerLeft.Y)
                                    + (2f * (upper.Y + (2f * middle.Y) + lower.Y))
                                    + (upperRight.Y + (2f * right.Y) + lowerRight.Y)
                                ) * (1f / 16f);

                            light.Z =
                                (
                                    (upperLeft.Z + (2f * left.Z) + lowerLeft.Z)
                                    + (2f * (upper.Z + (2f * middle.Z) + lower.Z))
                                    + (upperRight.Z + (2f * right.Z) + lowerRight.Z)
                                ) * (1f / 16f);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            );
        }

        var offset = (width - 1) * height;
        for (var i = 0; i < height; ++i)
        {
            try
            {
                _lights[i] = colors[i];
                _lights[i + offset] = colors[i + offset];
            }
            catch (IndexOutOfRangeException)
            {
                break;
            }
        }

        var end = (width - 1) * height;
        offset = height - 1;
        for (var i = height; i < end; i += height)
        {
            try
            {
                _lights[i] = colors[i];
                _lights[i + offset] = colors[i + offset];
            }
            catch (IndexOutOfRangeException)
            {
                break;
            }
        }
    }

    internal void CalculateSmoothLighting(bool cameraMode = false)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!_smoothLightingLightMapValid)
        {
            return;
        }

        if (!cameraMode && _smoothLightingComplete)
        {
            return;
        }

        if (Main.tile.Height == 0 || Main.tile.Width == 0)
        {
            return;
        }

        var xmin = _lightMapTileArea.X;
        var ymin = _lightMapTileArea.Y;
        var width = _lightMapTileArea.Width;
        var height = _lightMapTileArea.Height;
        var ymax = ymin + height;

        var clampedXmin = Math.Clamp(xmin, 0, Main.tile.Width);
        var clampedXmax = Math.Clamp(xmin + width, 0, Main.tile.Width);
        if (clampedXmax - clampedXmin < 1)
        {
            return;
        }

        var clampedStart = Math.Clamp(clampedXmin - xmin, 0, width);
        var clampedEnd = Math.Clamp(clampedXmax - clampedXmin, 0, width);
        if (clampedEnd - clampedStart < 1)
        {
            return;
        }

        var clampedYmin = Math.Clamp(ymin, 0, Main.tile.Height);
        var clampedYmax = Math.Clamp(ymax, 0, Main.tile.Height);
        if (clampedYmax - clampedYmin < 1)
        {
            return;
        }

        var offset = clampedYmin - ymin;
        if (offset < 0 || offset >= height)
        {
            return;
        }

        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();

        if (doOverbright)
        {
            CalculateSmoothLightingHdr(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                cameraMode
            );
        }
        else
        {
            CalculateSmoothLightingLdr(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                cameraMode
            );
        }

        var invokeEvent = PostUpdateLightMap != null;

        if (doBicubicUpscaling && invokeEvent)
        {
            RenderHiResLighting(_colors);
            _smoothLightingHiResComplete = true;
        }
        else
        {
            _smoothLightingHiResComplete = false;
        }

        if (!invokeEvent)
        {
            return;
        }

        var lightMapTexture = doBicubicUpscaling ? _colorsHiRes : _colors;
        var scale = doBicubicUpscaling ? 0.25f : 1f;

        var transformation = Matrix.Identity;
        transformation.Right = new(0f, 1f / (16f * scale * lightMapTexture.Height), 0f);
        transformation.Up = new(1f / (16f * scale * lightMapTexture.Width), 0f, 0f);
        var origin = 16f * new Vector2(_lightMapTileArea.X, _lightMapTileArea.Y);
        var translation = -Vector2.Transform(origin, transformation);
        transformation.Translation = new Vector3(translation.X, translation.Y, 0f);

        PostUpdateLightMap?.Invoke(
            lightMapTexture,
            transformation,
            _lightMapTileArea,
            cameraMode
        );
    }

    private void CalculateSmoothLightingHdr(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool cameraMode
    )
    {
        var length = width * height;

        ArrayUtils.MakeAtLeastSize(ref _finalLightsHiDef, length);
        _finalLights = null; // Save some memory

        var brightness = Lighting.GlobalBrightness;
        var shimmerAlpha = Main.shimmerAlpha;
        Main.shimmerAlpha = 0f;
        if (TileLightModifiers is null)
        {
            Parallel.For(
                clampedStart,
                clampedEnd,
                SettingsSystem._parallelOptions,
                (x1) =>
                {
                    var myClampedYmax = clampedYmax;
                    var lights = _lights;
                    var myBrightness = brightness;
                    var myShimmerAlpha = shimmerAlpha;
                    var finalLightsHiDef = _finalLightsHiDef;

                    var i = (height * x1) + offset;
                    var x = x1 + xmin;
                    for (var y = clampedYmin; y < myClampedYmax; ++y)
                    {
                        try
                        {
                            Vector3.Multiply(
                                ref lights[i],
                                myBrightness,
                                out var lightColor
                            );
                            var tile = Main.tile[x, y];

                            if (TileUtils.HasShimmer(tile))
                            {
                                lightColor.X = Math.Max(lightColor.X, 1f);
                                lightColor.Y = Math.Max(lightColor.Y, 1f);
                                lightColor.Z = Math.Max(lightColor.Z, 1f);
                            }

                            TileShine(ref lightColor, tile, myShimmerAlpha);
                            ColorUtils.Assign(ref finalLightsHiDef[i++], lightColor);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            );
        }
        else
        {
            // Can't be parallel due to thread safety issues
            for (var x1 = clampedStart; x1 < clampedEnd; ++x1)
            {
                var i = (height * x1) + offset;
                var x = x1 + xmin;
                for (var y = clampedYmin; y < clampedYmax; ++y)
                {
                    try
                    {
                        Vector3.Multiply(ref _lights[i], brightness, out var lightColor);
                        var tile = Main.tile[x, y];

                        if (
                            tile.HasTile
                            && TileLightModifiers[tile.TileType] is { } tileLightModifier
                        )
                        {
                            Main.shimmerAlpha = shimmerAlpha;
                            tileLightModifier(tile, x, y, ref lightColor);
                            Main.shimmerAlpha = 0f;
                        }
                        else
                        {
                            if (TileUtils.HasShimmer(tile))
                            {
                                lightColor.X = Math.Max(lightColor.X, 1f);
                                lightColor.Y = Math.Max(lightColor.Y, 1f);
                                lightColor.Z = Math.Max(lightColor.Z, 1f);
                            }

                            TileShine(ref lightColor, tile, shimmerAlpha);
                        }

                        ColorUtils.Assign(ref _finalLightsHiDef[i++], lightColor);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }
                }
            }
        }
        Main.shimmerAlpha = shimmerAlpha;

        TextureUtils.MakeAtLeastSize(
            ref _colors,
            height,
            width,
            SurfaceFormat.HalfVector4
        );

        _colors.SetData(0, new(0, 0, height, width), _finalLightsHiDef, 0, length);

        _smoothLightingComplete = !cameraMode;
    }

    private void CalculateSmoothLightingLdr(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool cameraMode
    )
    {
        var length = width * height;

        ArrayUtils.MakeAtLeastSize(ref _finalLights, length);
        _finalLightsHiDef = null; // Save some memory

        var brightness = Lighting.GlobalBrightness;
        var shimmerAlpha = Main.shimmerAlpha;
        Main.shimmerAlpha = 0f;
        if (TileLightModifiers is null)
        {
            Parallel.For(
                clampedStart,
                clampedEnd,
                SettingsSystem._parallelOptions,
                (x1) =>
                {
                    var myClampedYmax = clampedYmax;
                    var lights = _lights;
                    var myBrightness = brightness;
                    var myShimmerAlpha = shimmerAlpha;
                    var finalLights = _finalLights;

                    var i = (height * x1) + offset;
                    var x = x1 + xmin;
                    for (var y = clampedYmin; y < myClampedYmax; ++y)
                    {
                        try
                        {
                            Vector3.Multiply(
                                ref lights[i],
                                myBrightness,
                                out var lightColor
                            );
                            var tile = Main.tile[x, y];

                            if (TileUtils.HasShimmer(tile))
                            {
                                lightColor.X = Math.Max(lightColor.X, 1f);
                                lightColor.Y = Math.Max(lightColor.Y, 1f);
                                lightColor.Z = Math.Max(lightColor.Z, 1f);
                            }

                            TileShine(ref lightColor, tile, myShimmerAlpha);
                            ColorUtils.Assign(ref finalLights[i++], 1f, lightColor);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            );
        }
        else
        {
            // Can't be parallel due to thread safety issues
            for (var x1 = clampedStart; x1 < clampedEnd; ++x1)
            {
                var i = (height * x1) + offset;
                var x = x1 + xmin;
                for (var y = clampedYmin; y < clampedYmax; ++y)
                {
                    try
                    {
                        Vector3.Multiply(ref _lights[i], brightness, out var lightColor);
                        var tile = Main.tile[x, y];

                        if (
                            tile.HasTile
                            && TileLightModifiers[tile.TileType] is { } tileLightModifier
                        )
                        {
                            Main.shimmerAlpha = shimmerAlpha;
                            tileLightModifier(tile, x, y, ref lightColor);
                            Main.shimmerAlpha = 0f;
                        }
                        else
                        {
                            if (TileUtils.HasShimmer(tile))
                            {
                                lightColor.X = Math.Max(lightColor.X, 1f);
                                lightColor.Y = Math.Max(lightColor.Y, 1f);
                                lightColor.Z = Math.Max(lightColor.Z, 1f);
                            }

                            TileShine(ref lightColor, tile, shimmerAlpha);
                        }

                        ColorUtils.Assign(ref _finalLights[i++], 1f, lightColor);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }
                }
            }
        }
        Main.shimmerAlpha = shimmerAlpha;

        TextureUtils.MakeAtLeastSize(
            ref _colors,
            height,
            width,
            SurfaceFormat.Rgba1010102
        );

        _colors.SetData(0, new(0, 0, height, width), _finalLights, 0, length);

        _smoothLightingComplete = !cameraMode;
    }

    private void RenderHiResLighting(Texture2D lights)
    {
        TextureUtils.MakeSize(
            ref _colorsHiRes,
            4 * lights.Width,
            4 * lights.Height,
            TextureUtils.LightMapFormat
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(_colorsHiRes);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _bicubicFilteringShader
            .SetParameter("LightMapSize", lights.Size())
            .SetParameter("PixelSize", new Vector2(1f / lights.Width, 1f / lights.Height))
            .Apply();
        Main.spriteBatch.Draw(
            lights,
            Vector2.Zero,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();
    }

    internal void DrawSmoothLighting(
        RenderTarget2D target,
        RenderTarget2D outputTarget,
        bool background,
        bool disableNormalMaps = false,
        bool doScaling = false,
        bool invertOverbright = false,
        RenderTarget2D ambientOcclusionTarget = null
    )
    {
        if (!_smoothLightingComplete)
        {
            return;
        }

        Vector2 offset;
        var tmpTarget = outputTarget;
        if (tmpTarget is null)
        {
            TextureUtils.MakeSize(
                ref _drawTarget,
                target.Width,
                target.Height,
                TextureUtils.ScreenFormat
            );
            tmpTarget = _drawTarget;
            offset = new(Main.offScreenRange);
        }
        else
        {
            offset =
                (tmpTarget.Size() - new Vector2(Main.screenWidth, Main.screenHeight))
                / 2f;
        }

        var lightMapTexture = _colors;

        ApplySmoothLighting(
            lightMapTexture,
            tmpTarget,
            16f * new Vector2(_lightMapTileArea.X, _lightMapTileArea.Y),
            Main.screenPosition - offset,
            doScaling && !FancyLightingMod._inCameraMode
                ? Main.GameViewMatrix.Zoom
                    * new Vector2(1f, Main.LocalPlayer.gravDir < 0f ? -1f : 1f)
                : Vector2.One,
            target,
            background,
            disableNormalMaps,
            doScaling,
            invertOverbright,
            ambientOcclusionTarget
        );

        if (outputTarget is not null)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(target);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(tmpTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }

    internal RenderTarget2D GetCameraModeRenderTarget(RenderTarget2D screenTarget)
    {
        TextureUtils.MakeSize(
            ref _cameraModeTarget1,
            screenTarget.Width,
            screenTarget.Height,
            TextureUtils.ScreenFormat
        );
        return _cameraModeTarget1;
    }

    internal void DrawSmoothLightingCameraMode(
        RenderTarget2D worldTarget,
        RenderTarget2D screenTarget,
        bool background,
        bool skipFinalPass = false,
        bool disableNormalMaps = false,
        bool doOverbrightMax = false,
        bool invertOverbright = false,
        RenderTarget2D ambientOcclusionTarget = null,
        Texture2D glow = null,
        Texture2D lightedGlow = null
    )
    {
        var lightMapTexture = _colors;

        TextureUtils.MakeSize(
            ref _cameraModeTarget2,
            16 * lightMapTexture.Height,
            16 * lightMapTexture.Width,
            TextureUtils.ScreenFormat
        );

        ApplySmoothLighting(
            lightMapTexture,
            _cameraModeTarget2,
            16f * new Vector2(_lightMapTileArea.X, _lightMapTileArea.Y),
            16f
                * new Vector2(
                    FancyLightingMod._cameraModeArea.X,
                    FancyLightingMod._cameraModeArea.Y
                ),
            Vector2.One,
            worldTarget,
            background,
            disableNormalMaps,
            doOverbrightMax,
            invertOverbright,
            ambientOcclusionTarget
        );

        if (skipFinalPass)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_cameraModeTarget1, Vector2.Zero, Color.White);
        if (glow is null)
        {
            Main.spriteBatch.Draw(_cameraModeTarget2, Vector2.Zero, Color.White);
        }
        else
        {
            MainGraphics.ResetSavedTextures();

            if (lightedGlow is null)
            {
                _glowMaskShader
                    .SetParameter(
                        "GlowCoordMult",
                        new Vector2(
                            (float)_cameraModeTarget2.Width / glow.Width,
                            (float)_cameraModeTarget2.Height / glow.Height
                        )
                    )
                    .Apply();
            }
            else
            {
                _enhancedGlowMaskShader
                    .SetParameter(
                        "GlowCoordMult",
                        new Vector2(
                            (float)_cameraModeTarget2.Width / glow.Width,
                            (float)_cameraModeTarget2.Height / glow.Height
                        )
                    )
                    .SetParameter(
                        "LightedGlowCoordMult",
                        new Vector2(
                            (float)_cameraModeTarget2.Width / lightedGlow.Width,
                            (float)_cameraModeTarget2.Height / lightedGlow.Height
                        )
                    )
                    .Apply();
                MainGraphics.SetTexture(5, lightedGlow, SamplerState.PointClamp);
            }

            MainGraphics.SetTexture(4, glow, SamplerState.PointClamp);
            Main.spriteBatch.Draw(_cameraModeTarget2, Vector2.Zero, Color.White);
            MainGraphics.RestoreSavedTextures();
        }
        Main.spriteBatch.End();
    }

    private void ApplySmoothLighting(
        Texture2D lightMapTexture,
        RenderTarget2D outputTarget,
        Vector2 lightMapPosition,
        Vector2 worldPosition,
        Vector2 zoom,
        RenderTarget2D worldTarget,
        bool background,
        bool disableNormalMaps,
        bool doScaling,
        bool invertOverbright,
        RenderTarget2D ambientOcclusionTarget
    )
    {
        var fineNormalMaps = PreferencesConfig.Instance.FineNormalMaps;
        var doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();
        var simulateNormalMaps =
            !disableNormalMaps && LightingConfig.Instance.SimulateNormalMaps;
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var lightOnly = PreferencesConfig.Instance.RenderOnlyLight;
        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var doOneStepOnly = !(simulateNormalMaps || doOverbright);
        var doAmbientOcclusion = background && ambientOcclusionTarget is not null;
        var doDithering = doOverbright && !hiDef;

        var lightMapScale = 16f;

        if (doBicubicUpscaling)
        {
            if (!_smoothLightingHiResComplete)
            {
                RenderHiResLighting(lightMapTexture);
                _smoothLightingHiResComplete = true;
            }

            lightMapTexture = _colorsHiRes;
            lightMapScale = 4f;
        }

        // We need to correct for the orientation of the light map texture
        // In the light map texture, X and Y are flipped compared to world coordinates

        var lightMapCenter =
            lightMapPosition
            + (
                0.5f
                * lightMapScale
                * new Vector2(lightMapTexture.Height, lightMapTexture.Width)
            );
        var worldCenter =
            worldPosition + (0.5f * new Vector2(worldTarget.Width, worldTarget.Height));
        var position =
            (zoom * (lightMapCenter - worldCenter))
            + (0.5f * new Vector2(worldTarget.Width, worldTarget.Height));
        var rotation = -MathHelper.PiOver2;
        var origin = 0.5f * new Vector2(lightMapTexture.Width, lightMapTexture.Height);
        var scale = lightMapScale * new Vector2(zoom.Y, zoom.X);

        Main.graphics.GraphicsDevice.SetRenderTarget(outputTarget);

        if (doOneStepOnly)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(
                lightMapTexture,
                position,
                null,
                Color.White,
                rotation,
                origin,
                scale,
                SpriteEffects.FlipHorizontally,
                0f
            );
            Main.spriteBatch.End();

            if (!lightOnly)
            {
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendStates.Multiply,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(worldTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }

            return;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        var shader = simulateNormalMaps
            ? doOverbright
                ? lightOnly
                    ? background
                        ? doAmbientOcclusion
                            ? _normalsOverbrightLightOnlyOpaqueAmbientOcclusionShader
                            : _normalsOverbrightLightOnlyOpaqueShader
                        : _normalsOverbrightLightOnlyShader
                    : doAmbientOcclusion
                        ? _normalsOverbrightAmbientOcclusionShader
                        : _normalsOverbrightShader
                : _normalsShader
            : doScaling // doOverbright is guaranteed to be true here
                ? invertOverbright // if doScaling is true we're doing post-processing
                    ? _inverseOverbrightMaxHiDefShader
                    : _overbrightMaxShader
                : lightOnly
                    ? background
                        ? doAmbientOcclusion
                            ? _overbrightLightOnlyOpaqueAmbientOcclusionShader
                            : _overbrightLightOnlyOpaqueShader
                        : _overbrightLightOnlyShader
                    : doAmbientOcclusion
                        ? _overbrightAmbientOcclusionShader
                        : _overbrightShader;

        var gamma = PostProcessing.ContentGamma();
        var normalMapResolution = fineNormalMaps ? 1f : 2f;
        var overbrightMult = doOverbright
            ? hiDef
                ? 1f / PostProcessing.HiDefBrightnessScale
                : 1f
            : 1f;
        var normalMapGradientMult = 24f * overbrightMult * zoom;
        var normalMapMult = PreferencesConfig.Instance.NormalMapsMultiplier();
        normalMapMult *= background ? 0.75f : 0.875f;
        var normalMapStrength = 1f / (1f + normalMapMult);

        var worldCoordMult =
            new Vector2(scale.Y, scale.X)
            * new Vector2(lightMapTexture.Height, lightMapTexture.Width)
            / new Vector2(worldTarget.Width, worldTarget.Height);

        shader
            .SetParameter("ReciprocalGamma", 1f / gamma)
            .SetParameter(
                "NormalMapResolution",
                new Vector2(
                    normalMapResolution / worldTarget.Width,
                    normalMapResolution / worldTarget.Height
                )
            )
            .SetParameter("NormalMapGradientMult", normalMapGradientMult)
            .SetParameter("NormalMapStrength", normalMapStrength)
            .SetParameter("WorldCoordMult", worldCoordMult)
            .SetParameter(
                "WorldCoordOffset",
                (position / new Vector2(worldTarget.Width, worldTarget.Height))
                    - (worldCoordMult * new Vector2(0.5f))
            );

        MainGraphics.ResetSavedTextures();
        MainGraphics.SetTexture(4, worldTarget, SamplerState.PointClamp);
        if (doAmbientOcclusion)
        {
            MainGraphics.SetTexture(5, ambientOcclusionTarget, SamplerState.PointClamp);
        }

        if (doDithering)
        {
            shader
                .SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        4f * zoom.Y * lightMapTexture.Width / _ditherNoise.Width,
                        4f * zoom.X * lightMapTexture.Height / _ditherNoise.Height
                    )
                )
                .Apply();
            MainGraphics.SetTexture(6, _ditherNoise, SamplerState.PointWrap);
        }

        shader.Apply();
        Main.spriteBatch.Draw(
            lightMapTexture,
            position,
            null,
            Color.White,
            rotation,
            origin,
            scale,
            SpriteEffects.FlipHorizontally,
            0f
        );
        Main.spriteBatch.End();
        MainGraphics.RestoreSavedTextures();

        if (!doOverbright && !lightOnly)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendStates.Multiply,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(worldTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
    }

    internal void DrawGlow(
        Texture2D lighted,
        Texture2D glow,
        Texture2D lightedGlow = null
    )
    {
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        MainGraphics.ResetSavedTextures();

        if (lightedGlow is null)
        {
            _glowMaskShader
                .SetParameter(
                    "GlowCoordMult",
                    new Vector2(
                        (float)lighted.Width / glow.Width,
                        (float)lighted.Height / glow.Height
                    )
                )
                .Apply();
        }
        else
        {
            _enhancedGlowMaskShader
                .SetParameter(
                    "GlowCoordMult",
                    new Vector2(
                        (float)lighted.Width / glow.Width,
                        (float)lighted.Height / glow.Height
                    )
                )
                .SetParameter(
                    "LightedGlowCoordMult",
                    new Vector2(
                        (float)lighted.Width / lightedGlow.Width,
                        (float)lighted.Height / lightedGlow.Height
                    )
                )
                .Apply();
            MainGraphics.SetTexture(5, lightedGlow, SamplerState.PointClamp);
        }

        MainGraphics.SetTexture(4, glow, SamplerState.PointClamp);
        Main.spriteBatch.Draw(lighted, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        MainGraphics.RestoreSavedTextures();
    }
}
