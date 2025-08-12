using System.Reflection;
using FancyLighting.ColorProfiles;
using FancyLighting.ColorProfiles.SkyColor;
using FancyLighting.Config.Enums;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace FancyLighting;

public static class FancySkyColors
{
    private static ILHook _ilHook_SetBackColor;

    private static Texture2D _profilesTexture;

    public static Dictionary<SkyColorPreset, ISimpleColorProfile> Preset
    {
        get;
        private set;
    }

    internal static void Load()
    {
        Preset = new()
        {
            [SkyColorPreset.Preset1] = new SkyLightColors1(),
            [SkyColorPreset.Preset2] = new SkyLightColors2(),
            [SkyColorPreset.Preset3] = new SkyLightColors3(),
            [SkyColorPreset.Preset4] = new SkyLightColors4(),
            [SkyColorPreset.Preset5] = new SkyLightColors5(),
        };

        var detourMethod = typeof(Main).GetMethod(
            "SetBackColor",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (detourMethod is not null)
        {
            try
            {
                _ilHook_SetBackColor = new(detourMethod, IL_Main_SetBackColor, true);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.SetBackColor += _Main_SetBackColor;
    }

    internal static void Unload()
    {
        _ilHook_SetBackColor?.Dispose();

        _profilesTexture?.Dispose();
        _profilesTexture = null;
    }

    private static void _Main_SetBackColor(
        On_Main.orig_SetBackColor orig,
        Main.InfoToSetBackColor info,
        out Color sunColor,
        out Color moonColor
    )
    {
        if (
            LightingConfig.Instance?.FancySkyRenderingEnabled() is true
            || LightingConfig.Instance?.FancySkyColorsEnabled() is true
        )
        {
            // night color is normally overridden on main menu
            info.isInGameMenuOrIsServer = false;
        }

        orig(info, out sunColor, out moonColor);
    }

    private static void IL_Main_SetBackColor(ILContext context)
    {
        var cursor = new ILCursor(context);

        var setSkyColorMethod = typeof(FancySkyColors)
            .GetMethod("SetBaseSkyColor", BindingFlags.NonPublic | BindingFlags.Static)
            .AssertNotNull();
        var skyColorVariable = cursor.Body.Variables.First(x =>
            x.VariableType.Name is "Color"
        );

        cursor.GotoNext(
            MoveType.After,
            (instruction) =>
                instruction.OpCode == OpCodes.Call
                && (instruction.Operand as MethodReference)?.Name is "ModifyNightColor"
        );
        cursor.MoveAfterLabels();
        cursor.Emit(OpCodes.Ldloca, skyColorVariable);
        cursor.Emit(OpCodes.Call, setSkyColorMethod);
    }

    private static void SetBaseSkyColor(ref Color bgColor)
    {
        if (LightingConfig.Instance?.FancySkyColorsEnabled() is not true)
        {
            return;
        }

        ColorUtils.Assign(
            ref bgColor,
            1f,
            CalculateSkyColor(GameTimeUtils.CalculateCurrentHour())
        );
    }

    public static Vector3 CalculateSkyColor(double hour)
    {
        if (
            LightingConfig.Instance?.FancySkyColorsEnabled() is not true
            || Preset is null
        )
        {
            // This code is adapted from vanilla

            var dayTime = hour is >= 4.5 and < 19.5;
            var time =
                3600.0
                * (
                    hour
                    - (
                        dayTime ? 4.5
                        : hour < 12.0 ? -4.5
                        : 19.5
                    )
                );

            var color = Vector3.One;
            float level;
            if (dayTime)
            {
                if (time < 13500.0)
                {
                    level = (float)(time / 13500.0);
                    color.X = (level * 230f) + 25f;
                    color.Y = (level * 220f) + 35f;
                    color.Z = (level * 220f) + 35f;
                }
                if (time > 45900.0)
                {
                    level = (float)(
                        1.0 - (((time / 54000.0) - 0.85) * 6.666666666666667)
                    );
                    color.X = (level * 200f) + 35f;
                    color.Y = (level * 85f) + 35f;
                    color.Z = (level * 135f) + 35f;
                }
                else if (time > 37800.0)
                {
                    level = (float)(1.0 - (((time / 54000.0) - 0.7) * 6.666666666666667));
                    color.X = (level * 20f) + 235f;
                    color.Y = (level * 135f) + 120f;
                    color.Z = (level * 85f) + 170f;
                }
            }
            else
            {
                if (time < 16200.0)
                {
                    level = (float)(1.0 - (time / 16200.0));
                    color.X = (level * 30f) + 5f;
                    color.Y = (level * 30f) + 5f;
                    color.Z = (level * 30f) + 5f;
                }
                else
                {
                    level = (float)(((time / 32400.0) - 0.5) * 2.0);
                    color.X = (level * 20f) + 5f;
                    color.Y = (level * 30f) + 5f;
                    color.Z = (level * 30f) + 5f;
                }
            }

            return color / 255f;
        }

        var foundProfile = Preset.TryGetValue(
            PreferencesConfig.Instance.FancySkyColorsPreset,
            out var profile
        );

        if (!foundProfile)
        {
            profile = Preset[SkyColorPreset.Preset1];
        }

        return profile.GetColor(hour);
    }

    internal static void DrawColorProfiles()
    {
        if (
            PreferencesConfig.Instance?.ShowFancySkyColorGradients is not true
            || Main.gameMenu
            || Main.gamePaused
            || Main.mapFullscreen
        )
        {
            return;
        }

        if (_profilesTexture?.IsDisposed is not false)
        {
            _profilesTexture = CreateProfilesTexture();
        }

        const float ScaleY = 50f;

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(
            _profilesTexture,
            new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f),
            null,
            Color.White,
            0f,
            new Vector2(_profilesTexture.Width / 2f, _profilesTexture.Height / 2f),
            new Vector2(1f, ScaleY),
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();
    }

    private static Texture2D CreateProfilesTexture()
    {
        var width = 24 * 60;
        var height = (2 * 8) - 1;

        var texture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
        var colors = new Color[width * height];

        var i = 0;
        foreach (
            var colorProfile in (ISimpleColorProfile[])
                [
                    new SkyLightColors1(),
                    new SkyLightColors2(),
                    new SkyLightColors3(),
                    new SkyLightColors4(),
                    new SkyLightColors5(),
                    new SkyColorsHigh(),
                    new SkyColorsLow(),
                    new SunColors(),
                ]
        )
        {
            for (var minute = 0; minute < width; ++minute)
            {
                var hour = minute / 60.0;
                var colorVec = colorProfile.GetColor(hour);
                ColorUtils.Convert(out Color color, colorVec);
                colors[i++] = color;
            }

            i += width;
        }

        texture.SetData(colors);

        return texture;
    }
}
