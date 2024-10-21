﻿using System.Collections.Generic;
using FancyLighting.Config;
using FancyLighting.Profiles;
using FancyLighting.Profiles.SkyColor;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Terraria;

namespace FancyLighting;

public static class SkyColors
{
    public static Dictionary<SkyColorPreset, ISimpleColorProfile> Profiles
    {
        get;
        private set;
    }

    private static bool _dayTimeTmp;
    private static bool _dontStarveWorldTmp;
    private static bool _modifyNightColor = false;

    internal static void Initialize() =>
        Profiles = new()
        {
            [SkyColorPreset.Profile1] = new SkyColors1(),
            [SkyColorPreset.Profile2] = new SkyColors2(),
            [SkyColorPreset.Profile3] = new SkyColors3(),
        };

    internal static void AddSkyColorsHooks()
    {
        Initialize();

        Terraria.On_Main.SetBackColor += _Main_SetBackColor;
        Terraria.GameContent.On_DontStarveSeed.ModifyNightColor += _Main_ModifyNightColor;
    }

    private static void _Main_SetBackColor(
        Terraria.On_Main.orig_SetBackColor orig,
        Main.InfoToSetBackColor info,
        out Color sunColor,
        out Color moonColor
    )
    {
        if (!(PreferencesConfig.Instance?.CustomSkyColorsEnabled() ?? false))
        {
            orig(info, out sunColor, out moonColor);
            return;
        }

        _modifyNightColor = false;
        orig(info, out sunColor, out moonColor);

        _dayTimeTmp = Main.dayTime;
        _dontStarveWorldTmp = Main.dontStarveWorld;
        _modifyNightColor = true;
        Main.dayTime = false;
        Main.dontStarveWorld = true;
        // info is a struct, so we don't have to reset this value
        info.isInGameMenuOrIsServer = false;
        try
        {
            orig(info, out _, out _);
        }
        finally
        {
            Main.dayTime = _dayTimeTmp;
            Main.dontStarveWorld = _dontStarveWorldTmp;
            _modifyNightColor = false;
        }
    }

    private static void _Main_ModifyNightColor(
        Terraria.GameContent.On_DontStarveSeed.orig_ModifyNightColor orig,
        ref Color backColor,
        ref Color moonColor
    )
    {
        if (
            !_modifyNightColor
            || Profiles is null
            || !(PreferencesConfig.Instance?.CustomSkyColorsEnabled() ?? false)
        )
        {
            orig(ref backColor, ref moonColor);
            return;
        }

        Main.dayTime = _dayTimeTmp;
        Main.dontStarveWorld = _dontStarveWorldTmp;
        SetBaseSkyColor(ref backColor);
        if (!Main.dayTime && Main.dontStarveWorld)
        {
            orig(ref backColor, ref moonColor);
        }
    }

    public static void SetBaseSkyColor(ref Color bgColor)
    {
        var hour = Main.dayTime
            ? 4.5 + (Main.time / 3600.0)
            : 12.0 + 7.5 + (Main.time / 3600.0);
        VectorToColor.Assign(ref bgColor, 1f, CalculateSkyColor(hour));
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
