sampler TextureSampler : register(s0);

sampler LightSampler : register(s0);
sampler WorldSampler : register(s4);
sampler DitherSampler : register(s5);
sampler AmbientOcclusionSampler : register(s6);

sampler GlowSampler : register(s4);
sampler LightedGlowSampler : register(s5);

float Gamma;
float ReciprocalGamma;

float OverbrightMult;
float2 NormalMapResolution;
float2 NormalMapRadius;
float NormalMapStrength;
float2 WorldCoordMult;
float2 DitherCoordMult;
float2 AmbientOcclusionCoordMult;
float BackgroundBrightnessMult;
float2 GlowCoordMult;
float2 LightedGlowCoordMult;

// Gamma correction only applies when both overbright and HiDef are enabled

#define MIN_PREMULTIPLIER (0.5 / 255)

float3 GammaToLinear(float3 color)
{
    return pow(color, Gamma);
}

float3 LinearToGamma(float3 color)
{
    return pow(color, ReciprocalGamma);
}

float Luma(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float3 OverbrightLightAt(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return (255.0 / 128) * color;
}

float3 OverbrightLightAtHiDef(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return (65535.0 / 4096) * color;
}

// Input color should be in gamma space
float3 Dither(float3 color, float2 coords)
{
    float3 lo = (1.0 / 255) * floor(255 * color);
    float3 hi = lo + 1.0 / 255;
    float3 loLinear = GammaToLinear(lo);
    float3 hiLinear = GammaToLinear(hi);

    float3 t = (GammaToLinear(color) - loLinear) / (hiLinear - loLinear);
    float rand = (255.0 / 256) * tex2D(DitherSampler, DitherCoordMult * coords).r;
    float3 selector = step(t, rand);

    return lerp(hi, lo, selector);
}

float3 AmbientOcclusion(float2 coords)
{
    return tex2D(AmbientOcclusionSampler, coords * AmbientOcclusionCoordMult).rgb;
}

float2 Gradient(
    float horizontalColorDiff,
    float verticalColorDiff,
    float leftAlpha,
    float rightAlpha,
    float upAlpha,
    float downAlpha
)
{
    float2 gradient = float2(horizontalColorDiff, verticalColorDiff);

    gradient *= float2(
        (leftAlpha * rightAlpha) * 0.5,
        (upAlpha * downAlpha) * 0.5
    );

    return gradient;
}

// Intentionally use gamma values for simulating normal maps

float2 NormalsSurfaceGradient(float2 worldTexCoords)
{
    float4 left = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, 0));
    float4 right = tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, 0));
    float4 up = tex2D(WorldSampler, worldTexCoords - float2(0, NormalMapResolution.y));
    float4 down = tex2D(WorldSampler, worldTexCoords + float2(0, NormalMapResolution.y));
    float leftLuma = Luma(left.rgb);
    float rightLuma = Luma(right.rgb);
    float upLuma = Luma(up.rgb);
    float downLuma = Luma(down.rgb);
    float positiveDiagonal
        = Luma(tex2D(WorldSampler, worldTexCoords - NormalMapResolution).rgb) // up left
        - Luma(tex2D(WorldSampler, worldTexCoords + NormalMapResolution).rgb); // down right
    float negativeDiagonal
        = Luma(tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb) // down left
        - Luma(tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb); // up right

    float horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float luma = Luma(tex2D(WorldSampler, worldTexCoords).rgb);
    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return mult * Gradient(
        horizontalColorDiff, verticalColorDiff, left.a, right.a, up.a, down.a
    );
}

float2 NormalsLightGradient(float2 coords)
{
    float3 left = OverbrightMult * tex2D(LightSampler, coords - float2(NormalMapRadius.x, 0)).rgb;
    float3 right = OverbrightMult * tex2D(LightSampler, coords + float2(NormalMapRadius.x, 0)).rgb;
    float3 up = OverbrightMult * tex2D(LightSampler, coords - float2(0, NormalMapRadius.y)).rgb;
    float3 down = OverbrightMult * tex2D(LightSampler, coords + float2(0, NormalMapRadius.y)).rgb;
    float horizontalDiff = Luma(right) - Luma(left);
    float verticalDiff = Luma(down) - Luma(up);
    return float2(horizontalDiff, verticalDiff);
}

