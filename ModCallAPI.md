# Fancy Lighting Mod.Call() API

If an exception occurs, the exception object is returned. If the call arguments do not match any valid pattern, null is returned. Otherwise, the return value depends on the command.

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
            - `ref Vector3 lightColor`: The light color at the location of the tile, after being multiplied by `Lighting.GlobalBrightness`.
        - **Remarks:** It is highly recommended to avoid having side effects.
- **Call Returns:** `bool`
    - Whether any changes were made.

**Remarks:** If the affected tile type already has custom lighting, it will be replaced. Only one lighting modifier for a tile type can be active at a time. Custom tile lighting affects only how tiles appear to be lit when using Smooth Lighting; there is no effect on any other part of the game. Before adding custom tile lighting, it is recommended to test whether a tile appears differently using Smooth Lighting compared to vanilla lighting. In most cases, custom tile lighting is not needed since Smooth Lighting preserves glow effects.

#### `"AddCustomTileLighting", ushort tileType, void tileLightModifier(Tile, int, int, ref Vector3)`
Same as above, except `tileType` is passed as a ushort.

### AddHook

#### `"AddHook", "PostUpdateLightMap", void hook(Texture2D, Matrix, Rectangle, bool)`
Add a hook that runs after Smooth Lighting updates its light map texture. This exposes the light map texture.

- **Call Parameters:**
    - `void hook(Texture2D, Matrix, Rectangle, bool)`: The hook.
        - **Parameters:**
            - `Texture2D lightMapTexture`: The texture used to sample the light map.
            - `Matrix samplingTransformation`: A transformation matrix that converts world coordinates (in pixels) to UV coordinates for sampling `lightMapTexture`.
            - `Rectangle lightMapArea`: The area of the world covered by the light map, measured in tiles.
            - `bool cameraMode`: Whether the light map is for a camera mode capture.
        - **Remarks:** The dimensions of `lightMapTexture` may not match the dimensions of the light map in tiles. Do not rely on the values in the alpha channel of `lightMapTexture`. The alpha channel may be used by the Fancy Lighting mod for any purpose. Switching to a new render target or doing any drawing in the hook may cause graphical glitches.
- **Call Returns:** `Action`
    - A function that removes the hook. The mod cleans everything up while unloading, so calling this function is not necessary.

#### `"AddHook", "ModifyFancyAtmosphereColors", void hook(ref Vector3, ref Vector3, ref Vector3, ref Vector3)`
Add a hook that modifies the colors used by Fancy Atmosphere.

- **Call Parameters:**
    - `void hook(ref Vector3, ref Vector3, ref Vector3, ref Vector3)`: The hook.
        - **Parameters:**
            - `ref Vector3 lowSkyColor`: The color of the low part of the sky.
            - `ref Vector3 middleSkyColor`: The color of the middle part of the sky.
            - `ref Vector3 highSkyColor`: The color of the high part of the sky.
            - `ref Vector3 skyColorMult`: A color multiplier applied to the entire sky. Typically, this changes based on the biome or weather.
        - **Remarks:** The hook will be run both while on the main menu and while in a world.
- **Call Returns:** `Action`
    - A function that removes the hook. The mod cleans everything up while unloading, so calling this function is not necessary.

#### `"AddHook", "ModifyFancySkyLightShadingStrength", void hook(ref double)`
Add a hook that modifies the strength of Fancy Sky Light Shading.

- **Call Parameters:**
    - `void hook(ref float)`: The hook.
        - **Parameters:**
            - `ref float weatherMultiplier`: The shading strength multiplier due to the weather.
        - **Remarks:** The hook will be run both while on the main menu and while in a world. The hook may not be run at times during dawn and dusk. The hook will be run after the shading strength is modified due to vanilla weather such as rain.
- **Call Returns:** `Action`
    - A function that removes the hook. The mod cleans everything up while unloading, so calling this function is not necessary.

### GetConfigState{_}

#### Example

