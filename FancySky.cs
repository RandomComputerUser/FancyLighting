using System.Reflection;
using FancyLighting.ColorProfiles;
using FancyLighting.ColorProfiles.SkyColor;
using FancyLighting.Config.Enums;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using ReLogic.Content;

namespace FancyLighting;

public static class FancySky
{
    private static ILHook _ilHook_SetBackColor;

    private static Texture2D _pixel;

    private static RenderTarget2D _origSkyTarget;
    private static RenderTarget2D _tmpSkyTarget;

    private static Shader _fancySunSkyShader;
    private static Shader _fancySunShader;
    private static Shader _applyGammaShader;

    internal static bool _drawingFancySky;

    public static Dictionary<SkyColorPreset, ISimpleColorProfile> Preset
    {
        get;
        private set;
    }

    internal static void Load()
    {
        Preset = new()
        {
            [SkyColorPreset.Preset1] = new SkyColors1(),
            [SkyColorPreset.Preset2] = new SkyColors2(),
            [SkyColorPreset.Preset3] = new SkyColors3(),
            [SkyColorPreset.Preset4] = new SkyColors4(),
        };

        _pixel = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/Pixel",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _origSkyTarget = null;

        _fancySunSkyShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Sky",
            "FancySunSky"
        );
        _fancySunShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Sky",
            "FancySun"
        );
        _applyGammaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Sky",
            "ApplyGamma"
        );

        _drawingFancySky = false;

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.DrawStarsInBackground += _Main_DrawStarsInBackground;

        var detourMethod = typeof(Main).GetMethod(
            "SetBackColor",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (detourMethod is not null)
        {
            try
            {
                _ilHook_SetBackColor = new(detourMethod, _SetBackColor, true);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _pixel?.Dispose();
        _pixel = null;
        _origSkyTarget = null;
        _tmpSkyTarget?.Dispose();
        _tmpSkyTarget = null;
        EffectLoader.UnloadEffect(ref _fancySunSkyShader);
        EffectLoader.UnloadEffect(ref _fancySunShader);
        EffectLoader.UnloadEffect(ref _applyGammaShader);

        _ilHook_SetBackColor?.Dispose();
    }

    private static void _Main_DrawStarsInBackground(
        On_Main.orig_DrawStarsInBackground orig,
        Main self,
        Main.SceneArea sceneArea,
        bool artificial
    )
    {
        if (!LightingConfig.Instance.FancySunEnabled())
        {
            orig(self, sceneArea, artificial);
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transformMatrix = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var hour = GetCurrentHour();
        var sunCoords = CalculateSunCoords(hour);
        var sunEffect = Math.Clamp(2f * (sunCoords.Y + 0.5f), 0f, 1f);
        var darkSkyColor = ColorUtils.GammaToLinear(new Vector3(0f, 107f, 229f) / 255f);
        var lightSkyColor = ColorUtils.GammaToLinear(
            new Vector3(209f, 238f, 255f) / 255f
        );

        _origSkyTarget = MainGraphics.GetRenderTarget() ?? Main.screenTarget;
        TextureUtils.MakeSize(
            ref _tmpSkyTarget,
            _origSkyTarget.Width,
            _origSkyTarget.Height,
            SurfaceFormat.HalfVector4
        );
        Main.graphics.GraphicsDevice.SetRenderTarget(_tmpSkyTarget);

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _fancySunSkyShader
            .SetParameter(
                "CoordMultY",
                -4f * _origSkyTarget.Height / _origSkyTarget.Width
            )
            .SetParameter("SunEffect", sunEffect)
            .SetParameter("SunCoords", sunCoords)
            .SetParameter("DarkSkyColor", darkSkyColor)
            .SetParameter("LightSkyColor", lightSkyColor)
            .Apply();
        Main.spriteBatch.Draw(
            _pixel,
            Vector2.Zero,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            new Vector2(Main.screenWidth, Main.screenHeight),
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var gamma = PreferencesConfig.Instance.GammaExponent();
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transformMatrix
        );
        _applyGammaShader.SetParameter("Gamma", gamma).Apply();
        orig(self, sceneArea, artificial);

        _drawingFancySky = true;
    }

    internal static void DrawFancySun(On_Main.orig_DrawBG orig, Main self)
    {
        if (!_drawingFancySky)
        {
            orig(self);
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transformMatrix = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var hour = GetCurrentHour();
        var sunCoords = CalculateSunCoords(hour);
        var coordMult = _tmpSkyTarget.Width / 4f;
        sunCoords.X = (coordMult * sunCoords.X) + (_tmpSkyTarget.Width / 2f);
        sunCoords.Y = -(coordMult * sunCoords.Y) + (_tmpSkyTarget.Height / 2f);

        var bloom = LightingConfig.Instance.BloomEnabled();
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();

        float radius;
        float fadeBegin;
        float fadeEnd;
        Vector4 color;
        if (bloom)
        {
            radius = 27f;
            fadeBegin = 20f / radius;
            fadeEnd = 25f / radius;
        }
        else
        {
            radius = 62f;
            fadeBegin = 20f / radius;
            fadeEnd = 60f / radius;
        }
        if (hiDef)
        {
            color = new(200f, 200f, 100f, 1f);
        }
        else
        {
            color = new(1f, 1f, 1f, 1f);
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _fancySunShader
            .SetParameter("SunColor", color)
            .SetParameter("FadeBegin", fadeBegin)
            .SetParameter("FadeEnd", fadeEnd)
            .Apply();
        Main.spriteBatch.Draw(
            _pixel,
            sunCoords,
            null,
            Color.White,
            0f,
            new(0.5f),
            2f * radius,
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();

        var gamma = PreferencesConfig.Instance.GammaExponent();
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transformMatrix
        );
        _applyGammaShader.SetParameter("Gamma", gamma).Apply();
        orig(self);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(_origSkyTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _applyGammaShader.SetParameter("Gamma", 1f / gamma).Apply();
        Main.spriteBatch.Draw(_tmpSkyTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        _drawingFancySky = false;

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transformMatrix
        );
    }

    private static void _SetBackColor(ILContext context)
    {
        var cursor = new ILCursor(context);

        var setSkyColorMethod = typeof(FancySky)
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

        var hour = GetCurrentHour();
        ColorUtils.Assign(ref bgColor, 1f, CalculateSkyColor(hour));
    }

    public static double GetCurrentHour() =>
        Main.dayTime ? 4.5 + (Main.time / 3600.0) : 12.0 + 7.5 + (Main.time / 3600.0);

    public static Vector2 CalculateSunCoords(double hour)
    {
        hour %= 24.0;
        var angle = (float)((0.12f * (12 - hour)) + MathHelper.PiOver2);
        return new(2.5f * MathF.Cos(angle), (3f * MathF.Sin(angle)) - 2f);
    }

    public static Vector3 CalculateSkyColor(double hour)
    {
        var foundProfile = Preset.TryGetValue(
            PreferencesConfig.Instance.FancySkyColorsPreset,
            out var profile
        );

        if (!foundProfile)
        {
            return Vector3.One;
        }

        return profile.GetColor(hour);
    }
}
