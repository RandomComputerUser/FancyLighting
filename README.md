# The source code of the Fancy Lighting mod for tModLoader

### [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
### [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

[EasyXnb](https://github.com/SuperAndyHero/EasyXnb) is required to build this mod. The EasyXnb config file can be found in the Effects directory.

## Mod.Call() API

View the [Mod.Call() API documentation](ModCallAPI.md) for mod developers.

## Latest Version

### v1.2.0 (2026-??-??)
- Added Fancy Sky Light Shading
  - Note for mod developers: this change affects the `PostUpdateLightMap` hook in the Mod.Call() API
- Added tile entity normal maps
- Tweaked normal maps rendering and adjusted the normal maps strength scale
- Changed the default normal maps strength to 7 (from 3)
- Changed the maximum normal maps strength to 10 (from 15)
- Added a setting to apply smooth lighting to tile entities
- Added the "Neutral (LMS)" TMO, which is the new default
- Renamed the "Neutral" TMO to "Neutral (old)" and "Filmic" to "Filmic (sRGB)"
- Tweaked and optimized vibrance boost
- Made the "Bicubic" render mode a little brighter
- Added a setting to toggle whether normal maps are rendered on non-solid tiles
- Depth of field is no longer exclusive to full HDR rendering
- Increased the range of the gamma setting to 100–340% (from 140–300%)
- Changed the exposure setting to be logarithmic and increased its range
- Increased the max vibrance boost to 15 (from 10)
- Increased the max bloom strength to 30 (from 20)
- Increased the max sky brightness boost to 15 (from 10)
- Created two new configs: "Compatibility Settings" and "Developer Settings"
- Some settings from the "Preferences" config have been moved to the new configs
- Added tooltip lines to settings that have a high impact on performance
- Made other UI tweaks to the configs
- Added a setting to disable frame timing optimizations used by the Fancy Lighting Engine
- Fixed some graphical glitches that occurred when the mod was disabled when tModLoader was launched and then later enabled
- Fixed a minor bug with ambient occlusion when using full HDR rendering
- Added compatibility with screen flipping from sources other than the Gravitation buff
- Made some minor optimizations
- Improved unloading