An example of using these mod calls can be found [on the FancyLightingModCallTest repository](https://github.com/RexBerry/FancyLightingModCallTest/blob/main/FancyLightingConfigStateSystem.cs).
This example is not exhaustive, so to find out what information can be gotten from these mod calls, it is recommended that you inspect the output of these mod calls yourself and/or look at the [ConfigState source code](ModCompatibility/ConfigState.cs).

#### `"GetConfigStateInfo"`
Get information about the current configuration and rendering state.

- **Call Returns:** `int[]`
    - An array of entries of information about configuration and active rendering state. Use the `GetConfigStateInfoMetadataNames` and `GetConfigStateInfoMetadataTypes` calls to get more information about this array.
      - The first entry of this array (index 0) is a dummy value that is always set to an invalid value (-1) not in any enumeration, which can be used as a fallback.
    - **Remarks:** The returned array is global and lasts the lifetime of the mod. All calls will return the same object. Do not modify the array. This call may return null if the Fancy Lighting mod hasn't loaded yet.

#### `"GetConfigStateInfoMetadataNames"`
Get the names of the entries in the configuration and rendering state.

- **Call Returns:** `string[]`
    - An array of the names of the entries in the array returned by `GetConfigStateInfo`. The name of each entry corresponds to what configuration or rendering state that entry is for.
    - **Remarks:** This will return a new object every time.

The names of the entries are (this list may expand in future versions):
- `"Invalid"`
- `"SmoothLightingEnabled"`
- `"SmoothLightingRenderMode"`
- `"SmoothLightingGlowRenderMode"`
- `"SmoothLightingNormalMapsEnabled"`
- `"SmoothLightingNormalMapsNonSolidTilesEnabled"`
- `"SmoothLightingNormalMapsTileEntitiesEnabled"`
- `"AmbientOcclusionEnabled"`
- `"AmbientOcclusionNonSolidTilesEnabled"`
- `"AmbientOcclusionTileEntitiesEnabled"`
- `"FancyLightingEngineEnabled"`
- `"FancyLightingEngineQuality"`
- `"FancyLightingEngineTemporalOptimizationsEnabled"`
- `"FancyLightingEngineGlobalIlluminationEnabled"`
- `"FancyAtmosphereEnabled"`
- `"FancySkyLightColorsEnabled"`
- `"FancySkyLightShadingEnabled"`
- `"SmoothLightingActiveLightMapOverride"`
- `"AmbientOcclusionActiveRenderState"`
- `"RenderingOptimizationsEnabled"`

#### `"GetConfigStateInfoMetadataTypes"`
Get the types of the entries in the configuration and rendering state.

- **Call Returns:** `string[]`
    - An array of the names of the types of the entries in the array returned by `GetConfigStateInfo`. Use the `GetConfigStateTypeMetadata` call to get more information about these types.
    - **Remarks:** This will return a new object every time.

#### `"GetConfigStateTypeMetadata"`
Get metadata about the enumeration types used in the configuration and rendering state.

- **Call Returns:** `Dictionary<string, string[]>`
    - A dictionary containing metadata about the enumeration types used for configuration and rendering state.
        - The keys correspond to the names of the type.
        - The values correspond to the names of the enumeration values.
    - **Remarks:** This will return a new object every time.

### ForceEnableCompatibilitySetting

#### `"ForceEnableCompatibilitySetting", string settingName`
Force a particular compatibility setting to be enabled.

- **Call Parameters:**
    - `string settingName`: The name of the setting to force enable.
- **Call Returns:** `bool`
    - Whether `settingName` is a valid setting and was thus force enabled.

**Remarks:** This should be called during `Load()`, or else there may not be an effect.

Valid values of `settingName` are (this list may expand in future versions):
- `"DisableRenderingOptimizations"`
- `"DisableMainMenuCapture"`
- `"DisableSmoothLightingDuringBossFights"`
- `"UseAccurateGlowRendering"`
- `"DisableHdrEnhancedAlphaBlending"`
- `"DisableHdrLightingSync"`
- `"DisableFrameTimingOptimizations"`
- `"DisableSunAndMoonShaders"`

### RemoveCustomTileLighting

#### `"RemoveCustomTileLighting", int tileType`
Remove custom lighting for a particular tile type when using Smooth Lighting.

- **Call Parameters:**
    - `int tileType`: The affected tile type.
- **Call Returns:** `bool`
    - Whether any changes were made.

#### `"RemoveCustomTileLighting", ushort tileType`
Same as above, except `tileType` is passed as a ushort.