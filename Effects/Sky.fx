sampler TextureSampler : register(s0);
sampler DitherSampler : register(s0);

float2 DitherCoordMult;

float HighSkyLevel;
float LowSkyLevel;
float3 HighSkyColor;
float3 LowSkyColor;

float Gamma;
float InverseGamma;

// Not technically correct because it ignores gamma, but cheap and decent quality
float4 Dithered(float4 color, float2 coords)
{
    float noise
        = (255.0 / 256 / 255) * tex2D(DitherSampler, coords * DitherCoordMult).r
        - 0.5 / 255;
    
    color.rgb += noise;
    
    return color;
}

float4 CalculateSkyColor(float2 coords)
{
    float t = smoothstep(HighSkyLevel, LowSkyLevel, coords.y);
    return float4(lerp(HighSkyColor, LowSkyColor, t), 1);
}

float4 Sky(float2 coords : TEXCOORD0) : COLOR0
{
    return CalculateSkyColor(coords);
}

float4 SkyDithered(float2 coords : TEXCOORD0) : COLOR0
{
    return Dithered(CalculateSkyColor(coords), coords);
}

float4 Sun(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 baseColor = tex2D(TextureSampler, coords);
    baseColor.rgb = pow(baseColor.rgb, Gamma);
    baseColor.rgb += 200 * pow(baseColor.rgb, 8);
    
    // Desaturate
    float brightest = max(max(baseColor.r, baseColor.g), baseColor.b);
    baseColor.rgb = lerp(baseColor.rgb, brightest.xxx, 0.5);
    
    
    baseColor.rgb = pow(baseColor.rgb, InverseGamma);
    return color * baseColor;
}

technique Technique1
{   
    pass Sky
    {
        PixelShader = compile ps_3_0 Sky();
    } 
      
    pass SkyDithered
    {
        PixelShader = compile ps_3_0 SkyDithered();
    }
    
    pass Sun
    {
        PixelShader = compile ps_3_0 Sun();
    }
}
