using ReLogic.Content;

namespace FancyLighting;

public static class FancySkyRendering
{
    private static Texture2D _pixel;

    private static Shader _skyShader;
    private static Shader _sunShader;

    internal static void Load()
    {
        _pixel = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/Pixel",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _skyShader = EffectLoader.LoadEffect("FancyLighting/Effects/Sky", "Sky");
        _sunShader = EffectLoader.LoadEffect("FancyLighting/Effects/Sky", "Sun");

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.DrawStarsInBackground += _Main_DrawStarsInBackground;
    }

    internal static void Unload()
    {
        _pixel?.Dispose();
        _pixel = null;
        EffectLoader.UnloadEffect(ref _skyShader);
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
        )
        {
            orig(self, sceneArea, artificial);
            return;
        }

        var samplerState = MainGraphics.GetSamplerState();
        var transformMatrix = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;

        var hour = GameTimeUtils.CalculateCurrentHour();
        var skyColorMult =
            Main.ColorOfTheSkies.ToVector3() / FancySkyColors.CalculateSkyColor(hour);
        skyColorMult = Vector3.Clamp(skyColorMult, Vector3.Zero, Vector3.One);

        var highSkyColor = skyColorMult * new Vector3(20f, 90f, 255f) / 255f;
        var lowSkyColor = skyColorMult * new Vector3(140f, 210f, 255f) / 255f;

        var highLevel = (sceneArea.bgTopY + (0.1f * target.Width)) / target.Height;
        var lowLevel = highLevel + (0.2f * target.Width / target.Height);

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _skyShader
            .SetParameter("HighSkyLevel", highLevel)
            .SetParameter("LowSkyLevel", lowLevel)
            .SetParameter("HighSkyColor", highSkyColor)
            .SetParameter("LowSkyColor", lowSkyColor)
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
        transform.M42 += 50f;

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
