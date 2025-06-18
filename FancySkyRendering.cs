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

        var sunCoords = CalculateSunCoords(hour);
        var sunEffect = Math.Clamp(2f * (sunCoords.Y + 0.5f), 0f, 1f);
        var darkSkyColor = skyColorMult * new Vector3(30f, 110f, 255f) / 255f;
        var lightSkyColor = skyColorMult * new Vector3(195f, 220f, 255f) / 255f;
        var coordMultY = -4f * target.Height / target.Width;
        var horizonLevel =
            (coordMultY * ((float)sceneArea.bgTopY / target.Height)) + 0.25f;

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _skyShader
            .SetParameter("CoordMultY", coordMultY)
            .SetParameter("HorizonLevel", horizonLevel)
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
        Main.SceneArea sceneArea,
        Color sunColor,
        float tempMushroomInfluence
    )
    {
        var samplerState = MainGraphics.GetSamplerState();
        var transformMatrix = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;

        var hour = GameTimeUtils.CalculateCurrentHour();
        var sunCoords = CalculateSunCoords(hour);
        var coordMult = target.Width / 4f;
        sunCoords.X = (coordMult * sunCoords.X) + (target.Width / 2f);
        sunCoords.Y = -(coordMult * sunCoords.Y) + (target.Height / 2f);

        var bloom = LightingConfig.Instance.BloomEnabled();
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();

        var radius = bloom ? 16f : 20f;
        var glowRadius = bloom ? 60f : 90f;
        var glowIntensity = 1.25f;
        var glowFadeMult = bloom ? 0.125f : 0.5f;

        var brightness =
            bloom ? ColorUtils.LinearToGamma(150f)
            : hiDef ? 1.5f
            : 1f;
        var color = new Vector4(new Vector3(brightness), 1f);
        var glowColor = 0.6f * new Vector4(new Vector3(glowIntensity), 1f);

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _sunShader
            .SetParameter("SunColor", color)
            .SetParameter("SunGlowColor", glowColor)
            .SetParameter("SunRadius", radius)
            .SetParameter("SunGlowRadius", glowRadius)
            .SetParameter("SunGlowFadeMult", glowFadeMult)
            .Apply();
        Main.spriteBatch.Draw(
            _pixel,
            sunCoords,
            null,
            Color.White,
            0f,
            new(0.5f),
            2f * glowRadius,
            SpriteEffects.None,
            0f
        );
        Main.spriteBatch.End();

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

    public static Vector2 CalculateSunCoords(double hour)
    {
        hour %= 24.0;
        var angle = (0.061f * (12f - (float)hour)) + MathHelper.PiOver2;
        return new(5f * MathF.Cos(angle), (5f * MathF.Sin(angle)) - 3.96f);
    }
}
