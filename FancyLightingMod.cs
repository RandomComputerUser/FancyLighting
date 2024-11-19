using System;
using System.Reflection;
using FancyLighting.Config;
using FancyLighting.Config.Enums;
using FancyLighting.LightingEngines;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

public sealed class FancyLightingMod : Mod
{
    public static BlendState MultiplyBlend { get; private set; }

    private static bool _overrideLightColor;
    private static bool _useBlack;
    internal static bool _inCameraMode;
    private static bool _disableLightColorOverride;
    private static bool _preventDust;

    private SmoothLighting _smoothLightingInstance;
    private AmbientOcclusion _ambientOcclusionInstance;
    private ICustomLightingEngine _fancyLightingEngineInstance;
    private PostProcessing _postProcessingInstance;

    internal FieldInfo _field_activeEngine;
    private FieldInfo _field_activeLightMap;
    internal FieldInfo _field_workingProcessedArea;
    private FieldInfo _field_colors;
    private FieldInfo _field_mask;

    private delegate void TileDrawingMethod(TileDrawing self);

    private TileDrawingMethod _method_DrawMultiTileVines;
    private TileDrawingMethod _method_DrawMultiTileGrass;
    private TileDrawingMethod _method_DrawVoidLenses;
    private TileDrawingMethod _method_DrawTeleportationPylons;
    private TileDrawingMethod _method_DrawMasterTrophies;
    private TileDrawingMethod _method_DrawGrass;
    private TileDrawingMethod _method_DrawAnyDirectionalGrass;
    private TileDrawingMethod _method_DrawTrees;
    private TileDrawingMethod _method_DrawVines;
    private TileDrawingMethod _method_DrawReverseVines;

    internal static RenderTarget2D _cameraModeTarget;
    private static RenderTarget2D _cameraModeTmpTarget1;
    private static RenderTarget2D _cameraModeTmpTarget2;
    internal static Rectangle _cameraModeArea;
    private static CaptureBiome _cameraModeBiome;

    private static RenderTarget2D _screenTarget1;
    private static RenderTarget2D _screenTarget2;
    private static RenderTarget2D _tmpTarget1;
    private static RenderTarget2D _tmpTarget2;
    private static RenderTarget2D _tmpTarget3;

    private static RenderTarget2D _backgroundTarget;
    private static RenderTarget2D _cameraModeBackgroundTarget;

    private bool OverrideLightColor
    {
        get => _overrideLightColor;
        set
        {
            if (value == _overrideLightColor)
            {
                return;
            }

            if (!Lighting.UsingNewLighting)
            {
                return;
            }

            var activeEngine = _field_activeEngine.GetValue(null);
            if (activeEngine is not LightingEngine lightingEngine)
            {
                return;
            }

            if (value && _smoothLightingInstance._whiteLights is null)
            {
                return;
            }

            var lightMapInstance = (LightMap)
                _field_activeLightMap.GetValue(lightingEngine).AssertNotNull();

            if (value)
            {
                _smoothLightingInstance._tmpLights = (Vector3[])
                    _field_colors.GetValue(lightMapInstance).AssertNotNull();
            }

            _field_colors.SetValue(
                lightMapInstance,
                value
                    ? _smoothLightingInstance._whiteLights
                    : _smoothLightingInstance._tmpLights
            );

            if (!value)
            {
                _smoothLightingInstance._tmpLights = null;
            }

            _overrideLightColor = value;
        }
    }

    private bool UseBlackLights
    {
        get => _useBlack;
        set
        {
            if (value == _useBlack)
            {
                return;
            }

            if (!Lighting.UsingNewLighting)
            {
                return;
            }

            var activeEngine = _field_activeEngine.GetValue(null);
            if (activeEngine is not LightingEngine lightingEngine)
            {
                return;
            }

            if (value && _smoothLightingInstance._blackLights is null)
            {
                return;
            }

            var lightMapInstance = (LightMap)
                _field_activeLightMap.GetValue(lightingEngine).AssertNotNull();

            if (value)
            {
                _smoothLightingInstance._tmpLights = (Vector3[])
                    _field_colors.GetValue(lightMapInstance).AssertNotNull();
            }

            _field_colors.SetValue(
                lightMapInstance,
                value
                    ? _smoothLightingInstance._blackLights
                    : _smoothLightingInstance._tmpLights
            );

            if (!value)
            {
                _smoothLightingInstance._tmpLights = null;
            }

            _useBlack = value;
        }
    }

    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
        {
            return;
        }

