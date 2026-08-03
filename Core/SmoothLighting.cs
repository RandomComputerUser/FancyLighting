using FancyLighting.Core.Sky;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using ReLogic.Content;
using Terraria.Graphics.Light;
using Terraria.ID;

namespace FancyLighting.Core;

public sealed class SmoothLighting
{
    internal const float NormalMapGradientBaseMult = 1.5f;

    private readonly Texture2D _ditherNoise;

    private Texture2D _colors;
    private RenderTarget2D _colorsHiRes;
    private RenderTarget2D _prevColorsHiRes;

    private Rectangle _lightMapTileArea;
    private Rectangle _prevLightMapTileArea;

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
    private bool _useAlphaChannelAsSkyLightLuma;

    internal bool CanDrawSmoothLighting =>
        _smoothLightingComplete && LightingConfig.Instance.SmoothLightingEnabled();

    private readonly FullscreenEffect _bicubicFilteringEffect;
    private readonly FullscreenEffect _bicubicFilteringWithAlphaEffect;

    private readonly FullscreenEffect _smoothEffect;
    private readonly FullscreenEffect _smoothAmbientOcclusionEffect;
    private readonly FullscreenEffect _smoothOpaqueLightOnlyEffect;
    private readonly FullscreenEffect _smoothEnhancedGlowEffect;
    private readonly FullscreenEffect _smoothEnhancedGlowAmbientOcclusionEffect;
    private readonly FullscreenEffect _smoothDitheredEffect;
    private readonly FullscreenEffect _smoothDitheredAmbientOcclusionEffect;
    private readonly FullscreenEffect _smoothDitheredOpaqueLightOnlyEffect;
    private readonly FullscreenEffect _smoothDitheredEnhancedGlowEffect;
    private readonly FullscreenEffect _smoothDitheredEnhancedGlowAmbientOcclusionEffect;
    private readonly FullscreenEffect _normalsEffect;
    private readonly FullscreenEffect _normalsAmbientOcclusionEffect;
    private readonly FullscreenEffect _normalsOpaqueLightOnlyEffect;
    private readonly FullscreenEffect _normalsFancySkyEffect;
    private readonly FullscreenEffect _normalsEnhancedGlowEffect;
    private readonly FullscreenEffect _normalsEnhancedGlowAmbientOcclusionEffect;
    private readonly FullscreenEffect _normalsEnhancedGlowFancySkyEffect;
    private readonly FullscreenEffect _normalsDitheredEffect;
    private readonly FullscreenEffect _normalsDitheredAmbientOcclusionEffect;
    private readonly FullscreenEffect _normalsDitheredOpaqueLightOnlyEffect;
    private readonly FullscreenEffect _normalsDitheredFancySkyEffect;
    private readonly FullscreenEffect _normalsDitheredEnhancedGlowEffect;
    private readonly FullscreenEffect _normalsDitheredEnhancedGlowAmbientOcclusionEffect;
    private readonly FullscreenEffect _normalsDitheredEnhancedGlowFancySkyEffect;
    private readonly FullscreenEffect _overbrightMaxEffect;
    private readonly FullscreenEffect _overbrightMaxDitheredEffect;
    private readonly FullscreenEffect _inverseOverbrightMaxHiDefEffect;

    private readonly SpriteBatchEffect _tileEntityLightOnlyEffect;
    private readonly SpriteBatchEffect _tileEntityNormalsEffect;
    private readonly SpriteBatchEffect _tileEntityNormalsFancySkyEffect;
    private readonly SpriteBatchEffect _tileEntitySmoothEffect;
    private readonly SpriteBatchEffect _tileEntitySmoothNormalsEffect;
    private readonly SpriteBatchEffect _tileEntitySmoothNormalsFancySkyEffect;
    private readonly SpriteBatchEffect _tileEntitySmoothDitheredEffect;
    private readonly SpriteBatchEffect _tileEntitySmoothDitheredNormalsEffect;
    private readonly SpriteBatchEffect _tileEntitySmoothDitheredNormalsFancySkyEffect;

