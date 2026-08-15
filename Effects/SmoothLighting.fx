sampler TextureSampler : register(s0);

sampler TileSampler : register(s0);
sampler LightSampler : register(s8);
sampler GlowSampler : register(s9);
sampler LightedGlowSampler : register(s10);
sampler AmbientOcclusionSampler : register(s11);
sampler DitherSampler : register(s12);

#define DITHER_TEXTURE_SIZE 32

float4x4 LightMapTransform;

float Gamma;
float ReciprocalGamma;

float2 NormalMapResolution;
float NormalMapGradientMult;
float NormalMapStrength;
float2 SkyLightGradient;

float Brightness;

struct PixelShaderInput
{
    float4 Position : SV_Position;
    float2 TileTexCoord : TEXCOORD0;
    float2 LightTexCoord : TEXCOORD1;
};

struct LightMapOnlyPixelShaderInput
{
    float4 Position : SV_Position;
    float2 LightTexCoord : TEXCOORD1;
};

/* Helper functions *********************************************************************/

float Square(float x)
{
    return x * x;
}

float3 Square(float3 x)
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
    return sqrt(dot(Square(color), float3(0.2126, 0.7152, 0.0722)));
}

float AmbientOcclusion(float2 tileCoord, float4 tileColor)
{
    float alpha = saturate(tileColor.a);
    return lerp(
        1, tex2D(AmbientOcclusionSampler, tileCoord).a, alpha
    );
}

float AmbientOcclusionHiDef(float2 tileCoord, float4 tileColor)
{
    float alpha = saturate(tileColor.a);
    return LinearToGamma(
        lerp(
            1, tex2D(AmbientOcclusionSampler, tileCoord).a, alpha
        )
    );
}

