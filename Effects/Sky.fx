sampler TextureSampler : register(s0);

float HighSkyLevel;
float LowSkyLevel;
float3 HighSkyColor;
float3 LowSkyColor;

float Gamma;
float InverseGamma;

float4 Sky(float2 coords : TEXCOORD0) : COLOR0
{
    float t = smoothstep(HighSkyLevel, LowSkyLevel, coords.y);
    return float4(lerp(HighSkyColor, LowSkyColor, t), 1);
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
    
    pass Sun
    {
        PixelShader = compile ps_3_0 Sun();
    }
}