    private readonly FullscreenEffect _syncHdrEffect;

    internal bool ReadyForHdrSync =>
        _smoothLightingHiResComplete && _prevColorsHiRes is not null;

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
    /// This method changes the <see cref="TileLightModifiers"/> array.
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

            ArrayUtils.MakeAtLeastSizePreserveContents(
                ref TileLightModifiers,
                TileLoader.TileCount
            );
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
    /// Do not rely on the values in the alpha channel of <paramref name="lightMapTexture"></paramref>. The alpha channel may be used by the Fancy Lighting mod for any purpose.
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
        _useAlphaChannelAsSkyLightLuma = false;

        _tmpLights = null;

        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        var effect = EffectLoader.Load("Upscaling");
        _bicubicFilteringEffect = new(effect, "BicubicFiltering");
        _bicubicFilteringWithAlphaEffect = new(effect, "BicubicFilteringWithAlpha");

        effect = EffectLoader.Load("SmoothLighting");
        _smoothEffect = new(effect, "Smooth", EffectFeatures.LightOnly);
        _smoothAmbientOcclusionEffect = new(
            effect,
            "SmoothAmbientOcclusion",
            EffectFeatures.All
        );
        _smoothOpaqueLightOnlyEffect = new(effect, "SmoothOpaqueLightOnly");
        _smoothEnhancedGlowEffect = new(effect, "SmoothEnhancedGlow");
        _smoothEnhancedGlowAmbientOcclusionEffect = new(
            effect,
            "SmoothEnhancedGlowAmbientOcclusion",
            EffectFeatures.HiDef
        );
        _smoothDitheredEffect = new(effect, "SmoothDithered", EffectFeatures.LightOnly);
        _smoothDitheredAmbientOcclusionEffect = new(
            effect,
            "SmoothDitheredAmbientOcclusion",
            EffectFeatures.LightOnly
        );
        _smoothDitheredOpaqueLightOnlyEffect = new(
            effect,
            "SmoothDitheredOpaqueLightOnly"
        );
        _smoothDitheredEnhancedGlowEffect = new(effect, "SmoothDitheredEnhancedGlow");
        _smoothDitheredEnhancedGlowAmbientOcclusionEffect = new(
            effect,
            "SmoothDitheredEnhancedGlowAmbientOcclusion"
        );
        _normalsEffect = new(effect, "Normals", EffectFeatures.LightOnly);
        _normalsAmbientOcclusionEffect = new(
            effect,
            "NormalsAmbientOcclusion",
            EffectFeatures.All
        );
        _normalsOpaqueLightOnlyEffect = new(effect, "NormalsOpaqueLightOnly");
        _normalsFancySkyEffect = new(effect, "NormalsFancySky", EffectFeatures.LightOnly);
        _normalsEnhancedGlowEffect = new(effect, "NormalsEnhancedGlow");
        _normalsEnhancedGlowAmbientOcclusionEffect = new(
            effect,
            "NormalsEnhancedGlowAmbientOcclusion",
            EffectFeatures.HiDef
        );
        _normalsEnhancedGlowFancySkyEffect = new(effect, "NormalsEnhancedGlowFancySky");
        _normalsDitheredEffect = new(effect, "NormalsDithered", EffectFeatures.LightOnly);
        _normalsDitheredAmbientOcclusionEffect = new(
            effect,
            "NormalsDitheredAmbientOcclusion",
            EffectFeatures.LightOnly
        );
        _normalsDitheredOpaqueLightOnlyEffect = new(
            effect,
            "NormalsDitheredOpaqueLightOnly"
        );
        _normalsDitheredFancySkyEffect = new(
            effect,
            "NormalsDitheredFancySky",
            EffectFeatures.LightOnly
        );
        _normalsDitheredEnhancedGlowEffect = new(effect, "NormalsDitheredEnhancedGlow");
        _normalsDitheredEnhancedGlowAmbientOcclusionEffect = new(
            effect,
            "NormalsDitheredEnhancedGlowAmbientOcclusion"
        );
        _normalsDitheredEnhancedGlowFancySkyEffect = new(
            effect,
            "NormalsDitheredEnhancedGlowFancySky"
        );
        _overbrightMaxEffect = new(effect, "OverbrightMax");
        _overbrightMaxDitheredEffect = new(effect, "OverbrightMaxDithered");
        _inverseOverbrightMaxHiDefEffect = new(effect, "InverseOverbrightMaxHiDef");

