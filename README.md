# The source code of the Fancy Lighting mod for tModLoader

### [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2822950837)
### [Terraria Community Forums Thread](https://forums.terraria.org/index.php?threads/fancy-lighting-mod.113067/)

This mod is not endorsed by the creators of either Terraria or tModLoader.

[EasyXnb](https://github.com/SuperAndyHero/EasyXnb) is required to build this mod. The EasyXnb config file can be found in the Effects directory.

## Mod.Call() API

If the call arguments do not match any valid pattern or an exception occurs, null is returned. Otherwise, the return value depends on the command.

### AddCustomTileLighting

#### `"AddCustomTileLighting", int tileType, void tileLightModifier(Tile, int, int, ref Vector3)`
Add custom lighting for a particular tile type when using Smooth Lighting.

- **Call Parameters:**
  - `int tileType`: The affected tile type.
  - `void tileLightModifier(Tile, int, int, ref Vector3)`: The function that modifies the tile's light color.
    - **Parameters:**
      - `Tile tile`: The affected tile.
      - `int x`: The x-coordinate of the tile.
      - `int y`: The y-coordinate of the tile.
      - `ref Vector3 lightColor`: The light color at the location of the tile, after global brightness is applied.
    - **Remarks:** It is highly recommended to avoid having side effects.
- **Call Returns:** `bool`
  - Whether any changes were made.

**Remarks:** Custom tile lighting affects only how tiles appear to be lit when using Smooth Lighting; there is no effect on any other part of the game. Before adding custom tile lighting, it is recommended to test whether a tile appears differently using Smooth Lighting compared to vanilla lighting. In most cases, custom tile lighting is not needed since Smooth Lighting preserves glow effects.

#### `"AddCustomTileLighting", ushort tileType, void tileLightModifier(Tile, int, int, ref Vector3)`
Same as above, except `tileType` is passed as a ushort.

### AddHook

#### `"AddHook", "PostUpdateLightMap", void hook(Texture2D, Matrix, Rectangle, bool)`
Add a hook that runs after Smooth Lighting updates its light map texture. This exposes the light map texture.

- **Call Parameters:**
  - `void hook(Texture2D, Matrix, Rectangle, bool)`: The hook.
    - **Parameters:**
      - `Vector2D lightMapTexture`: The texture used to sample the light map.
      - `Matrix samplingTransformation`: A transformation matrix that converts world coordinates (in pixels) to normalized coordinates for sampling `lightMapTexture`.
      - `Rectangle lightMapArea`: The area of the world covered by the light map, measured in tiles.
      - `bool cameraMode`: Whether the light map is for a camera mode capture.
    - **Remarks:** The dimensions of `lightMapTexture` may not match the dimensions of the light map in tiles.
- **Call Returns:** `Action`
  - A function that removes the hook. The mod cleans everything up while unloading, so calling this function is not necessary.
            
#### `"AddHook", "PreDrawSky", void hook(ref Vector3, ref Vector3, ref Vector3)`
Add a hook that runs before Fancy Atmosphere draws the sky. This allows the color of the sky to be modified.

- **Call Parameters:**
  - `void hook(ref Vector3, ref Vector3, ref Vector3)`: The hook.
    - **Parameters:**
      - `ref Vector3 highSkyColor`: The color of the high part of the sky.
      - `ref Vector3 lowSkyColor`: The color of the low part of the sky.
      - `ref Vector3 skyColorMult`: A color multiplier applied to the entire sky. Typically, this changes based on the biome.
    - **Remarks:** The hook will be run both while on the main menu and while in a world.
- **Call Returns:** `Action`
  - A function that removes the hook. The mod cleans everything up while unloading, so calling this function is not necessary.

### RemoveCustomTileLighting

#### `"RemoveCustomTileLighting", int tileType`
Remove custom lighting for a particular tile type when using Smooth Lighting.

- **Call Parameters:**
    - `int tileType`: The affected tile type.
- **Call Returns:** `bool`
    - Whether any changes were made.

#### `"RemoveCustomTileLighting", ushort tileType`
Same as above, except `tileType` is passed as a ushort.

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
- Improved Ambient Occlusion, making it subtler
- Increased the default ambient light proportion to 90% (from 60%) to partly compensate for the previous change
- Decreased the default normal maps strength to 3 (from 4)
- Slightly improved the quality of the bicubic render mode
- The gamma setting now adjusts output gamma (instead of content gamma)
- Gamma and sRGB output no longer affect camera mode
- Increased the range of the gamma setting to 140–300% (from 160–280%)
- Increased the maximum normal maps strength to 15 (from 12)
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
- Fix a bug that caused Ambient Occlusion to not update immediately after saving configs
- Fixed some potential graphical glitches caused by Fancy Atmosphere and Smooth Lighting
- Made various minor optimizations
- Slightly improved frame time consistency when using the Fancy Lighting Engine
- Made Smooth Lighting compatible with the custom water lighting in Spirit Reforged
- Fixed a graphical glitch that occurred in the Sunken Sea biome in Calamity Mod
- Made more classes and methods public
- Added a Mod.Call() API for other mods to use
- Added new commands `"AddCustomTileLighting"`, `"AddHook"`, and `"RemoveCustomTileLighting"` to the Mod.Call() API
- Added a new event `PreDrawSky` in the FancySkyRendering class, which can be accessed via the `"AddHook"` command in the Mod.Call() API
- Added a new event `PostUpdateLightMap` in the SmoothLighting class, which can be accessed via the `"AddHook"` command in the Mod.Call() API
- Updated the mod icon and description
