sampler TextureSampler : register(s0);

sampler LightSampler : register(s0);
sampler WorldSampler : register(s4);
sampler AmbientOcclusionSampler : register(s5);

sampler GlowSampler : register(s4);
sampler LightedGlowSampler : register(s5);

float Gamma;
float ReciprocalGamma;
float TranslucentGlowBrightness;

float OverbrightMult;
float2 NormalMapResolution;
float2 NormalMapRadius;
float NormalMapStrength;
float2 WorldCoordMult;
float2 DitherCoordMult;
float2 AmbientOcclusionCoordMult;
float BrightnessMult;
float2 GlowCoordMult;
float2 LightedGlowCoordMult;

// Gamma correction only applies when both overbright and HiDef are enabled

float Square(float x)
{
    return x * x;
}

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

float Luminance(float3 color)
{
    return Luma(pow(color, Gamma));
}

float3 OverbrightLightAt(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return (255.0 / 128) * color;
}

float3 OverbrightLightAtHiDef(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return color;
}

float3 TranslucentGlowMult(float4 color)
{
    return pow(
        1 + TranslucentGlowBrightness * max(0, Luminance(color) - pow(color.a, Gamma)),
        ReciprocalGamma
    );
}

float3 AmbientOcclusion(float2 coords, float alpha)
{
    return lerp(
        1, tex2D(AmbientOcclusionSampler, coords * AmbientOcclusionCoordMult).rgb, alpha
    );
}

float4 GradientAndMult(
    float horizontalColorDiff,
    float verticalColorDiff,
    float leftAlpha,
    float rightAlpha,
    float upAlpha,
    float downAlpha
)
{
    float2 gradient = float2(horizontalColorDiff, verticalColorDiff);
    gradient *= 0.5;
    float2 mult = float2(leftAlpha * rightAlpha, upAlpha * downAlpha);
    return float4(gradient, mult);
}

// Intentionally use gamma values for simulating normal maps

float4 NormalsSurfaceGradientAndMult(float2 worldTexCoords)
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
    return float4(mult, mult, 1, 1) * GradientAndMult(
        horizontalColorDiff,
        verticalColorDiff,
        left.a,
        right.a,
        up.a,
        down.a
    );
}

float2 NormalsLightGradient(float2 coords)
{
    float3 left = tex2D(LightSampler, coords - float2(NormalMapRadius.x, 0)).rgb;
    float3 right = tex2D(LightSampler, coords + float2(NormalMapRadius.x, 0)).rgb;
    float3 up = tex2D(LightSampler, coords - float2(0, NormalMapRadius.y)).rgb;
    float3 down = tex2D(LightSampler, coords + float2(0, NormalMapRadius.y)).rgb;
    float horizontalDiff = Luma(right) - Luma(left);
    float verticalDiff = Luma(down) - Luma(up);
    return OverbrightMult * float2(horizontalDiff, verticalDiff);
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
    
    float4 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(worldTexCoords);
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0 ? 0 : surfaceGradient / surfaceGradientLength;
    surfaceGradient *= surfaceGradientAndMult.zw;
    
    float lightMult = lerp(1.0, 1.5, dot(lightGradient, surfaceGradient));
    return lerp(
        1.0,
        lightMult,
        pow(surfaceGradientLength, NormalMapStrength)
            * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float3 NormalsColor(float2 coords)
{
    float2 worldTexCoords = WorldCoordMult * coords;
    return NormalsMultiplier(coords, worldTexCoords) * tex2D(LightSampler, coords);
}

float4 NormalsColorOverbright(float2 coords)
{
    float2 worldTexCoords = WorldCoordMult * coords;
    return float4(
        OverbrightLightAt(coords),
        NormalsMultiplier(coords, worldTexCoords)
    );
}

float4 NormalsColorOverbrightHiDef(float2 coords)
{
    float2 worldTexCoords = WorldCoordMult * coords;
    return float4(
        OverbrightLightAtHiDef(coords),
        NormalsMultiplier(coords, worldTexCoords)
    );
}

float4 Normals(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColor(coords), 1);
}

float4 NormalsHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(NormalsColor(coords), 1);
}

float4 NormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbright(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(min(lightColor.rgb, 1) * lightColor.a, 1) * texColor;
}

float4 NormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor) * min(lightColor.rgb, 1) * lightColor.a, 1
    ) * texColor;
}

