sampler TextureSampler : register(s0);

float CoordMultY;

float HorizonLevel;
float SunEffect;
float2 SunCoords;
float3 DarkSkyColor;
float3 LightSkyColor;

float4 SunColor;
float4 SunGlowColor;
float SunRadius;
float SunGlowRadius;
float SunGlowFadeExponent;

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

float4 Sun(float2 coords : TEXCOORD0) : COLOR0
{
    coords = 2 * coords - 1;
    float radius = SunGlowRadius * length(coords);
    return lerp(
        SunColor,
        pow(
            saturate((radius - SunGlowRadius) / (SunRadius - SunGlowRadius)), 
            SunGlowFadeExponent
        ) * SunGlowColor,
        smoothstep(0.0, 1.0, saturate(0.5 * (radius - SunRadius) - 0.5))
    );
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