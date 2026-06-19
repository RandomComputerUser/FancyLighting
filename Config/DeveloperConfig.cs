using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class DeveloperConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static DeveloperConfig Instance;

    public override void OnChanged()
    {
        ModContent.GetInstance<SettingsSystem>()?.OnConfigChange();
    }

    [Header("General")]
    [DefaultValue(DefaultOptions.MonitorPerformance)]
    public bool MonitorPerformance { get; set; }

    [Header("SmoothLighting")]
    [DefaultValue(DefaultOptions.RenderOnlyLight)]
    public bool RenderOnlyLight { get; set; }

    [Header("FancySky")]
    [DefaultValue(DefaultOptions.ShowFancySkyColorGradients)]
    public bool ShowFancySkyColorGradients { get; set; }
}