// Not technically correct because it ignores gamma, but cheap and decent quality
float4 Dithered(float2 position, float4 color)
{
    float noise
        = (1.0 / 256) * tex2D(DitherSampler, (1.0 / DITHER_TEXTURE_SIZE) * position).r
        - 0.5 / 255;
    
    color.rgb += noise;
    
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

float SampleForNormal(float2 tileCoord, float fallback)
{
    float4 color = tex2D(TileSampler, tileCoord);
    return color.a < 1 ? fallback : saturate(Luma(color.rgb));
}

float3 NormalsSurfaceGradientAndMult(float2 tileCoord, float4 tileColor)
{
    float luma = saturate(Luma(tileColor.rgb));
    
    float leftLuma = SampleForNormal(tileCoord - float2(NormalMapResolution.x, 0), luma);
    float rightLuma = SampleForNormal(tileCoord + float2(NormalMapResolution.x, 0), luma);
    float upLuma = SampleForNormal(tileCoord - float2(0, NormalMapResolution.y), luma);
    float downLuma = SampleForNormal(tileCoord + float2(0, NormalMapResolution.y), luma);
    float positiveDiagonal
        = SampleForNormal(tileCoord - NormalMapResolution, luma) // up left
        - SampleForNormal(tileCoord + NormalMapResolution, luma); // down right
    float negativeDiagonal
        = SampleForNormal(tileCoord - float2(NormalMapResolution.x, -NormalMapResolution.y), luma) // down left
        - SampleForNormal(tileCoord + float2(NormalMapResolution.x, -NormalMapResolution.y), luma); // up right

    float horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return float3(
        mult * Gradient(horizontalColorDiff, verticalColorDiff),
        tileColor.a < 1.0 ? 0.0 : 1.0
    );
}

float2 NormalsLightGradient(float luma)
{
    return NormalMapGradientMult * float2(ddx(luma), ddy(luma));
}

float4 NormalsLightGradientFancySky(float luma, float alpha)
{
    return float4(
        NormalMapGradientMult * float2(ddx(luma), ddy(luma)),
        SkyLightGradient * alpha
    );
}

float NormalsMultiplier(float2 tileCoord, float4 tileColor, float3 lightColor)
{
    float luma = Luma(lightColor);
    float2 lightGradient = NormalsLightGradient(luma);
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0 || luma <= 0)
    {
        return 1.0;
    }
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(tileCoord, tileColor);
    
    lightGradient /= lightGradientLength;
    lightGradientLength /= luma;
    
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0 
        ? 0
        : surfaceGradient / surfaceGradientLength;
    surfaceGradient *= surfaceGradientAndMult.z;
    
    float lightMult = 1.0 + NormalMapStrength * dot(lightGradient, surfaceGradient);
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float NormalsMultiplierFancySky(float2 tileCoord, float4 tileColor, float4 lightColor)
{
    float luma = Luma(lightColor.rgb);
    float4 lightAndSkyLightGradient = NormalsLightGradientFancySky(luma, lightColor.a);
    
    float2 lightGradient = lightAndSkyLightGradient.xy + lightAndSkyLightGradient.zw;
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0 || luma <= 0)
    {
        return 1.0;
    }
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(tileCoord, tileColor);
    
    lightGradient /= lightGradientLength;
    lightGradientLength /= luma;
    
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0
        ? 0 
        : surfaceGradient / surfaceGradientLength;
    surfaceGradient *= surfaceGradientAndMult.z;
    
    float lightMult = 1.0 + NormalMapStrength * dot(lightGradient, surfaceGradient);
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float4 Glow(float2 tileCoord, float4 smoothColor, float3 tileColor, float3 lightColor)
{
    float3 glow = tex2D(GlowSampler, tileCoord).rgb;
    float3 level = saturate((255.0 / 254) * lightColor - (1.0 / 254));
    float3 bright = max(smoothColor.rgb, lerp(glow, tileColor, level));
    
    return float4(
        lerp(smoothColor.rgb, bright, step(2.5 / 255, glow)),
        smoothColor.a
    );
}

float4 EnhancedGlow(float2 tileCoord, float4 smoothColor)
{
    float3 selector = tex2D(GlowSampler, tileCoord).rgb;
    float3 glow = tex2D(LightedGlowSampler, tileCoord).rgb;
    float3 bright = max(smoothColor.rgb, glow);
    
    return float4(
        lerp(smoothColor.rgb, bright, step(2.5 / 255, selector)),
        smoothColor.a
    );
}

/* Base function ************************************************************************/

float4 SmoothLighting(
    float4 position,
    float2 tileCoord,
    float2 lightCoord,
    bool normals,
    bool dithered,
    bool enhancedGlow,
    bool fancySky,
    bool ambientOcclusion,
    bool opaque,
    bool lightOnly,
    bool hiDef
)
{
    float4 tileColor = tex2D(TileSampler, tileCoord);
    float4 lightColor = tex2D(LightSampler, lightCoord);
    float lightColorMult = 1.0;
    
    if (ambientOcclusion)
    {
        if (hiDef)
        {
            lightColorMult *= AmbientOcclusionHiDef(tileCoord, tileColor);
        }
        else
        {
            lightColorMult *= AmbientOcclusion(tileCoord, tileColor);
        }
    }
    
    if (normals)
    {
        if (fancySky)
        {
            lightColorMult *= NormalsMultiplierFancySky(tileCoord, tileColor, lightColor);
        }
        else
        {
            lightColorMult *= NormalsMultiplier(tileCoord, tileColor, lightColor.rgb);
        }
    }
    
    float4 color = float4(lightColorMult * saturate(lightColor.rgb), 1);
    
    if (lightOnly)
    {
        if (!(ambientOcclusion || opaque))
        {
            color *= tileColor.a;
        }
    }
    else
    {
        color *= tileColor;
    }
    
    if (dithered)
    {
        color = Dithered(position, color);
    }
    
    if (enhancedGlow)
    {
        color = EnhancedGlow(tileCoord, color);
    }
    else if (!lightOnly)
    {
        color = Glow(tileCoord, color, tileColor.rgb, lightColor.rgb);
    }
    
    return color;
}

/* Vertex shaders ***********************************************************************/

PixelShaderInput SmoothLighting_VS(
    float4 position : SV_Position, float2 texCoord : TEXCOORD0
)
{
    PixelShaderInput output;
    
    output.Position = position;
    output.TileTexCoord = texCoord;
    output.LightTexCoord = mul(float4(texCoord, 0, 1), LightMapTransform).xy;
    
    return output;
}

LightMapOnlyPixelShaderInput LightMapOnly_VS(
    float4 position : SV_Position, float2 texCoord : TEXCOORD0
)
{
    LightMapOnlyPixelShaderInput output;
    
    output.Position = position;
    output.LightTexCoord = mul(float4(texCoord, 0, 1), LightMapTransform).xy;
    
    return output;
}

/* Pixel shaders ************************************************************************/

float4 Smooth_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, false, false, false, false
    );
}

float4 SmoothLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, false, false, true, false
    );
}

float4 SmoothAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, true, false, false, false
    );
}

float4 SmoothAmbientOcclusionLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, true, false, true, false
    );
}

float4 SmoothAmbientOcclusionHiDef_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, true, false, false, true
    );
}

float4 SmoothAmbientOcclusionLightOnlyHiDef_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, true, false, true, true
    );
}

