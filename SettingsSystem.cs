using FancyLighting.Config.Enums;
using FancyLighting.Utils.Accessors;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;

namespace FancyLighting;

internal sealed class SettingsSystem : ModSystem
{
    internal static ParallelOptions _parallelOptions =
        new() { MaxDegreeOfParallelism = DefaultOptions.ThreadCount };

    internal static bool _hiDef;

    private bool _prevNeedsPostProcessing = false;
    private bool _prevHdrDisabled = false;

    public override void Unload()
    {
        _parallelOptions = null;
        Filters.Scene.OnPostDraw -= DoNothing;
    }

    internal void OnConfigChange()
    {
        SettingsUpdate();
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();
    }

    internal void SettingsUpdate()
    {
        if (LightingConfig.Instance?.NeedsColorLightMode() is true)
        {
            if (Lighting.Mode is not LightMode.Color)
            {
                Lighting.Mode = LightMode.Color;
            }
        }

        _parallelOptions.MaxDegreeOfParallelism = Math.Max(
            PreferencesConfig.Instance?.ThreadCount ?? DefaultOptions.ThreadCount,
            1
        );
        _hiDef = LightingConfig.Instance?.HiDefFeaturesEnabled() is true;
        ColorUtils._gamma = PostProcessing.ContentGamma();
        ColorUtils._reciprocalGamma = 1f / ColorUtils._gamma;
        PerformanceTracker.Enabled =
            PreferencesConfig.Instance?.MonitorPerformance is true;

        var needsPostProcessing =
            NeedsPostProcessing(true)
            || (
                LightingConfig.Instance?.SmoothLightingEnabled() is true
                && LightingConfig.Instance?.SimulateNormalMaps is true
                && LightingConfig.Instance?.SimulateTileEntityNormals is true
            );
        if (needsPostProcessing && !_prevNeedsPostProcessing)
        {
            Filters.Scene.OnPostDraw += DoNothing;
            _prevNeedsPostProcessing = true;
        }
        else if (!needsPostProcessing && _prevNeedsPostProcessing)
        {
            Filters.Scene.OnPostDraw -= DoNothing;
            _prevNeedsPostProcessing = false;
        }

        EnsureRenderTargets();

        var hdrDisabled = HdrDisabled();
        if (
            hdrDisabled != _prevHdrDisabled
            && LightingConfig.Instance?.SmoothLightingEnabled() is true
            && LightingConfig.Instance?.LightMapRenderMode
                is RenderMode.BicubicOverbright
                    or RenderMode.EnhancedHdr
        )
        {
            ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();
        }
        _prevHdrDisabled = hdrDisabled;
    }

    internal static void EnsureRenderTargets(bool reset = false)
    {
        var format = reset ? SurfaceFormat.Color : TextureUtils.ScreenFormat;

        TextureUtils.EnsureFormat(ref Main.waterTarget, format);
        TextureUtils.EnsureFormat(ref Main.instance.backWaterTarget, format);
        TextureUtils.EnsureFormat(ref Main.instance.blackTarget, format);
        TextureUtils.EnsureFormat(ref Main.instance.tileTarget, format);
        TextureUtils.EnsureFormat(ref Main.instance.tile2Target, format);
        TextureUtils.EnsureFormat(ref Main.instance.wallTarget, format);
        TextureUtils.EnsureFormat(ref Main.instance.backgroundTarget, format);
        TextureUtils.EnsureFormat(ref Main.screenTarget, format);
        TextureUtils.EnsureFormat(ref Main.screenTargetSwap, format);
    }

    internal static bool PostProcessingAllowed() =>
        !(Main.gameMenu || Main.mapFullscreen || Main.drawToScreen);

    private static bool IsBossFightOccurring() =>
        BigProgressBarSystemAccessors._currentBar(Main.BigBossProgressBar) is not null;

    private static bool IsEventOccurring() => Main.invasionProgressNearInvasion;

    internal static bool NeedsPostProcessing(bool force = false) =>
        PreferencesConfig.Instance is not null
        && LightingConfig.Instance is not null
        && (
            (
                (force || !FancyLightingMod._isGameInCameraMode)
                && (
                    PreferencesConfig.Instance.UseCustomGamma()
                    || PreferencesConfig.Instance.UseSrgb
                )
            )
            || (
                LightingConfig.Instance.SmoothLightingEnabled()
                && LightingConfig.Instance.DrawOverbright()
            )
        );

    internal static bool HdrCompatibilityEnabled() =>
        PreferencesConfig.Instance.UseHdrCompatibilityFixes
        && LightingConfig.Instance.HiDefFeaturesEnabled();

    internal static bool HdrDisabled() =>
        PreferencesConfig.Instance?.DisableHdrDuringBossFights is true
        && (IsBossFightOccurring() || IsEventOccurring());

    private static void DoNothing() { }
}
