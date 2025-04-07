using System.Reflection;
using FancyLighting.Config.Enums;
using FancyLighting.LightingEngines;
using FancyLighting.ModCompatibility;
using FancyLighting.Utils.Accessors;
using Terraria.GameContent.Drawing;
using Terraria.Graphics;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.Map;

namespace FancyLighting;

public sealed class FancyLightingMod : Mod
{
    private static bool _overrideLightColor;
    private static bool _useBlack;
    internal static bool _inCameraMode;
    private static bool _cameraModeDrawBackground;
    private static bool _disableLightColorOverride;
    private static bool _preventDust;
    private static bool _makePartialLiquidTranslucent;

    internal static bool _doingFilterManagerCapture;

    private SmoothLighting _smoothLightingInstance;
    private AmbientOcclusion _ambientOcclusionInstance;
    private ICustomLightingEngine _fancyLightingEngineInstance;
    private PostProcessing _postProcessingInstance;

    private FieldInfo _field_filterFrameBuffer1;
    private FieldInfo _field_filterFrameBuffer2;

    internal static RenderTarget2D _cameraModeTarget;
    private static RenderTarget2D _cameraModeTmpTarget1;
    private static RenderTarget2D _cameraModeTmpTarget2;
    internal static Rectangle _cameraModeArea;
    private static CaptureBiome _cameraModeBiome;

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

            if (OverrideLightMap(value, _smoothLightingInstance._whiteLights))
            {
                _overrideLightColor = value;
            }
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

