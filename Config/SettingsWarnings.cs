using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace FancyLighting.Config;

internal static class SettingsWarnings
{
    public static bool DoWarnings()
    {
        var didWarning = false;

        try
        {
            if (!Lighting.UsingNewLighting)
            {
                if (
                    PreferencesConfig.Instance.UseCustomSkyColors
                    || LightingConfig.Instance.UseSmoothLighting
                    || LightingConfig.Instance.UseAmbientOcclusion
                    || LightingConfig.Instance.UseFancyLightingEngine
                )
                {
                    Main.NewText(
                        "[Fancy Lighting] Some currently enabled settings require Lighting in the video settings to be set to Color.",
                        Color.Yellow
                    );
                    didWarning = true;
                }
            }

            if (Main.WaveQuality is not (1 or 2 or 3))
            {
                if (
                    PreferencesConfig.Instance.UseCustomGamma()
                    || PreferencesConfig.Instance.UseSrgb
                    || (
                        LightingConfig.Instance.UseSmoothLighting
                        && LightingConfig.Instance.DrawOverbright()
                    )
                )
                {
                    Main.NewText(
                        "[Fancy Lighting] Some currently enabled settings require Waves Quality in the video settings to not be set to Off.",
                        Color.Yellow
                    );
                    didWarning = true;
                }
            }
        }
        catch (Exception ex)
        {
            return didWarning;
        }

        return didWarning;
    }
}
