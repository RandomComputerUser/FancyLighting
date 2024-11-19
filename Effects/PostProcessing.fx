sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);
sampler BloomBlurSampler : register(s4);

float2 DitherCoordMult;
float GammaRatio;
float Exposure;
float BloomStrength;

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
    return (
        tex2D(DitherSampler, coords * DitherCoordMult).rgb - 128 / 255.0
    ) * (0.5 / 128);
}

// Input color should be in gamma 2.2
float3 Dither(float3 color, float2 coords)
{
    float3 lo = (1.0 / 255) * floor(255 * color);
    float3 hi = lo + 1.0 / 255;
    float3 loLinear = pow(lo, 2.2);
    float3 hiLinear = pow(hi, 2.2);

    float3 t = (pow(color, 2.2) - loLinear) / (hiLinear - loLinear);
    float rand = (255.0 / 256) * tex2D(DitherSampler, DitherCoordMult * coords).r;
    float3 selector = step(t, rand);

    return lerp(hi, lo, selector);
}

float3 ToneMapColor(float3 x)
{
    float c1 = 1.35555555556;
    float c2 = 0.815573770492;
    float c3 = 22500;
    float c4 = 2;
    float c5 = 1.77777777778;
    return saturate(
        c1 * (1 - c2 / (c3 * pow(x, c4) + 1)) * (x / (x + c5))
    );
}

float4 GammaToLinear(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(Exposure * pow(tex2D(ScreenSampler, coords).rgb, GammaRatio), 1);
}

float4 GammaToGammaDither(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        Dither(
            pow(tex2D(ScreenSampler, coords).rgb, GammaRatio),
            coords
        ),
        1
    );
}

float4 GammaToSrgbDither(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        LinearToSrgb(
            pow(tex2D(ScreenSampler, coords).rgb, GammaRatio)
        ) + DitherNoise(coords),
        1
    );
}

float4 ToneMap(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColor(color.rgb);
    return color;
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
