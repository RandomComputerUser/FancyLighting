using System.Reflection;
using FancyLighting.Utils.Accessors;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace FancyLighting;

public static class FancySkyClouds
{
    private const int IndividualCloudPixelSize = 2;
    private const int BackgroundCloudPixelSize = 1;

    private static SpriteBatchEffect _cloudShadingEffect;

    internal static void Load()
    {
        _cloudShadingEffect = SpriteBatchEffectLoader.LoadEffect(
            "FancyLighting/Effects/Cloud",
            "CloudShading"
        );

        AddHooks();
    }

    private static void AddHooks()
    {
        On_Main.DrawSurfaceBG += _Main_DrawSurfaceBG;
        IL_Main.DrawSurfaceBG += IL_Main_DrawSurfaceBG;
    }

    internal static void Unload()
    {
        SpriteBatchEffectLoader.UnloadEffect(ref _cloudShadingEffect);
    }

    private static void _Main_DrawSurfaceBG(On_Main.orig_DrawSurfaceBG orig, Main self)
    {
        if (!SettingsSystem._useFancyClouds)
        {
            orig(self);
            return;
        }

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
            var setCloudScaleMethod = typeof(FancySkyClouds)
                .GetMethod(
                    nameof(SetCloudScale),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                .AssertNotNull();

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(13)
            );
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.Emit(OpCodes.Ldc_I4, IndividualCloudPixelSize);
            cursor.Emit(OpCodes.Call, setCloudScaleMethod);
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
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.Emit(OpCodes.Ldc_I4, BackgroundCloudPixelSize);
            cursor.Emit(OpCodes.Call, setCloudScaleMethod);
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
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.Emit(OpCodes.Ldc_I4, BackgroundCloudPixelSize);
            cursor.Emit(OpCodes.Call, setCloudScaleMethod);
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
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.Emit(OpCodes.Ldc_I4, IndividualCloudPixelSize);
            cursor.Emit(OpCodes.Call, setCloudScaleMethod);
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
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.Emit(OpCodes.Ldc_I4, IndividualCloudPixelSize);
            cursor.Emit(OpCodes.Call, setCloudScaleMethod);
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

    private static void Begin()
    {
        if (!SettingsSystem._useFancyClouds)
        {
            return;
        }

        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transformMatrix = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.Default,
            rasterizerState,
            _cloudShadingEffect.Effect,
            transformMatrix
        );
    }

    private static void End()
    {
        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transformMatrix = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.Default,
            rasterizerState,
            null,
            transformMatrix
        );
    }

    private static void SetCloudScale(int pixelSize)
    {
        _cloudShadingEffect.SetParameter("PixelSize", (float)pixelSize);
    }
}