float NormalsMultiplier(float2 coords, float2 worldTexCoords)
{
    float2 lightGradient = NormalsLightGradient(coords);
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float2 surfaceGradient = NormalsSurfaceGradient(worldTexCoords);
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0 ? 0 : surfaceGradient / surfaceGradientLength;
    
    float lightMult = lerp(1.0, 1.5, dot(lightGradient, surfaceGradient));
    return lerp(
        1.0,
        lightMult,
        pow(surfaceGradientLength, NormalMapStrength)
            * pow(1 - 1.0 / (16.0 * lightGradientLength + 1), 2)
    );
}

float3 NormalsColor(float2 coords)
{
    float2 worldTexCoords = WorldCoordMult * coords;
    return NormalsMultiplier(coords, worldTexCoords) * tex2D(LightSampler, coords);
}

float3 NormalsColorOverbright(float2 coords)
{
    float2 worldTexCoords = WorldCoordMult * coords;
    return NormalsMultiplier(coords, worldTexCoords) * OverbrightLightAt(coords);
}

float3 NormalsColorOverbrightHiDef(float2 coords)
{
    float2 worldTexCoords = WorldCoordMult * coords;
    return NormalsMultiplier(coords, worldTexCoords) * OverbrightLightAtHiDef(coords);
}

float4 Normals(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColor(coords), 1);
}

float4 NormalsHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(Dither(NormalsColor(coords), coords), 1);
}

float4 NormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColorOverbright(coords), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 NormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(Dither(lightColor * texColor.rgb, coords), texColor.a);
}

float4 NormalsOverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColorOverbright(coords) * AmbientOcclusion(coords), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 NormalsOverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        Dither(
            lightColor * LinearToGamma(AmbientOcclusion(coords)) * texColor.rgb,
            coords
        ),
        texColor.a
    );
}

float4 NormalsOverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColorOverbright(coords), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords).a;
}

float4 NormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(Dither(lightColor * texColor.a, coords), texColor.a);
}

float4 NormalsOverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColorOverbright(coords), 1);
}

float4 NormalsOverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(Dither(NormalsColorOverbrightHiDef(coords), coords), 1);
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        NormalsColorOverbright(coords)
            * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WorldCoordMult * coords).a),
        1
    );
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords);

    return float4(
        Dither(
            color * LinearToGamma(lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WorldCoordMult * coords).a)),
            coords
        ),
        1
    );
}

float4 Overbright(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1) * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 OverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(WorldSampler, WorldCoordMult * coords);
    return float4(Dither(OverbrightLightAtHiDef(coords) * color.rgb, coords), color.a);
}

float4 OverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords) * AmbientOcclusion(coords), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 OverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        Dither(
            OverbrightLightAtHiDef(coords) * LinearToGamma(AmbientOcclusion(coords)) * color.rgb,
            coords
        ),
        color.a
    );
}

float4 OverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1) * tex2D(WorldSampler, WorldCoordMult * coords).a;
}

float4 OverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(Dither(OverbrightLightAtHiDef(coords) * color.a, coords), color.a);
}

float4 OverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1);
}

float4 OverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(Dither(OverbrightLightAtHiDef(coords), coords), 1);
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        OverbrightLightAt(coords)
            * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WorldCoordMult * coords).a),
        1
    );
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = OverbrightLightAtHiDef(coords);

    return float4(
        Dither(
            color * LinearToGamma(lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WorldCoordMult * coords).a)),
            coords
        ),
        1
    );
}

