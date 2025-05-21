sampler TextureSampler : register(s0);

float CoordMultY;

float SunEffect;
float2 SunCoords;
float3 DarkSkyColor;
float3 LightSkyColor;

float4 SunColor;
float FadeBegin;
float FadeEnd;

float Gamma;

float4 FancySunSky(float2 coords : TEXCOORD0) : COLOR0
{
    coords -= 0.5;
    coords *= float2(4.0, CoordMultY);
    
    float2 diffFromSun = coords - SunCoords;
    
    float horizonTerm = 0.8 / (1 + exp(6 * coords.y - 1));
    float sunTerm = SunEffect * exp(-2 * dot(diffFromSun, diffFromSun));
    float level = horizonTerm + sunTerm;
    level = min((2.0 / 3) * level, 1.0);
    
    float3 skyColor = lerp(DarkSkyColor, LightSkyColor, level);
    return float4(skyColor, 1.0);
}

float4 FancySun(float2 coords : TEXCOORD0) : COLOR0
{
    coords = 2 * coords - 1;
    float radius = length(coords);
    float alpha = 1 - smoothstep(FadeBegin, FadeEnd, radius);
    return alpha * SunColor;
}

float4 ApplyGamma(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    color *= tex2D(TextureSampler, coords);
    color.rgb = max(color.rgb, 0); // prevent NaN and negative numbers
    color.rgb = pow(color.rgb, Gamma);
    color.rgb = min(color.rgb, 10000); // prevent infinity
    color.a = saturate(color.a);
    return color;
}

technique Technique1
{   
    pass FancySunSky
    {
        PixelShader = compile ps_3_0 FancySunSky();
    }
    
    pass FancySun
    {
        PixelShader = compile ps_3_0 FancySun();
    }
    
    pass ApplyGamma
    {
        PixelShader = compile ps_3_0 ApplyGamma();
    }
}
