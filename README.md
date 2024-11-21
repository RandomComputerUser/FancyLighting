# The source code of the Fancy Lighting mod for tModLoader

### [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
### [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

[EasyXnb](https://github.com/SuperAndyHero/EasyXnb) is required to build this mod. The EasyXnb config file can be found in the Effects directory.

## Latest Version

**v0.9.0 (2024-11-??)**
- Overhauled HDR rendering (previously called overbright lighting)
- Removed the extreme preset and the settings to apply overbright lighting to different objects, which are now effectively always enabled
- Removed the setting to enable enhanced shaders and colors, which is now included in the enhanced HDR render mode
- Removed the setting to enable light map tone mapping, which is now included in the bicubic and basic HDR render modes
- Added filmic HDR tone mapping
- Added bloom (available in the enhanced HDR render mode)
- Removed the option to disable smooth lighting glow mask support
- The spelunker, dangersense, and biome sight potions no longer use a custom glow effect
- Slightly reduced the strength of simulated normal maps on translucent surfaces
- Fixed a visual issue that sometimes caused water to appear black
- Made some minor optimizations