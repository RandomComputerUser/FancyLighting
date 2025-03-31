using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Config.Enums;
using FancyLighting.Utils;
using FancyLighting.Utils.Accessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class SettingsSystem : ModSystem
{
    internal static readonly ParallelOptions _parallelOptions =
        new() { MaxDegreeOfParallelism = DefaultOptions.ThreadCount };

    internal static bool _hiDef;

    private bool _prevNeedsPostProcessing = false;
    private bool _prevHdrDisabled = false;

    public override void Unload()
    {
        Filters.Scene.OnPostDraw -= DoNothing;
    }

    internal void OnConfigChange()
    {
        SettingsUpdate();
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();
    }

    internal void SettingsUpdate()
    {
        if (
            PreferencesConfig.Instance?.NeedsColorLightMode() is true
            || LightingConfig.Instance?.NeedsColorLightMode() is true
        )
        {
            if (Lighting.Mode is not LightMode.Color)
            {
                Lighting.Mode = LightMode.Color;
            }
        }

        _parallelOptions.MaxDegreeOfParallelism =
            PreferencesConfig.Instance?.ThreadCount ?? DefaultOptions.ThreadCount;
        _hiDef = LightingConfig.Instance?.HiDefFeaturesEnabled() is true;
        ColorUtils._gamma = PreferencesConfig.Instance?.GammaExponent() ?? 2.2f;
        ColorUtils._reciprocalGamma = 1f / ColorUtils._gamma;
        PostProcessing.RecalculateHiDefSurfaceBrightness();

        var needsPostProcessing = NeedsPostProcessing();
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

    internal static bool IsBossFightOccurring() =>
        BigProgressBarSystemAccessors._currentBar(Main.BigBossProgressBar) is not null;

    internal static bool NeedsPostProcessing() =>
        PreferencesConfig.Instance is not null
        && LightingConfig.Instance is not null
        && (
            PreferencesConfig.Instance.UseCustomGamma()
            || PreferencesConfig.Instance.UseSrgb
            || (
                LightingConfig.Instance.SmoothLightingEnabled()
                && LightingConfig.Instance.DrawOverbright()
            )
        );

    internal static bool HdrCompatibilityEnabled() =>
        PreferencesConfig.Instance.UseHdrCompatibilityFixes
        && LightingConfig.Instance.HiDefFeaturesEnabled();

    internal static bool HdrDisabled() =>
        IsBossFightOccurring()
        && PreferencesConfig.Instance?.DisableHdrDuringBossFights is true;

    private static void DoNothing() { }
}
