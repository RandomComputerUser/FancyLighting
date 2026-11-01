# The source code of the Fancy Lighting mod for tModLoader

### [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
### [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

[EasyXnb](https://github.com/SuperAndyHero/EasyXnb) is required to build this mod. The EasyXnb config file can be found in the Effects directory.

## Mod.Call() API

View the [Mod.Call() API documentation](ModCallAPI.md) for mod developers.

## Latest Version

### v1.2.5 (2026-11-01)
- Added a setting to control the brightness of highlights when using full HDR rendering
- Tweaked how highlights appear by default when using full HDR rendering
- Replaced the Vivid tone mapping operator with Filmic
- Improved how semitransparent tile entities are rendered when using Smooth Lighting
  - This fixes a bug where blooming fireblossoms had the wrong color
