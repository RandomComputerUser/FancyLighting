using FancyLighting.Config;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class FancyLightingModSystem : ModSystem
{
    private bool _doWarning = false;

    public override void OnWorldLoad()
    {
        _doWarning = true;
    }

    public override void PostUpdateEverything()
    {
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
}
