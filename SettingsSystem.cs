using FancyLighting.Config;
using FancyLighting.Config.Enums;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;

namespace FancyLighting;

internal sealed class SettingsSystem : ModSystem
{
    internal static ParallelOptions _parallelOptions = new()
    {
        MaxDegreeOfParallelism = DefaultOptions.ThreadCount,
    };

    internal static bool _hiDef;
    internal static bool _lightOnly;
    internal static bool _useSkyLightLuma;
    internal static bool _useFancyClouds;

    private bool _prevNeedsPostProcessing;
    private bool _prevHdrDisabled;

    public override void Unload()
    {
        _parallelOptions = null;
        Filters.Scene.OnPostDraw -= DoNothing;
    }

    internal void OnConfigChange()
    {
        SettingsUpdate();
        ModContent.GetInstance<FancyLightingMod>().OnConfigChange();
    }

    internal void SettingsUpdate()
    {
        if (LightingConfig.Instance.NeedsColorLightMode())
        {
            if (Lighting.Mode is not LightMode.Color)
            {
                Lighting.Mode = LightMode.Color;
            }
        }

        _parallelOptions.MaxDegreeOfParallelism = Math.Max(
            PreferencesConfig.Instance.ThreadCount,
            1
        );
        _hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        _lightOnly =
            LightingConfig.Instance.SmoothLightingEnabled()
            && DeveloperConfig.Instance.RenderOnlyLight;
        _useSkyLightLuma = LightingConfig.Instance.UseSkyLightLuma();
        _useFancyClouds = LightingConfig.Instance.FancySkyLightingEnabled();
        ColorUtils._gamma = PostProcessing.ContentGamma();
        ColorUtils._reciprocalGamma = 1f / ColorUtils._gamma;
        PerformanceTracker.Enabled = DeveloperConfig.Instance.MonitorPerformance;

        var needsPostProcessing = NeedsPostProcessing(true) || NeedsCapture();
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
            && LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.LightMapRenderMode
                is RenderMode.BicubicOverbright
                    or RenderMode.EnhancedHdr
        )
        {
            ModContent.GetInstance<FancyLightingMod>().OnConfigChange();
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

    private static bool IsBossFightOccurring() =>
        BigProgressBarSystemAccessors._currentBar(Main.BigBossProgressBar) is not null;

    private static bool IsEventOccurring() => Main.invasionProgressNearInvasion;

    internal static bool NeedsPostProcessing(bool force = false) =>
        (
            (force || !MainGraphics.InCameraMode)
            && (
                PreferencesConfig.Instance.UseCustomGamma()
                || PreferencesConfig.Instance.UseSrgb
            )
        )
        || (
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright()
        );

    private static bool NeedsCapture() =>
        (
            LightingConfig.Instance.SmoothLightingEnabled()
            && (
                (
                    LightingConfig.Instance.SimulateNormalMaps
                    && LightingConfig.Instance.SimulateTileEntityNormals
                ) || LightingConfig.Instance.UseTileEntitySmoothLighting
            )
        ) || PreferencesConfig.Instance.DepthOfField;

    internal static bool HdrEnhancedAlphaBlendingDisabled() =>
        CompatibilityConfig.Instance.DisableHdrEnhancedAlphaBlending
        && LightingConfig.Instance.HiDefFeaturesEnabled();

    internal static bool HdrDisabled() =>
        CompatibilityConfig.Instance.DisableHdrDuringBossFights
        && (IsBossFightOccurring() || IsEventOccurring());

    private static void DoNothing() { }
}
