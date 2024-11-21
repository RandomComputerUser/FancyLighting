using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Utils;
using Terraria;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class FancyLightingModSystem : ModSystem
{
    internal static readonly ParallelOptions _parallelOptions =
        new() { MaxDegreeOfParallelism = DefaultOptions.ThreadCount };

    internal static bool _hiDef;

    private bool _doWarning = false;

    internal void OnConfigChange()
    {
        _doWarning = true;
        SettingsUpdate();
    }

    public override void OnWorldLoad()
    {
        _doWarning = true;
    }

    public override void PostUpdateEverything()
    {
        SettingsUpdate();

        if (!_doWarning)
        {
            return;
        }

        var didWarning = SettingsWarnings.DoWarnings();
        if (didWarning)
        {
            _doWarning = false;
        }
    }

    internal static void SettingsUpdate()
    {
        _parallelOptions.MaxDegreeOfParallelism =
            PreferencesConfig.Instance?.ThreadCount ?? DefaultOptions.ThreadCount;
        _hiDef = LightingConfig.Instance?.HiDefFeaturesEnabled() ?? false;
        PostProcessing.CalculateHiDefSurfaceBrightness();
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
}