float4 OverbrightMax(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(max(OverbrightLightAt(coords), 1), 1) * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 OverbrightMaxHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        Dither(
            max(OverbrightLightAtHiDef(coords), 1) * color.rgb,
            coords
        ),
        color.a
    );
}

float4 LightOnly(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    return color * tex2D(TextureSampler, coords).a;
}

float4 BrightenBackground(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    color.rgb *= BackgroundBrightnessMult;
    return color * tex2D(TextureSampler, coords);
}

float4 GlowMask(float2 coords : TEXCOORD0) : COLOR0
{
    float4 primary = tex2D(TextureSampler, coords);
    float4 glow = tex2D(GlowSampler, GlowCoordMult * coords);
    float4 bright = max(primary, glow);
    
    return float4(
        lerp(primary.rgb, bright.rgb, step(2.0 / 255, glow.rgb)),
        bright.a
    );
}

float4 EnhancedGlowMask(float2 coords : TEXCOORD0) : COLOR0
{
    float4 primary = tex2D(TextureSampler, coords);
    float4 selector = tex2D(GlowSampler, GlowCoordMult * coords);
    float4 glow = tex2D(LightedGlowSampler, LightedGlowCoordMult * coords);
    float4 bright = max(primary, glow);
    
    return float4(
        lerp(primary.rgb, bright.rgb, step(2.0 / 255, selector.rgb)),
        bright.a
    );
}

technique Technique1
{
    pass Normals
    {
        PixelShader = compile ps_3_0 Normals();
    }

    pass NormalsHiDef
    {
        PixelShader = compile ps_3_0 NormalsHiDef();
    }

    pass NormalsOverbright
    {
        PixelShader = compile ps_3_0 NormalsOverbright();
    }

    pass NormalsOverbrightHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightHiDef();
    }

    pass NormalsOverbrightAmbientOcclusion
    {
        PixelShader = compile ps_3_0 NormalsOverbrightAmbientOcclusion();
    }

    pass NormalsOverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightAmbientOcclusionHiDef();
    }

    pass NormalsOverbrightLightOnly
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnly();
    }

    pass NormalsOverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyHiDef();
    }

    pass NormalsOverbrightLightOnlyOpaque
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaque();
    }

    pass NormalsOverbrightLightOnlyOpaqueHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueHiDef();
    }

    pass NormalsOverbrightLightOnlyOpaqueAmbientOcclusion
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion();
    }

    pass NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef();
    }

    pass Overbright
    {
        PixelShader = compile ps_3_0 Overbright();
    }

    pass OverbrightHiDef
    {
        PixelShader = compile ps_3_0 OverbrightHiDef();
    }

    pass OverbrightAmbientOcclusion
    {
        PixelShader = compile ps_3_0 OverbrightAmbientOcclusion();
    }

    pass OverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 OverbrightAmbientOcclusionHiDef();
    }

    pass OverbrightLightOnly
    {
        PixelShader = compile ps_3_0 OverbrightLightOnly();
    }

    pass OverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyHiDef();
    }

    pass OverbrightLightOnlyOpaque
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaque();
    }

    pass OverbrightLightOnlyOpaqueHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueHiDef();
    }

    pass OverbrightLightOnlyOpaqueAmbientOcclusion
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueAmbientOcclusion();
    }

    pass OverbrightLightOnlyOpaqueAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef();
    }

    pass OverbrightMax
    {
        PixelShader = compile ps_3_0 OverbrightMax();
    }

    pass OverbrightMaxHiDef
    {
        PixelShader = compile ps_3_0 OverbrightMaxHiDef();
    }

    pass LightOnly
    {
        PixelShader = compile ps_3_0 LightOnly();
    }

    pass BrightenBackground
    {
        PixelShader = compile ps_3_0 BrightenBackground();
    }
    
    pass GlowMask
    {
        PixelShader = compile ps_3_0 GlowMask();
    }
    
    pass EnhancedGlowMask
    {
        PixelShader = compile ps_3_0 EnhancedGlowMask();
    }
}
