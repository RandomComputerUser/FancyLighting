sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);

float2 DitherCoordMult;

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
float3 Dither(float2 coords)
{
    return (tex2D(DitherSampler, coords * DitherCoordMult).rgb - 128 / 255.0) * (0.5 / 128);
}

float4 GammaToSrgb(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(LinearToSrgb(GammaToLinear(tex2D(ScreenSampler, coords).rgb)) + Dither(coords), 1);
}

technique Technique1
{
    pass GammaToSrgb
    {
        PixelShader = compile ps_2_0 GammaToSrgb();
    }
}
