sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);
sampler BloomBlurSampler : register(s4);

float2 DitherCoordMult;
float GammaRatio;
float OutputGamma;
float Exposure;
float BloomStrength;

float4 VibranceBoostParams1;
float2 VibranceBoostParams2;

// https://www.colour-science.org/apps/

// Square root of the transformation matrix from sRGB to ACEScg
// Use the square root to make desaturation of bright colors less intense
static const float3x3 SqrtSrgbToAcescg =
{
    0.77731090, 0.19479431, 0.02789479,
    0.04078929, 0.95361152, 0.00559919,
    0.01080702, 0.05551474, 0.93367824
};

static const float3x3 SqrtAcescgToSrgb =
{
     1.30083470, -0.26355115, -0.03728355,
    -0.05557223,  1.06027029, -0.00469806,
    -0.01175251, -0.05999115,  1.07174366
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

float4 GammaToLinearNoAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = max(color.rgb, 0); // prevent NaN and negative numbers
    color.rgb = pow(color.rgb, GammaRatio);
    color.rgb = min(color.rgb, 10000); // prevent infinity
    color.rgb *= Exposure;
    color.a = 1;
    return color;
}

float4 GammaToLinear(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = max(color.rgb, 0); // prevent NaN and negative numbers
    color.a = saturate(color.a);
    color = pow(color, GammaRatio);
    color.rgb = min(color.rgb, 10000); // prevent infinity
    color.rgb *= Exposure;
    return color;
}

float4 GammaToGammaDitherNoAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);

    return float4(Dither(pow(color.rgb, GammaRatio), coords), 1);
}

float4 GammaToGammaDither(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    
    color = pow(color, GammaRatio);
    return float4(Dither(color.rgb, coords), color.a);
}

float4 GammaToGammaNoDitherNoAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);

    return float4(pow(color.rgb, GammaRatio), 1);
}

float4 GammaToGammaNoDither(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);

    return pow(color, GammaRatio);
}

float4 GammaToSrgbDitherNoAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    
    return float4(
        LinearToSrgb(
            pow(color.rgb, GammaRatio)
        ) + DitherNoise(coords),
        1
    );
}

float4 GammaToSrgbNoDitherNoAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    
    return float4(LinearToSrgb(pow(color.rgb, GammaRatio)), 1);
}

float4 BloomComposite(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    float4 bloomColor = tex2D(BloomBlurSampler, coords);
    return lerp(color, bloomColor, BloomStrength);
}

float SaturationCurve(float x)
{
    x = VibranceBoostParams1.x + VibranceBoostParams1.y * sqrt(
        VibranceBoostParams1.z + VibranceBoostParams1.w * x
    );
    return saturate(VibranceBoostParams2.x * x * (VibranceBoostParams2.y + x));
}

float3 MakeVibrant(float3 x)
{
	float maxComponent = pow(max(x.r, max(x.g, x.b)), 1 / 2.2);
	if (maxComponent <= 0)
	{
	    return x;
	}
	
	float minComponent = pow(min(x.r, min(x.g, x.b)), 1 / 2.2);
	float saturation = 1 - minComponent / maxComponent;
	if (saturation <= 0)
	{
	    return x;
	}
	
	float targetSaturation = SaturationCurve(saturation);
	
	float mult = targetSaturation / saturation;
	float3 result = pow(
	    max(lerp(maxComponent.xxx, pow(x, 1 / 2.2), mult), 0.0), 
	    2.2
	);
    result *= Luminance(x) / Luminance(result);
	return result;
}

float3 ToneMapColor1(float3 x)
{
    const float c1 = 1.8;
    const float c2 = 4.0;
    x = mul(SqrtSrgbToAcescg, x);
    x = saturate(c1 * (x / (x + c2)));
    return saturate(mul(SqrtAcescgToSrgb, x));
}

float4 ToneMap1(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColor1(color.rgb);
    return color;
}

float4 ToneMap1VibranceBoost(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColor1(color.rgb);
    color.rgb = saturate(MakeVibrant(color.rgb));
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

float4 ToneMap2VibranceBoost(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColor2(color.rgb);
    color.rgb = saturate(MakeVibrant(color.rgb));
    return color;
}

float4 VibranceBoost(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = max(MakeVibrant(color.rgb), 0);
    return color;
}

technique Technique1
{
    pass GammaToLinearNoAlpha
    {
        PixelShader = compile ps_3_0 GammaToLinearNoAlpha();
    }

    pass GammaToLinear
    {
        PixelShader = compile ps_3_0 GammaToLinear();
    }

    pass GammaToGammaDitherNoAlpha
    {
        PixelShader = compile ps_3_0 GammaToGammaDitherNoAlpha();
    }

    pass GammaToGammaDither
    {
        PixelShader = compile ps_3_0 GammaToGammaDither();
    }

    pass GammaToGammaNoDitherNoAlpha
    {
        PixelShader = compile ps_3_0 GammaToGammaNoDitherNoAlpha();
    }

    pass GammaToGammaNoDither
    {
        PixelShader = compile ps_3_0 GammaToGammaNoDither();
    }
    
    pass GammaToSrgbDitherNoAlpha
    {
        PixelShader = compile ps_3_0 GammaToSrgbDitherNoAlpha();
    }
    
    pass GammaToSrgbNoDitherNoAlpha
    {
        PixelShader = compile ps_3_0 GammaToSrgbNoDitherNoAlpha();
    }
    
    pass BloomComposite
    {
        PixelShader = compile ps_3_0 BloomComposite();
    }
    
    pass ToneMap1
    {
        PixelShader = compile ps_3_0 ToneMap1();
    }
    
    pass ToneMap1VibranceBoost
    {
        PixelShader = compile ps_3_0 ToneMap1VibranceBoost();
    }
    
    pass ToneMap2
    {
        PixelShader = compile ps_3_0 ToneMap2();
    }
    
    pass ToneMap2VibranceBoost
    {
        PixelShader = compile ps_3_0 ToneMap2VibranceBoost();
    }
    
    pass VibranceBoost
    {
        PixelShader = compile ps_3_0 VibranceBoost();
    }
}
