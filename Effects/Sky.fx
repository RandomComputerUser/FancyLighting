sampler TextureSampler : register(s0);

float CoordMultY;

float SunEffect;
float HorizonLevel;
float2 SunCoords;
float3 DarkSkyColor;
float3 LightSkyColor;

float4 SunColor;
float4 SunGlowColor;
float SunRadius;
float SunGlowRadius;
float SunGlowFadeMult;

float square(float x)
{
    return x * x;
}

float4 Sky(float2 coords : TEXCOORD0) : COLOR0
{
    coords -= 0.5;
    coords *= float2(4.0, CoordMultY);
    
    float2 diffFromSun = coords - SunCoords;
    
    float horizonTerm = 0.8 / (1 + exp(6 * (coords.y - HorizonLevel)));
    float sunTerm = SunEffect * exp(-2 * dot(diffFromSun, diffFromSun));
    float level = horizonTerm + sunTerm;
    level = min((2.0 / 3) * level, 1.0);
    
    float3 skyColor = lerp(DarkSkyColor, LightSkyColor, level);
    return float4(skyColor, 1.0);
}

float4 Sun(float2 coords : TEXCOORD0) : COLOR0
{
    coords = 2 * coords - 1;
    float radius = SunGlowRadius * length(coords);
    return lerp(
        SunColor,
        square(saturate((radius - SunGlowRadius) / (SunRadius - SunGlowRadius)))
            * SunGlowColor,
        smoothstep(0.0, 1.0, saturate(SunGlowFadeMult * (radius - SunRadius) - 0.5))
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