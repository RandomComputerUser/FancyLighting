from code_generator import Effect, EffectFeatures, EffectFlag, generate_effect_code

normals = EffectFlag("Normals", "Smooth")
dithered = EffectFlag("Dithered")
enhanced_glow = EffectFlag("EnhancedGlow")
ambient_occlusion = EffectFlag("AmbientOcclusion")
fancy_sky = EffectFlag("FancySky")
opaque = EffectFlag("Opaque")

# HiDef feature only matters when using ambient occlusion
# HiDef is never used when using dithering
# Light-only is never used when using enhanced glow

effect_code = generate_effect_code(
    "FullscreenEffect",
    "SmoothLighting_VS",
    "SmoothLighting",
    "PixelShaderInput",
    ["Position", "TileTexCoord", "LightTexCoord"],
    [normals, dithered, enhanced_glow, fancy_sky, ambient_occlusion, opaque],
    [
        Effect(set(), EffectFeatures.LightOnly()),
        Effect({ambient_occlusion}, EffectFeatures.All()),
        Effect({opaque}, EffectFeatures.LIGHT_ONLY),
        Effect({enhanced_glow}),
        Effect({enhanced_glow, ambient_occlusion}, EffectFeatures.HiDef()),
        Effect({dithered}, EffectFeatures.LightOnly()),
        Effect({dithered, ambient_occlusion}, EffectFeatures.LightOnly()),
        Effect({dithered, opaque}, EffectFeatures.LIGHT_ONLY),
        Effect({dithered, enhanced_glow}),
        Effect({dithered, enhanced_glow, ambient_occlusion}),
        Effect({normals}, EffectFeatures.LightOnly()),
        Effect({normals, ambient_occlusion}, EffectFeatures.All()),
        Effect({normals, opaque}, EffectFeatures.LIGHT_ONLY),
        Effect({normals, fancy_sky}, EffectFeatures.LightOnly()),
        Effect({normals, enhanced_glow}),
        Effect({normals, enhanced_glow, ambient_occlusion}, EffectFeatures.HiDef()),
        Effect({normals, enhanced_glow, fancy_sky}),
        Effect({normals, dithered}, EffectFeatures.LightOnly()),
        Effect({normals, dithered, ambient_occlusion}, EffectFeatures.LightOnly()),
        Effect({normals, dithered, opaque}, EffectFeatures.LIGHT_ONLY),
        Effect({normals, dithered, fancy_sky}, EffectFeatures.LightOnly()),
        Effect({normals, dithered, enhanced_glow}),
        Effect({normals, dithered, enhanced_glow, ambient_occlusion}),
        Effect({normals, dithered, enhanced_glow, fancy_sky}),
    ],
)

print()
print()
print(effect_code.hlsl)
print()
print()

print()
print()
print(effect_code.declarations)
print()
print()

print()
print()
print(effect_code.initializers)
print()
print()

print()
print()
print(effect_code.selector)
print()
print()
