sampler TextureSampler : register(s0);

float CoordMultY;

float HorizonLevel;
float SunEffect;
float2 SunCoords;
float3 DarkSkyColor;
float3 LightSkyColor;

float Gamma;
float InverseGamma;

float4 Sky(float2 coords : TEXCOORD0) : COLOR0
{
    coords -= 0.5;
    coords *= float2(16.0, CoordMultY);
    coords.y = max(coords.y, HorizonLevel);
    
    float2 diffFromHorizon = coords.y - HorizonLevel;
    float2 diffFromSun = coords - SunCoords;
    
    float horizonFactor = pow(sqrt(1 + diffFromHorizon * diffFromHorizon) - 0.5, 1.5);
    float sunFactor = pow(dot(diffFromSun, diffFromSun), 0.75);
    sunFactor = lerp(20.0, sunFactor, SunEffect);
    float level = exp(-0.015 * horizonFactor * sunFactor);
    
    float3 skyColor = lerp(DarkSkyColor, LightSkyColor, level);
    return float4(skyColor, 1.0);
}

float4 Sun(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 baseColor = tex2D(TextureSampler, coords);
    baseColor.rgb = pow(baseColor.rgb, Gamma);
    baseColor.rgb += 200 * pow(baseColor.rgb, 10);
    
    // Desaturate
    float brightest = max(max(baseColor.r, baseColor.g), baseColor.b);
    baseColor.rgb = lerp(baseColor.rgb, brightest.xxx, 0.6);
    
    
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