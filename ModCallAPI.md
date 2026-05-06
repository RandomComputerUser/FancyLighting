# Fancy Lighting Mod.Call() API

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