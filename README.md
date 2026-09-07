# The source code of the Fancy Lighting mod for tModLoader

### [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
### [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

[EasyXnb](https://github.com/SuperAndyHero/EasyXnb) is required to build this mod. The EasyXnb config file can be found in the Effects directory.

## Mod.Call() API

View the [Mod.Call() API documentation](ModCallAPI.md) for mod developers.

## Latest Version

### v1.2.0 (2026-??-??)

#### Features and Improvements
- Added Fancy Sky Light Shading
- Added tile entity normal maps and smooth lighting
- Added a new default tone mapping operator for full HDR rendering, *Neutral (LMS)*
- Added bright light synchronization for full HDR rendering
- Improved basic glow rendering (without increased accuracy enabled) and made it the default for all quality presets
- Made the *Bicubic* render mode a little brighter
- Improved enhanced light map blurring
- Improved Ambient Occlusion visual quality
- The setting to disable HDRR during bosses and events now affects Smooth Lighting as a whole
- Depth of field is no longer exclusive to full HDR rendering
- Adjusted overall brightness and contrast when using full HDR rendering
- Slightly increased the brightness of the background when using full HDR rendering
- Improved vibrance boost
- Tweaked the appearance of bloom

#### Settings Tweaks
- Created two new configs: *Compatibility Settings* and *Developer Settings*
- Some settings from the *Preferences* config have been moved to the new configs
- Added tooltip lines to settings that have a major effect on performance
- Made other improvements to settings tooltips
- Rearranged and renamed some settings
- Added a setting to toggle whether normal maps are rendered on non-solid tiles
- Tweaked normal maps rendering and adjusted the normal maps strength scale
- Changed the default normal maps strength to 6 (from 3)
- Changed the maximum normal maps strength to 10 (from 15)
- Increased the range of the gamma setting to 100–340 (from 140–300)
- Gamma can now be adjusted in increments of 5 (from 10)
- Changed the exposure setting to be logarithmic and increased its range
- Increased the default vibrance boost to 3 (from 2)
- Increased the max vibrance boost to 15 (from 10)
- Changed the bloom strength scale and increased its default and maximum values
- Increased the max sky brightness boost to 15 (from 10)
- Added a setting to disable frame timing optimizations used by the Fancy Lighting Engine
- Added a setting to disable dithering

#### Bug Fixes, Compatibility, and Performance
- Rewrote and optimized all of the rendering code
- Fixed graphical glitches that occurred when the mod was disabled when tModLoader was launched and then later enabled
- Fixed bloom and depth of field blur radius not scaling with resolution
- Fixed a minor bug with Ambient Occlusion when using full HDR rendering
- Fixed screen flashing after saving configs when at high resolutions
- Added compatibility with screen flipping from sources other than the Gravitation buff
- Memory is now freed when disabling some features
- Made unloading more thorough

#### Mod.Call() Changes
- Mod.Call() now returns the exception object if an exception occurs (previously, null was returned)
- Added new remarks to the `PostUpdateLightMap` hook
