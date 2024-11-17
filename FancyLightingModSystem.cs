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

    private bool _doWarning = false;

    internal void OnConfigChange()
    {
        _doWarning = true;
    }

    public override void OnWorldLoad()
    {
        _doWarning = true;
    }

    public override void PostUpdateEverything()
    {
        _parallelOptions.MaxDegreeOfParallelism =
            PreferencesConfig.Instance?.ThreadCount ?? DefaultOptions.ThreadCount;
        PostProcessing.CalculateHiDefSurfaceBrightness();
        EnsureRenderTargets();

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

    internal static void EnsureRenderTargets()
    {
        Main.graphics.GraphicsDevice.PresentationParameters.BackBufferFormat =
            TextureUtils.TextureSurfaceFormat;
        TextureUtils.EnsureFormat(ref Main.waterTarget);
        TextureUtils.EnsureFormat(ref Main.instance.backWaterTarget);
        TextureUtils.EnsureFormat(ref Main.instance.blackTarget);
        TextureUtils.EnsureFormat(ref Main.instance.tileTarget);
        TextureUtils.EnsureFormat(ref Main.instance.tile2Target);
        TextureUtils.EnsureFormat(ref Main.instance.wallTarget);
        TextureUtils.EnsureFormat(ref Main.instance.backgroundTarget);
        TextureUtils.EnsureFormat(ref Main.screenTarget);
        TextureUtils.EnsureFormat(ref Main.screenTargetSwap);
    }
}
