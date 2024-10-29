sampler TextureSampler : register(s0);

sampler LightSampler : register(s0);
sampler WorldSampler : register(s4);
sampler DitherSampler : register(s5);
sampler AmbientOcclusionSampler : register(s6);

sampler GlowSampler : register(s4);
sampler LightedGlowSampler : register(s5);

float2 NormalMapResolution;
float2 NormalMapRadius;
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
    return pow(color, 2.2);
}

float3 LinearToGamma(float3 color)
{
    return pow(color, 1 / 2.2);
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

// Input color should be in 2.2 gamma
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
    float3 horizontalColorDiff,
    float3 verticalColorDiff,
    float leftAlpha,
    float rightAlpha,
    float upAlpha,
    float downAlpha
)
{
    float horizontal = dot(horizontalColorDiff, 1);
    float vertical = dot(verticalColorDiff, 1);
    float2 gradient = float2(horizontal, vertical);

    gradient *= float2(
        (leftAlpha * rightAlpha) * (1 / (2.0 * 3)),
        (upAlpha * downAlpha) * (1 / (2.0 * 3))
    );

    return gradient;
}

// Intentionally use gamma values for simulating normal maps

float2 NormalsGradientBase(float2 worldTexCoords)
{
    float4 left = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, 0));
    float4 right = tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, 0));
    float4 up = tex2D(WorldSampler, worldTexCoords - float2(0, NormalMapResolution.y));
    float4 down = tex2D(WorldSampler, worldTexCoords + float2(0, NormalMapResolution.y));
    float3 positiveDiagonal
        = tex2D(WorldSampler, worldTexCoords - NormalMapResolution).rgb // up left
        - tex2D(WorldSampler, worldTexCoords + NormalMapResolution).rgb; // down right
    float3 negativeDiagonal
        = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb // down left
        - tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb; // up right

    float3 horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (left.rgb - right.rgb);
    float3 verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (up.rgb - down.rgb);

    return Gradient(horizontalColorDiff, verticalColorDiff, left.a, right.a, up.a, down.a);
}

float2 NormalsGradient(float2 worldTexCoords)
{
    float2 gradient = NormalsGradientBase(worldTexCoords);

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 25.0 - dot(color, 25.0 / 3.0);

    gradient = sign(gradient) * (1.0 - rsqrt(abs(multiplier * gradient) + 1.0));

    return gradient * NormalMapRadius;
}

float3 NormalsColorHiDef(float2 coords, float2 worldTexCoords)
{
    return tex2D(LightSampler, coords + NormalsGradient(worldTexCoords));
}

float3 NormalsColorOverbrightHiDef(float2 coords, float2 worldTexCoords)
{
    return OverbrightLightAtHiDef(coords + NormalsGradient(worldTexCoords));
}

float4 Normals(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(WorldCoordMult * coords);

    return float4(tex2D(LightSampler, coords + gradient).rgb, 1);
}

float4 NormalsHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorHiDef(coords, WorldCoordMult * coords);

    return float4(Dither(color, coords), 1);
}

float4 NormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(WorldCoordMult * coords);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 NormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = NormalsColorOverbrightHiDef(coords, WorldCoordMult * coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(Dither(lightColor * texColor.rgb, coords), texColor.a);
}

float4 NormalsOverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(WorldCoordMult * coords);

    return float4(OverbrightLightAt(coords + gradient) * AmbientOcclusion(coords), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords);
}

float4 NormalsOverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = NormalsColorOverbrightHiDef(coords, WorldCoordMult * coords);
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
    float2 gradient = NormalsGradient(WorldCoordMult * coords);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WorldCoordMult * coords).a;
}

float4 NormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = NormalsColorOverbrightHiDef(coords, WorldCoordMult * coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords).a;

    return float4(Dither(lightColor * texColor.a, coords), texColor.a);
}

float4 NormalsOverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(WorldCoordMult * coords);

    return float4(OverbrightLightAt(coords + gradient), 1);
}

float4 NormalsOverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WorldCoordMult * coords);

    return float4(Dither(color, coords), 1);
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(WorldCoordMult * coords);

    return float4(
        OverbrightLightAt(coords + gradient)
            * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WorldCoordMult * coords).a),
        1
    );
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WorldCoordMult * coords);

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
        PixelShader = compile ps_2_0 Normals();
    }

    pass NormalsHiDef
    {
        PixelShader = compile ps_3_0 NormalsHiDef();
    }

    pass NormalsOverbright
    {
        PixelShader = compile ps_2_0 NormalsOverbright();
    }

    pass NormalsOverbrightHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightHiDef();
    }

    pass NormalsOverbrightAmbientOcclusion
    {
        PixelShader = compile ps_2_0 NormalsOverbrightAmbientOcclusion();
    }

    pass NormalsOverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightAmbientOcclusionHiDef();
    }

    pass NormalsOverbrightLightOnly
    {
        PixelShader = compile ps_2_0 NormalsOverbrightLightOnly();
    }

    pass NormalsOverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyHiDef();
    }

    pass NormalsOverbrightLightOnlyOpaque
    {
        PixelShader = compile ps_2_0 NormalsOverbrightLightOnlyOpaque();
    }

    pass NormalsOverbrightLightOnlyOpaqueHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueHiDef();
    }

    pass NormalsOverbrightLightOnlyOpaqueAmbientOcclusion
    {
        PixelShader = compile ps_2_0 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion();
    }

    pass NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef();
    }

    pass Overbright
    {
        PixelShader = compile ps_2_0 Overbright();
    }

    pass OverbrightHiDef
    {
        PixelShader = compile ps_3_0 OverbrightHiDef();
    }

    pass OverbrightAmbientOcclusion
    {
        PixelShader = compile ps_2_0 OverbrightAmbientOcclusion();
    }

    pass OverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 OverbrightAmbientOcclusionHiDef();
    }

    pass OverbrightLightOnly
    {
        PixelShader = compile ps_2_0 OverbrightLightOnly();
    }

    pass OverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyHiDef();
    }

    pass OverbrightLightOnlyOpaque
    {
        PixelShader = compile ps_2_0 OverbrightLightOnlyOpaque();
    }

    pass OverbrightLightOnlyOpaqueHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueHiDef();
    }

    pass OverbrightLightOnlyOpaqueAmbientOcclusion
    {
        PixelShader = compile ps_2_0 OverbrightLightOnlyOpaqueAmbientOcclusion();
    }

    pass OverbrightLightOnlyOpaqueAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef();
    }

    pass OverbrightMax
    {
        PixelShader = compile ps_2_0 OverbrightMax();
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
        PixelShader = compile ps_2_0 GlowMask();
    }
    
    pass EnhancedGlowMask
    {
        PixelShader = compile ps_2_0 EnhancedGlowMask();
    }
}
