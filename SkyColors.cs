using System.Reflection;
using FancyLighting.Config.Enums;
using FancyLighting.Profiles;
using FancyLighting.Profiles.SkyColor;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace FancyLighting;

public static class SkyColors
{
    private static ILHook _ilHook_SetBackColor;

    public static Dictionary<SkyColorPreset, ISimpleColorProfile> Profiles
    {
        get;
        private set;
    }

    private static void Initialize() =>
        Profiles = new()
        {
            [SkyColorPreset.Profile1] = new SkyColors1(),
            [SkyColorPreset.Profile2] = new SkyColors2(),
            [SkyColorPreset.Profile3] = new SkyColors3(),
            [SkyColorPreset.Profile4] = new SkyColors4(),
        };

    internal static void Load()
    {
        Initialize();

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
        _ilHook_SetBackColor?.Dispose();
    }

    private static void _SetBackColor(ILContext context)
    {
        var cursor = new ILCursor(context);

        var setSkyColorMethod = typeof(SkyColors)
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
        if (PreferencesConfig.Instance?.CustomSkyColorsEnabled() is not true)
        {
            return;
        }

        var hour = Main.dayTime
            ? 4.5 + (Main.time / 3600.0)
            : 12.0 + 7.5 + (Main.time / 3600.0);
        ColorUtils.Assign(ref bgColor, 1f, CalculateSkyColor(hour));
    }

    public static Vector3 CalculateSkyColor(double hour)
    {
        var foundProfile = Profiles.TryGetValue(
            PreferencesConfig.Instance.CustomSkyPreset,
            out var profile
        );

        if (!foundProfile)
        {
            return Vector3.One;
        }

        return profile.GetColor(hour);
    }
}