float4 SmoothOpaqueLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, false, false, false, true, true, false
    );
}

float4 SmoothEnhancedGlow_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, true, false, false, false, false, false
    );
}

float4 SmoothEnhancedGlowAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, true, false, true, false, false, false
    );
}

float4 SmoothEnhancedGlowAmbientOcclusionHiDef_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, false, true, false, true, false, false, true
    );
}

float4 SmoothDithered_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, false, false, false, false, false, false
    );
}

float4 SmoothDitheredLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, false, false, false, false, true, false
    );
}

float4 SmoothDitheredAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, false, false, true, false, false, false
    );
}

float4 SmoothDitheredAmbientOcclusionLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, false, false, true, false, true, false
    );
}

float4 SmoothDitheredOpaqueLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, false, false, false, true, true, false
    );
}

float4 SmoothDitheredEnhancedGlow_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, true, false, false, false, false, false
    );
}

float4 SmoothDitheredEnhancedGlowAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        false, true, true, false, true, false, false, false
    );
}

float4 Normals_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, false, false, false, false
    );
}

float4 NormalsLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, false, false, true, false
    );
}

float4 NormalsAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, true, false, false, false
    );
}

float4 NormalsAmbientOcclusionLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, true, false, true, false
    );
}

float4 NormalsAmbientOcclusionHiDef_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, true, false, false, true
    );
}

float4 NormalsAmbientOcclusionLightOnlyHiDef_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, true, false, true, true
    );
}

float4 NormalsOpaqueLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, false, false, true, true, false
    );
}

float4 NormalsFancySky_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, true, false, false, false, false
    );
}

float4 NormalsFancySkyLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, false, true, false, false, true, false
    );
}

float4 NormalsEnhancedGlow_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, true, false, false, false, false, false
    );
}

float4 NormalsEnhancedGlowAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, true, false, true, false, false, false
    );
}

float4 NormalsEnhancedGlowAmbientOcclusionHiDef_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, true, false, true, false, false, true
    );
}

float4 NormalsEnhancedGlowFancySky_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, false, true, true, false, false, false, false
    );
}

float4 NormalsDithered_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, false, false, false, false, false
    );
}

float4 NormalsDitheredLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, false, false, false, true, false
    );
}

float4 NormalsDitheredAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, false, true, false, false, false
    );
}

float4 NormalsDitheredAmbientOcclusionLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, false, true, false, true, false
    );
}

float4 NormalsDitheredOpaqueLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, false, false, true, true, false
    );
}

float4 NormalsDitheredFancySky_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, true, false, false, false, false
    );
}

float4 NormalsDitheredFancySkyLightOnly_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, false, true, false, false, true, false
    );
}

float4 NormalsDitheredEnhancedGlow_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, true, false, false, false, false, false
    );
}

float4 NormalsDitheredEnhancedGlowAmbientOcclusion_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, true, false, true, false, false, false
    );
}

float4 NormalsDitheredEnhancedGlowFancySky_PS(PixelShaderInput input) : COLOR0
{
    return SmoothLighting(
        input.Position, input.TileTexCoord, input.LightTexCoord,
        true, true, true, true, false, false, false, false
    );
}

float4 LightColor_PS(LightMapOnlyPixelShaderInput input) : COLOR0
{
    float4 lightColor = tex2D(TextureSampler, input.LightTexCoord);

    return saturate(lightColor);
}

float4 LightColorDithered_PS(LightMapOnlyPixelShaderInput input) : COLOR0
{
    float4 lightColor = tex2D(TextureSampler, input.LightTexCoord);

    return Dithered(input.Position, saturate(lightColor));
}

float4 OverbrightMax_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TileSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);

    return tileColor * max(lightColor, 1);
}

float4 OverbrightMaxDithered_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TileSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);

    return Dithered(input.Position, tileColor * max(lightColor, 1));
}

float4 OverbrightMaxInplace_PS(LightMapOnlyPixelShaderInput input) : COLOR0
{
    float4 lightColor = tex2D(TextureSampler, input.LightTexCoord);

    return max(lightColor, 1);
}

float4 InverseOverbrightMax_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TileSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);

    return Brightness * tileColor / max(lightColor, 1);
}

float4 InverseOverbrightMaxInplace_PS(LightMapOnlyPixelShaderInput input) : COLOR0
{
    float4 lightColor = tex2D(TextureSampler, input.LightTexCoord);

    return Brightness / max(lightColor, 1);
}

/* Techniques ***************************************************************************/

technique Smooth
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 Smooth_PS();
    }
}

