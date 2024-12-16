using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Utils;
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
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();
        Main.renderNow = true;
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
