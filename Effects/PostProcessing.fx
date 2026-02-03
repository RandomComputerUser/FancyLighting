sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);
sampler BloomBlurSampler : register(s4);

float2 DitherCoordMult;
float GammaRatio;
float Exposure;
float BloomStrength;

// https://www.colour-science.org/apps/

// Use P3 primaries for increased saturation
static const float3x3 P3ToAcescg =
{
    {0.735022, 0.211362, 0.053616},
    {0.047736, 0.939409, 0.012855},
    {0.003798, 0.038104, 0.958098}
};

static const float3x3 AcescgToSrgb =
{
    { 1.707255, -0.620035, -0.087220},
    {-0.131157,  1.139101, -0.007944},
    {-0.024550, -0.124805,  1.149354}
};

float3 LinearToSrgb(float3 color)
{
    float3 lowPart = 12.92 * color;
    float3 highPart = 1.055 * pow(color, 1 / 2.4) - 0.055;
    float3 selector = step(color, 0.0031308);
    return lerp(highPart, lowPart, selector);
}

// Dithering in sRGB isn't technically correct but the difference is too small to matter (around 10^-5)
// Also dark colors in sRGB are mapped linearly so there is no difference for dark colors
float3 DitherNoise(float2 coords)
{
    return 0;
}

// Input color should be in gamma 2.2
float3 Dither(float3 color, float2 coords)
{
    return color;
}

float3 ToneMapColor(float3 x)
{
    float c1 = 1.6;
    float c2 = 3.0;
    return saturate(
        c1 * (x / (x + c2))
    );
}

float4 GammaToLinear(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = max(color.rgb, 0); // prevent NaN and negative numbers
    color.rgb = pow(color.rgb, GammaRatio);
    color.rgb = min(color.rgb, 10000); // prevent infinity
    color.rgb *= Exposure;
    color.a = saturate(color.a);
    return color;
}

float4 GammaToGammaDither(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);

    return float4(
        Dither(
            pow(color.rgb, GammaRatio),
            coords
        ),
        color.a
    );
}

float4 GammaToSrgbDither(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    
    return float4(
        LinearToSrgb(
            pow(color.rgb, GammaRatio)
        ) + DitherNoise(coords),
        color.a
    );
}

float4 ToneMap(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    return max(color, 0);
}

float4 BloomComposite(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    float4 bloomColor = tex2D(BloomBlurSampler, coords);
    return lerp(color, bloomColor, BloomStrength);
}

technique Technique1
{   
    pass GammaToLinear
    {
        PixelShader = compile ps_3_0 GammaToLinear();
    }

    pass GammaToGammaDither
    {
        PixelShader = compile ps_3_0 GammaToGammaDither();
    }
    
    pass GammaToSrgbDither
    {
        PixelShader = compile ps_3_0 GammaToSrgbDither();
    }
    
    pass ToneMap
    {
        PixelShader = compile ps_3_0 ToneMap();
    }
    
    pass BloomComposite
    {
        PixelShader = compile ps_3_0 BloomComposite();
    }
}
