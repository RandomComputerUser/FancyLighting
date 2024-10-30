sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);

float2 DitherCoordMult;
float Gamma;

float3 GammaToLinear(float3 color)
{
    return pow(color, 2.2);
}

float3 LinearToSrgb(float3 color)
{
    float3 lowPart = 12.92 * color;
    float3 highPart = 1.055 * pow(color, 1 / 2.4) - 0.055;
    float3 selector = step(color, 0.0031308);
    return lerp(highPart, lowPart, selector);
}

// Dithering isn't gamma-correct but the difference is too small to matter (around 10^-5)
// Also dark colors in sRGB are mapped linearly so there is no difference for dark colors
float3 DitherNoise(float2 coords)
{
    return (
        tex2D(DitherSampler, coords * DitherCoordMult).rgb - 128 / 255.0
    ) * (0.5 / 128);
}

// Input color should be in 2.2 gamma
float3 Dither(float3 color, float2 coords)
{
    float3 lo = (1.0 / 255) * floor(255 * color);
    float3 hi = lo + 1.0 / 255;
    float3 loLinear = GammaToLinear(lo);
    float3 hiLinear = GammaToLinear(hi);

    float3 t = (GammaToLinear(color) - loLinear) / (hiLinear - loLinear);
    float rand = (255.0 / 256) * tex2D(DitherSampler, DitherCoordMult * coords).r;
    float3 selector = step(t, rand);

    return lerp(hi, lo, selector);
}

float4 CustomGammaToGamma(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        Dither(
            pow(tex2D(ScreenSampler, coords).rgb, Gamma),
            coords
        ),
        1
    );
}

float4 CustomGammaToSrgb(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        LinearToSrgb(
            pow(tex2D(ScreenSampler, coords).rgb, Gamma)
        ) + DitherNoise(coords),
        1
    );
}

float4 GammaToSrgb(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        LinearToSrgb(
            GammaToLinear(tex2D(ScreenSampler, coords).rgb)
        ) + DitherNoise(coords),
        1
    );
}

technique Technique1
{   
    pass CustomGammaToGamma
    {
        PixelShader = compile ps_3_0 CustomGammaToGamma();
    }
    
    pass CustomGammaToSrgb
    {
        PixelShader = compile ps_3_0 CustomGammaToSrgb();
    }
    
    pass GammaToSrgb
    {
        PixelShader = compile ps_3_0 GammaToSrgb();
    }
}
