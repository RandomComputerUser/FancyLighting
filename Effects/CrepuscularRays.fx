// https://web.archive.org/web/20111209175141/http://xna-uk.net/blogs/randomchaos/archive/2011/09/15/crepuscular-god-rays-and-web-ui-sample.aspx

sampler TextureSampler : register(s0);

float2 Resolution;
float2 SunPosition;
float3 LightColor;

float Gamma;
float InverseGamma;

float Density = 0.5;
float Decay = 0.95;
float Weight = 1.0;
float Exposure = 0.15;

#define SAMPLE_COUNT 128

float4 Light(float2 coords : TEXCOORD0) : COLOR0
{
    coords *= Resolution;
    float distance = min(length(coords - SunPosition), 1);
    return float4(LightColor / pow(distance, 2), 1);
}

float4 Scene(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(TextureSampler, coords);
    return float4(0, 0, 0, color.a);
}

float4 Rays(float2 coords : TEXCOORD0) : COLOR0
{
    float2 deltaCoord = SunPosition - coords;
    deltaCoord *= 1.0 / SAMPLE_COUNT * Density;
    
    float3 color = tex2D(TextureSampler, coords);
    float illuminationDecay = 1.0;
    for (int i = SAMPLE_COUNT; i-- > 0; )
    {
        coords += deltaCoord;
        float3 sample = tex2D(TextureSampler, coords);
        sample *= illuminationDecay * Weight;
        color += sample;
        illuminationDecay *= Decay;            
    }

    return float4(color * Exposure, 1);
}

technique Technique1
{   
    pass Light
    {
        PixelShader = compile ps_3_0 Light();
    } 
      
    pass Scene
    {
        PixelShader = compile ps_3_0 Scene();
    }
    
    pass Rays
    {
        PixelShader = compile ps_3_0 Rays();
    }
}
