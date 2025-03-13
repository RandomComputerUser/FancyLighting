sampler TextureSampler : register(s0);

sampler LightSampler : register(s0);
sampler WorldSampler : register(s4);
sampler AmbientOcclusionSampler : register(s5);
sampler DitherSampler : register(s6);

sampler GlowSampler : register(s4);
sampler LightedGlowSampler : register(s5);

float ReciprocalGamma;

float2 NormalMapResolution;
float2 NormalMapGradientMult;
float NormalMapStrength;
float2 WorldCoordMult;
float2 WorldCoordOffset;
float2 DitherCoordMult;
float BrightnessMult;
float2 GlowCoordMult;
float2 LightedGlowCoordMult;

// Gamma correction only applies when both overbright and HiDef are enabled

float Square(float x)
{
    return x * x;
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
    return color;
}

float3 OverbrightLightAtHiDef(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return color;
}

float2 WorldCoords(float2 lightMapCoords)
{
    return WorldCoordMult * lightMapCoords.yx + WorldCoordOffset;
}

float AmbientOcclusion(float2 coords, float alpha)
{
    alpha = saturate(alpha);
    return lerp(
        1, tex2D(AmbientOcclusionSampler, WorldCoords(coords)).a, alpha
    );
}

// Not technically correct because it ignores gamma, but cheap and decent quality
float4 Dithered(float4 color, float2 coords)
{
    float noise
        = (255.0 / 256 / 255) * tex2D(DitherSampler, coords * DitherCoordMult).r
        - 0.5 / 255;
    
    color.r += noise;
    color.g += noise;
    color.b += noise;
    
    return color;
}

float2 Gradient(
    float horizontalColorDiff,
    float verticalColorDiff
)
{
    float2 gradient = float2(horizontalColorDiff, verticalColorDiff);
    gradient *= 0.5;
    return gradient;
}

// Intentionally use gamma values for simulating normal maps

float3 NormalsSurfaceGradientAndMult(float2 worldTexCoords)
{
    float leftLuma
        = Luma(tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, 0)).rgb);
    float rightLuma
        = Luma(tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, 0)).rgb);
    float upLuma
        = Luma(tex2D(WorldSampler, worldTexCoords - float2(0, NormalMapResolution.y)).rgb);
    float downLuma
        = Luma(tex2D(WorldSampler, worldTexCoords + float2(0, NormalMapResolution.y)).rgb);
    float positiveDiagonal
        = Luma(tex2D(WorldSampler, worldTexCoords - NormalMapResolution).rgb) // up left
        - Luma(tex2D(WorldSampler, worldTexCoords + NormalMapResolution).rgb); // down right
    float negativeDiagonal
        = Luma(tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb) // down left
        - Luma(tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb); // up right

    float horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float4 color = tex2D(WorldSampler, worldTexCoords);
    float luma = Luma(color.rgb);
    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return float3(
        mult * Gradient(horizontalColorDiff, verticalColorDiff),
        color.a < 1.0 ? 0.0 : 1.0
    );
}

float2 NormalsLightGradient(float2 coords)
{
    float3 light = tex2D(LightSampler, coords).rgb;
    float luma = Luma(light);
    return NormalMapGradientMult * float2(ddx(luma), ddy(luma));
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
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(worldTexCoords);
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0 ? 0 : surfaceGradient / surfaceGradientLength;
    surfaceGradient *= surfaceGradientAndMult.z;
    
    float lightMult = lerp(1.0, 1.5, dot(lightGradient, surfaceGradient));
    return lerp(
        1.0,
        lightMult,
        pow(surfaceGradientLength, NormalMapStrength)
            * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float3 NormalsColor(float2 coords, float2 worldTexCoords)
{
    return NormalsMultiplier(coords, worldTexCoords) * tex2D(LightSampler, coords);
}

float4 NormalsColorOverbright(float2 coords, float2 worldTexCoords)
{
    return float4(
        OverbrightLightAt(coords),
        NormalsMultiplier(coords, worldTexCoords)
    );
}

float4 NormalsColorOverbrightHiDef(float2 coords, float2 worldTexCoords)
{
    return float4(
        OverbrightLightAtHiDef(coords),
        NormalsMultiplier(coords, worldTexCoords)
    );
}

float4 Normals(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    
    return float4(NormalsColor(coords, worldTexCoords), 1);
}

float4 NormalsHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    
    return float4(NormalsColor(coords, worldTexCoords), 1);
}

float4 NormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbright(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return Dithered(
        float4(min(lightColor.rgb, 1) * lightColor.a, 1) * texColor,
        coords
    );
}

float4 NormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbrightHiDef(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return float4(min(lightColor.rgb, 1) * lightColor.a, 1) * texColor;
}

float4 NormalsOverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbright(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return Dithered(
        float4(
            min(lightColor.rgb, 1) * lightColor.a * AmbientOcclusion(coords, texColor.a),
            1
        ) * texColor,
        coords
    );
}

float4 NormalsOverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbrightHiDef(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return float4(
        min(lightColor.rgb, 1) * lightColor.a
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)),
        1
    ) * texColor;
}

