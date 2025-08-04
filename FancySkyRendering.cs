using FancyLighting.ColorProfiles;
using FancyLighting.ColorProfiles.SkyColor;
using ReLogic.Content;

namespace FancyLighting;

public static class FancySkyRendering
{
    private static Texture2D _ditherNoise;

    private static Shader _skyShader;
    private static Shader _skyDitheredShader;
    private static Shader _sunShader;

    private static ISimpleColorProfile _highSkyColorProfile = new SkyColorsHigh();
    private static ISimpleColorProfile _lowSkyColorProfile = new SkyColorsLow();
    private static ISimpleColorProfile _sunColorProfile = new SunColors();

    internal static void Load()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _skyShader = EffectLoader.LoadEffect("FancyLighting/Effects/Sky", "Sky");
        _skyDitheredShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Sky",
            "SkyDithered"
        );
        _sunShader = EffectLoader.LoadEffect("FancyLighting/Effects/Sky", "Sun");

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.DrawStarsInBackground += _Main_DrawStarsInBackground;
    }

    internal static void Unload()
    {
        _ditherNoise?.Dispose();
        _ditherNoise = null;
        EffectLoader.UnloadEffect(ref _skyShader);
        EffectLoader.UnloadEffect(ref _skyDitheredShader);
        EffectLoader.UnloadEffect(ref _sunShader);
    }

    // Draw sky
    private static void _Main_DrawStarsInBackground(
        On_Main.orig_DrawStarsInBackground orig,
        Main self,
        Main.SceneArea sceneArea,
        bool artificial
    )
    {
        if (
            !LightingConfig.Instance.FancySkyRenderingEnabled()
            || Main.gameMenu
            || artificial
            || FancyLightingMod._inCameraMode
        )
        {
            orig(self, sceneArea, artificial);
            return;
        }

        var doDithering =
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright()
            && !LightingConfig.Instance.HiDefFeaturesEnabled();

        var samplerState = MainGraphics.GetSamplerState();
        var transformMatrix = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;

        var hour = GameTimeUtils.CalculateCurrentHour();
        var skyColorMult =
            Main.ColorOfTheSkies.ToVector3() / FancySkyColors.CalculateSkyColor(hour);
        skyColorMult = Vector3.Clamp(skyColorMult, Vector3.Zero, Vector3.One);

        var highSkyColor = skyColorMult * _highSkyColorProfile.GetColor(hour);
        var lowSkyColor = skyColorMult * _lowSkyColorProfile.GetColor(hour);

        var highLevel = (sceneArea.bgTopY + (0.05f * target.Width)) / target.Height;
        var lowLevel = highLevel + (0.3f * target.Width / target.Height);

        if (Main.LocalPlayer.gravDir < 0f)
        {
            (highSkyColor, lowSkyColor) = (lowSkyColor, highSkyColor);
            (highLevel, lowLevel) = (1f - lowLevel, 1f - highLevel);
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointWrap,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        var scale = new Vector2(
            (float)Main.screenWidth / _ditherNoise.Width,
            (float)Main.screenHeight / _ditherNoise.Height
        );

        (
            doDithering
                ? _skyDitheredShader.SetParameter("DitherCoordMult", scale)
                : _skyShader
        )
            .SetParameter("HighSkyLevel", highLevel)
            .SetParameter("LowSkyLevel", lowLevel)
            .SetParameter("HighSkyColor", highSkyColor)
            .SetParameter("LowSkyColor", lowSkyColor)
            .Apply();

        Main.spriteBatch.Draw(
            _ditherNoise,
            Vector2.Zero,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transformMatrix
        );
        orig(self, sceneArea, artificial);
    }

    internal static void DrawSun(
        On_Main.orig_DrawSunAndMoon orig,
        Main self,
        Main.SceneArea sceneArea,
        Color moonColor,
        Color sunColor,
        float tempMushroomInfluence
    )
    {
        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        // shift sun downward
        transform.M42 += Main.LocalPlayer.gravDir < 0f ? -25f : 25f;

        if (!Main.eclipse)
        {
            var hour = GameTimeUtils.CalculateCurrentHour();
            var sunColorVec = _sunColorProfile.GetColor(hour);
            ColorUtils.Convert(out sunColor, sunColorVec);
        }

        var gamma = PreferencesConfig.Instance.GammaExponent();

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            FancyLightingMod._inCameraMode ? RasterizerState.CullNone : Main.Rasterizer,
            null,
            transform
        );
        _sunShader
            .SetParameter("Gamma", gamma)
            .SetParameter("InverseGamma", 1f / gamma)
            .Apply();
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
    }
}
