using System.Reflection;
using FancyLighting.Config;
using FancyLighting.Config.Enums;
using FancyLighting.Core.LightingEngines;
using FancyLighting.Core.Sky;
using FancyLighting.ModCompatibility;
using FancyLighting.VFX;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Events;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.Map;

namespace FancyLighting;

public sealed class FancyLightingMod : Mod
{
    internal static bool _preventTileParticles;
    private static bool _makePartialLiquidTranslucent;
    private static bool _suppressRenderBlack;

    private SmoothLighting _smoothLightingInstance;
    private AmbientOcclusion _ambientOcclusionInstance;
    private ICustomLightingEngine _fancyLightingEngineInstance;
    private PostProcessing _postProcessingInstance;
    private FancySkyColors _fancySkyColorsInstance;
    private FancySkyRendering _fancySkyRenderingInstance;

    private RenderTarget2D _tmpTarget1;
    private RenderTarget2D _tmpTarget2;
    private RenderTarget2D _tmpTarget3;

    private RenderTarget2D _tmpScreenTarget;

    private RenderTarget2D _backgroundTarget;

    private RenderTarget2D _activeAmbientOcclusionTarget;

    private void UseWhiteLightMap(bool enable)
    {
        if (enable == _useWhiteLightMap)
        {
            return;
        }

        if (OverrideLightMap(enable, _smoothLightingInstance._whiteLights))
        {
            _useWhiteLightMap = enable;
        }
    }

    private bool _useWhiteLightMap;

    private void UseBlackLightMap(bool enable)
    {
        if (enable == _useBlackLightMap)
        {
            return;
        }

        if (OverrideLightMap(enable, _smoothLightingInstance._blackLights))
        {
            _useBlackLightMap = enable;
        }
    }

    private bool _useBlackLightMap;

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

        SpriteBatchEffectLoader.Load();
        BlurRenderer.Load();

        _smoothLightingInstance = new();
        _ambientOcclusionInstance = new();
        SetFancyLightingEngineInstance();
        _postProcessingInstance = new();
        _fancySkyColorsInstance = new();
        _fancySkyRenderingInstance = new();
        FancySkyClouds.Load();

        CalamityModCompatibility.Load();
        LightsCompatibility.Load();
        SpiritReforgedCompatibility.Load();

        Main.QueueMainThreadAction(() =>
        {
            Blitter.Load();
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
            _tmpTarget1?.Dispose();
            _tmpTarget2?.Dispose();
            _tmpTarget3?.Dispose();
            _tmpScreenTarget?.Dispose();
            _backgroundTarget?.Dispose();

            FancySkyLighting.Unload();
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
            CustomBlendStates.Unload();
            BlurRenderer.Unload();
            SpriteBatchEffectLoader.Unload();
            Blitter.Unload();
        });

