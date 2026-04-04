sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);
sampler BloomBlurSampler : register(s4);

float2 DitherCoordMult;
float GammaRatio;
float OutputGamma;
float Exposure;
float BloomStrength;

// https://www.colour-science.org/apps/

static const float3x3 SrgbToAcescg =
{
    {0.612459, 0.338722, 0.048818},
    {0.070664, 0.917631, 0.011705},
    {0.020755, 0.106878, 0.872367}
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
    return (
        (255.0 / 256 / 255) * tex2D(DitherSampler, coords * DitherCoordMult).r - 0.5 / 255
    ).xxx;
}

// Input color should be in output gamma
float3 Dither(float3 color, float2 coords)
{
    float3 lo = (1.0 / 255) * floor(255 * color);
    float3 hi = lo + 1.0 / 255;
    float3 loLinear = pow(lo, OutputGamma);
    float3 hiLinear = pow(hi, OutputGamma);

    float3 t = (pow(color, OutputGamma) - loLinear) / (hiLinear - loLinear);
    float rand = (255.0 / 256) * tex2D(DitherSampler, DitherCoordMult * coords).r;
    float3 selector = step(t, rand);

    return lerp(hi, lo, selector);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
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

float4 GammaToGammaNoDither(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);

    return float4(pow(color.rgb, GammaRatio), color.a);
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

float4 GammaToSrgbNoDither(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    
    return float4(LinearToSrgb(pow(color.rgb, GammaRatio)), color.a);
}

float3 ToneMapColor1(float3 x)
{
    const float c1 = 1.8;
    const float c2 = 4.0;
    return saturate(c1 * (x / (x + c2)));
}

float SaturationCurve(float x)
{
    const float c1 = -1.5;
    const float c2 = 2;
    const float c3 = 0.5625;
    const float c4 = 1;
    const float c5 = -0.25;
    const float c6 = -5;
    x = c1 + c2 * sqrt(c3 + c4 * x);
    return saturate(c5 * x * (c6 + x));
}

float3 MakeVibrant(float3 x)
{
	float maxComponent = max(x.r, max(x.g, x.b));
	if (maxComponent <= 0)
	{
	    return x;
	}
	
	float minComponent = min(x.r, min(x.g, x.b));
	float saturation = 1 - minComponent / maxComponent;
	if (saturation <= 0)
	{
	    return x;
	}
	
	float targetSaturation = SaturationCurve(saturation);
	
	float mult = targetSaturation / saturation;
	float3 result = maxComponent - mult * (maxComponent - x);
    result *= Luminance(x) / Luminance(result);
	return saturate(result);
}

float4 ToneMap1(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = mul(SrgbToAcescg, color.rgb);
    color.rgb = ToneMapColor1(color.rgb);
    color.rgb = MakeVibrant(saturate(mul(AcescgToSrgb, color.rgb)));
    return color;
}

float3 ToneMapColor2(float3 x)
{
    const float c1 = 1.46666666667;
    const float c2 = 0.363636363636;
    const float c3 = 256;
    const float c4 = 2;
    const float c5 = 2.33333333333;
    return saturate(
        c1 * (1 - c2 / (c3 * pow(x, c4) + 1)) * (x / (x + c5))
    );
}

float4 ToneMap2(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColor2(color.rgb);
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

    pass GammaToGammaNoDither
    {
        PixelShader = compile ps_3_0 GammaToGammaNoDither();
    }
    
    pass GammaToSrgbDither
    {
        PixelShader = compile ps_3_0 GammaToSrgbDither();
    }
    
    pass GammaToSrgbNoDither
    {
        PixelShader = compile ps_3_0 GammaToSrgbNoDither();
    }
    
    pass ToneMap1
    {
        PixelShader = compile ps_3_0 ToneMap1();
    }
    
    pass ToneMap2
    {
        PixelShader = compile ps_3_0 ToneMap2();
    }
    
    pass BloomComposite
    {
        PixelShader = compile ps_3_0 BloomComposite();
    }
}
