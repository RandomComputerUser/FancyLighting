using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace FancyLighting.ModCompatibility;

internal static class CalamityModCompatibility
{
    private static ILHook _ilHook_NewThreshold;
    private static ILHook _ilHook_ChangeBlackThreshold_Delegate;

    internal static void Load()
    {
        if (!ModLoader.HasMod("CalamityMod"))
        {
            return;
        }

        var modClass = ModLoader.GetMod("CalamityMod").GetType();
        var modAssembly = modClass.Assembly;

        Type sunkenSeaBiomeClass;
        try
        {
            sunkenSeaBiomeClass = modAssembly.GetType(
                "CalamityMod.BiomeManagers.SunkenSeaBiome"
            );
        }
        catch (Exception)
        {
            // Unable to load class
            return;
        }

        if (sunkenSeaBiomeClass is null)
        {
            return;
        }

        var detourMethod = sunkenSeaBiomeClass.GetMethod(
            "NewThreshold",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _ilHook_NewThreshold = new(detourMethod, IL_NewThreshold, true);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }

        try
        {
            sunkenSeaBiomeClass = sunkenSeaBiomeClass.GetNestedType(
                "<>c",
                BindingFlags.NonPublic
            );
        }
        catch (Exception)
        {
            // Unable to load class
            return;
        }

        if (sunkenSeaBiomeClass is null)
        {
            return;
        }

        detourMethod = sunkenSeaBiomeClass.GetMethod(
            "<ChangeBlackThreshold>b__7_1",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _ilHook_ChangeBlackThreshold_Delegate = new(
                    detourMethod,
                    IL_ChangeBlackThreshold_Delegate,
                    true
                );
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _ilHook_NewThreshold?.Dispose();
        _ilHook_ChangeBlackThreshold_Delegate?.Dispose();
    }

    private static void IL_NewThreshold(ILContext context)
    {
        var cursor = new ILCursor(context);

        var newThresholdMethod = typeof(CalamityModCompatibility)
            .GetMethod("NewThreshold", BindingFlags.NonPublic | BindingFlags.Static)
            .AssertNotNull();

        cursor.GotoNext(
            MoveType.After,
            instruction => instruction.OpCode == OpCodes.Ldc_R4
        );
        cursor.Emit(OpCodes.Call, newThresholdMethod);
    }

    private static float NewThreshold(float threshold)
    {
        if (SettingsSystem._hiDef)
        {
            threshold *= PostProcessing.HiDefBrightnessScale;
        }

        return threshold;
    }

    private static void IL_ChangeBlackThreshold_Delegate(ILContext context)
    {
        var cursor = new ILCursor(context);

        // Fix bug in Calamity Mod's code
        cursor.GotoNext(
            MoveType.Before,
            instruction => instruction.OpCode == OpCodes.Ldc_I4_S
        );
        cursor.Remove();
        cursor.Emit(OpCodes.Ldc_I4, 14); // originally ldc.i4.s 13
    }
}