        base.Unload();
    }

    public override void PostSetupContent()
    {
        // MonoMod hooks that are added later get run earlier
        AddHooks();
        MainGraphics.AddHooks();
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
        // Hooks for frequently called methods (IL Hooks used to increase performance)
        IL_Dust.NewDust += IL_Dust_NewDust;
        IL_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += IL_Gore_NewGore;
        IL_TileDrawing.DrawTiles_EmitParticles += IL_TileDrawing_DrawTiles_EmitParticles;
        IL_TileDrawing.ShouldTileShine += IL_TileDrawing_ShouldTileShine;
        IL_Main.ShouldDrawBackgroundTileAt += IL_Main_ShouldDrawBackgroundTileAt;
        IL_WorldMap.UpdateLighting += IL_WorldMap_UpdateLighting;

        // Tile Light Scanner hooks
        On_TileLightScanner.GetTileLight += _TileLightScanner_GetTileLight;
        On_TileLightScanner.ApplySurfaceLight += _TileLightScanner_ApplySurfaceLight;
        On_TileLightScanner.ApplyHellLight += _TileLightScanner_ApplyHellLight;
        On_TileLightScanner.ApplyLiquidLight += _TileLightScanner_ApplyLiquidLight;

        // Liquid slope rendering
        On_TileDrawing.DrawPartialLiquid += _TileDrawing_DrawPartialLiquid;

        // Drawing and post-processing hooks
        On_Main.DoDraw += _Main_DoDraw;
        On_FilterManager.BeginCapture += _FilterManager_BeginCapture;
        On_FilterManager.EndCapture += _FilterManager_EndCapture;

        // Fancy Sky
        On_Main.DrawSunAndMoon += _Main_DrawSunAndMoon;

        // Foreground/background separation
        On_Main.DoLightTiles += _Main_DoLightTiles;
        On_Main.DrawUnderworldBackground += _Main_DrawUnderworldBackground;

        // Smooth Lighting and Ambient Occlusion
        On_TileDrawing.PostDrawTiles += _TileDrawing_PostDrawTiles;
        On_Main.RenderBackground += _Main_RenderBackground;
        On_Main.DrawBackground += _Main_DrawBackground;
        On_Main.DrawWaters += _Main_DrawWaters;
        On_Main.DrawLiquid += _Main_DrawLiquid;
        On_Main.RenderWater += _Main_RenderWater;
        On_Main.RenderBlack += _Main_RenderBlack;
        On_Main.DrawBlack += _Main_DrawBlack;
        On_Main.RenderTiles += _Main_RenderTiles;
        On_Main.RenderTiles2 += _Main_RenderTiles2;
        On_Main.DrawTiles += _Main_DrawTiles;
        On_Main.RenderWalls += _Main_RenderWalls;
        On_Main.DrawWalls += _Main_DrawWalls;

        // Lighting engine hooks
        On_TileLightScanner.ExportTo += _TileLightScanner_ExportTo;
        On_LightingEngine.ProcessBlur += _LightingEngine_ProcessBlur;
        On_LightMap.Blur += _LightMap_Blur;

        // Force these methods to be recompiled
        // Otherwise our hooks for On_LightingEngine.ProcessBlur and On_LightMap.Blur may fail to be applied due to inlining
        IL_LightingEngine.ProcessBlur += _ => { };
        IL_LightingEngine.ProcessArea += _ => { };
    }

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
                    nameof(_useWhiteLightMap),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_useWhiteLightMap)
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
                    nameof(_useWhiteLightMap),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();

            var afterIfBlockLabel = cursor.DefineLabel();

            /*
            if (_useWhiteLightMap)
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

    private static void _TileLightScanner_GetTileLight(
        On_TileLightScanner.orig_GetTileLight orig,
        TileLightScanner self,
        int x,
        int y,
        out Vector3 outputColor
    )
    {
        if (SettingsSystem._useSkyLightLuma)
        {
            FancySkyLighting.SetSkyLightLuma(x, y, 0f);
        }

        orig(self, x, y, out outputColor);
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

        if (SettingsSystem._hiDef)
        {
            lightColor *= PostProcessing.HiDefBackgroundBrightnessMult;
        }

        if (SettingsSystem._useSkyLightLuma)
        {
            FancySkyLighting.SetSkyLightLuma(x, y, ColorUtils.Luma(lightColor));
        }
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
            brightness *= 2.5f; // Make lava brighter
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

    private void _Main_DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        PerformanceTracker.StopTiming("Delta Time");
        PerformanceTracker.StartTiming("Delta Time");
        PerformanceTracker.DisplayStatistics(false);

        SpriteBatchEffectLoader.Reset();

        ModContent.GetInstance<SettingsSystem>().SettingsUpdate();
        MainGraphics.ResetCaptureInfo();

        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            || !LightingConfig.Instance.DrawOverbright()
            || SettingsSystem.HdrEnhancedAlphaBlendingDisabled()
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

    private void _FilterManager_BeginCapture(
        On_FilterManager.orig_BeginCapture orig,
        FilterManager self,
        RenderTarget2D screenTarget1,
        Color clearColor
    )
    {
        orig(self, screenTarget1, clearColor);

        MainGraphics.BeginCapture();
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
        MainGraphics.EndCapture_Pre();

        if (!SettingsSystem.NeedsPostProcessing())
        {
            MainGraphics.EndCapture_Post();
            orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
            return;
        }

        var backgroundTarget =
            MainGraphics.InCameraMode && !MainGraphics.CameraModeCaptureBackground
                ? null
                : _backgroundTarget;

        if (CompatibilityConfig.Instance.DisableRenderingOptimizations)
        {
            MainGraphics.EndCapture_Post();
            _postProcessingInstance.ApplyPostProcessing(
                ref screenTarget1,
                ref screenTarget2,
                backgroundTarget,
                _smoothLightingInstance
            );
        }
        else
        {
            _postProcessingInstance.ApplyPostProcessing(
                ref MainGraphics.ScreenTarget,
                ref MainGraphics.ScreenTargetSwap,
                backgroundTarget,
                _smoothLightingInstance
            );

            screenTarget1 = MainGraphics.ScreenTarget;
            screenTarget2 = MainGraphics.ScreenTargetSwap;
            MainGraphics.EndCapture_Post();
        }

        orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
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
                tempMushroomInfluence,
                _postProcessingInstance
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

        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transform = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
        Main.spriteBatch.End();

        var sunMoonBrightness = Main.dayTime ? 2.3f : 1.8f;
        sunMoonBrightness /= PostProcessing.HiDefBackgroundBrightnessMult;

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            rasterizerState,
            _postProcessingInstance.GetBrightenPixelOnlyEffect(sunMoonBrightness).Effect,
            transform
        );
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

    private void _Main_DoLightTiles(On_Main.orig_DoLightTiles orig, Main self)
    {
        orig(self);

        var doOverbright =
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright();
        var doDepthOfField = PreferencesConfig.Instance.DepthOfField;

        if (
            MainGraphics.InCameraMode
            || !MainGraphics.DoingCapture
            || !(doOverbright || doDepthOfField)
        )
        {
            return;
        }

        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transform = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
        Main.spriteBatch.End();

        SeparateBackground(cameraMode: false);

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

    private void _Main_DrawUnderworldBackground(
        On_Main.orig_DrawUnderworldBackground orig,
        Main self,
        bool flat
    )
    {
        var doOverbright =
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright();
        var doDepthOfField = PreferencesConfig.Instance.DepthOfField;

        if (!MainGraphics.InCameraMode || !(doOverbright || doDepthOfField))
        {
            return;
        }

        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transform = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
        Main.spriteBatch.End();

        SeparateBackground(cameraMode: true);

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

    private void SeparateBackground(bool cameraMode)
    {
        var doOverbright =
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright();
        var doDepthOfField = PreferencesConfig.Instance.DepthOfField;
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var hdrCompatBlending = SettingsSystem.HdrEnhancedAlphaBlendingDisabled();

        ref var screenTarget = ref MainGraphics.ScreenTarget;

        if (doOverbright)
        {
            TextureUtils.MatchSizeAndFormat(ref _backgroundTarget, screenTarget);
        }

        if (!hiDef)
        {
            if (doDepthOfField)
            {
                _postProcessingInstance.Blur(
                    screenTarget,
                    doOverbright ? _backgroundTarget : screenTarget,
                    PreferencesConfig.Instance.DepthOfFieldRadius
                );
            }
            else
            {
                // doOverbright must be true here
                Blitter.BlitOrSwap(ref screenTarget, ref _backgroundTarget);
                MainGraphics.AssignScreenTargets();
            }

            if (doOverbright)
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            }

            return;
        }

        if (!hdrCompatBlending)
        {
            Blitter.BlitOrSwap(ref screenTarget, ref _backgroundTarget);
            MainGraphics.AssignScreenTargets();
            Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            return;
        }

        var brightness = PostProcessing.CalculateHiDefBackgroundBrightness();
        var effect = doDepthOfField
            ? cameraMode
                ? _postProcessingInstance.GetGammaEffect(
                    ColorUtils.GammaToLinear(brightness),
                    PostProcessing.ContentGamma()
                )
                : _postProcessingInstance.GetGammaNoAlphaEffect(
                    ColorUtils.GammaToLinear(brightness),
                    PostProcessing.ContentGamma()
                )
            : _postProcessingInstance.GetBrightenEffect(brightness);
        Blitter.Blit(screenTarget, _backgroundTarget, effect);

        if (doDepthOfField)
        {
            _postProcessingInstance.Blur(
                _backgroundTarget,
                _backgroundTarget,
                PreferencesConfig.Instance.DepthOfFieldRadius
            );

            effect = cameraMode
                ? _postProcessingInstance.GetGammaEffect(
                    1f,
                    1f / PostProcessing.ContentGamma()
                )
                : _postProcessingInstance.GetGammaNoAlphaEffect(
                    1f,
                    1f / PostProcessing.ContentGamma()
                );
            Blitter.Blit(_backgroundTarget, screenTarget, effect);
        }
        else
        {
            Blitter.BlitOrSwap(ref _backgroundTarget, ref screenTarget);
            MainGraphics.AssignScreenTargets();
        }

        _smoothLightingInstance.CalculateSmoothLighting(cameraMode);
        if (_smoothLightingInstance.CanDrawSmoothLighting)
        {
            _smoothLightingInstance.DrawSmoothLighting(
                screenTarget,
                null,
                background: false,
                disableNormalMaps: true,
                doScaling: true,
                overbrightPass: true
            );
        }
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
            !MainGraphics.DoingCapture
            || (solidLayer || intoRenderTargets)
            || _ambientOcclusionInstance._drawingTileEntities
            || !LightingConfig.Instance.SmoothLightingEnabled()
        )
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        var useGlowMasks =
            LightingConfig.Instance.UseTileEntitySmoothLighting
            && !DeveloperConfig.Instance.RenderOnlyLight;
        var (effect, usedTmpTarget) = _smoothLightingInstance.GetTileEntityEffect(
            ref MainGraphics.ScreenTarget,
            ref MainGraphics.ScreenTargetSwap
        );

        if (effect is null)
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        if (useGlowMasks)
        {
            TextureUtils.MatchSizeAndFormat(
                ref _tmpScreenTarget,
                MainGraphics.ScreenTarget
            );

            if (!usedTmpTarget)
            {
                Blitter.BlitOrSwap(
                    ref MainGraphics.ScreenTarget,
                    ref MainGraphics.ScreenTargetSwap
                );
                MainGraphics.AssignScreenTargets();
                usedTmpTarget = true;
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(_tmpScreenTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);

            UseBlackLightMap(true);
            _preventTileParticles = true;
            try
            {
                orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            }
            finally
            {
                _preventTileParticles = false;
                UseBlackLightMap(false);
            }
        }

        if (usedTmpTarget)
        {
            Blitter.Blit(MainGraphics.ScreenTargetSwap, MainGraphics.ScreenTarget);
        }

        var glowTarget = useGlowMasks ? _tmpScreenTarget : null;
        _smoothLightingInstance.ApplyTileEntityEffect(effect, glowTarget);
        orig(self, solidLayer, forRenderTargets, intoRenderTargets);
        SpriteBatchEffectLoader.Reset();
        MainGraphics.RestoreSavedTextures();
    }

    // Cave backgrounds
    private void _Main_RenderBackground(On_Main.orig_RenderBackground orig, Main self)
    {
        if (Main.drawToScreen || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        if (DeveloperConfig.Instance.RenderOnlyLight)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.backgroundTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.backWaterTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting();

        if (!_smoothLightingInstance.CanDrawSmoothLighting)
        {
            orig(self);
            return;
        }

        UseWhiteLightMap(true);
        try
        {
            orig(self);
        }
        finally
        {
            UseWhiteLightMap(false);
        }
    }

    private void _Main_DrawBackground(On_Main.orig_DrawBackground orig, Main self)
    {
        if (
            (!MainGraphics.InCameraMode && Main.drawToScreen)
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || DeveloperConfig.Instance.RenderOnlyLight
        )
        {
            orig(self);
            return;
        }

        if (MainGraphics.InCameraMode)
        {
            Main.tileBatch.End();
            Main.spriteBatch.End();
            DoSmoothLightingBackgroundCameraMode(() => orig(self));
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
        }
        else
        {
            DoSmoothLightingBackground(() => orig(self), Main.instance.backgroundTarget);
        }
    }

    private void _Main_DrawWaters(
        On_Main.orig_DrawWaters orig,
        Main self,
        bool isBackground
    )
    {
        if (
            !isBackground
            || MainGraphics.InCameraMode
            || Main.drawToScreen
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || DeveloperConfig.Instance.RenderOnlyLight
        )
        {
            orig(self, isBackground);
            return;
        }

        DoSmoothLightingBackground(
            () => orig(self, isBackground),
            Main.instance.backWaterTarget
        );
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
            !MainGraphics.InCameraMode
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || (bg && DeveloperConfig.Instance.RenderOnlyLight)
        )
        {
            orig(self, bg, Style, Alpha, drawSinglePassLiquids);
            return;
        }

        Main.tileBatch.End();
        Main.spriteBatch.End();

        if (bg)
        {
            DoSmoothLightingBackgroundCameraMode(() =>
                orig(self, bg, Style, Alpha, drawSinglePassLiquids)
            );
        }
        else
        {
            DoSmoothLightingCameraMode(
                () => orig(self, bg, Style, Alpha, drawSinglePassLiquids),
                background: false,
                disableNormalMaps: true
            );
        }

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void _Main_RenderWater(On_Main.orig_RenderWater orig, Main self)
    {
        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            || (
                SpiritReforgedCompatibility._disableCustomLiquidRendering
                && !DeveloperConfig.Instance.RenderOnlyLight
            )
        )
        {
            orig(self);
            return;
        }

        DoSmoothLighting(
            () => orig(self),
            ref Main.waterTarget,
            background: false,
            disableNormalMaps: true
        );
    }

    private static void _Main_RenderBlack(On_Main.orig_RenderBlack orig, Main self)
    {
        if (_suppressRenderBlack)
        {
            return;
        }

        orig(self);
    }

    private void _Main_DrawBlack(On_Main.orig_DrawBlack orig, Main self, bool force)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self, force);
            return;
        }

        if (DeveloperConfig.Instance.RenderOnlyLight)
        {
            return;
        }

        var prevUseWhiteLightMap = _useWhiteLightMap;
        UseWhiteLightMap(false);
        try
        {
            orig(self, force);
        }
        finally
        {
            UseWhiteLightMap(prevUseWhiteLightMap);
        }
    }

    private void _Main_RenderTiles(On_Main.orig_RenderTiles orig, Main self)
    {
        if (Main.drawToScreen || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        DoSmoothLighting(
            () => orig(self),
            ref Main.instance.tileTarget,
            background: false,
            disableNormalMaps: false
        );
    }

    private void _Main_RenderTiles2(On_Main.orig_RenderTiles2 orig, Main self)
    {
        if (Main.drawToScreen || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        DoSmoothLighting(
            () => orig(self),
            ref Main.instance.tile2Target,
            background: false,
            disableNormalMaps: !LightingConfig.Instance.SimulateNonSolidNormals
        );
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
        if (
            !MainGraphics.InCameraMode || !LightingConfig.Instance.SmoothLightingEnabled()
        )
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

        Main.tileBatch.End();
        Main.spriteBatch.End();

        DoSmoothLightingCameraMode(
            () =>
                orig(
                    self,
                    solidLayer,
                    forRenderTargets,
                    intoRenderTargets,
                    waterStyleOverride
                ),
            background: false,
            disableNormalMaps: !solidLayer
                && !LightingConfig.Instance.SimulateNonSolidNormals
        );

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void _Main_RenderWalls(On_Main.orig_RenderWalls orig, Main self)
    {
        var smoothLighting = LightingConfig.Instance.SmoothLightingEnabled();
        var ambientOcclusion = LightingConfig.Instance.AmbientOcclusionEnabled();

        if (Main.drawToScreen || !(smoothLighting || ambientOcclusion))
        {
            orig(self);
            return;
        }

        var ambientOcclusionTarget = (RenderTarget2D)null;
        if (ambientOcclusion)
        {
            ambientOcclusionTarget = _ambientOcclusionInstance.DrawAmbientOcclusion(
                Main.instance.wallTarget,
                Main.instance.tileTarget,
                LightingConfig.Instance.DoNonSolidAmbientOcclusion
                    ? Main.instance.tile2Target
                    : null,
                tileEntityShadows: LightingConfig.Instance.DoTileEntityAmbientOcclusion,
                cameraMode: false
            );

            if (!smoothLighting)
            {
                _activeAmbientOcclusionTarget = ambientOcclusionTarget;
                try
                {
                    orig(self);
                }
                finally
                {
                    _activeAmbientOcclusionTarget = null;
                }

                return;
            }
        }

        // smoothLighting is always true here
        DoSmoothLighting(
            () => orig(self),
            ref Main.instance.wallTarget,
            background: true,
            disableNormalMaps: false,
            ambientOcclusion: ambientOcclusionTarget
        );
    }

    private void _Main_DrawWalls(On_Main.orig_DrawWalls orig, Main self)
    {
        var smoothLighting = LightingConfig.Instance.SmoothLightingEnabled();
        var ambientOcclusion = LightingConfig.Instance.AmbientOcclusionEnabled();

        if (!MainGraphics.InCameraMode || !(smoothLighting || ambientOcclusion))
        {
            orig(self);

            if (_activeAmbientOcclusionTarget is not null)
            {
                Main.tileBatch.End();
                Main.spriteBatch.End();
                Blitter.Blit(
                    _activeAmbientOcclusionTarget,
                    null,
                    blendState: CustomBlendStates.MultiplyColorByAlpha,
                    setTarget: false
                );
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
            }

            return;
        }

        Main.tileBatch.End();
        Main.spriteBatch.End();

        var ambientOcclusionTarget = (RenderTarget2D)null;
        if (ambientOcclusion)
        {
            TextureUtils.MatchSizeAndFormat(ref _tmpTarget1, MainGraphics.ScreenTarget);
            TextureUtils.MatchSizeAndFormat(ref _tmpTarget2, MainGraphics.ScreenTarget);

            ambientOcclusionTarget = _ambientOcclusionInstance.DrawAmbientOcclusion(
                Main.instance.wallTarget,
                Main.instance.tileTarget,
                LightingConfig.Instance.DoNonSolidAmbientOcclusion
                    ? Main.instance.tile2Target
                    : null,
                tileEntityShadows: LightingConfig.Instance.DoTileEntityAmbientOcclusion,
                cameraMode: false
            );

            if (!smoothLighting)
            {
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                orig(self);
                Main.tileBatch.End();
                Main.spriteBatch.End();

                Blitter.Blit(
                    ambientOcclusionTarget,
                    null,
                    blendState: CustomBlendStates.MultiplyColorByAlpha,
                    setTarget: false
                );

                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();

                return;
            }
        }

        // smoothLighting is always true here
        DoSmoothLightingCameraMode(
            () => orig(self),
            background: true,
            disableNormalMaps: false,
            ambientOcclusion: ambientOcclusionTarget
        );

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void DoSmoothLighting(
        Action drawAction,
        ref RenderTarget2D tileTarget,
        bool background,
        bool disableNormalMaps,
        RenderTarget2D ambientOcclusion = null
    )
    {
        var useGlowMasks = !DeveloperConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;
        var optimize = !CompatibilityConfig.Instance.DisableRenderingOptimizations;

        _smoothLightingInstance.CalculateSmoothLighting();
        if (!_smoothLightingInstance.CanDrawSmoothLighting)
        {
            drawAction();
            return;
        }

        if (useGlowMasks)
        {
            TextureUtils.MatchSizeAndFormat(ref _tmpTarget1, tileTarget);
            TextureUtils.MatchSizeAndFormat(ref _tmpTarget2, tileTarget);

            if (optimize)
            {
                (tileTarget, _tmpTarget2) = (_tmpTarget2, tileTarget);
            }

            UseBlackLightMap(true);
            _preventTileParticles = true;
            _suppressRenderBlack = true;
            try
            {
                drawAction();
            }
            finally
            {
                _suppressRenderBlack = false;
                _preventTileParticles = false;
                UseBlackLightMap(false);
            }

            if (optimize)
            {
                (tileTarget, _tmpTarget2) = (_tmpTarget2, tileTarget);
            }
            else
            {
                Blitter.Blit(tileTarget, _tmpTarget2);
            }

            if (enhancedGlowMasks)
            {
                TextureUtils.MatchSizeAndFormat(ref _tmpTarget3, tileTarget);

                if (optimize)
                {
                    (tileTarget, _tmpTarget3) = (_tmpTarget3, tileTarget);
                }

                drawAction();

                if (optimize)
                {
                    (tileTarget, _tmpTarget3) = (_tmpTarget3, tileTarget);
                }
                else
                {
                    Blitter.Blit(tileTarget, _tmpTarget3);
                }
            }
        }

        if (optimize)
        {
            (tileTarget, _tmpTarget1) = (_tmpTarget1, tileTarget);
        }

        UseWhiteLightMap(_smoothLightingInstance.CanDrawSmoothLighting);
        _preventTileParticles = enhancedGlowMasks;
        _makePartialLiquidTranslucent = LightingConfig.Instance.SimulateNormalMaps;
        _suppressRenderBlack = enhancedGlowMasks;
        try
        {
            drawAction();
        }
        finally
        {
            _suppressRenderBlack = false;
            _makePartialLiquidTranslucent = false;
            _preventTileParticles = false;
            UseWhiteLightMap(false);
        }

        if (optimize)
        {
            (tileTarget, _tmpTarget1) = (_tmpTarget1, tileTarget);
        }
        else
        {
            Blitter.Blit(tileTarget, _tmpTarget1);
        }

        _smoothLightingInstance.DrawSmoothLighting(
            _tmpTarget1,
            tileTarget,
            background,
            disableNormalMaps,
            doScaling: false,
            glow: useGlowMasks ? _tmpTarget2 : null,
            lightedGlow: enhancedGlowMasks ? _tmpTarget3 : null,
            ambientOcclusion: ambientOcclusion
        );
        Main.graphics.GraphicsDevice.SetRenderTarget(null);
    }

    private void DoSmoothLightingCameraMode(
        Action drawAction,
        bool background,
        bool disableNormalMaps,
        RenderTarget2D ambientOcclusion = null
    )
    {
        ref var screenTarget = ref MainGraphics.ScreenTarget;
        var useGlowMasks = !DeveloperConfig.Instance.RenderOnlyLight;
        var enhancedGlowMasks =
            useGlowMasks && LightingConfig.Instance.UseEnhancedGlowMaskSupport;

        _smoothLightingInstance.CalculateSmoothLighting(cameraMode: true);
        if (!_smoothLightingInstance.CanDrawSmoothLighting)
        {
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            drawAction();
            Main.tileBatch.End();
            Main.spriteBatch.End();
        }

        if (useGlowMasks)
        {
            TextureUtils.MatchSizeAndFormat(ref _tmpTarget1, screenTarget);
            TextureUtils.MatchSizeAndFormat(ref _tmpTarget2, screenTarget);

            UseBlackLightMap(true);
            _preventTileParticles = true;
            try
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget2);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                drawAction();
                Main.tileBatch.End();
                Main.spriteBatch.End();
            }
            finally
            {
                _preventTileParticles = false;
                UseBlackLightMap(false);
            }

            if (enhancedGlowMasks)
            {
                TextureUtils.MatchSizeAndFormat(ref _tmpTarget3, screenTarget);

                Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget3);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                drawAction();
                Main.tileBatch.End();
                Main.spriteBatch.End();
            }
        }

        UseWhiteLightMap(true);
        _preventTileParticles = enhancedGlowMasks;
        _makePartialLiquidTranslucent = LightingConfig.Instance.SimulateNormalMaps;
        try
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget1);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            drawAction();
            Main.tileBatch.End();
            Main.spriteBatch.End();
        }
        finally
        {
            _makePartialLiquidTranslucent = false;
            _preventTileParticles = false;
            UseWhiteLightMap(false);
        }

        Blitter.BlitOrSwap(
            ref MainGraphics.ScreenTarget,
            ref MainGraphics.ScreenTargetSwap
        );
        MainGraphics.AssignScreenTargets();
        Blitter.Blit(MainGraphics.ScreenTargetSwap, screenTarget);
        _smoothLightingInstance.DrawSmoothLighting(
            _tmpTarget1,
            null,
            background,
            disableNormalMaps,
            doScaling: true,
            glow: useGlowMasks ? _tmpTarget2 : null,
            lightedGlow: enhancedGlowMasks ? _tmpTarget3 : null,
            ambientOcclusion: ambientOcclusion
        );
    }

    private void DoSmoothLightingBackground(
        Action drawAction,
        RenderTarget2D backgroundTarget
    )
    {
        drawAction();
        Main.tileBatch.End();
        Main.spriteBatch.End();

        _smoothLightingInstance.CalculateSmoothLighting();

        if (_smoothLightingInstance.CanDrawSmoothLighting)
        {
            _smoothLightingInstance.DrawSmoothLighting(
                backgroundTarget,
                null,
                background: true,
                disableNormalMaps: true,
                doScaling: false,
                lightColorPass: true
            );
        }

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void DoSmoothLightingBackgroundCameraMode(Action drawAction)
    {
        _smoothLightingInstance.CalculateSmoothLighting(cameraMode: true);
        if (!_smoothLightingInstance.CanDrawSmoothLighting)
        {
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            drawAction();
            Main.tileBatch.End();
            Main.spriteBatch.End();
            return;
        }

        TextureUtils.MatchSizeAndFormat(ref _tmpTarget1, MainGraphics.ScreenTarget);
        Main.graphics.GraphicsDevice.SetRenderTarget(_tmpTarget1);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        UseWhiteLightMap(true);
        try
        {
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            drawAction();
            Main.tileBatch.End();
            Main.spriteBatch.End();
        }
        finally
        {
            UseWhiteLightMap(false);
        }

        _smoothLightingInstance.DrawSmoothLighting(
            _tmpTarget1,
            null,
            background: true,
            disableNormalMaps: true,
            doScaling: true,
            lightColorPass: true
        );

        Blitter.BlitOrSwap(
            ref MainGraphics.ScreenTarget,
            ref MainGraphics.ScreenTargetSwap
        );
        MainGraphics.AssignScreenTargets();
        _postProcessingInstance.BlitTwo(
            _tmpTarget1,
            MainGraphics.ScreenTargetSwap,
            MainGraphics.ScreenTarget
        );
    }

    private void _TileLightScanner_ExportTo(
        On_TileLightScanner.orig_ExportTo orig,
        TileLightScanner self,
        Rectangle area,
        LightMap outputMap,
        TileLightScannerOptions options
    )
    {
        if (LightingConfig.Instance.UseSkyLightLuma())
        {
            FancySkyLighting.SetLightMapArea(area);
        }

        orig(self, area, outputMap, options);
    }

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

        if (!CompatibilityConfig.Instance.DisableFrameTimingOptimizations)
        {
            Main.renderCount = 2;
        }
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

            if (
                LightingConfig.Instance.HiDefFeaturesEnabled()
                && !CompatibilityConfig.Instance.DisableHdrLightingSync
            )
            {
                SyncHdrLighting();
            }
        }
    }

    private void SyncHdrLighting()
    {
        if (
            MainGraphics.InCameraMode
            || !MainGraphics.DoingCapture
            || Main.instance.tileTarget is not { Width: > 0, Height: > 0 }
        )
        {
            return;
        }

        Main.spriteBatch.End();

        _smoothLightingInstance.CalculateSmoothLighting();
        if (!_smoothLightingInstance.ReadyForHdrSync)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                Main.Transform
            );
            return;
        }

        TextureUtils.MatchSizeAndFormat(ref _tmpTarget1, Main.instance.tileTarget);

        MainGraphics.ResetSavedTextures();
        _smoothLightingInstance.BindHdrSyncTextures();

        _smoothLightingInstance.DoHdrSync(
            ref Main.waterTarget,
            ref _tmpTarget1,
            Main.sceneWaterPos
        );
        _smoothLightingInstance.DoHdrSync(
            ref Main.instance.backgroundTarget,
            ref _tmpTarget1,
            Main.sceneBackgroundPos
        );
        _smoothLightingInstance.DoHdrSync(
            ref Main.instance.backWaterTarget,
            ref _tmpTarget1,
            Main.sceneBackgroundPos
        );
        _smoothLightingInstance.DoHdrSync(
            ref Main.instance.tileTarget,
            ref _tmpTarget1,
            Main.sceneTilePos
        );
        _smoothLightingInstance.DoHdrSync(
            ref Main.instance.tile2Target,
            ref _tmpTarget1,
            Main.sceneTile2Pos
        );
        _smoothLightingInstance.DoHdrSync(
            ref Main.instance.wallTarget,
            ref _tmpTarget1,
            Main.sceneWallPos
        );

        MainGraphics.RestoreSavedTextures();

        Blitter.BlitOrSwap(
            ref MainGraphics.ScreenTarget,
            ref MainGraphics.ScreenTargetSwap
        );
        MainGraphics.AssignScreenTargets();
        Blitter.Blit(MainGraphics.ScreenTargetSwap, MainGraphics.ScreenTarget);

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            Main.DefaultSamplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.Transform
        );
    }
}
