using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Utils;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class FancyLightingModSystem : ModSystem
{
    internal static readonly ParallelOptions _parallelOptions =
        new() { MaxDegreeOfParallelism = DefaultOptions.ThreadCount };

    internal static bool _hiDef;

    private bool _needsPostProcessing = false;

    public override void Unload()
    {
        Filters.Scene.OnPostDraw -= DoNothing;
    }

    public override void PostUpdateEverything()
    {
        SettingsUpdate();
    }

    internal void OnConfigChange()
    {
        SettingsUpdate();
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
        _hiDef = LightingConfig.Instance?.HiDefFeaturesEnabled() ?? false;
        ColorUtils._gamma = PreferencesConfig.Instance?.GammaExponent() ?? 2.2f;
        ColorUtils._reciprocalGamma = 1f / ColorUtils._gamma;
        PostProcessing.CalculateHiDefSurfaceBrightness();

        var needsPostProcessing = NeedsPostProcessing();
        if (needsPostProcessing && !_needsPostProcessing)
        {
            Filters.Scene.OnPostDraw += DoNothing;
            _needsPostProcessing = true;
        }
        else if (!needsPostProcessing && _needsPostProcessing)
        {
            Filters.Scene.OnPostDraw -= DoNothing;
            _needsPostProcessing = false;
        }

        EnsureRenderTargets();
    }

    internal static void EnsureRenderTargets(bool reset = false)
    {
        TextureUtils.EnsureFormat(ref Main.waterTarget, reset);
        TextureUtils.EnsureFormat(ref Main.instance.backWaterTarget, reset);
        TextureUtils.EnsureFormat(ref Main.instance.blackTarget, reset);
        TextureUtils.EnsureFormat(ref Main.instance.tileTarget, reset);
        TextureUtils.EnsureFormat(ref Main.instance.tile2Target, reset);
        TextureUtils.EnsureFormat(ref Main.instance.wallTarget, reset);
        TextureUtils.EnsureFormat(ref Main.instance.backgroundTarget, reset);
        TextureUtils.EnsureFormat(ref Main.screenTarget, reset);
        TextureUtils.EnsureFormat(ref Main.screenTargetSwap, reset);
    }

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
            || LightingConfig.Instance.HiDefFeaturesEnabled()
        );

    private static void DoNothing() { }
}
