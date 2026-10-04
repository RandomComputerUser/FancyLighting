using System.Reflection;
using FancyLighting.ColorGradients;
using FancyLighting.ColorGradients.SkyColor.Gradients;
using FancyLighting.Config.Enums;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria.GameContent;

namespace FancyLighting.Core.Sky;

public sealed class FancySkyColors
{
    public static FancySkyColors Instance { get; private set; }

    private Texture2D _gradientsTexture;

    internal FancySkyColors()
    {
        Instance = this;

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
        _gradientsTexture?.Dispose();
        _gradientsTexture = null;
    }

    private static void _Main_SetBackColor(
        On_Main.orig_SetBackColor orig,
        Main.InfoToSetBackColor info,
        out Color sunColor,
        out Color moonColor
    )
    {
        if (
            LightingConfig.Instance.FancySkyRenderingEnabled()
            || LightingConfig.Instance.FancySkyColorsEnabled()
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
                x.VariableType.Name is nameof(Color)
            );

            cursor.GotoNext(
                MoveType.After,
                instruction =>
                    instruction.OpCode == OpCodes.Call
                    && (instruction.Operand as MethodReference)?.Name
                        is nameof(DontStarveSeed.ModifyNightColor)
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
        if (!LightingConfig.Instance.FancySkyColorsEnabled())
        {
            return;
        }

        ColorUtils.Assign(
            ref bgColor,
            Instance.CalculateSkyColor(GameTimeUtils.CalculateCurrentHour())
        );
    }

    public Vector3 CalculateSkyColor(double hour)
    {
        if (!LightingConfig.Instance.FancySkyColorsEnabled())
        {
            return ModContent.GetInstance<VanillaSkyLightColorGradient>().GetColor(hour);
        }

        ITimedBasedColorGradient colorGradient = PreferencesConfig
            .Instance
            .FancySkyColorGradientsPreset switch
        {
            SkyColorGradientsPreset.Natural =>
                ModContent.GetInstance<NaturalSkyLightColorGradient>(),
            SkyColorGradientsPreset.Vivid =>
                ModContent.GetInstance<VividSkyLightColorGradient>(),
            _ => ModContent.GetInstance<VanillaSkyLightColorGradient>(),
        };

        return colorGradient.GetColor(hour);
    }

    internal void DrawColorGradients()
    {
        if (
            !DeveloperConfig.Instance.ShowFancySkyColorGradients
            || Main.gameMenu
            || Main.gamePaused
            || Main.mapFullscreen
        )
        {
            return;
        }

        if (_gradientsTexture?.IsDisposed is not false)
        {
            _gradientsTexture = CreateGradientsTexture();
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
            _gradientsTexture,
            new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f),
            null,
            Color.White,
            0f,
            new Vector2(_gradientsTexture.Width / 2f, _gradientsTexture.Height / 2f),
            new Vector2(1f, ScaleY),
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();
    }

    private static Texture2D CreateGradientsTexture()
    {
        ITimedBasedColorGradient[] colorGradients =
        [
            ModContent.GetInstance<VanillaSkyLightColorGradient>(),
            ModContent.GetInstance<NaturalSkyLightColorGradient>(),
            ModContent.GetInstance<NaturalSunColorGradient>(),
            ModContent.GetInstance<NaturalAtmosphereColorGradientSet>()._lowGradient,
            ModContent.GetInstance<NaturalAtmosphereColorGradientSet>()._middleGradient,
            ModContent.GetInstance<NaturalAtmosphereColorGradientSet>()._highGradient,
            ModContent.GetInstance<VividSkyLightColorGradient>(),
            ModContent.GetInstance<VividSunColorGradient>(),
            ModContent.GetInstance<VividAtmosphereColorGradientSet>()._lowGradient,
            ModContent.GetInstance<VividAtmosphereColorGradientSet>()._middleGradient,
            ModContent.GetInstance<VividAtmosphereColorGradientSet>()._highGradient,
        ];

        var width = 24 * 60;
        var height = (2 * colorGradients.Length) - 1;

        var texture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
        var colors = new Color[width * height];

        var i = 0;
        foreach (var colorGradient in colorGradients)
        {
            for (var minute = 0; minute < width; ++minute)
            {
                var hour = minute / 60.0;
                var colorVec = colorGradient.GetColor(hour);
                ColorUtils.Convert(out colors[i++], colorVec);
            }

            i += width;
        }

        texture.SetData(colors);

        return texture;
    }
}