float4 NormalsOverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbright(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        min(lightColor.rgb, 1) * lightColor.a * AmbientOcclusion(coords, texColor.a), 1
    ) * texColor;
}

float4 NormalsOverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor)
            * min(lightColor.rgb, 1) * lightColor.a
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)),
        1
    ) * texColor;
}

float4 NormalsOverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbright(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(min(lightColor.rgb, 1) * lightColor.a, 1) * texColor.a;
}

float4 NormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor) * min(lightColor.rgb, 1) * lightColor.a,
        1
    ) * texColor.a;
}

float4 NormalsOverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbright(coords);

    return float4(min(lightColor.rgb, 1) * lightColor.a, 1);
}

float4 NormalsOverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor) * min(lightColor.rgb, 1) * lightColor.a,
        1
    );
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbright(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);
    
    return float4(
        min(lightColor.rgb, 1) * lightColor.a * AmbientOcclusion(coords, texColor.a), 1
    );
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 lightColor = NormalsColorOverbrightHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor)
            * min(lightColor.rgb, 1) * lightColor.a
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)),
        1
    );
}

float4 Overbright(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(min(lightColor, 1), 1) * texColor;
}

float4 OverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);
    
    return float4(TranslucentGlowMult(texColor) * min(lightColor, 1), 1) * texColor;
}

float4 OverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(min(lightColor, 1) * AmbientOcclusion(coords, texColor.a), 1) * texColor;
}

float4 OverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor)
            * min(lightColor, 1)
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)),
        1
    ) * texColor;
}

float4 OverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(min(lightColor, 1), 1) * texColor.a;
}

float4 OverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(TranslucentGlowMult(texColor) * min(lightColor, 1), 1) * texColor.a;
}

float4 OverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);

    return float4(min(lightColor, 1), 1);
}

float4 OverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(TranslucentGlowMult(texColor) * min(lightColor, 1), 1);
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(min(lightColor, 1) * AmbientOcclusion(coords, texColor.a), 1);
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(
        TranslucentGlowMult(texColor)
            * min(lightColor, 1)
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)), 
        1
    );
}

float4 OverbrightMax(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(max(lightColor, 1), 1) * texColor;
}

float4 OverbrightMaxHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoordMult * coords);

    return float4(max(lightColor, 1), 1) * texColor;
}

float4 LightOnly(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    return color * tex2D(TextureSampler, coords).a;
}

float4 Brighten(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    color.rgb *= BrightnessMult;
    return color * tex2D(TextureSampler, coords);
}

float4 BrightenTranslucentGlow(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(TextureSampler, coords);
    color.rgb *= TranslucentGlowMult(color);
    return color;
}

float4 GlowMask(float2 coords : TEXCOORD0) : COLOR0
{
    float4 primary = tex2D(TextureSampler, coords);
    float4 glow = tex2D(GlowSampler, GlowCoordMult * coords);
    float4 selector = glow;
    float4 bright = max(primary, glow);
    
    return float4(
        lerp(primary.rgb, bright.rgb, step(2.0 / 255, glow.rgb)),
        bright.a
    );
}

float4 GlowMaskHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 primary = tex2D(TextureSampler, coords);
    float4 glow = tex2D(GlowSampler, GlowCoordMult * coords);
    float4 selector = glow;
    glow.rgb *= TranslucentGlowMult(glow);
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

float4 EnhancedGlowMaskHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float4 primary = tex2D(TextureSampler, coords);
    float4 selector = tex2D(GlowSampler, GlowCoordMult * coords);
    float4 glow = tex2D(LightedGlowSampler, LightedGlowCoordMult * coords);
    glow.rgb *= TranslucentGlowMult(glow);
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

    pass Brighten
    {
        PixelShader = compile ps_3_0 Brighten();
    }

    pass BrightenTranslucentGlow
    {
        PixelShader = compile ps_3_0 BrightenTranslucentGlow();
    }
    
    pass GlowMask
    {
        PixelShader = compile ps_3_0 GlowMask();
    }
    
    pass GlowMaskHiDef
    {
        PixelShader = compile ps_3_0 GlowMaskHiDef();
    }
    
    pass EnhancedGlowMask
    {
        PixelShader = compile ps_3_0 EnhancedGlowMask();
    }
    
    pass EnhancedGlowMaskHiDef
    {
        PixelShader = compile ps_3_0 EnhancedGlowMaskHiDef();
    }
}
