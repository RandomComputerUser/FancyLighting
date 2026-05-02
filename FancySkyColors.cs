using System.Reflection;
using FancyLighting.ColorProfiles;
using FancyLighting.ColorProfiles.SkyColor;
using FancyLighting.Config.Enums;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace FancyLighting;

public sealed class FancySkyColors
{
    public static FancySkyColors Instance { get; private set; }

    private Texture2D _profilesTexture;

    public Dictionary<SkyColorPreset, ISimpleColorProfile> Preset { get; private set; }

    internal FancySkyColors()
    {
        Instance = this;

        Preset = new()
        {
            [SkyColorPreset.Preset1] = ModContent.GetInstance<SkyLightColors1>(),
            [SkyColorPreset.Preset2] = ModContent.GetInstance<SkyLightColors2>(),
            [SkyColorPreset.Preset3] = ModContent.GetInstance<SkyLightColors3>(),
            [SkyColorPreset.Preset4] = ModContent.GetInstance<SkyLightColors4>(),
            [SkyColorPreset.Preset5] = ModContent.GetInstance<SkyLightColors5>(),
        };

        AddHooks();
    }

    private void AddHooks()
    {
        IL_Main.SetBackColor += IL_Main_SetBackColor;
        On_Main.SetBackColor += _Main_SetBackColor;
    }

    internal void Unload()
    {
        Instance = null;
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
        try
        {
            var cursor = new ILCursor(context);

            var setSkyColorMethod = typeof(FancySkyColors)
                .GetMethod(
                    nameof(SetBaseSkyColor),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();
            var skyColorVariable = cursor.Body.Variables.First(x =>
                x.VariableType.Name is "Color"
            );

            cursor.GotoNext(
                MoveType.After,
                instruction =>
                    instruction.OpCode == OpCodes.Call
                    && (instruction.Operand as MethodReference)?.Name
                        is "ModifyNightColor"
            );
            cursor.MoveAfterLabels();
            cursor.Emit(OpCodes.Ldloca, skyColorVariable);
            cursor.Emit(OpCodes.Call, setSkyColorMethod);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
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
            Instance.CalculateSkyColor(GameTimeUtils.CalculateCurrentHour())
        );
    }

    public Vector3 CalculateSkyColor(double hour)
    {
        if (
            LightingConfig.Instance?.FancySkyColorsEnabled() is not true
            || Preset is null
        )
        {
            return ModContent.GetInstance<VanillaSkyLightColors>().GetColor(hour);
        }

        var foundProfile = Preset.TryGetValue(
            PreferencesConfig.Instance.FancySkyColorsPreset,
            out var profile
        );

        if (!foundProfile)
        {
            profile = ModContent.GetInstance<VanillaSkyLightColors>();
        }

        return profile.GetColor(hour);
    }

    internal void DrawColorProfiles()
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
        var height = (2 * 9) - 1;

        var texture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
        var colors = new Color[width * height];

        var i = 0;
        foreach (
            var colorProfile in (ISimpleColorProfile[])
                [
                    ModContent.GetInstance<VanillaSkyLightColors>(),
                    ModContent.GetInstance<SkyLightColors1>(),
                    ModContent.GetInstance<SkyLightColors2>(),
                    ModContent.GetInstance<SkyLightColors3>(),
                    ModContent.GetInstance<SkyLightColors4>(),
                    ModContent.GetInstance<SkyLightColors5>(),
                    ModContent.GetInstance<SkyColorsHigh>(),
                    ModContent.GetInstance<SkyColorsLow>(),
                    ModContent.GetInstance<SunColors>(),
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