            if (OverrideLightMap(value, _smoothLightingInstance._blackLights))
            {
                _useBlack = value;
            }
        }
    }

    private bool OverrideLightMap(bool doOverride, Vector3[] overrideLights)
    {
        if (!Lighting.UsingNewLighting)
        {
            return false;
        }

        var activeEngine = LightingAccessors._activeEngine(null);
        if (activeEngine is not LightingEngine lightingEngine)
        {
            return false;
        }

        if (doOverride && overrideLights is null)
        {
            return false;
        }

        var lightMapInstance = LightingEngineAccessors
            ._activeLightMap(lightingEngine)
            .AssertNotNull();

        if (doOverride)
        {
            _smoothLightingInstance._tmpLights = LightMapAccessors
                ._colors(lightMapInstance)
                .AssertNotNull();
        }

        LightMapAccessors._colors(lightMapInstance) = doOverride
            ? overrideLights
            : _smoothLightingInstance._tmpLights;

        if (!doOverride)
        {
            _smoothLightingInstance._tmpLights = null;
        }

        return true;
    }

    public override void Load()
    {
        if (Main.netMode is NetmodeID.Server)
        {
            return;
        }

        _overrideLightColor = false;
        _useBlack = false;
        _inCameraMode = false;
        _disableLightColorOverride = false;
        _preventDust = false;
        _makePartialLiquidTranslucent = false;

        _doingFilterManagerCapture = false;

        _smoothLightingInstance = new();
        _ambientOcclusionInstance = new();
        SetFancyLightingEngineInstance();
        _postProcessingInstance = new();

        AddHooks();

        FancySkyColors.Load();

        LightsCompatibility.Load();
        NitrateCompatibility.Load();
        SpiritReforgedCompatibility.Load();

        Main.QueueMainThreadAction(() =>
        {
            ColorUtils.Load();
        });
    }

    public override void Unload()
    {
        if (Main.netMode is NetmodeID.Server)
        {
            return;
        }

        Main.QueueMainThreadAction(() =>
        {
            // Do not dispose _cameraModeTarget
            // _cameraModeTarget comes from the Main class, so we don't own it
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

            SettingsSystem.EnsureRenderTargets(true);

            FancySkyColors.Unload();

            LightsCompatibility.Unload();
            NitrateCompatibility.Unload();
            SpiritReforgedCompatibility.Unload();
        });

        base.Unload();
    }

    public override void PostSetupContent()
    {
        // MonoMod hooks that are added later get run earlier
        AddPriorityHooks();
    }

    private void SetFancyLightingEngineInstance()
    {
        var mode =
            LightingConfig.Instance?.FancyLightingEngineMode ?? LightingEngineMode.Low;
        switch (mode)
        {
            default:
            case LightingEngineMode.Low:
                if (_fancyLightingEngineInstance is not FancyLightingEngine1X)
                {
                    _fancyLightingEngineInstance?.Unload();
                    _fancyLightingEngineInstance = new FancyLightingEngine1X();
                }

                break;

            case LightingEngineMode.Medium:
                if (_fancyLightingEngineInstance is not FancyLightingEngine2X)
                {
                    _fancyLightingEngineInstance?.Unload();
                    _fancyLightingEngineInstance = new FancyLightingEngine2X();
                }

                break;

            case LightingEngineMode.High:
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
        Main.renderNow = true;
        if (!Main.gameMenu)
        {
            Main.GetAreaToLight(
                out var firstTileX,
                out var lastTileX,
                out var firstTileY,
                out var lastTileY
            );
            for (var i = 4; i-- > 0; )
            {
                Lighting.LightTiles(firstTileX, lastTileX, firstTileY, lastTileY);
            }

            _smoothLightingInstance?.InvalidateSmoothLighting();
        }

        SetFancyLightingEngineInstance();
    }

    private void AddHooks()
    {
        On_Dust.NewDust += _Dust_NewDust;
        On_TileDrawing.ShouldTileShine += _TileDrawing_ShouldTileShine;
        On_Main.ShouldDrawBackgroundTileAt += _Main_ShouldDrawBackgroundTileAt;
        On_WorldMap.UpdateLighting += _WorldMap_UpdateLighting;
        On_TileDrawing.DrawPartialLiquid += _TileDrawing_DrawPartialLiquid;
        On_Main.DrawBG += _Main_DrawBG;
        On_Main.DrawUnderworldBackground += _Main_DrawUnderworldBackground;
        On_Main.DrawSunAndMoon += _Main_DrawSunAndMoon;
        On_TileDrawing.PostDrawTiles += _TileDrawing_PostDrawTiles;
        On_Main.RenderWater += _Main_RenderWater;
        On_Main.DrawWaters += _Main_DrawWaters;
        On_Main.RenderBackground += _Main_RenderBackground;
        On_Main.DrawBackground += _Main_DrawBackground;
        On_Main.RenderTiles += _Main_RenderTiles;
        On_Main.RenderTiles2 += _Main_RenderTiles2;
        On_Main.RenderWalls += _Main_RenderWalls;
        On_Main.DoLightTiles += _Main_DoLightTiles;
        On_LightingEngine.ProcessBlur += _LightingEngine_ProcessBlur;
        On_LightMap.Blur += _LightMap_Blur;
        On_TileLightScanner.ApplySurfaceLight += _TileLightScanner_ApplySurfaceLight;
        On_TileLightScanner.ApplyHellLight += _TileLightScanner_ApplyHellLight;
        On_TileLightScanner.ApplyLiquidLight += _TileLightScanner_ApplyLiquidLight;
        // Camera mode hooks added below
        // For some reason the order in which these are added matters to ensure that camera mode works
        // Maybe DrawCapture needs to be added last
        On_CaptureCamera.DrawTick += _CaptureCamera_DrawTick;
        On_Main.DrawLiquid += _Main_DrawLiquid;
        On_Main.DrawWalls += _Main_DrawWalls;
        On_Main.DrawTiles += _Main_DrawTiles;
        On_Main.DrawCapture += _Main_DrawCapture;
        On_Main.DoDraw += _Main_DoDraw;
    }

    private void AddPriorityHooks()
    {
        On_FilterManager.BeginCapture += _FilterManager_BeginCapture;
        On_FilterManager.EndCapture += _FilterManager_EndCapture;
        On_Main.DrawBlack += _Main_DrawBlack;
    }

    private static int _Dust_NewDust(
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

    private static bool _TileDrawing_ShouldTileShine(
        On_TileDrawing.orig_ShouldTileShine orig,
        ushort type,
        short frameX
    ) => !_overrideLightColor && orig(type, frameX);

    private static bool _Main_ShouldDrawBackgroundTileAt(
        On_Main.orig_ShouldDrawBackgroundTileAt orig,
        int i,
        int j
    ) => _overrideLightColor || orig(i, j);

    private static bool _WorldMap_UpdateLighting(
        On_WorldMap.orig_UpdateLighting orig,
        WorldMap self,
        int x,
        int y,
        byte light
    )
    {
        if (SettingsSystem._hiDef)
        {
            // Update if PostProcessing.HiDefBrightnessScale changes
            light = (byte)Math.Min((int)light << 1, 255);
        }

        return orig(self, x, y, light);
    }

    private static void _TileDrawing_DrawPartialLiquid(
        On_TileDrawing.orig_DrawPartialLiquid orig,
        TileDrawing self,
        bool behindBlocks,
        Tile tileCache,
        ref Vector2 position,
        ref Rectangle liquidSize,
        int liquidType,
        ref VertexColors colors
    )
    {
        if (_makePartialLiquidTranslucent)
        {
            colors.TopLeftColor.A = Math.Min(colors.TopLeftColor.A, (byte)254);
            colors.TopRightColor.A = Math.Min(colors.TopRightColor.A, (byte)254);
            colors.BottomLeftColor.A = Math.Min(colors.BottomLeftColor.A, (byte)254);
            colors.BottomRightColor.A = Math.Min(colors.BottomRightColor.A, (byte)254);
        }

        orig(
            self,
            behindBlocks,
            tileCache,
            ref position,
            ref liquidSize,
            liquidType,
            ref colors
        );
    }

    // Post-processing
    private void _FilterManager_BeginCapture(
        On_FilterManager.orig_BeginCapture orig,
        FilterManager self,
        RenderTarget2D screenTarget1,
        Color clearColor
    )
    {
        orig(self, screenTarget1, clearColor);

        _doingFilterManagerCapture = true;
    }

    private void _FilterManager_EndCapture(
        On_FilterManager.orig_EndCapture orig,
        FilterManager self,
        RenderTarget2D finalTexture,
        RenderTarget2D screenTarget1,
        RenderTarget2D screenTarget2,
        Color clearColor
    )
    {
        _doingFilterManagerCapture = false;

        if (
            (!SettingsSystem.PostProcessingAllowed() && !_inCameraMode)
            || !SettingsSystem.NeedsPostProcessing()
        )
        {
            orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
            return;
        }

        var backgroundTarget = _inCameraMode
            ? _cameraModeDrawBackground
                ? _cameraModeBackgroundTarget
                : null
            : _backgroundTarget;

        _postProcessingInstance.ApplyPostProcessing(
            screenTarget1,
            screenTarget2,
            backgroundTarget,
            _smoothLightingInstance
        );

        orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
    }

    // Separate background layer (so we don't apply overbright to it)

    private void _Main_DrawBG(On_Main.orig_DrawBG orig, Main self)
    {
        orig(self);

        if (
            !LightingConfig.Instance.OverbrightOverrideBackground()
            || SettingsSystem.HdrCompatibilityEnabled()
        )
        {
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;
        TextureUtils.MakeSize(
            ref _backgroundTarget,
            target.Width,
            target.Height,
            TextureUtils.ScreenFormat
        );

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

        if (
            !_inCameraMode
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || !LightingConfig.Instance.DrawOverbright()
        )
        {
            return;
        }

        Main.spriteBatch.End();

        TextureUtils.MakeSize(
            ref _cameraModeBackgroundTarget,
            _cameraModeTarget.Width,
            _cameraModeTarget.Height,
            TextureUtils.ScreenFormat
        );

        if (SettingsSystem.HdrCompatibilityEnabled())
        {
            _smoothLightingInstance.CalculateSmoothLighting(true);
            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget);
            _smoothLightingInstance.DrawSmoothLightingCameraMode(
                _cameraModeTarget,
                _cameraModeBackgroundTarget,
                false,
                false,
                true,
                true,
                true
            );

            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _smoothLightingInstance.ApplyBrightenShader(
                PostProcessing.CalculateHiDefBackgroundBrightness()
            );
            Main.spriteBatch.Draw(_cameraModeBackgroundTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
        else
        {
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
        }

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
        if (
            !LightingConfig.Instance.HiDefFeaturesEnabled()
            || !LightingConfig.Instance.OverbrightOverrideBackground()
            || _inCameraMode // For some reason this affects the whole sky in camera mode
        )
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var sunMoonBrightness = Main.dayTime ? 2.3f : 1.8f;
        sunMoonBrightness /= PostProcessing.HiDefBackgroundBrightnessMult;

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
            (solidLayer || intoRenderTargets)
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

        TileDrawingAccessors.DrawMultiTileVines(self);
        TileDrawingAccessors.DrawMultiTileGrass(self);
        TileDrawingAccessors.DrawVoidLenses(self);
        TileDrawingAccessors.DrawTeleportationPylons(self);
        TileDrawingAccessors.DrawMasterTrophies(self);
        TileDrawingAccessors.DrawGrass(self);
        TileDrawingAccessors.DrawAnyDirectionalGrass(self);
        TileDrawingAccessors.DrawTrees(self);
        TileDrawingAccessors.DrawVines(self);
        TileDrawingAccessors.DrawReverseVines(self);
        TileDrawingAccessors.DrawCustom(self, false);

        Main.spriteBatch.End();
    }

    // Liquids

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
            TextureUtils.MakeSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
            );
            TextureUtils.MakeSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
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
                TextureUtils.MakeSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height,
                    TextureUtils.ScreenFormat
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

        _smoothLightingInstance.DrawSmoothLighting(
            tileTarget,
            useGlowMasks ? _tmpTarget2 : null,
            false,
            true
        );

        if (!useGlowMasks)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        _smoothLightingInstance.DrawGlow(
            _tmpTarget2,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );
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
            null,
            true,
            true
        );
        _smoothLightingInstance.DrawSmoothLighting(
            Main.instance.backWaterTarget,
            null,
            true,
            true
        );
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
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
                _smoothLightingInstance._cameraModeTarget1,
                _cameraModeTarget,
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

    private void _Main_DrawBlack(On_Main.orig_DrawBlack orig, Main self, bool force)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self, force);
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
            orig(self, force);
        }
        finally
        {
            OverrideLightColor = initialLightingOverride;
        }
    }

    // Non-moving objects (tiles, walls, etc.)

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
            TextureUtils.MakeSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
            );
            TextureUtils.MakeSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
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
                TextureUtils.MakeSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height,
                    TextureUtils.ScreenFormat
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
        _makePartialLiquidTranslucent = LightingConfig.Instance.SimulateNormalMaps;
        try
        {
            orig(self);
        }
        finally
        {
            _makePartialLiquidTranslucent = false;
            _preventDust = false;
            OverrideLightColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(
            tileTarget,
            useGlowMasks ? _tmpTarget2 : null,
            false
        );

        if (!useGlowMasks)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        _smoothLightingInstance.DrawGlow(
            _tmpTarget2,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );
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
            TextureUtils.MakeSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
            );
            TextureUtils.MakeSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
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
                TextureUtils.MakeSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height,
                    TextureUtils.ScreenFormat
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

        _smoothLightingInstance.DrawSmoothLighting(
            tileTarget,
            useGlowMasks ? _tmpTarget2 : null,
            false
        );

        if (!useGlowMasks)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        _smoothLightingInstance.DrawGlow(
            _tmpTarget2,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );
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
                Main.graphics.GraphicsDevice.SetRenderTarget(null);
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
            TextureUtils.MakeSize(
                ref _tmpTarget1,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
            );
            TextureUtils.MakeSize(
                ref _tmpTarget2,
                tileTarget.Width,
                tileTarget.Height,
                TextureUtils.ScreenFormat
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
                TextureUtils.MakeSize(
                    ref _tmpTarget3,
                    tileTarget.Width,
                    tileTarget.Height,
                    TextureUtils.ScreenFormat
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
                null,
                false,
                false
            );
        }

        _smoothLightingInstance.DrawSmoothLighting(
            tileTarget,
            useGlowMasks ? _tmpTarget2 : null,
            true,
            ambientOcclusionTarget: ambientOcclusionTarget
        );

        var lightedTarget = _tmpTarget2;
        if (doAmbientOcclusion && !doOverbright)
        {
            lightedTarget = _ambientOcclusionInstance.ApplyAmbientOcclusion(
                lightedTarget,
                true,
                false
            );
        }

        if (!useGlowMasks)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(tileTarget);
        _smoothLightingInstance.DrawGlow(
            lightedTarget,
            _tmpTarget1,
            enhancedGlowMasks ? _tmpTarget3 : null
        );
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    private void _Main_DoLightTiles(On_Main.orig_DoLightTiles orig, Main self)
    {
        orig(self);

        if (
            !LightingConfig.Instance.OverbrightOverrideBackground()
            || !SettingsSystem.HdrCompatibilityEnabled()
            || _inCameraMode
        )
        {
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;
        TextureUtils.MakeSize(
            ref _backgroundTarget,
            target.Width,
            target.Height,
            TextureUtils.ScreenFormat
        );

        _smoothLightingInstance.CalculateSmoothLighting();
        _smoothLightingInstance.DrawSmoothLighting(
            target,
            _backgroundTarget,
            false,
            true,
            true,
            true
        );

        Main.graphics.GraphicsDevice.SetRenderTarget(target);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _smoothLightingInstance.ApplyBrightenShader(
            PostProcessing.CalculateHiDefBackgroundBrightness()
        );
        Main.spriteBatch.Draw(_backgroundTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

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
            LightingEngineAccessors._workingProcessedArea(self)
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

        var colors = LightMapAccessors._colors(self);
        var lightMasks = LightMapAccessors._mask(self);
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

        if (!SettingsSystem._hiDef)
        {
            return;
        }

        lightColor *= PostProcessing.HiDefBackgroundBrightnessMult;
    }

    private static void _TileLightScanner_ApplyHellLight(
        On_TileLightScanner.orig_ApplyHellLight orig,
        TileLightScanner self,
        Tile tile,
        int x,
        int y,
        ref Vector3 lightColor
    )
    {
        orig(self, tile, x, y, ref lightColor);

        if (!SettingsSystem._hiDef)
        {
            return;
        }

        lightColor *= PostProcessing.HiDefBackgroundBrightnessMult;
    }

    private static void _TileLightScanner_ApplyLiquidLight(
        On_TileLightScanner.orig_ApplyLiquidLight orig,
        TileLightScanner self,
        Tile tile,
        ref Vector3 lightColor
    )
    {
        // This code is adapted from vanilla

        if (!SettingsSystem._hiDef)
        {
            orig(self, tile, ref lightColor);
            return;
        }

        if (tile.LiquidAmount <= 0)
        {
            return;
        }

        if (tile.LiquidType is LiquidID.Lava)
        {
            var brightness = 0.55f;
            brightness += (270 - Main.mouseTextColor) / 900f;
            brightness *= 2.5f;
            lightColor.X = Math.Max(lightColor.X, brightness);
            lightColor.Y = Math.Max(lightColor.Y, 0.3f * brightness);
            lightColor.Z = Math.Max(lightColor.Z, 0.05f * brightness);
        }
        else if (tile.LiquidType is LiquidID.Shimmer)
        {
            var redBlue = 0.7f;
            var green = 0.7f;
            redBlue += (270 - Main.mouseTextColor) / 900f;
            green += (270 - Main.mouseTextColor) / 125f;
            lightColor.X = Math.Max(lightColor.X, redBlue * 0.6f);
            lightColor.Y = Math.Max(lightColor.Y, green * 0.25f);
            lightColor.Z = Math.Max(lightColor.Z, redBlue * 0.9f);
        }
    }

    private void _CaptureCamera_DrawTick(On_CaptureCamera.orig_DrawTick orig, object self)
    {
        _field_filterFrameBuffer1 ??= self.GetType()
            .GetField(
                "_filterFrameBuffer1",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
            .AssertNotNull();

        _field_filterFrameBuffer2 ??= self.GetType()
            .GetField(
                "_filterFrameBuffer2",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
            .AssertNotNull();

        RenderTarget2D target;
        target = (RenderTarget2D)_field_filterFrameBuffer1.GetValue(self);
        TextureUtils.EnsureFormat(ref target, TextureUtils.ScreenFormat);
        _field_filterFrameBuffer1.SetValue(self, target);
        target = (RenderTarget2D)_field_filterFrameBuffer2.GetValue(self);
        TextureUtils.EnsureFormat(ref target, TextureUtils.ScreenFormat);
        _field_filterFrameBuffer2.SetValue(self, target);

        _inCameraMode = LightingConfig.Instance.ModifyCameraModeRendering();
        try
        {
            orig(self);
        }
        finally
        {
            _inCameraMode = false;
            _cameraModeTarget = null;
        }
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
                _cameraModeTarget.Height,
                TextureUtils.ScreenFormat
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
                    _cameraModeTarget.Height,
                    TextureUtils.ScreenFormat
                );

                Main.spriteBatch.End();
                Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTmpTarget2);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin();
                orig(self, bg, Style, Alpha, drawSinglePassLiquids);
            }
        }

        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(
            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
        );
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();

        OverrideLightColor = true;
        _preventDust = enhancedGlowMasks;
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
            _smoothLightingInstance._cameraModeTarget1,
            _cameraModeTarget,
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
                _cameraModeTarget.Height,
                TextureUtils.ScreenFormat
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
                    _cameraModeTarget.Height,
                    TextureUtils.ScreenFormat
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

        Main.tileBatch.End();
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(wallTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();

        OverrideLightColor = true;
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
            wallTarget,
            _cameraModeTarget,
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
                _cameraModeTarget.Height,
                TextureUtils.ScreenFormat
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
                    _cameraModeTarget.Height,
                    TextureUtils.ScreenFormat
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

        Main.tileBatch.End();
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(
            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
        );
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();

        OverrideLightColor = true;
        _preventDust = enhancedGlowMasks;
        _makePartialLiquidTranslucent = LightingConfig.Instance.SimulateNormalMaps;
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
            _makePartialLiquidTranslucent = false;
            _preventDust = false;
            OverrideLightColor = false;
        }
        Main.tileBatch.End();
        Main.spriteBatch.End();

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _smoothLightingInstance._cameraModeTarget1,
            _cameraModeTarget,
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
            _inCameraMode = _inCameraMode && _cameraModeTarget is not null;
        }
        else
        {
            _inCameraMode = false;
        }

        if (_inCameraMode)
        {
            _cameraModeArea = area;
            _cameraModeBiome = settings.Biome;
            _cameraModeDrawBackground = settings.CaptureBackground;
            ModContent.GetInstance<SettingsSystem>().SettingsUpdate();
        }

        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            || !LightingConfig.Instance.DrawOverbright()
            || SettingsSystem.HdrCompatibilityEnabled()
        )
        {
            orig(self, area, settings);
            return;
        }

        var originalAlphaSourceBlend = BlendState.Additive.AlphaSourceBlend;
        BlendState.Additive.AlphaSourceBlend = Blend.Zero;
        try
        {
            orig(self, area, settings);
        }
        finally
        {
            BlendState.Additive.AlphaSourceBlend = originalAlphaSourceBlend;
        }
    }

    private void _Main_DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        ModContent.GetInstance<SettingsSystem>().SettingsUpdate();
        _doingFilterManagerCapture = false;

        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            || !LightingConfig.Instance.DrawOverbright()
            || SettingsSystem.HdrCompatibilityEnabled()
        )
        {
            orig(self, gameTime);
            return;
        }

        var originalAlphaSourceBlend = BlendState.Additive.AlphaSourceBlend;
        BlendState.Additive.AlphaSourceBlend = Blend.Zero;
        try
        {
            orig(self, gameTime);
        }
        finally
        {
            BlendState.Additive.AlphaSourceBlend = originalAlphaSourceBlend;
        }
    }
}
