using System.Threading.Tasks;
using FancyLighting.Config;
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
