using System.Reflection;
using FancyLighting.Config.Enums;
using FancyLighting.LightingEngines;
using FancyLighting.ModCompatibility;
using FancyLighting.Utils.Accessors;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Events;
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
    internal static bool _isGameInCameraMode;
    private static bool _cameraModeDrawBackground;
    private static bool _disableLightColorOverride;
    internal static bool _preventTileParticles;
    private static bool _makePartialLiquidTranslucent;
    private static bool _suppressRenderBlack;

    internal static bool _doingFilterManagerCapture;

    private SmoothLighting _smoothLightingInstance;
    private AmbientOcclusion _ambientOcclusionInstance;
    private ICustomLightingEngine _fancyLightingEngineInstance;
    private PostProcessing _postProcessingInstance;
    private FancySkyColors _fancySkyColorsInstance;
    private FancySkyRendering _fancySkyRenderingInstance;

    private FieldInfo _field_filterFrameBuffer1;
    private FieldInfo _field_filterFrameBuffer2;

    internal static RenderTarget2D _cameraModeTarget;
    private RenderTarget2D _cameraModeTmpTarget1;
    private RenderTarget2D _cameraModeTmpTarget2;
    internal static Rectangle _cameraModeArea;
    private CaptureBiome _cameraModeBiome;

    private RenderTarget2D _tmpTarget1;
    private RenderTarget2D _tmpTarget2;
    private RenderTarget2D _tmpTarget3;

    private RenderTarget2D _backgroundTarget;
    private RenderTarget2D _cameraModeBackgroundTarget;

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

    public override object Call(params object[] args)
    {
        try
        {
            return ModCalls.Call(args);
        }
        catch (Exception)
        {
            return null;
        }
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
        _preventTileParticles = false;
        _makePartialLiquidTranslucent = false;

        _doingFilterManagerCapture = false;

        SpriteBatchEffectLoader.Load();

        _smoothLightingInstance = new();
        _ambientOcclusionInstance = new();
        SetFancyLightingEngineInstance();
        _postProcessingInstance = new();
        _fancySkyColorsInstance = new();
        _fancySkyRenderingInstance = new();

        CalamityModCompatibility.Load();
        LightsCompatibility.Load();
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

            _fancySkyRenderingInstance?.Unload();
            _fancySkyColorsInstance?.Unload();
            _postProcessingInstance?.Unload();
            _fancyLightingEngineInstance?.Unload();
            _ambientOcclusionInstance?.Unload();
            _smoothLightingInstance?.Unload();

            SettingsSystem.EnsureRenderTargets(true);

            CalamityModCompatibility.Unload();
            LightsCompatibility.Unload();
            SpiritReforgedCompatibility.Unload();

            PerformanceTracker.Unload();
            PresetOptions.Unload();

            MainGraphics.Unload();
            SpriteBatchEffectLoader.Unload();
        });

        base.Unload();
    }

    public override void PostSetupContent()
    {
        // MonoMod hooks that are added later get run earlier
        AddHooks();
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
                if (_fancyLightingEngineInstance is not FancyLightingEngine2XVec)
                {
                    _fancyLightingEngineInstance?.Unload();
                    _fancyLightingEngineInstance = new FancyLightingEngine2XVec();
                }

                break;
        }
    }

    internal void OnConfigChange()
    {
        SetFancyLightingEngineInstance();

        if (Main.gameMenu || Main.mapFullscreen)
        {
            return;
        }

        // Ensure that the transition is seamless by updating everything needed

        Main.renderNow = true;

        // This code is adapted from vanilla
        MainAccessors.SetBackColor(
            null,
            new Main.InfoToSetBackColor
            {
                isInGameMenuOrIsServer =
                    Main.gameMenu || Main.netMode == NetmodeID.Server,
                CorruptionBiomeInfluence =
                    (float)Main.SceneMetrics.EvilTileCount
                    / SceneMetrics.CorruptionTileMax,
                CrimsonBiomeInfluence =
                    (float)Main.SceneMetrics.BloodTileCount / SceneMetrics.CrimsonTileMax,
                JungleBiomeInfluence =
                    (float)Main.SceneMetrics.JungleTileCount / SceneMetrics.JungleTileMax,
                MushroomBiomeInfluence = Main.SmoothedMushroomLightInfluence,
                GraveyardInfluence = Main.GraveyardVisualIntensity,
                BloodMoonActive = Main.bloodMoon || Main.SceneMetrics.BloodMoonMonolith,
                LanternNightActive = LanternNight.LanternsUp,
            },
            out _,
            out _
        );
        MainAccessors.ApplyColorOfTheSkiesToTiles(null);
        MainAccessors.UpdateAtmosphereTransparencyToSkyColor(null);

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

        // Ensure ambient occlusion updates immediately
        MainAccessors.RenderTiles2(Main.instance);
        Main.sceneTile2Pos.X = Main.screenPosition.X - Main.offScreenRange;
        Main.sceneTile2Pos.Y = Main.screenPosition.Y - Main.offScreenRange;
    }

    private void AddHooks()
    {
        IL_Dust.NewDust += IL_Dust_NewDust;
        IL_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += IL_Gore_NewGore;
        IL_TileDrawing.DrawTiles_EmitParticles += IL_TileDrawing_DrawTiles_EmitParticles;
        IL_TileDrawing.ShouldTileShine += IL_TileDrawing_ShouldTileShine;
        IL_Main.ShouldDrawBackgroundTileAt += IL_Main_ShouldDrawBackgroundTileAt;
        IL_WorldMap.UpdateLighting += IL_WorldMap_UpdateLighting;
        On_TileLightScanner.ApplySurfaceLight += _TileLightScanner_ApplySurfaceLight;
        On_TileLightScanner.ApplyHellLight += _TileLightScanner_ApplyHellLight;
        On_TileLightScanner.ApplyLiquidLight += _TileLightScanner_ApplyLiquidLight;
        On_TileDrawing.DrawPartialLiquid += _TileDrawing_DrawPartialLiquid;
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
        On_Main.DoLightTiles += _Main_DoLightTiles;
        On_LightingEngine.ProcessBlur += _LightingEngine_ProcessBlur;
        On_LightMap.Blur += _LightMap_Blur;

        // Camera mode hooks
        // For some reason the order in which these are added matters to ensure that camera mode works
        // Maybe DrawCapture needs to be added last
        On_CaptureCamera.DrawTick += _CaptureCamera_DrawTick;
        On_Main.DrawLiquid += _Main_DrawLiquid;
        On_Main.DrawWalls += _Main_DrawWalls;
        On_Main.DrawTiles += _Main_DrawTiles;
        On_Main.DrawCapture += _Main_DrawCapture;

        On_Main.DoDraw += _Main_DoDraw;
        On_FilterManager.BeginCapture += _FilterManager_BeginCapture;
        On_FilterManager.EndCapture += _FilterManager_EndCapture;
        On_Main.DrawBlack += _Main_DrawBlack;
    }

    // These methods are run often
    // Use IL hooks for better performance

    private static void IL_Dust_NewDust(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var preventTileParticlesField = typeof(FancyLightingMod)
                .GetField(
                    nameof(_preventTileParticles),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();
            var mainDustField = typeof(Main)
                .GetField(nameof(Main.dust), BindingFlags.Public | BindingFlags.Static)
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_preventTileParticles)
            {
                return Main.dust.Length - 1; // no dust
            }
            */
            cursor.Emit(OpCodes.Ldsfld, preventTileParticlesField);
            cursor.Emit(OpCodes.Brfalse, afterIfBlockLabel);
            cursor.Emit(OpCodes.Ldsfld, mainDustField);
            cursor.Emit(OpCodes.Ldlen);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Sub);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(afterIfBlockLabel);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private static void IL_Gore_NewGore(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var preventTileParticlesField = typeof(FancyLightingMod)
                .GetField(
                    nameof(_preventTileParticles),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();
            var mainGoreField = typeof(Main)
                .GetField(nameof(Main.gore), BindingFlags.Public | BindingFlags.Static)
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_preventTileParticles)
            {
                return Main.gore.Length - 1; // no gore
            }
            */
            cursor.Emit(OpCodes.Ldsfld, preventTileParticlesField);
            cursor.Emit(OpCodes.Brfalse, afterIfBlockLabel);
            cursor.Emit(OpCodes.Ldsfld, mainGoreField);
            cursor.Emit(OpCodes.Ldlen);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Sub);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(afterIfBlockLabel);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private static void IL_TileDrawing_DrawTiles_EmitParticles(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var preventTileParticlesField = typeof(FancyLightingMod)
                .GetField(
                    nameof(_preventTileParticles),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_preventTileParticles)
            {
                return;
            }
            */
            cursor.Emit(OpCodes.Ldsfld, preventTileParticlesField);
            cursor.Emit(OpCodes.Brfalse, afterIfBlockLabel);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(afterIfBlockLabel);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private static void IL_TileDrawing_ShouldTileShine(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var overrideLightColorField = typeof(FancyLightingMod)
                .GetField(
                    nameof(_overrideLightColor),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_overrideLightColor)
            {
                return false;
            }
            */
            cursor.Emit(OpCodes.Ldsfld, overrideLightColorField);
            cursor.Emit(OpCodes.Brfalse, afterIfBlockLabel);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(afterIfBlockLabel);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private static void IL_Main_ShouldDrawBackgroundTileAt(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var overrideLightColorField = typeof(FancyLightingMod)
                .GetField(
                    nameof(_overrideLightColor),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_overrideLightColor)
            {
                return true;
            }
            */
            cursor.Emit(OpCodes.Ldsfld, overrideLightColorField);
            cursor.Emit(OpCodes.Brfalse, afterIfBlockLabel);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(afterIfBlockLabel);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private static void IL_WorldMap_UpdateLighting(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var settingsSystemHiDefField = typeof(SettingsSystem)
                .GetField(
                    nameof(SettingsSystem._hiDef),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();
            var mathMinMethod = typeof(Math)
                .GetMethod(
                    nameof(Math.Min),
                    BindingFlags.Public | BindingFlags.Static,
                    [typeof(int), typeof(int)]
                )
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            // Instance method
            // Args are: (int x, int y, byte light)
            /*
            if (SettingsSystem._hiDef)
            {
                // Update if PostProcessing.HiDefBrightnessScale changes
                light = (byte)Math.Min((int)light << 1, 255);
            }
            */
            cursor.Emit(OpCodes.Ldsfld, settingsSystemHiDefField);
            cursor.Emit(OpCodes.Brfalse, afterIfBlockLabel);
            cursor.Emit(OpCodes.Ldarg_3);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Shl);
            cursor.Emit(OpCodes.Ldc_I4, 255);
            cursor.Emit(OpCodes.Call, mathMinMethod);
            cursor.Emit(OpCodes.Starg, 3);
            cursor.MarkLabel(afterIfBlockLabel);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
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

    // Only needed if LiquidSlopeFix is set to false in tModLoader
    // Otherwise this method never runs
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
            var doDepthOfField =
                LightingConfig.Instance.HiDefFeaturesEnabled()
                && PreferencesConfig.Instance.DepthOfField;

            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeBackgroundTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            var brightness = PostProcessing.CalculateHiDefBackgroundBrightness();
            if (doDepthOfField)
            {
                _postProcessingInstance.ApplyGammaShader(
                    ColorUtils.GammaToLinear(brightness),
                    PostProcessing.ContentGamma()
                );
            }
            else
            {
                _smoothLightingInstance.ApplyBrightenShader(brightness);
            }

            Main.spriteBatch.Draw(_cameraModeTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            if (doDepthOfField)
            {
                _postProcessingInstance.Blur(
                    _cameraModeBackgroundTarget,
                    _cameraModeTarget,
                    PreferencesConfig.Instance.DepthOfFieldRadius
                );

                Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeBackgroundTarget);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                _postProcessingInstance.ApplyGammaShader(
                    1f,
                    1f / PostProcessing.ContentGamma()
                );
                Main.spriteBatch.Draw(_cameraModeTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
            }

            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget);
            _smoothLightingInstance.CalculateSmoothLighting(true);
            _smoothLightingInstance.DrawSmoothLightingCameraMode(
                _cameraModeBackgroundTarget,
                _cameraModeTarget,
                false,
                false,
                true,
                true,
                true
            );
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
        if (LightingConfig.Instance.FancySkyRenderingEnabled())
        {
            _fancySkyRenderingInstance.DrawSunAndMoon(
                orig,
                self,
                sceneArea,
                moonColor,
                sunColor,
                tempMushroomInfluence
            );
            return;
        }

        if (
            !LightingConfig.Instance.HiDefFeaturesEnabled()
            || !LightingConfig.Instance.OverbrightOverrideBackground()
        )
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        var rasterizerState = _inCameraMode ? RasterizerState.CullNone : Main.Rasterizer;
        Main.spriteBatch.End();

        var sunMoonBrightness = Main.dayTime ? 2.3f : 1.8f;
        sunMoonBrightness /= PostProcessing.HiDefBackgroundBrightnessMult;

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            rasterizerState,
            null,
            transform
        );
        _smoothLightingInstance.ApplyBrightenShader(sunMoonBrightness);
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            rasterizerState,
            null,
            transform
        );
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

        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            || (
                SpiritReforgedCompatibility._disableCustomLiquidRendering
                && !PreferencesConfig.Instance.RenderOnlyLight
            )
        )
        {
            orig(self);
            return;
        }

        var tileTarget = Main.waterTarget;
        var useGlowMasks = !PreferencesConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
            _disableLightColorOverride = true;
            try
            {
                orig(self);
            }
            finally
            {
                _disableLightColorOverride = false;
                _preventTileParticles = false;
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

        _preventTileParticles = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventTileParticles = false;
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
            || (
                SpiritReforgedCompatibility._disableCustomLiquidRendering
                && !PreferencesConfig.Instance.RenderOnlyLight
            )
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

    private static void _Main_RenderBlack(On_Main.orig_RenderBlack orig, Main self)
    {
        if (_suppressRenderBlack)
        {
            return;
        }

        orig(self);
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
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
            _suppressRenderBlack = true;
            try
            {
                orig(self);
            }
            finally
            {
                _suppressRenderBlack = false;
                _preventTileParticles = false;
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
        _preventTileParticles = enhancedGlowMasks;
        _makePartialLiquidTranslucent = LightingConfig.Instance.SimulateNormalMaps;
        _suppressRenderBlack = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _suppressRenderBlack = false;
            _makePartialLiquidTranslucent = false;
            _preventTileParticles = false;
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
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
            try
            {
                orig(self);
            }
            finally
            {
                _preventTileParticles = false;
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
        _preventTileParticles = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventTileParticles = false;
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
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
            try
            {
                orig(self);
            }
            finally
            {
                _preventTileParticles = false;
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
        _preventTileParticles = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventTileParticles = false;
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

        var doDepthOfField =
            LightingConfig.Instance.HiDefFeaturesEnabled()
            && PreferencesConfig.Instance.DepthOfField;

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
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        var brightness = PostProcessing.CalculateHiDefBackgroundBrightness();
        if (doDepthOfField)
        {
            // not in camera mode
            // background should never have transparency, so we can use no-alpha shader
            _postProcessingInstance.ApplyGammaNoAlphaShader(
                ColorUtils.GammaToLinear(brightness),
                PostProcessing.ContentGamma()
            );
        }
        else
        {
            _smoothLightingInstance.ApplyBrightenShader(brightness);
        }

        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        if (doDepthOfField)
        {
            _postProcessingInstance.Blur(
                _backgroundTarget,
                target,
                PreferencesConfig.Instance.DepthOfFieldRadius
            );

            Main.graphics.GraphicsDevice.SetRenderTarget(_backgroundTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _postProcessingInstance.ApplyGammaNoAlphaShader(
                1f,
                1f / PostProcessing.ContentGamma()
            );
            Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }

        _smoothLightingInstance.CalculateSmoothLighting();
        _smoothLightingInstance.DrawSmoothLighting(
            _backgroundTarget,
            target,
            false,
            true,
            true,
            true
        );

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

        // frame timing optimization
        Main.renderCount = 2;
    }

    private void _LightMap_Blur(On_LightMap.orig_Blur orig, LightMap self)
    {
        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            && !LightingConfig.Instance.FancyLightingEngineEnabled()
        )
        {
            PerformanceTracker.StartTiming("Vanilla Lighting Engine");
            orig(self);
            PerformanceTracker.StopTiming("Vanilla Lighting Engine");
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
            PerformanceTracker.StartTiming("Fancy Lighting Engine");
            _fancyLightingEngineInstance.SpreadLight(
                self,
                colors,
                lightMasks,
                self.Width,
                self.Height
            );
            PerformanceTracker.StopTiming("Fancy Lighting Engine");
        }
        else
        {
            PerformanceTracker.StartTiming("Vanilla Lighting Engine");
            orig(self);
            PerformanceTracker.StopTiming("Vanilla Lighting Engine");
        }

        if (LightingConfig.Instance.SmoothLightingEnabled())
        {
            PerformanceTracker.StartTiming("Smooth Lighting (Light Map Array)");
            _smoothLightingInstance.GetAndBlurLightMap(
                colors,
                lightMasks,
                self.Width,
                self.Height
            );
            PerformanceTracker.StopTiming("Smooth Lighting (Light Map Array)");
        }
    }

    // Camera mode hooks below

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
        _isGameInCameraMode = true;
        try
        {
            orig(self);
        }
        finally
        {
            _inCameraMode = false;
            _isGameInCameraMode = false;
            _cameraModeTarget = null;
        }
    }

    private void _Main_DrawLiquid(
        On_Main.orig_DrawLiquid orig,
        Main self,
        bool bg,
        int Style,
        float Alpha,
        bool drawSinglePassLiquids
    )
    {
        if (
            !_inCameraMode
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || (
                SpiritReforgedCompatibility._disableCustomLiquidRendering
                && !PreferencesConfig.Instance.RenderOnlyLight
            )
        )
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
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
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
                _preventTileParticles = false;
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
        _preventTileParticles = enhancedGlowMasks;
        try
        {
            orig(self, bg, Style, Alpha, drawSinglePassLiquids);
        }
        finally
        {
            _preventTileParticles = false;
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
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
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
                _preventTileParticles = false;
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
        _preventTileParticles = enhancedGlowMasks;
        try
        {
            orig(self);
        }
        finally
        {
            _preventTileParticles = false;
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
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

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
            _preventTileParticles = true;
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
                _preventTileParticles = false;
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
        _preventTileParticles = enhancedGlowMasks;
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
            _preventTileParticles = false;
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
        PerformanceTracker.StopTiming("Delta Time");
        PerformanceTracker.StartTiming("Delta Time");
        PerformanceTracker.DisplayStatistics(false);

        SpriteBatchEffectLoader.ClearEffect();

        ModContent.GetInstance<SettingsSystem>().SettingsUpdate();
        _doingFilterManagerCapture = false;

        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            || !LightingConfig.Instance.DrawOverbright()
            || SettingsSystem.HdrCompatibilityEnabled()
        )
        {
            orig(self, gameTime);
            _fancySkyColorsInstance.DrawColorProfiles();
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

        _fancySkyColorsInstance.DrawColorProfiles();
    }
}