technique SmoothLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothLightOnly_PS();
    }
}

technique SmoothAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothAmbientOcclusion_PS();
    }
}

technique SmoothAmbientOcclusionLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothAmbientOcclusionLightOnly_PS();
    }
}

technique SmoothAmbientOcclusionHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothAmbientOcclusionHiDef_PS();
    }
}

technique SmoothAmbientOcclusionLightOnlyHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothAmbientOcclusionLightOnlyHiDef_PS();
    }
}

technique SmoothOpaqueLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothOpaqueLightOnly_PS();
    }
}

technique SmoothEnhancedGlow
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothEnhancedGlow_PS();
    }
}

technique SmoothEnhancedGlowAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothEnhancedGlowAmbientOcclusion_PS();
    }
}

technique SmoothEnhancedGlowAmbientOcclusionHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothEnhancedGlowAmbientOcclusionHiDef_PS();
    }
}

technique SmoothDithered
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDithered_PS();
    }
}

technique SmoothDitheredLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredLightOnly_PS();
    }
}

technique SmoothDitheredAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredAmbientOcclusion_PS();
    }
}

technique SmoothDitheredAmbientOcclusionLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredAmbientOcclusionLightOnly_PS();
    }
}

technique SmoothDitheredOpaqueLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredOpaqueLightOnly_PS();
    }
}

technique SmoothDitheredEnhancedGlow
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredEnhancedGlow_PS();
    }
}

technique SmoothDitheredEnhancedGlowAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredEnhancedGlowAmbientOcclusion_PS();
    }
}

technique Normals
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 Normals_PS();
    }
}

technique NormalsLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsLightOnly_PS();
    }
}

technique NormalsAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsAmbientOcclusion_PS();
    }
}

technique NormalsAmbientOcclusionLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsAmbientOcclusionLightOnly_PS();
    }
}

technique NormalsAmbientOcclusionHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsAmbientOcclusionHiDef_PS();
    }
}

technique NormalsAmbientOcclusionLightOnlyHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsAmbientOcclusionLightOnlyHiDef_PS();
    }
}

technique NormalsOpaqueLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsOpaqueLightOnly_PS();
    }
}

technique NormalsFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsFancySky_PS();
    }
}

technique NormalsFancySkyLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsFancySkyLightOnly_PS();
    }
}

technique NormalsEnhancedGlow
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsEnhancedGlow_PS();
    }
}

technique NormalsEnhancedGlowAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsEnhancedGlowAmbientOcclusion_PS();
    }
}

technique NormalsEnhancedGlowAmbientOcclusionHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsEnhancedGlowAmbientOcclusionHiDef_PS();
    }
}

technique NormalsEnhancedGlowFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsEnhancedGlowFancySky_PS();
    }
}

technique NormalsDithered
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDithered_PS();
    }
}

technique NormalsDitheredLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredLightOnly_PS();
    }
}

technique NormalsDitheredAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredAmbientOcclusion_PS();
    }
}

technique NormalsDitheredAmbientOcclusionLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredAmbientOcclusionLightOnly_PS();
    }
}

technique NormalsDitheredOpaqueLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredOpaqueLightOnly_PS();
    }
}

technique NormalsDitheredFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredFancySky_PS();
    }
}

technique NormalsDitheredFancySkyLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredFancySkyLightOnly_PS();
    }
}

technique NormalsDitheredEnhancedGlow
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredEnhancedGlow_PS();
    }
}

technique NormalsDitheredEnhancedGlowAmbientOcclusion
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredEnhancedGlowAmbientOcclusion_PS();
    }
}

technique NormalsDitheredEnhancedGlowFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsDitheredEnhancedGlowFancySky_PS();
    }
}

technique LightColor
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 LightMapOnly_VS();
        PixelShader = compile ps_3_0 LightColor_PS();
    }
}

technique LightColorDithered
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 LightMapOnly_VS();
        PixelShader = compile ps_3_0 LightColorDithered_PS();
    }
}

technique OverbrightMax
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 OverbrightMax_PS();
    }
}

technique OverbrightMaxDithered
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 OverbrightMaxDithered_PS();
    }
}

technique OverbrightMaxInplace
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 LightMapOnly_VS();
        PixelShader = compile ps_3_0 OverbrightMaxInplace_PS();
    }
}

technique InverseOverbrightMax
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 InverseOverbrightMax_PS();
    }
}

technique InverseOverbrightMaxInplace
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 LightMapOnly_VS();
        PixelShader = compile ps_3_0 InverseOverbrightMaxInplace_PS();
    }
}