        _overrideLightColor = false;
        _useBlack = false;
        _inCameraMode = false;
        _disableLightColorOverride = false;
        _preventDust = false;

        MultiplyBlend = new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceColor,
        };

        _smoothLightingInstance = new SmoothLighting(this);
        _ambientOcclusionInstance = new AmbientOcclusion();
        SetFancyLightingEngineInstance();
        _postProcessingInstance = new PostProcessing();

        _field_activeEngine = typeof(Lighting)
            .GetField("_activeEngine", BindingFlags.NonPublic | BindingFlags.Static)
            .AssertNotNull();
        _field_activeLightMap = typeof(LightingEngine)
            .GetField("_activeLightMap", BindingFlags.NonPublic | BindingFlags.Instance)
            .AssertNotNull();
        _field_workingProcessedArea = typeof(LightingEngine)
            .GetField(
                "_workingProcessedArea",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
            .AssertNotNull();
        _field_colors = typeof(LightMap)
            .GetField("_colors", BindingFlags.NonPublic | BindingFlags.Instance)
            .AssertNotNull();
        _field_mask = typeof(LightMap)
            .GetField("_mask", BindingFlags.NonPublic | BindingFlags.Instance)
            .AssertNotNull();

        _method_DrawMultiTileVines = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawMultiTileVines",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawMultiTileGrass = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawMultiTileGrass",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawVoidLenses = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawVoidLenses",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawTeleportationPylons = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawTeleportationPylons",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawMasterTrophies = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawMasterTrophies",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawGrass = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawGrass",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawAnyDirectionalGrass = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawAnyDirectionalGrass",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawTrees = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawTrees",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawVines = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawVines",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );
        _method_DrawReverseVines = (TileDrawingMethod)
            Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing)
                    .GetMethod(
                        "DrawReverseVines",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        []
                    )
                    .AssertNotNull()
            );

        AddHooks();
        SkyColors.AddSkyColorsHooks();
    }

    public override void Unload()
    {
        Main.QueueMainThreadAction(() =>
        {
            // Do not dispose _cameraModeTarget
            // _cameraModeTarget comes from the Main class, so we don't own it
            _screenTarget1?.Dispose();
            _screenTarget2?.Dispose();
            _tmpTarget1?.Dispose();
            _tmpTarget2?.Dispose();
            _tmpTarget3?.Dispose();
            _cameraModeTarget = null;
            _cameraModeTmpTarget1?.Dispose();
            _cameraModeTmpTarget2?.Dispose();
            _backgroundTarget?.Dispose();
            _cameraModeBackgroundTarget?.Dispose();
            _smoothLightingInstance?.Unload();
            _ambientOcclusionInstance?.Unload();
            _fancyLightingEngineInstance?.Unload();
            _postProcessingInstance?.Unload();
        });

        base.Unload();
    }

    private void SetFancyLightingEngineInstance()
    {
        var mode =
            LightingConfig.Instance?.FancyLightingEngineMode ?? LightingEngineMode.One;
        switch (mode)
        {
            default:
            case LightingEngineMode.One:
                if (_fancyLightingEngineInstance is not FancyLightingEngine1X)
                {
                    _fancyLightingEngineInstance?.Unload();
                    _fancyLightingEngineInstance = new FancyLightingEngine1X();
                }

                break;

            case LightingEngineMode.Two:
                if (_fancyLightingEngineInstance is not FancyLightingEngine2X)
                {
                    _fancyLightingEngineInstance?.Unload();
                    _fancyLightingEngineInstance = new FancyLightingEngine2X();
                }

                break;

            case LightingEngineMode.Four:
                if (_fancyLightingEngineInstance is not FancyLightingEngine4X)
                {
                    _fancyLightingEngineInstance?.Unload();
                    _fancyLightingEngineInstance = new FancyLightingEngine4X();
                }

                break;
        }
    }

    internal void OnConfigChange()
    {
        _smoothLightingInstance?.CalculateSmoothLighting(false, true);
        _smoothLightingInstance?.CalculateSmoothLighting(false, true);

        if (_fancyLightingEngineInstance is not null)
        {
            SetFancyLightingEngineInstance();
        }
    }

    private void AddHooks()
    {
        On_Dust.NewDust += _Dust_NewDust;
        On_TileDrawing.ShouldTileShine += _TileDrawing_ShouldTileShine;
        On_FilterManager.EndCapture += _FilterManager_EndCapture;
        On_Main.DrawBG += _Main_DrawBG;
        On_Main.DrawUnderworldBackground += _Main_DrawUnderworldBackground;
        On_Main.DrawSunAndMoon += _Main_DrawSunAndMoon;
        On_TileDrawing.PostDrawTiles += _TileDrawing_PostDrawTiles;
        On_Main.RenderWater += _Main_RenderWater;
        On_Main.DrawWaters += _Main_DrawWaters;
        On_Main.RenderBackground += _Main_RenderBackground;
        On_Main.DrawBackground += _Main_DrawBackground;
        On_Main.RenderBlack += _Main_RenderBlack;
        On_Main.RenderTiles += _Main_RenderTiles;
        On_Main.RenderTiles2 += _Main_RenderTiles2;
        On_Main.RenderWalls += _Main_RenderWalls;
        On_LightingEngine.ProcessBlur += _LightingEngine_ProcessBlur;
        On_LightMap.Blur += _LightMap_Blur;
        On_TileLightScanner.ApplySurfaceLight += _TileLightScanner_ApplySurfaceLight;
        // Camera mode hooks added below
        // For some reason the order in which these are added matters to ensure that camera mode works
        // Maybe DrawCapture needs to be added last
        On_Main.DrawLiquid += _Main_DrawLiquid;
        On_Main.DrawWalls += _Main_DrawWalls;
        On_Main.DrawTiles += _Main_DrawTiles;
        On_Main.DrawCapture += _Main_DrawCapture;
    }

    private int _Dust_NewDust(
        On_Dust.orig_NewDust orig,
        Vector2 Position,
        int Width,
        int Height,
        int Type,
        float SpeedX,
        float SpeedY,
        int Alpha,
        Color newColor,
        float Scale
    ) =>
        _preventDust
            ? Main.dust.Length - 1 // no dust
            : orig(Position, Width, Height, Type, SpeedX, SpeedY, Alpha, newColor, Scale);

    private bool _TileDrawing_ShouldTileShine(
        On_TileDrawing.orig_ShouldTileShine orig,
        ushort type,
        short frameX
    ) => !_overrideLightColor && orig(type, frameX);

    // Post-processing
    private void _FilterManager_EndCapture(
        On_FilterManager.orig_EndCapture orig,
        FilterManager self,
        RenderTarget2D finalTexture,
        RenderTarget2D screenTarget1,
        RenderTarget2D screenTarget2,
        Color clearColor
    )
    {
        _postProcessingInstance.ApplyPostProcessing(
            screenTarget1,
            screenTarget2,
            _inCameraMode ? _cameraModeBackgroundTarget : _backgroundTarget,
            _smoothLightingInstance
        );

        orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
    }

    // Separate background layer (so we don't apply overbright to it)

    private void _Main_DrawBG(On_Main.orig_DrawBG orig, Main self)
    {
        orig(self);

        if (!LightingConfig.Instance.DrawOverbright())
        {
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;
        TextureUtils.MakeSize(ref _backgroundTarget, target.Width, target.Height);

        Main.graphics.GraphicsDevice.SetRenderTarget(_backgroundTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(target);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transform
        );
    }

    private void _Main_DrawUnderworldBackground(
        On_Main.orig_DrawUnderworldBackground orig,
        Main self,
        bool flat
    )
    {
        orig(self, flat);

        if (!_inCameraMode || !LightingConfig.Instance.DrawOverbright())
        {
            return;
        }

        Main.spriteBatch.End();

        TextureUtils.MakeSize(
            ref _cameraModeBackgroundTarget,
            _cameraModeTarget.Width,
            _cameraModeTarget.Height
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeBackgroundTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_cameraModeTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        Main.spriteBatch.Begin();
    }

    private void _Main_DrawSunAndMoon(
        On_Main.orig_DrawSunAndMoon orig,
        Main self,
        Main.SceneArea sceneArea,
        Color moonColor,
        Color sunColor,
        float tempMushroomInfluence
    )
    {
        if (!LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var sunMoonBrightness = Main.dayTime ? 1.65f : 1.5f;
        sunMoonBrightness /= PostProcessing.HiDefSurfaceBrightness;

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            _inCameraMode ? RasterizerState.CullNone : Main.Rasterizer,
            null,
            transform
        );
        _smoothLightingInstance.ApplyBrightenShader(sunMoonBrightness);
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
    }

    // Tile entities
    private void _TileDrawing_PostDrawTiles(
        On_TileDrawing.orig_PostDrawTiles orig,
        TileDrawing self,
        bool solidLayer,
        bool forRenderTargets,
        bool intoRenderTargets
    )
    {
        if (
            intoRenderTargets
            || _ambientOcclusionInstance._drawingTileEntities
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || !LightingConfig.Instance.DrawOverbright()
            || !PreferencesConfig.Instance.RenderOnlyLight
        )
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            Main.DefaultSamplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.Transform
        );

        _smoothLightingInstance.ApplyLightOnlyShader();

        _method_DrawMultiTileVines(self);
        _method_DrawMultiTileGrass(self);
        _method_DrawVoidLenses(self);
        _method_DrawTeleportationPylons(self);
        _method_DrawMasterTrophies(self);
        _method_DrawGrass(self);
        _method_DrawAnyDirectionalGrass(self);
        _method_DrawTrees(self);
        _method_DrawVines(self);
        _method_DrawReverseVines(self);

        Main.spriteBatch.End();
    }

    // Non-moving objects (tiles, walls, etc.)

    private void _Main_RenderWater(On_Main.orig_RenderWater orig, Main self)
    {
        if (
            LightingConfig.Instance.SmoothLightingEnabled()
            && PreferencesConfig.Instance.RenderOnlyLight
            && !LightingConfig.Instance.DrawOverbright()
        )
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.waterTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        var tileTarget = Main.waterTarget;
        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting();

        if (useGlowMasks)
        {
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height
            );
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            _disableLightColorOverride = true;
            try
            {
                orig(self);
            }
            finally
            {
                _disableLightColorOverride = false;
                _preventDust = false;
                UseBlackLights = false;
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget1);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeAtLeastSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height
                );

                _disableLightColorOverride = true;
                try
                {
                    orig(self);
                }
                finally
                {
                    _disableLightColorOverride = false;
                }

                Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget3);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }
        }

        _preventDust = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventDust = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(tileTarget, false, true);

        if (!useGlowMasks)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget2);
        _smoothLightingInstance.DrawGlow(
            tileTarget,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_tmpTarget2, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    private void _Main_DrawWaters(
        On_Main.orig_DrawWaters orig,
        Main self,
        bool isBackground
    )
    {
        if (
            LightingConfig.Instance.SmoothLightingEnabled()
            && PreferencesConfig.Instance.RenderOnlyLight
            && !LightingConfig.Instance.DrawOverbright()
        )
        {
            return;
        }

        if (
            _inCameraMode
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || _disableLightColorOverride
            || UseBlackLights
        )
        {
            orig(self, isBackground);
            return;
        }

        OverrideLightColor = _smoothLightingInstance.CanDrawSmoothLighting;
        try
        {
            orig(self, isBackground);
        }
        finally
        {
            OverrideLightColor = false;
        }
    }

    // Cave backgrounds
    private void _Main_RenderBackground(On_Main.orig_RenderBackground orig, Main self)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        if (PreferencesConfig.Instance.RenderOnlyLight)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.backgroundTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.backWaterTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting();
        orig(self);

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(
            Main.instance.backgroundTarget,
            true,
            true
        );
        _smoothLightingInstance.DrawSmoothLighting(
            Main.instance.backWaterTarget,
            true,
            true
        );
    }

    private void _Main_DrawBackground(On_Main.orig_DrawBackground orig, Main self)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        if (
            _inCameraMode
            && LightingConfig.Instance.SmoothLightingEnabled()
            && PreferencesConfig.Instance.RenderOnlyLight
        )
        {
            return;
        }

        if (_inCameraMode)
        {
            _smoothLightingInstance.CalculateSmoothLighting(true);

            Main.tileBatch.End();
            Main.spriteBatch.End();
            Main.graphics.GraphicsDevice.SetRenderTarget(
                _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
            );
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            OverrideLightColor = true;
            try
            {
                orig(self);
            }
            finally
            {
                OverrideLightColor = false;
            }

            Main.tileBatch.End();
            Main.spriteBatch.End();

            _smoothLightingInstance.DrawSmoothLightingCameraMode(
                _cameraModeTarget,
                _smoothLightingInstance._cameraModeTarget1,
                true,
                false,
                true
            );

            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
        }
        else
        {
            OverrideLightColor = _smoothLightingInstance.CanDrawSmoothLighting;
            try
            {
                orig(self);
            }
            finally
            {
                OverrideLightColor = false;
            }
        }
    }

    private void _Main_RenderBlack(On_Main.orig_RenderBlack orig, Main self)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        if (PreferencesConfig.Instance.RenderOnlyLight)
        {
            return;
        }

        var initialLightingOverride = OverrideLightColor;
        OverrideLightColor = false;
        try
        {
            orig(self);
        }
        finally
        {
            OverrideLightColor = initialLightingOverride;
        }
    }

    private void _Main_RenderTiles(On_Main.orig_RenderTiles orig, Main self)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        var tileTarget = Main.instance.tileTarget;
        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting();

        if (useGlowMasks)
        {
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height
            );
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            try
            {
                orig(self);
            }
            finally
            {
                _preventDust = false;
                UseBlackLights = false;
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget1);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeAtLeastSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height
                );

                orig(self);

                Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget3);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }
        }

        OverrideLightColor = _smoothLightingInstance.CanDrawSmoothLighting;
        _preventDust = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventDust = false;
            OverrideLightColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(tileTarget, false);

        if (!useGlowMasks)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget2);
        _smoothLightingInstance.DrawGlow(
            tileTarget,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_tmpTarget2, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    private void _Main_RenderTiles2(On_Main.orig_RenderTiles2 orig, Main self)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        var tileTarget = Main.instance.tile2Target;
        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting();

        if (useGlowMasks)
        {
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height
            );
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            try
            {
                orig(self);
            }
            finally
            {
                _preventDust = false;
                UseBlackLights = false;
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget1);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeAtLeastSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height
                );

                orig(self);

                Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget3);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }
        }

        OverrideLightColor = _smoothLightingInstance.CanDrawSmoothLighting;
        _preventDust = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventDust = false;
            OverrideLightColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(tileTarget, false);

        if (!useGlowMasks)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget2);
        _smoothLightingInstance.DrawGlow(
            tileTarget,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_tmpTarget2, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    private void _Main_RenderWalls(On_Main.orig_RenderWalls orig, Main self)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            if (LightingConfig.Instance.AmbientOcclusionEnabled())
            {
                _ambientOcclusionInstance.ApplyAmbientOcclusion();
            }

            return;
        }

        if (
            PreferencesConfig.Instance.RenderOnlyLight
            && !LightingConfig.Instance.DrawOverbright()
        )
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.wallTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        var tileTarget = Main.instance.wallTarget;
        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting();

        if (useGlowMasks)
        {
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height
            );
            TextureUtils.MakeAtLeastSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            try
            {
                orig(self);
            }
            finally
            {
                _preventDust = false;
                UseBlackLights = false;
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget1);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeAtLeastSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height
                );

                orig(self);

                Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget3);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(tileTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }
        }

        _smoothLightingInstance.CalculateSmoothLighting();
        OverrideLightColor = _smoothLightingInstance.CanDrawSmoothLighting;
        _preventDust = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventDust = false;
            OverrideLightColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        var doAmbientOcclusion = LightingConfig.Instance.AmbientOcclusionEnabled();
        var doOverbright = LightingConfig.Instance.DrawOverbright();

        RenderTarget2D ambientOcclusionTarget = null;
        if (doAmbientOcclusion && doOverbright)
        {
            ambientOcclusionTarget = _ambientOcclusionInstance.ApplyAmbientOcclusion(
                false
            );
        }

        _smoothLightingInstance.DrawSmoothLighting(
            Main.instance.wallTarget,
            true,
            ambientOcclusionTarget: ambientOcclusionTarget
        );

        if (doAmbientOcclusion && !doOverbright)
        {
            _ambientOcclusionInstance.ApplyAmbientOcclusion();
        }

        if (!useGlowMasks)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget2);
        _smoothLightingInstance.DrawGlow(
            tileTarget,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(_tmpTarget2, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    // Lighting engine

    private void _LightingEngine_ProcessBlur(
        On_LightingEngine.orig_ProcessBlur orig,
        LightingEngine self
    )
    {
        if (!LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            orig(self);
            return;
        }

        _fancyLightingEngineInstance.SetLightMapArea(
            (Rectangle)_field_workingProcessedArea.GetValue(self).AssertNotNull()
        );
        orig(self);
    }

    private void _LightMap_Blur(On_LightMap.orig_Blur orig, LightMap self)
    {
        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            && !LightingConfig.Instance.FancyLightingEngineEnabled()
        )
        {
            orig(self);
            return;
        }

        var colors = (Vector3[])_field_colors.GetValue(self);
        var lightMasks = (LightMaskMode[])_field_mask.GetValue(self);
        if (colors is null || lightMasks is null)
        {
            orig(self);
            return;
        }

        if (LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            _fancyLightingEngineInstance.SpreadLight(
                self,
                colors,
                lightMasks,
                self.Width,
                self.Height
            );
        }
        else
        {
            orig(self);
        }

        if (LightingConfig.Instance.SmoothLightingEnabled())
        {
            _smoothLightingInstance.GetAndBlurLightMap(
                colors,
                lightMasks,
                self.Width,
                self.Height
            );
        }
    }

    private static void _TileLightScanner_ApplySurfaceLight(
        On_TileLightScanner.orig_ApplySurfaceLight orig,
        TileLightScanner self,
        Tile tile,
        int x,
        int y,
        ref Vector3 lightColor
    )
    {
        orig(self, tile, x, y, ref lightColor);

        if (!LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            return;
        }

        lightColor *= PostProcessing.HiDefSurfaceBrightness;
    }

    // Camera mode hooks below

    private void _Main_DrawLiquid(
        On_Main.orig_DrawLiquid orig,
        Main self,
        bool bg,
        int Style,
        float Alpha,
        bool drawSinglePassLiquids
    )
    {
        if (!_inCameraMode || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self, bg, Style, Alpha, drawSinglePassLiquids);
            return;
        }

        if (
            PreferencesConfig.Instance.RenderOnlyLight
            && !LightingConfig.Instance.DrawOverbright()
        )
        {
            return;
        }

        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting(true);

        if (useGlowMasks)
        {
            TextureUtils.MakeSize(
                ref _cameraModeTmpTarget1,
                _cameraModeTarget.Width,
                _cameraModeTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            Main.spriteBatch.End();
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget1);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin();
            try
            {
                orig(self, bg, Style, Alpha, drawSinglePassLiquids);
            }
            finally
            {
                _preventDust = false;
                UseBlackLights = false;
            }

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeSize(
                    ref _cameraModeTmpTarget2,
                    _cameraModeTarget.Width,
                    _cameraModeTarget.Height
                );

                Main.spriteBatch.End();
                Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget2);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin();
                orig(self, bg, Style, Alpha, drawSinglePassLiquids);
            }
        }

        OverrideLightColor = true;
        _preventDust = enhancedGlowMasks;
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(
            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
        );
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        try
        {
            orig(self, bg, Style, Alpha, drawSinglePassLiquids);
        }
        finally
        {
            _preventDust = false;
            OverrideLightColor = false;
        }
        Main.spriteBatch.End();

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _cameraModeTarget,
            _smoothLightingInstance._cameraModeTarget1,
            bg,
            false,
            true,
            glow: useGlowMasks ? _cameraModeTmpTarget1 : null,
            lightedGlow: useGlowMasks && enhancedGlowMasks ? _cameraModeTmpTarget2 : null
        );

        Main.spriteBatch.Begin();
    }

    private void _Main_DrawWalls(On_Main.orig_DrawWalls orig, Main self)
    {
        if (!_inCameraMode)
        {
            orig(self);
            return;
        }

        if (
            LightingConfig.Instance.SmoothLightingEnabled()
            && PreferencesConfig.Instance.RenderOnlyLight
            && !LightingConfig.Instance.DrawOverbright()
        )
        {
            return;
        }

        var wallTarget = _smoothLightingInstance.GetCameraModeRenderTarget(
            _cameraModeTarget
        );

        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            // To get here, ambient occlusion must be enabled
            Main.tileBatch.End();
            Main.spriteBatch.End();
            Main.graphics.GraphicsDevice.SetRenderTarget(wallTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            orig(self);
            Main.tileBatch.End();
            Main.spriteBatch.End();

            _ambientOcclusionInstance.ApplyAmbientOcclusionCameraMode(
                _cameraModeTarget,
                wallTarget,
                _cameraModeBiome
            );

            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            return;
        }

        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting(true);

        if (useGlowMasks)
        {
            TextureUtils.MakeSize(
                ref _cameraModeTmpTarget1,
                _cameraModeTarget.Width,
                _cameraModeTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            Main.tileBatch.End();
            Main.spriteBatch.End();
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget1);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            try
            {
                orig(self);
            }
            finally
            {
                _preventDust = false;
                UseBlackLights = false;
            }

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeSize(
                    ref _cameraModeTmpTarget2,
                    _cameraModeTarget.Width,
                    _cameraModeTarget.Height
                );

                Main.tileBatch.End();
                Main.spriteBatch.End();
                Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget2);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                orig(self);
            }
        }

        OverrideLightColor = true;
        _preventDust = enhancedGlowMasks;
        Main.tileBatch.End();
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(wallTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
        try
        {
            orig(self);
        }
        finally
        {
            _preventDust = false;
            OverrideLightColor = false;
        }
        Main.tileBatch.End();
        Main.spriteBatch.End();

        var doAmbientOcclusion = LightingConfig.Instance.AmbientOcclusionEnabled();
        var doOverbright =
            LightingConfig.Instance.DrawOverbright()
            && LightingConfig.Instance.SmoothLightingEnabled();

        RenderTarget2D ambientOcclusionTarget = null;
        if (doAmbientOcclusion && doOverbright)
        {
            ambientOcclusionTarget =
                _ambientOcclusionInstance.ApplyAmbientOcclusionCameraMode(
                    _cameraModeTarget,
                    wallTarget,
                    _cameraModeBiome,
                    false
                );
        }

        var skipFinalPass = doAmbientOcclusion && !doOverbright;

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _cameraModeTarget,
            wallTarget,
            true,
            skipFinalPass,
            ambientOcclusionTarget: ambientOcclusionTarget,
            glow: !skipFinalPass && useGlowMasks ? _cameraModeTmpTarget1 : null,
            lightedGlow: !skipFinalPass && useGlowMasks && enhancedGlowMasks
                ? _cameraModeTmpTarget2
                : null
        );

        if (skipFinalPass)
        {
            _ambientOcclusionInstance.ApplyAmbientOcclusionCameraMode(
                _cameraModeTarget,
                _smoothLightingInstance._cameraModeTarget2,
                _cameraModeBiome,
                glow: useGlowMasks ? _cameraModeTmpTarget1 : null,
                lightedGlow: useGlowMasks && enhancedGlowMasks
                    ? _cameraModeTmpTarget2
                    : null
            );
        }

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void _Main_DrawTiles(
        On_Main.orig_DrawTiles orig,
        Main self,
        bool solidLayer,
        bool forRenderTargets,
        bool intoRenderTargets,
        int waterStyleOverride
    )
    {
        if (!_inCameraMode || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(
                self,
                solidLayer,
                forRenderTargets,
                intoRenderTargets,
                waterStyleOverride
            );
            return;
        }

        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks = LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting(true);

        if (useGlowMasks)
        {
            TextureUtils.MakeSize(
                ref _cameraModeTmpTarget1,
                _cameraModeTarget.Width,
                _cameraModeTarget.Height
            );

            UseBlackLights = true;
            _preventDust = true;
            Main.tileBatch.End();
            Main.spriteBatch.End();
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget1);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            try
            {
                orig(
                    self,
                    solidLayer,
                    forRenderTargets,
                    intoRenderTargets,
                    waterStyleOverride
                );
            }
            finally
            {
                _preventDust = false;
                UseBlackLights = false;
            }

            if (enhancedGlowMasks)
            {
                TextureUtils.MakeSize(
                    ref _cameraModeTmpTarget2,
                    _cameraModeTarget.Width,
                    _cameraModeTarget.Height
                );

                Main.tileBatch.End();
                Main.spriteBatch.End();
                Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget2);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                orig(
                    self,
                    solidLayer,
                    forRenderTargets,
                    intoRenderTargets,
                    waterStyleOverride
                );
            }
        }

        OverrideLightColor = true;
        _preventDust = enhancedGlowMasks;
        Main.tileBatch.End();
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(
            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
        );
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
        try
        {
            orig(
                self,
                solidLayer,
                forRenderTargets,
                intoRenderTargets,
                waterStyleOverride
            );
        }
        finally
        {
            _preventDust = false;
            OverrideLightColor = false;
        }
        Main.tileBatch.End();
        Main.spriteBatch.End();

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _cameraModeTarget,
            _smoothLightingInstance._cameraModeTarget1,
            false,
            false,
            glow: useGlowMasks ? _cameraModeTmpTarget1 : null,
            lightedGlow: useGlowMasks && enhancedGlowMasks ? _cameraModeTmpTarget2 : null
        );

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void _Main_DrawCapture(
        On_Main.orig_DrawCapture orig,
        Main self,
        Rectangle area,
        CaptureSettings settings
    )
    {
        if (LightingConfig.Instance.ModifyCameraModeRendering())
        {
            _cameraModeTarget = MainGraphics.GetRenderTarget();
            _inCameraMode = _cameraModeTarget is not null;
        }
        else
        {
            _inCameraMode = false;
        }

        if (_inCameraMode)
        {
            _cameraModeArea = area;
            _cameraModeBiome = settings.Biome;
            FancyLightingModSystem.EnsureRenderTargets();
        }

        try
        {
            orig(self, area, settings);
        }
        finally
        {
            _inCameraMode = false;
        }
    }
}