float4 NormalsOverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbright(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return Dithered(
        float4(min(lightColor.rgb, 1) * lightColor.a, 1) * texColor.a,
        coords
    );
}

float4 NormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbrightHiDef(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return float4(min(lightColor.rgb, 1) * lightColor.a, 1) * texColor.a;
}

float4 NormalsOverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbright(coords, worldTexCoords);

    return Dithered(
        float4(min(lightColor.rgb, 1) * lightColor.a, 1),
        coords
    );
}

float4 NormalsOverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbrightHiDef(coords, worldTexCoords);

    return float4(min(lightColor.rgb, 1) * lightColor.a, 1);
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbright(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);
    
    return Dithered(
        float4(
            min(lightColor.rgb, 1) * lightColor.a * AmbientOcclusion(coords, texColor.a),
            1
        ),
        coords
    );
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = WorldCoords(coords);
    float4 lightColor = NormalsColorOverbrightHiDef(coords, worldTexCoords);
    float4 texColor = tex2D(WorldSampler, worldTexCoords);

    return float4(
        min(lightColor.rgb, 1) * lightColor.a
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)),
        1
    );
}

float4 Overbright(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return Dithered(
        float4(min(lightColor, 1), 1) * texColor,
        coords
    );
}

float4 OverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));
    
    return float4(min(lightColor, 1), 1) * texColor;
}

float4 OverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return Dithered(
        float4(min(lightColor, 1) * AmbientOcclusion(coords, texColor.a), 1) * texColor,
        coords
    );
}

float4 OverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return float4(
        min(lightColor, 1)
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)),
        1
    ) * texColor;
}

float4 OverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return Dithered(
        float4(min(lightColor, 1), 1) * texColor.a,
        coords
    );
}

float4 OverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return float4(min(lightColor, 1), 1) * texColor.a;
}

float4 OverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);

    return Dithered(
        float4(min(lightColor, 1), 1),
        coords
    );
}

float4 OverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);

    return float4(min(lightColor, 1), 1);
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return Dithered(
        float4(min(lightColor, 1) * AmbientOcclusion(coords, texColor.a), 1),
        coords
    );
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return float4(
        min(lightColor, 1)
            * LinearToGamma(AmbientOcclusion(coords, texColor.a)), 
        1
    );
}

float4 OverbrightMax(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAt(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return Dithered(
        float4(max(lightColor, 1), 1) * texColor,
        coords
    );
}

float4 OverbrightMaxHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return float4(max(lightColor, 1), 1) * texColor;
}

float4 InverseOverbrightMaxHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 lightColor = OverbrightLightAtHiDef(coords);
    float4 texColor = tex2D(WorldSampler, WorldCoords(coords));

    return texColor / float4(max(lightColor, 1), 1);
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

    pass InverseOverbrightMaxHiDef
    {
        PixelShader = compile ps_3_0 InverseOverbrightMaxHiDef();
    }

    pass LightOnly
    {
        PixelShader = compile ps_3_0 LightOnly();
    }

    pass Brighten
    {
        PixelShader = compile ps_3_0 Brighten();
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