        effect = EffectLoader.Load("TileEntityLighting");
        _tileEntityLightOnlyEffect = new(effect, "LightOnly");
        _tileEntityNormalsEffect = new(effect, "Normals", EffectFeatures.LightOnly);
        _tileEntityNormalsFancySkyEffect = new(
            effect,
            "NormalsFancySky",
            EffectFeatures.LightOnly
        );
        _tileEntitySmoothEffect = new(effect, "Smooth", EffectFeatures.LightOnly);
        _tileEntitySmoothNormalsEffect = new(
            effect,
            "SmoothNormals",
            EffectFeatures.LightOnly
        );
        _tileEntitySmoothNormalsFancySkyEffect = new(
            effect,
            "SmoothNormalsFancySky",
            EffectFeatures.LightOnly
        );
        _tileEntitySmoothDitheredEffect = new(
            effect,
            "SmoothDithered",
            EffectFeatures.LightOnly
        );
        _tileEntitySmoothDitheredNormalsEffect = new(
            effect,
            "SmoothDitheredNormals",
            EffectFeatures.LightOnly
        );
        _tileEntitySmoothDitheredNormalsFancySkyEffect = new(
            effect,
            "SmoothDitheredNormalsFancySky",
            EffectFeatures.LightOnly
        );

        effect = EffectLoader.Load("HdrSync");
        _syncHdrEffect = new(effect, "SyncHdr");
    }

    internal void Unload()
    {
        TileLightModifiers = null;
        PostUpdateLightMap = null;

        _colors?.Dispose();
        _colorsHiRes?.Dispose();
        _prevColorsHiRes?.Dispose();
    }

    internal void InvalidateSmoothLighting() => _smoothLightingComplete = false;

    private static bool ShouldTileShine(ushort type, short frameX, float shimmerAlpha)
    {
        // This code is adapted from vanilla

        if ((shimmerAlpha > 0f && Main.tileSolid[type]) || type == TileID.Stalactite)
        {
            return true;
        }

        if (!Main.tileShine2[type])
        {
            return false;
        }

        switch (type)
        {
            case TileID.Containers:
            case TileID.FakeContainers:
                return frameX is >= 36 and < 178;
            case TileID.Containers2:
            case TileID.FakeContainers2:
                return frameX is >= 144 and < 178;
        }

        return true;
    }

    private static void TileShine(ref Vector3 color, Tile tile, float shimmerAlpha)
    {
        var type = tile.TileType;
        if (!ShouldTileShine(type, tile.TileFrameX, shimmerAlpha))
        {
            return;
        }

        Main.shine(ref color, type);
        if (shimmerAlpha > 0f)
        {
            // This code is adapted from vanilla
            // The vanilla Main.shine() method limits each component to a max of 1
            // We don't want that
            var inverseShimmerAlpha = 1f - shimmerAlpha;
            color.X *= inverseShimmerAlpha + (shimmerAlpha * 1.2f);
            color.Z *= inverseShimmerAlpha + (shimmerAlpha * 1.6f);
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

        var low = (0.999f / 255f) / Lighting.GlobalBrightness;
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
            ArrayUtils.MakeAtLeastSizePreserveContents(
                ref TileLightModifiers,
                TileLoader.TileCount
            );

            // Can't be parallel due to thread safety issues
            var tileX = lightMapTileArea.X;
            for (var x = 0; x < width; ++x)
            {
                var tileY = lightMapTileArea.Y;
                var end = height * (x + 1);
                for (var i = height * x; i < end; ++i, ++tileY)
                {
                    try
                    {
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
                                && TileLightModifiers[tile.TileType] is not null
                            )
                            {
                                _hasLight[i] = true;
                                continue;
                            }
                        }

                        ref var color = ref _lights[i];
                        _hasLight[i] = color.X >= low || color.Y >= low || color.Z >= low;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }
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
                            blackLight.X = blackLight.Y = blackLight.Z = 1.001f / 255f;
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

        _prevLightMapTileArea = _lightMapTileArea;
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

    internal void CalculateSmoothLighting(
        bool cameraMode = false,
        bool doHiResLightingRender = false
    )
    {
        if (!_smoothLightingLightMapValid)
        {
            return;
        }

        if (_smoothLightingComplete)
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

        TextureUtils.MakeAtLeastSize(
            ref _colors,
            height,
            width,
            TextureUtils.LightMapFormat,
            2
        );

        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();

        PerformanceTracker.StartTiming("Smooth Lighting (Light Map Texture)");
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

            _colorsHiRes?.Dispose();
            _colorsHiRes = null;
            _prevColorsHiRes?.Dispose();
            _prevColorsHiRes = null;
        }
        PerformanceTracker.StopTiming("Smooth Lighting (Light Map Texture)");

        var invokeEvent = PostUpdateLightMap != null;

        if (doBicubicUpscaling && (doHiResLightingRender || invokeEvent))
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
        TexturePosition
            .FromTextureTileCoords(
                lightMapTexture,
                _lightMapTileArea.X,
                _lightMapTileArea.Y,
                scale,
                true
            )
            .WorldToTextureTransform(out var transformation);
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
        var skyLightArea = FancySkyLighting._lightMapArea;
        var useLuma =
            LightingConfig.Instance.UseSkyLightLuma()
            && skyLightArea == _lightMapTileArea;

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
                    var myUseLuma = useLuma;

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
                            if (tile.HasTile)
                            {
                                TileShine(ref lightColor, tile, myShimmerAlpha);
                            }

                            ColorUtils.Assign(
                                ref finalLightsHiDef[i],
                                lightColor,
                                myUseLuma ? FancySkyLighting._skyLightLuma[i] : 1f
                            );
                            ++i;
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
            ArrayUtils.MakeAtLeastSizePreserveContents(
                ref TileLightModifiers,
                TileLoader.TileCount
            );

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
                        if (tile.HasTile)
                        {
                            if (
                                TileLightModifiers[tile.TileType] is { } tileLightModifier
                            )
                            {
                                Main.shimmerAlpha = shimmerAlpha;
                                tileLightModifier(tile, x, y, ref lightColor);
                                Main.shimmerAlpha = 0f;
                            }
                            else
                            {
                                TileShine(ref lightColor, tile, shimmerAlpha);
                            }
                        }

                        ColorUtils.Assign(
                            ref _finalLightsHiDef[i],
                            lightColor,
                            useLuma ? FancySkyLighting._skyLightLuma[i] : 1f
                        );
                        ++i;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }
                }
            }
        }
        Main.shimmerAlpha = shimmerAlpha;

        _colors.SetData(0, new(0, 0, height, width), _finalLightsHiDef, 0, length);

        _smoothLightingComplete = !cameraMode;
        _useAlphaChannelAsSkyLightLuma = useLuma;
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
                            if (tile.HasTile)
                            {
                                TileShine(ref lightColor, tile, myShimmerAlpha);
                            }

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
            ArrayUtils.MakeAtLeastSizePreserveContents(
                ref TileLightModifiers,
                TileLoader.TileCount
            );

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
                        if (tile.HasTile)
                        {
                            if (
                                TileLightModifiers[tile.TileType] is { } tileLightModifier
                            )
                            {
                                Main.shimmerAlpha = shimmerAlpha;
                                tileLightModifier(tile, x, y, ref lightColor);
                                Main.shimmerAlpha = 0f;
                            }
                            else
                            {
                                TileShine(ref lightColor, tile, shimmerAlpha);
                            }
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

        _colors.SetData(0, new(0, 0, height, width), _finalLights, 0, length);

        _smoothLightingComplete = !cameraMode;
        _useAlphaChannelAsSkyLightLuma = false;
    }

    private void RenderHiResLighting(Texture2D lights)
    {
        if (
            LightingConfig.Instance.HiDefFeaturesEnabled()
            && !CompatibilityConfig.Instance.DisableHdrLightingSync
        )
        {
            (_colorsHiRes, _prevColorsHiRes) = (_prevColorsHiRes, _colorsHiRes);
        }
        else
        {
            _prevColorsHiRes?.Dispose();
            _prevColorsHiRes = null;
        }

        TextureUtils.MakeSize(
            ref _colorsHiRes,
            4 * lights.Width,
            4 * lights.Height,
            TextureUtils.LightMapFormat
        );

        var effect = _useAlphaChannelAsSkyLightLuma
            ? _bicubicFilteringWithAlphaEffect
            : _bicubicFilteringEffect;
        effect
            .SetParameter("LightMapSize", lights.Size())
            .SetParameter(
                "PixelSize",
                new Vector2(1f / lights.Width, 1f / lights.Height)
            );
        Blitter.Blit(
            lights,
            _colorsHiRes,
            effect,
            samplerState: SamplerState.LinearClamp
        );
    }

    internal void DrawSmoothLighting(
        Texture2D src,
        RenderTarget2D dst,
        bool background,
        bool disableNormalMaps,
        bool doScaling,
        bool overbrightPass = false,
        bool invertOverbright = false,
        Texture2D glow = null,
        Texture2D lightedGlow = null,
        Texture2D ambientOcclusion = null
    )
    {
        var fineNormalMaps = PreferencesConfig.Instance.FineNormalMaps;
        var doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var lightOnly = DeveloperConfig.Instance.RenderOnlyLight;
        var doOverbright = LightingConfig.Instance.DrawOverbright();

        var normalsEffectFlag =
            !disableNormalMaps && LightingConfig.Instance.SimulateNormalMaps;
        var ditheredEffectFlag =
            !DeveloperConfig.Instance.DisableDithering && doOverbright && !hiDef;
        var enhancedGlowEffectFlag = lightedGlow is not null;
        var fancySkyEffectFlag =
            !overbrightPass && _useAlphaChannelAsSkyLightLuma && !background;
        var ambientOcclusionEffectFlag = ambientOcclusion is not null;
        var opaqueEffectFlag = lightOnly && background && ambientOcclusion is null;

        var lightMapTexture = _colors;
        var lightMapScale = 1f;
        if (doBicubicUpscaling)
        {
            if (!_smoothLightingHiResComplete)
            {
                RenderHiResLighting(lightMapTexture);
                _smoothLightingHiResComplete = true;
            }

            lightMapTexture = _colorsHiRes;
            lightMapScale = 0.25f;
        }

        var effect = overbrightPass
            ? invertOverbright
                ? _inverseOverbrightMaxHiDefEffect
                : ditheredEffectFlag
                    ? _overbrightMaxDitheredEffect
                    : _overbrightMaxEffect
            : normalsEffectFlag
                ? ditheredEffectFlag
                    ? enhancedGlowEffectFlag
                        ? fancySkyEffectFlag
                            ? _normalsDitheredEnhancedGlowFancySkyEffect
                            : ambientOcclusionEffectFlag
                                ? _normalsDitheredEnhancedGlowAmbientOcclusionEffect
                                : _normalsDitheredEnhancedGlowEffect
                        : fancySkyEffectFlag
                            ? _normalsDitheredFancySkyEffect
                            : ambientOcclusionEffectFlag
                                ? _normalsDitheredAmbientOcclusionEffect
                                : opaqueEffectFlag
                                    ? _normalsDitheredOpaqueLightOnlyEffect
                                    : _normalsDitheredEffect
                    : enhancedGlowEffectFlag
                        ? fancySkyEffectFlag
                            ? _normalsEnhancedGlowFancySkyEffect
                            : ambientOcclusionEffectFlag
                                ? _normalsEnhancedGlowAmbientOcclusionEffect
                                : _normalsEnhancedGlowEffect
                        : fancySkyEffectFlag
                            ? _normalsFancySkyEffect
                            : ambientOcclusionEffectFlag
                                ? _normalsAmbientOcclusionEffect
                                : opaqueEffectFlag
                                    ? _normalsOpaqueLightOnlyEffect
                                    : _normalsEffect
                : ditheredEffectFlag
                    ? enhancedGlowEffectFlag
                        ? ambientOcclusionEffectFlag
                            ? _smoothDitheredEnhancedGlowAmbientOcclusionEffect
                            : _smoothDitheredEnhancedGlowEffect
                        : ambientOcclusionEffectFlag
                            ? _smoothDitheredAmbientOcclusionEffect
                            : opaqueEffectFlag
                                ? _smoothDitheredOpaqueLightOnlyEffect
                                : _smoothDitheredEffect
                    : enhancedGlowEffectFlag
                        ? ambientOcclusionEffectFlag
                            ? _smoothEnhancedGlowAmbientOcclusionEffect
                            : _smoothEnhancedGlowEffect
                        : ambientOcclusionEffectFlag
                            ? _smoothAmbientOcclusionEffect
                            : opaqueEffectFlag
                                ? _smoothOpaqueLightOnlyEffect
                                : _smoothEffect;

        if (src is not null)
        {
            var position = doScaling
                ? TexturePosition.GetScreenPosition(src)
                : TexturePosition.GetTileTargetPosition(src);

            position.TextureToWorldTransform(out var tileToWorldTransform);
            TexturePosition
                .FromTextureTileCoords(
                    lightMapTexture,
                    _lightMapTileArea.X,
                    _lightMapTileArea.Y,
                    lightMapScale,
                    true
                )
                .WorldToTextureTransform(out var lightMapTransform);
            Matrix.Multiply(
                ref tileToWorldTransform,
                ref lightMapTransform,
                out lightMapTransform
            );

            effect.SetParameter("LightMapTransform", lightMapTransform);
        }

        var gamma = PostProcessing.ContentGamma();
        var normalMapResolution = fineNormalMaps ? 1f : 2f;
        var overbrightMult = hiDef ? 1f / PostProcessing.HiDefBrightnessScale : 1f;
        var normalMapGradientMult = 16f * NormalMapGradientBaseMult * overbrightMult;
        var normalMapStrength = Math.Clamp(
            PreferencesConfig.Instance.NormalMapsMultiplier(),
            0f,
            1f
        );

        if (background)
        {
            normalMapStrength *= 0.85f;
        }

        effect
            .SetParameter("Gamma", gamma)
            .SetParameter("ReciprocalGamma", 1f / gamma)
            .SetParameter(
                "NormalMapResolution",
                new Vector2(
                    normalMapResolution / src.Width,
                    normalMapResolution / src.Height
                )
            )
            .SetParameter("NormalMapGradientMult", normalMapGradientMult)
            .SetParameter("NormalMapStrength", normalMapStrength);

        if (fancySkyEffectFlag)
        {
            var hour = GameTimeUtils.CalculateCurrentHour();
            var (skyLightAngle, skyLightMult, _) =
                FancySkyLighting.CalculateSkyLightAngleAndMultiplier(hour);
            var normalMapSkyGradientMult = (float)skyLightMult * overbrightMult;
            effect.SetParameter(
                "SkyLightGradient",
                -normalMapSkyGradientMult
                    * new Vector2(
                        (float)Math.Cos(skyLightAngle),
                        (float)Math.Sin(skyLightAngle)
                    )
            );
        }

        MainGraphics.ResetSavedTextures();

        if (src is not null)
        {
            MainGraphics.SetTexture(4, lightMapTexture, SamplerState.LinearClamp);
        }
        if (glow is not null)
        {
            MainGraphics.SetTexture(5, glow, SamplerState.PointClamp);
        }
        if (lightedGlow is not null)
        {
            MainGraphics.SetTexture(6, lightedGlow, SamplerState.PointClamp);
        }
        if (ambientOcclusion is not null)
        {
            MainGraphics.SetTexture(7, ambientOcclusion, SamplerState.PointClamp);
        }
        if (ditheredEffectFlag)
        {
            MainGraphics.SetTexture(8, _ditherNoise, SamplerState.PointWrap);
        }

        Blitter.Blit(
            src ?? lightMapTexture,
            dst,
            effect,
            blendState: dst is null
                ? src is null
                    ? CustomBlendStates.MultiplyColor
                    : BlendState.AlphaBlend
                : BlendState.Opaque,
            setTarget: dst is not null
        );
        MainGraphics.RestoreSavedTextures();
    }

    internal (SpriteBatchEffect, bool) GetTileEntityEffect(
        ref RenderTarget2D screenTarget,
        ref RenderTarget2D tmpTarget
    )
    {
        var doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();
        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var fineNormalMaps = PreferencesConfig.Instance.FineNormalMaps;
        var lightOnly = DeveloperConfig.Instance.RenderOnlyLight;

        var smoothEffectFlag = LightingConfig.Instance.UseTileEntitySmoothLighting;
        var ditheredEffectFlag =
            !DeveloperConfig.Instance.DisableDithering
            && smoothEffectFlag
            && doOverbright
            && !hiDef;
        var normalsEffectFlag =
            LightingConfig.Instance.SimulateNormalMaps
            && LightingConfig.Instance.SimulateTileEntityNormals;
        var fancySkyEffectFlag = doOverbright && _useAlphaChannelAsSkyLightLuma;

        var needsLightMap = smoothEffectFlag || normalsEffectFlag;
        var cameraMode = MainGraphics.InCameraMode;

        var effect = smoothEffectFlag
            ? ditheredEffectFlag
                ? normalsEffectFlag
                    ? fancySkyEffectFlag
                        ? _tileEntitySmoothDitheredNormalsFancySkyEffect
                        : _tileEntitySmoothDitheredNormalsEffect
                    : _tileEntitySmoothDitheredEffect
                : normalsEffectFlag
                    ? fancySkyEffectFlag
                        ? _tileEntitySmoothNormalsFancySkyEffect
                        : _tileEntitySmoothNormalsEffect
                    : _tileEntitySmoothEffect
            : normalsEffectFlag
                ? fancySkyEffectFlag
                    ? _tileEntityNormalsFancySkyEffect
                    : _tileEntityNormalsEffect
                : lightOnly
                    ? _tileEntityLightOnlyEffect
                    : null;

        if (effect is null)
        {
            return (null, false);
        }

        var usedTmpTarget = false;

        if (needsLightMap)
        {
            CalculateSmoothLighting(cameraMode);

            if (_colors is null)
            {
                return (null, false);
            }

            if (doBicubicUpscaling && !_smoothLightingHiResComplete)
            {
                Blitter.BlitOrSwap(ref screenTarget, ref tmpTarget);

                RenderHiResLighting(_colors);
                _smoothLightingHiResComplete = true;

                usedTmpTarget = true;
            }

            var lightMapTexture = doBicubicUpscaling ? _colorsHiRes : _colors;
            var lightMapScale = doBicubicUpscaling ? 0.25f : 1f;
            TexturePosition
                .GetScreenPosition(screenTarget)
                .VertexToWorldTransform(out var tileToWorldTransform);
            TexturePosition
                .FromTextureTileCoords(
                    lightMapTexture,
                    _lightMapTileArea.X,
                    _lightMapTileArea.Y,
                    lightMapScale,
                    true
                )
                .WorldToTextureTransform(out var lightMapTransform);
            Matrix.Multiply(
                ref tileToWorldTransform,
                ref lightMapTransform,
                out lightMapTransform
            );

            effect.SetParameter("LightMapMatrixTransform", lightMapTransform);
        }

        if (normalsEffectFlag)
        {
            var zoom = cameraMode ? 1f : Main.GameViewMatrix.Zoom.X;

            var normalMapResolution = fineNormalMaps ? 1f : 2f;
            var overbrightMult = hiDef ? 1f / PostProcessing.HiDefBrightnessScale : 1f;
            var normalMapGradientMult =
                16f * NormalMapGradientBaseMult * overbrightMult * zoom;
            var normalMapStrength = Math.Clamp(
                PreferencesConfig.Instance.NormalMapsMultiplier(),
                0f,
                1f
            );

            effect
                .SetParameter("Zoom", zoom)
                .SetParameter("NormalMapResolution", normalMapResolution)
                .SetParameter("NormalMapGradientMult", normalMapGradientMult)
                .SetParameter("NormalMapStrength", normalMapStrength);

            if (fancySkyEffectFlag)
            {
                var zoomWithFlipping = cameraMode
                    ? Vector2.One
                    : new Vector2(
                        Main.GameViewMatrix.TransformationMatrix.M11,
                        Main.GameViewMatrix.TransformationMatrix.M22
                    );

                var hour = GameTimeUtils.CalculateCurrentHour();
                var (skyLightAngle, skyLightMult, _) =
                    FancySkyLighting.CalculateSkyLightAngleAndMultiplier(hour);
                var normalMapSkyGradientMult =
                    (float)skyLightMult * overbrightMult * zoomWithFlipping;

                effect.SetParameter(
                    "SkyLightGradient",
                    -normalMapSkyGradientMult
                        * new Vector2(
                            (float)Math.Cos(skyLightAngle),
                            (float)Math.Sin(skyLightAngle)
                        )
                );
            }
        }

        return (effect, usedTmpTarget);
    }

    internal void ApplyTileEntityEffect(
        SpriteBatchEffect effect,
        RenderTarget2D glow = null
    )
    {
        var doDithering =
            !DeveloperConfig.Instance.DisableDithering
            && LightingConfig.Instance.UseTileEntitySmoothLighting
            && LightingConfig.Instance.DrawOverbright()
            && !LightingConfig.Instance.HiDefFeaturesEnabled();

        var lightMapTexture = LightingConfig.Instance.UseBicubicScaling()
            ? _colorsHiRes
            : _colors;

        SpriteBatchEffectLoader.Apply(effect);
        MainGraphics.ResetSavedTextures();
        MainGraphics.SetTexture(4, lightMapTexture, SamplerState.LinearClamp);
        if (glow is not null)
        {
            MainGraphics.SetTexture(5, glow, SamplerState.PointClamp);
        }

        if (doDithering)
        {
            MainGraphics.SetTexture(6, _ditherNoise, SamplerState.PointWrap);
        }
    }

    internal void BindHdrSyncTextures()
    {
        MainGraphics.SetTexture(4, _prevColorsHiRes, SamplerState.LinearClamp);
        MainGraphics.SetTexture(5, _colorsHiRes, SamplerState.LinearClamp);
    }

    internal void DoHdrSync(
        ref RenderTarget2D tileTarget,
        ref RenderTarget2D tmpTarget,
        Vector2 tilesPosition
    )
    {
        TexturePosition
            .GetTileTargetPosition(tileTarget, tilesPosition)
            .TextureToWorldTransform(out var tileToWorldTransform);
        TexturePosition
            .FromTextureTileCoords(
                _prevColorsHiRes,
                _prevLightMapTileArea.X,
                _prevLightMapTileArea.Y,
                0.25f,
                true
            )
            .WorldToTextureTransform(out var prevMatrixTransform);
        TexturePosition
            .FromTextureTileCoords(
                _colorsHiRes,
                _lightMapTileArea.X,
                _lightMapTileArea.Y,
                0.25f,
                true
            )
            .WorldToTextureTransform(out var currMatrixTransform);
        Matrix.Multiply(
            ref tileToWorldTransform,
            ref prevMatrixTransform,
            out prevMatrixTransform
        );
        Matrix.Multiply(
            ref tileToWorldTransform,
            ref currMatrixTransform,
            out currMatrixTransform
        );

        _syncHdrEffect.SetParameter("PrevLightMapMatrixTransform", prevMatrixTransform);
        _syncHdrEffect.SetParameter("CurrLightMapMatrixTransform", currMatrixTransform);

        Blitter.Blit(tileTarget, tmpTarget, _syncHdrEffect);
        Blitter.BlitOrSwap(ref tmpTarget, ref tileTarget);
    }
}
