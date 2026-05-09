# The source code of the Fancy Lighting mod for tModLoader

### [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
### [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

[EasyXnb](https://github.com/SuperAndyHero/EasyXnb) is required to build this mod. The EasyXnb config file can be found in the Effects directory.

## Mod.Call() API

View the [Mod.Call() API documentation](ModCallAPI.md) for mod developers.

## Latest Version

### v1.1.0 (2026-??-??)
- Added a setting to switch between various tone mapping operators (TMOs) for full HDR rendering
- Added the Bright TMO, which is the new default TMO 
- The old TMO is now called Filmic and is available as another option
- Added the Linear TMO, which may be used with add-ons to achieve HDR output
- Added a setting to boost color vibrance when using full HDR rendering
- Color vibrance is now boosted by default when using full HDR rendering
- Added a setting to enable depth of field when using full HDR rendering
- Slightly increased the contrast of lighting when using full HDR rendering
- Tweaked how translucent walls and blocks appear when using full HDR rendering
- Made Fancy Atmosphere more colorful
- Added a setting to boost sky brightness when Fancy Atmosphere is enabled
- Sky brightness is now boosted by default when using Fancy Atmosphere
- Fancy Atmosphere is now (mostly) compatible with camera mode
- Tweaked the sky colors used by Fancy Atmosphere
- Tweaked the appearance of the sun in Fancy Atmosphere
- Increased the brightness of stars in Fancy Atmosphere (previously they were much darker than in vanilla)
- Tweaked sky light color Preset 1 (now called “Natural”)
- Made the underworld background slightly brighter when using full HDR rendering
- Improved and optimized global illumination
- Improved how water absorbs light when using the Fancy Lighting Engine
- Improved Ambient Occlusion, making it subtler than before when settings are equal
- Increased the default ambient occlusion exponent to 200% (from 100%)
- Increased the default ambient light proportion to 75% (from 60%)
- Increased the maximum ambient occlusion exponent to 500% (from 400%)
- Decreased the default normal maps strength to 3 (from 4)
- Increased the maximum normal maps strength to 15 (from 12)
- Slightly improved the quality of the bicubic render mode
- The gamma setting now adjusts output gamma (instead of content gamma)
- Gamma and sRGB output no longer affect camera mode
- Increased the range of the gamma setting to 140–300% (from 160–280%)
- Increased the maximum bloom strength to 20 (from 15)
- Full HDRR exposure can now be adjusted in increments of 5 (instead of 10)
- Changed the default quality preset to Medium (from Low)
- Removed the Basic preset
- Downgraded some settings in the Low and Medium presets (Medium is still better than Low was before)
- Rearranged some settings in the config
- Made some UI tweaks in the config
- Updated some config tooltips and labels
- Fixed a bug that caused extra gore to spawn with Smooth Lighting enabled
- Fixed some uncommon graphical glitches
- Fix some bugs that caused minor visual disruption after updating a config
- Fixed some potential graphical glitches caused by Fancy Atmosphere and Smooth Lighting
- Made various minor optimizations
- Slightly improved frame time consistency when using the Fancy Lighting Engine
- Made the mod unload more completely
- Made Smooth Lighting compatible with the custom water lighting in Spirit Reforged
- Fixed a graphical glitch that occurred in the Sunken Sea biome in Calamity Mod
- Made more classes and methods public
- Added a Mod.Call() API for other mods to use
- Added new commands `"AddCustomTileLighting"`, `"AddHook"`, and `"RemoveCustomTileLighting"` to the Mod.Call() API
- Added a new event `PreDrawSky` in the FancySkyRendering class, which can be accessed via the `"AddHook"` command in the Mod.Call() API
- Added a new event `PostUpdateLightMap` in the SmoothLighting class, which can be accessed via the `"AddHook"` command in the Mod.Call() API
- Updated the mod icon and description
