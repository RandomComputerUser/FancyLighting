using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace FancyLighting.Core.Sky;

public static class FancySkyClouds
{
    private static SamplerState _samplerState = SamplerState.LinearClamp;

    private static SpriteBatchEffect _cloudShadingEffect;
    private static SpriteBatchEffect _cloudShadingWrapEffect;

    internal static void Load()
    {
        var effect = EffectLoader.Load("CloudShading");
        _cloudShadingEffect = new(effect, "CloudShading");
        _cloudShadingWrapEffect = new(effect, "CloudShadingWrap");

        AddHooks();
    }

    internal static void Unload()
    {
        _samplerState = null;
        _cloudShadingEffect = null;
        _cloudShadingWrapEffect = null;
    }

    private static void AddHooks()
    {
        On_Main.DrawSurfaceBG += _Main_DrawSurfaceBG;
        IL_Main.DrawSurfaceBG += IL_Main_DrawSurfaceBG;
    }

    private static void _Main_DrawSurfaceBG(On_Main.orig_DrawSurfaceBG orig, Main self)
    {
        if (!SettingsSystem._useFancyClouds)
        {
            orig(self);
            return;
        }

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();

        var overbrightMult = hiDef ? 1f / PostProcessing.HiDefBrightnessScale : 1f;

        var zoomWithFlipping = MainGraphics.InCameraMode
            ? Vector2.One
            : new Vector2(1f, MathF.Sign(Main.GameViewMatrix.TransformationMatrix.M22));

        var hour = GameTimeUtils.CalculateCurrentHour();
        var (skyLightAngle, _, skyLightMult) =
            FancySkyLighting.CalculateSkyLightAngleAndMultiplier(hour);
        var normalMapSkyGradientMult = overbrightMult * zoomWithFlipping;

        _cloudShadingEffect
            .SetParameter(
                "SkyLightGradient",
                -normalMapSkyGradientMult
                    * new Vector2(
                        (float)Math.Cos(skyLightAngle),
                        (float)Math.Sin(skyLightAngle)
                    )
            )
            .SetParameter("SkyLightMult", 0.75f * (float)skyLightMult);

        orig(self);
    }

    private static void IL_Main_DrawSurfaceBG(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var beginMethod = typeof(FancySkyClouds)
                .GetMethod(nameof(Begin), BindingFlags.NonPublic | BindingFlags.Static)
                .AssertNotNull();
            var endMethod = typeof(FancySkyClouds)
                .GetMethod(nameof(End), BindingFlags.NonPublic | BindingFlags.Static)
                .AssertNotNull();

            // average cloud scale for each cloud layer
            // this is adapted from vanilla code
            const float Layer1Scale = (0.70f + 0.99f) / 2f;
            const float Layer2Scale = 1.65f / 2f;
            const float Layer3Scale = 1.85f / 2f;
            const float Layer4Scale = (1.00f + 1.15f) / 2f;
            const float Layer5Scale = (1.16f + 1.30f) / 2f;

            const float Layer1Mult = 0.6f;
            const float Layer2Mult = 1f;
            const float Layer3Mult = 1f;
            const float Layer4Mult = 1f;
            const float Layer5Mult = 1f;

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(13)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer1Scale);
            cursor.Emit(OpCodes.Ldc_R4, Layer1Mult);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(13),
                instruction => instruction.OpCode == OpCodes.Ldc_I4,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // cloud background
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(21)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer2Scale);
            cursor.Emit(OpCodes.Ldc_R4, Layer2Mult);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(21),
                instruction => instruction.OpCode == OpCodes.Ldarg_0,
                instruction => instruction.OpCode == OpCodes.Ldfld,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // cloud background
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(22)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer3Scale);
            cursor.Emit(OpCodes.Ldc_R4, Layer3Mult);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(22),
                instruction => instruction.OpCode == OpCodes.Ldarg_0,
                instruction => instruction.OpCode == OpCodes.Ldfld,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(23)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer4Scale);
            cursor.Emit(OpCodes.Ldc_R4, Layer4Mult);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(23),
                instruction => instruction.OpCode == OpCodes.Ldc_I4,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(31)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer5Scale);
            cursor.Emit(OpCodes.Ldc_R4, Layer5Mult);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(31),
                instruction => instruction.OpCode == OpCodes.Ldc_I4,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private static void Begin(float scale, float mult, bool wrap)
    {
        if (!SettingsSystem._useFancyClouds)
        {
            return;
        }

        _samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transformMatrix = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
        Main.spriteBatch.End();

        var newSamplerState = wrap
            ? CustomSamplerStates.LinearWrapUClampV
            : SamplerState.LinearClamp;

        var cloudShadingStrength = Math.Clamp(
            PreferencesConfig.Instance.CloudShadingMultiplier(),
            0f,
            1f
        );

        if (!MainGraphics.InCameraMode)
        {
            scale *= Main.BackgroundViewMatrix.Zoom.X;
        }

        var effect = wrap ? _cloudShadingWrapEffect : _cloudShadingEffect;
        effect
            .SetParameter("Scale", 2f * scale)
            .SetParameter("ShadingStrength", mult * cloudShadingStrength);
        SpriteBatchEffectLoader.Apply(effect);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            newSamplerState,
            DepthStencilState.Default,
            rasterizerState,
            null,
            transformMatrix
        );
    }

    private static void End()
    {
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transformMatrix = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);

        Main.spriteBatch.End();
        SpriteBatchEffectLoader.Reset();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            _samplerState,
            DepthStencilState.Default,
            rasterizerState,
            null,
            transformMatrix
        );
    }
}
