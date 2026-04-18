using FancyLighting.ColorProfiles.SkyColor;

namespace FancyLighting;

public static class FancySkyRendering
{
    private static Texture2D _ditherNoise;

    private static Shader _skyShader;
    private static Shader _skyDitheredShader;
    private static Shader _sunShader;

    private static bool _modifyStarDrawing = false;

    private const float SkyBrightness = 1.3f;
    private const float SkyBrightnessHiDef = 1.5f;

    private const float FadeBegin = 0.12f;
    private const float FadeHeight = 0.24f;
    private const float FadeHeightMult = 15f / 8; // 3f / 2 for smoothstep

    public delegate void SkyColorModifier(
        ref Vector3 highSkyColor,
        ref Vector3 lowSkyColor,
        ref Vector3 skyColorMult
    );

    // Meant for other mods to use
    public static event SkyColorModifier ModifySkyColors;

    internal static void Load()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>("FancyLighting/Effects/DitherNoise")
            .Value;

        _skyShader = EffectLoader.LoadEffect("FancyLighting/Effects/Sky", "Sky");
        _skyDitheredShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Sky",
            "SkyDithered"
        );
        _sunShader = EffectLoader.LoadEffect("FancyLighting/Effects/Sky", "Sun", true);

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.DrawStarsInBackground += _Main_DrawStarsInBackground;
        On_Main.DrawStar += _Main_DrawStar;
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
            LightingConfig.Instance?.FancySkyRenderingEnabled() is not true
            || FancyLightingMod._inCameraMode
            || artificial
        )
        {
            _modifyStarDrawing = false;
            orig(self, sceneArea, artificial);
            return;
        }

        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled() && !Main.gameMenu;
        var doDithering =
            LightingConfig.Instance.SmoothLightingEnabled() && doOverbright && !hiDef;
        var gamma = Main.gameMenu
            ? PostProcessing.DefaultGamma
            : PostProcessing.ContentGamma();

        var samplerState = MainGraphics.GetSamplerState();
        var transformMatrix = MainGraphics.GetTransformMatrix();
        Main.spriteBatch.End();

        var target = MainGraphics.GetRenderTarget() ?? Main.screenTarget;

        var hour = GameTimeUtils.CalculateCurrentHour();
        // skyColorMult is the effect the current biome has on the color of sky light
        var skyColorMult =
            Main.ColorOfTheSkies.ToVector3()
            / new Color(FancySkyColors.CalculateSkyColor(hour)).ToVector3();
        skyColorMult = Vector3.Clamp(skyColorMult, Vector3.Zero, Vector3.One);
        var skyBrightness = hiDef ? SkyBrightnessHiDef : SkyBrightness;

        var highSkyColor = SkyColorsHigh.Instance.GetColor(hour);
        var lowSkyColor = SkyColorsLow.Instance.GetColor(hour);

        ModifySkyColors?.Invoke(ref highSkyColor, ref lowSkyColor, ref skyColorMult);

        highSkyColor *= skyColorMult;
        lowSkyColor *= skyColorMult;
        highSkyColor.X = MathF.Pow(highSkyColor.X, gamma);
        highSkyColor.Y = MathF.Pow(highSkyColor.Y, gamma);
        highSkyColor.Z = MathF.Pow(highSkyColor.Z, gamma);
        lowSkyColor.X = MathF.Pow(lowSkyColor.X, gamma);
        lowSkyColor.Y = MathF.Pow(lowSkyColor.Y, gamma);
        lowSkyColor.Z = MathF.Pow(lowSkyColor.Z, gamma);
        highSkyColor *= skyBrightness;
        lowSkyColor *= skyBrightness;

        var highLevel = (sceneArea.bgTopY + (FadeBegin * target.Width)) / target.Height;
        if (Main.gameMenu)
        {
            highLevel -= 0.02f * target.Width / target.Height;
        }
        var lowLevel = highLevel + (FadeHeight * target.Width / target.Height);

        var midLevel = (highLevel + lowLevel) / 2f;
        highLevel = midLevel + (FadeHeightMult * (highLevel - midLevel));
        lowLevel = midLevel + (FadeHeightMult * (lowLevel - midLevel));

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
            .SetParameter("InverseGamma", 1f / gamma)
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

        var samplerState = MainGraphics.GetSamplerState();
        var transform = MainGraphics.GetTransformMatrix();
        var origTransform = transform;
        var rasterizerState = FancyLightingMod._inCameraMode
            ? RasterizerState.CullNone
            : Main.Rasterizer;
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
            rasterizerState,
            null,
            transform
        );
        if (isDay)
        {
            var gamma = Main.gameMenu
                ? PostProcessing.DefaultGamma
                : PostProcessing.ContentGamma();
            _sunShader
                .SetParameter("Gamma", gamma)
                .SetParameter("InverseGamma", 1f / gamma)
                .Apply();
        }
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            rasterizerState,
            null,
            origTransform
        );
    }
}
