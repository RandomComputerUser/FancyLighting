# The source code of the Fancy Lighting mod for tModLoader 1.4.4

## [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
## [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

### Latest Version

**v0.8.2 (2024-10-28)**
- Tweaked simulated normal maps
- Enhanced shaders and colors no longer uses special normal maps rendering, as it was causing visual issues
- Increased the default normal maps strength from 100 to 150

**v0.8.1.1 (2024-10-28)**
- Tweaked enhanced light map blurring in vines
- Removed an unnecessary file from the build

**v0.8.1 (2024-10-27)**
- Added a new mode for enhanced glow mask support
- Fixed a bug that caused stars in the Aether to flicker when moving with smooth lighting enabled
- Fixed a bug that caused too few stars in the Aether to be drawn with enhanced shaders and colors enabled
- Fixed a camera mode bug that could render the background at an incorrect scale with enhanced shaders and colors enabled
- Tweaked the mod icon

**v0.8.0 (2024-10-27)**
- Split settings into two pages
- The settings preset now only affects settings in the Quality Settings page
- Renamed some settings and presets
- Tweaked some preset settings
- Added a new Extreme preset
- Added warnings when the correct video settings aren't being used
- Gamma correction now uses 2.2 gamma instead of sRGB
- There is now a separate option to use the sRGB transfer function
- Added a setting to control the gamma used by lighting and textures
- Added support for glow masks
- Improved global illumination and enabled it in the Ultra and Extreme presets
- Added a setting to control the brightness of indirect lighting from global illumination
- Added settings to apply overbright lighting to more things
- Added a setting to toggle whether vines and seaweed block light
- Tweaked ambient occlusion when gamma correction is disabled to better match how it looks when gamma correction is enabled
- Increased the range of the ambient occlusion exponent setting
- Tweaked the default ambient occlusion settings
- Tweaked simulated normal maps when gamma correction is disabled to better match how it looks when gamma correction is enabled
- Increased the range of the normal maps strength setting
- Increased the range of the solid block light absorption setting
- Improved dithering with overbright lighting and enhanced shaders and colors enabled
- Increased the max overbright brightness with enhanced shaders and colors enabled from 4x to 16x
- Optimized overbright lighting
- Removed the Brighter Lighting setting, which is now effectively always enabled
- Fixed graphical glitches with shimmer waterfalls
- Fixed camera mode not working correctly with smooth lighting disabled and ambient occlusion enabled
- Fixed camera mode not rendering cave backgrounds correctly
- Reduced the possibility of camera mode crashing due to there being too many tile entities
- Added compatibility with the Pixelated Backgrounds mod (thanks to [Centri3](https://github.com/Centri3))
- Changed the mod icon
- Upgraded to .NET 8