using FancyLighting.ColorProfiles.SkyColor;
using ReLogic.Content;

namespace FancyLighting;

public static class FancySkyRendering
{
    internal static RenderTarget2D _skyTarget;

    private static Texture2D _ditherNoise;

    private static Shader _skyShader;
    private static Shader _skyDitheredShader;
    private static Shader _sunShader;

    private static Shader _lightShader;
    private static Shader _sceneShader;
    private static Shader _raysShader;

    private static bool _modifyStarDrawing = false;

    private static Vector2 _sunPosition;

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

        _lightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/CrepuscularRays",
            "Light"
        );
        _sceneShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/CrepuscularRays",
            "Scene"
        );
        _raysShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/CrepuscularRays",
            "Rays"
        );

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.DrawStarsInBackground += _Main_DrawStarsInBackground;
        On_Main.DrawStar += _Main_DrawStar;
    }

    internal static void Unload()
    {
        _skyTarget?.Dispose();
        _skyTarget = null;
        _ditherNoise?.Dispose();
        _ditherNoise = null;
        EffectLoader.UnloadEffect(ref _skyShader);
        EffectLoader.UnloadEffect(ref _skyDitheredShader);
        EffectLoader.UnloadEffect(ref _sunShader);
        EffectLoader.UnloadEffect(ref _lightShader);
        EffectLoader.UnloadEffect(ref _sceneShader);
        EffectLoader.UnloadEffect(ref _raysShader);
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
            LightingConfig.Instance?.FancySkyRenderingEnabled() is not true
            || FancyLightingMod._inCameraMode
            || artificial
        )
        {
            _modifyStarDrawing = false;
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

        var highSkyColor = skyColorMult * SkyColorsHigh.Instance.GetColor(hour);
        var lowSkyColor = skyColorMult * SkyColorsLow.Instance.GetColor(hour);

        var highLevel = (sceneArea.bgTopY + (0.04f * target.Width)) / target.Height;
        if (Main.gameMenu)
        {
            highLevel -= 0.02f * target.Width / target.Height;
        }
        var lowLevel = highLevel + (0.32f * target.Width / target.Height);

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

        _modifyStarDrawing = true;
        try
        {
            orig(self, sceneArea, artificial);
        }
        finally
        {
            _modifyStarDrawing = false;
        }
    }

    private static void _Main_DrawStar(
        On_Main.orig_DrawStar orig,
        Main self,
        ref Main.SceneArea sceneArea,
        float starOpacity,
        Color bgColorForStars,
        int i,
        Star theStar,
        bool artificial,
        bool foreground
    )
    {
        if (_modifyStarDrawing)
        {
            // prevent stars from appearing during sunrise/sunset
            var colorVec = 1.25f * bgColorForStars.ToVector3();
            colorVec.X = MathF.Sqrt(colorVec.X);
            colorVec.Y = MathF.Sqrt(colorVec.Y);
            colorVec.Z = MathF.Sqrt(colorVec.Z);
            ColorUtils.Convert(out bgColorForStars, colorVec);
        }

        orig(
            self,
            ref sceneArea,
            starOpacity,
            bgColorForStars,
            i,
            theStar,
            artificial,
            foreground
        );
    }

    internal static void DrawSunAndMoon(
        On_Main.orig_DrawSunAndMoon orig,
        Main self,
        Main.SceneArea sceneArea,
        Color moonColor,
        Color sunColor,
        float tempMushroomInfluence
    )
    {
        if (_sunShader is null)
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }

        _sunPosition = CalculateSunPosition(Main.time, sceneArea);

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        if (!Main.gameMenu)
        {
            // shift sun/moon downward
            transform.M42 += Main.LocalPlayer.gravDir < 0f ? -25f : 25f;
        }

        if (!Main.eclipse)
        {
            var hour = GameTimeUtils.CalculateCurrentHour();
            var sunColorVec = SunColors.Instance.GetColor(hour);
            ColorUtils.Convert(out sunColor, sunColorVec);
        }

        var isDay = Main.dayTime;

        Main.spriteBatch.Begin(
            isDay ? SpriteSortMode.Immediate : SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transform
        );
        if (isDay)
        {
            var gamma = Main.gameMenu ? 2.2f : PreferencesConfig.Instance.GammaExponent();
            _sunShader
                .SetParameter("Gamma", gamma)
                .SetParameter("InverseGamma", 1f / gamma)
                .Apply();
        }
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);

        if (!LightingConfig.Instance.CrepuscularRaysActive())
        {
            return;
        }

        Main.spriteBatch.End();

        var screenTarget = MainGraphics.GetRenderTarget() ?? Main.screenTarget;

        TextureUtils.MakeSize(
            ref _skyTarget,
            screenTarget.Width,
            screenTarget.Height,
            TextureUtils.ScreenFormat
        );

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();

        Main.graphics.GraphicsDevice.SetRenderTarget(_skyTarget);
        Main.spriteBatch.Begin(
            hiDef ? SpriteSortMode.Immediate : SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            transform
        );
    }

    internal static void DrawCrepuscularRays(
        RenderTarget2D outputTarget,
        RenderTarget2D tmpTarget,
        RenderTarget2D foregroundTarget1,
        RenderTarget2D foregroundTarget2
    )
    {
        var width = outputTarget.Width;
        var height = outputTarget.Height;

        var sunPosition = _sunPosition;
        if (Main.LocalPlayer.gravDir < 0f)
        {
            sunPosition.Y = Main.screenHeight - sunPosition.Y;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(tmpTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _lightShader
            .SetParameter("Resolution", new Vector2(width, height))
            .SetParameter("SunPosition", sunPosition)
            .SetParameter("LightColor", new Vector3(1f, 0.8f, 0.5f))
            .Apply();
        Main.spriteBatch.Draw(foregroundTarget1, Vector2.Zero, Color.White);
        _sceneShader.Apply();
        Main.spriteBatch.Draw(foregroundTarget1, Vector2.Zero, Color.White);
        if (foregroundTarget2 is not null)
        {
            Main.spriteBatch.Draw(foregroundTarget2, Vector2.Zero, Color.White);
        }
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(outputTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _raysShader
            .SetParameter("SunPosition", sunPosition / new Vector2(width, height))
            .Apply();
        Main.spriteBatch.Draw(tmpTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }

    private static Vector2 CalculateSunPosition(double time, Main.SceneArea sceneArea)
    {
        // TODO: Fix this
        var x = time / 54000.0 * sceneArea.totalWidth;
        var y =
            sceneArea.bgTopY
            + (
                (
                    time < 27000.0
                        ? Math.Pow(1.0 - (time / 54000.0 * 2.0), 2.0)
                        : Math.Pow(((time / 54000.0) - 0.5) * 2.0, 2.0)
                ) * 250.0
            )
            + 180.0;
        return new Vector2((float)x, (float)y) + sceneArea.SceneLocalScreenPositionOffset;
    }
}
