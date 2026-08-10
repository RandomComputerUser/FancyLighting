sampler ScreenSampler : register(s0);
sampler BackgroundSampler : register(s8);
sampler DitherSampler : register(s8);
sampler BloomBlurSampler : register(s8);

#define DITHER_TEXTURE_SIZE 32

float BrightnessMult;
float GammaRatio;
float OutputGamma;
float Exposure;
float BackgroundExposure;
float BloomStrength;

float4 VibranceBoostParams1;
float2 VibranceBoostParams2;

// https://en.wikipedia.org/wiki/SRGB#Primaries

static const float3x3 SrgbToXyzD65 =
{
     0.4124,  0.2126,  0.0193,
     0.3576,  0.7152,  0.1192,
     0.1805,  0.0722,  0.9505
};

static const float3x3 XyzD65ToSrgb =
{
     3.2406255, -0.9689307,  0.0557101,
    -1.5372080,  1.8757561, -0.2040211,
    -0.4986286,  0.0415175,  1.0569959
};

// http://brucelindbloom.com/index.html?Eqn_ChromAdapt.html

// Hunt–Pointer–Estevez matrix
static const float3x3 XyzD65ToLmsD65 =
{
     0.4002400, -0.2263000,  0.0000000,
     0.7076000,  1.1653200,  0.0000000,
    -0.0808100,  0.0457000,  0.9182200
};

static const float3x3 LmsD65ToXyzD65 =
{
     1.8599364,  0.3611914,  0.0000000,
    -1.1293816,  0.6388125,  0.0000000,
     0.2198974, -0.0000064,  1.0890636
};

static const float3x3 SrgbToLmsD65 = mul(SrgbToXyzD65, XyzD65ToLmsD65);

static const float3x3 LmsD65ToSrgb = mul(LmsD65ToXyzD65, XyzD65ToSrgb);

// https://www.colour-science.org/apps/

// Square root of the transformation matrix from sRGB to ACEScg with chromatic adaptation
// Use the square root to make desaturation of bright colors less intense
static const float3x3 SqrtSrgbToAcescg =
{
    0.77731090, 0.04078929, 0.01080702,
    0.19479431, 0.95361152, 0.05551474,
    0.02789479, 0.00559919, 0.93367824
};

static const float3x3 SqrtAcescgToSrgb =
{
     1.30083470, -0.05557223, -0.01175251,
    -0.26355115,  1.06027029, -0.05999115,
    -0.03728355, -0.00469806,  1.07174366
};

/* Helper functions *********************************************************************/

float3 LinearToSrgb(float3 color)
{
    float3 lowPart = 12.92 * color;
    float3 highPart = 1.055 * pow(color, 1 / 2.4) - 0.055;
    float3 selector = step(color, 0.0031308);
    return lerp(highPart, lowPart, selector);
}

// Dithering in sRGB isn't technically correct but the difference is too small to matter (around 10^-5)
// Also dark colors in sRGB are mapped linearly so there is no difference for dark colors
float3 DitherNoise(float2 position)
{
    return (
        (1.0 / 256) * tex2D(DitherSampler, (1.0 / DITHER_TEXTURE_SIZE) * position).r - 0.5 / 255
    ).xxx;
}

// Input color should be in output gamma
float3 Dither(float3 color, float2 position)
{
    float3 lo = (1.0 / 255) * floor(255 * color);
    float3 hi = lo + 1.0 / 255;
    float3 loLinear = pow(lo, OutputGamma);
    float3 hiLinear = pow(hi, OutputGamma);

    float3 t = (pow(color, OutputGamma) - loLinear) / (hiLinear - loLinear);
    float rand = (255.0 / 256) * tex2D(DitherSampler, (1.0 / DITHER_TEXTURE_SIZE) * position).r;
    float3 selector = step(t, rand);

    return lerp(hi, lo, selector);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

/* Vertex shaders ***********************************************************************/

void Blit_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
}

/* Pixel shaders ************************************************************************/

float4 Brighten_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb *= BrightnessMult;
    return color;
}

float3 GammaToLinearColor(float3 color, float exposure)
{
    color.rgb = max(color.rgb, 0); // prevent NaN and negative numbers
    color.rgb = pow(color.rgb, GammaRatio);
    color.rgb = min(color.rgb, 10000); // prevent infinity
    color.rgb *= exposure;
    return color;
}

float4 GammaToLinearNoAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = GammaToLinearColor(color.rgb, Exposure);
    return float4(color.rgb, 1);
}

float4 GammaToLinear_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = GammaToLinearColor(color.rgb, Exposure);
    return color;
}

float4 CombineLayersNoAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    return float4((1 - foregroundColor.a) * backgroundColor.rgb + foregroundColor.rgb, 1);
}

float4 CombineLayers_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    return (1 - foregroundColor.a) * backgroundColor + foregroundColor;
}

float4 CombineLayersGammaToLinearNoAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    foregroundColor.rgb = GammaToLinearColor(foregroundColor.rgb, Exposure);
    backgroundColor.rgb = GammaToLinearColor(backgroundColor.rgb, BackgroundExposure);
    return float4((1 - foregroundColor.a) * backgroundColor.rgb + foregroundColor.rgb, 1);
}

float4 CombineLayersGammaToLinear_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    foregroundColor.rgb = GammaToLinearColor(foregroundColor.rgb, Exposure);
    backgroundColor.rgb = GammaToLinearColor(backgroundColor.rgb, BackgroundExposure);
    return (1 - foregroundColor.a) * backgroundColor + foregroundColor;
}

float4 GammaToGammaDitherNoAlpha_PS(
    float2 coords : TEXCOORD0, float2 position : SV_Position
) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = Dither(pow(color.rgb, GammaRatio), position);
    return float4(color.rgb, 1);
}

float4 GammaToGammaDither_PS(
    float2 coords : TEXCOORD0, float2 position : SV_Position
) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = Dither(pow(color.rgb, GammaRatio), position);
    return color;
}

float4 GammaToGammaNoDitherNoAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = pow(color.rgb, GammaRatio);
    return float4(color.rgb, 1);
}

float4 GammaToGammaNoDither_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = pow(color.rgb, GammaRatio);
    return color;
}

float4 GammaToSrgbDitherNoAlpha_PS(
    float2 coords : TEXCOORD0, float2 position : SV_Position
) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = LinearToSrgb(pow(color.rgb, GammaRatio)) + DitherNoise(position);
    return float4(color.rgb, 1);
}

float4 GammaToSrgbNoDitherNoAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = LinearToSrgb(pow(color.rgb, GammaRatio));
    return float4(color.rgb, 1);
}

float4 BloomComposite_PS(float2 coords : TEXCOORD0) : COLOR0
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
    float luminance = Luminance(x);
    if (luminance <= 0)
    {
        return x;
    }

	float minComponent = min(x.r, min(x.g, x.b));
	float saturation = saturate(1 - minComponent / luminance);
	if (saturation <= 0)
	{
	    return x;
	}
	
	float targetSaturation = SaturationCurve(saturation);
	float mult = targetSaturation / saturation;
	float3 result = max(lerp(luminance.xxx, x, mult), 0.0);
	return result;
}

float4 VibranceBoost_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = max(MakeVibrant(max(color.rgb, 0.0)), 0);
    return color;
}

float3 ToneMapColorNeutralLms(float3 x)
{
    const float c1 = 2.05;
    const float c2 = 5.25;
    x = mul(x, SrgbToLmsD65);
    x = saturate(c1 * (x / (x + c2)));
    return saturate(mul(x, LmsD65ToSrgb));
}

float4 ToneMapNeutralLms_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColorNeutralLms(color.rgb);
    return color;
}

float3 ToneMapColorNeutralOld(float3 x)
{
    const float c1 = 2.05;
    const float c2 = 5.25;
    x = mul(x, SqrtSrgbToAcescg);
    x = saturate(c1 * (x / (x + c2)));
    return saturate(mul(x, SqrtAcescgToSrgb));
}

float4 ToneMapNeutralOld_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColorNeutralOld(color.rgb);
    return color;
}

float3 ToneMapColorFilmicSrgb(float3 x)
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

float4 ToneMapFilmicSrgb_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColorFilmicSrgb(color.rgb);
    return color;
}

/* Techniques ***************************************************************************/

technique BrightenPixelOnly
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 Brighten_PS();
    }
}

technique Brighten
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 Brighten_PS();
    }
}

technique GammaToLinearNoAlpha
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToLinearNoAlpha_PS();
    }
}

technique GammaToLinear
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToLinear_PS();
    }
}

technique CombineLayersNoAlpha
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 CombineLayersNoAlpha_PS();
    }
}

technique CombineLayers
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 CombineLayers_PS();
    }
}

technique CombineLayersGammaToLinearNoAlpha
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 CombineLayersGammaToLinearNoAlpha_PS();
    }
}

technique CombineLayersGammaToLinear
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 CombineLayersGammaToLinear_PS();
    }
}

technique GammaToGammaDitherNoAlpha
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToGammaDitherNoAlpha_PS();
    }
}

technique GammaToGammaDither
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToGammaDither_PS();
    }
}

technique GammaToGammaNoDitherNoAlpha
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToGammaNoDitherNoAlpha_PS();
    }
}

technique GammaToGammaNoDither
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToGammaNoDither_PS();
    }
}

technique GammaToSrgbDitherNoAlpha
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToSrgbDitherNoAlpha_PS();
    }
}

technique GammaToSrgbNoDitherNoAlpha
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToSrgbNoDitherNoAlpha_PS();
    }
}

technique BloomComposite
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 BloomComposite_PS();
    }
}

technique VibranceBoost
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 VibranceBoost_PS();
    }
}

technique ToneMapNeutralLms
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneMapNeutralLms_PS();
    }
}

technique ToneMapNeutralOld
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneMapNeutralOld_PS();
    }
}
technique ToneMapFilmicSrgb
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneMapFilmicSrgb_PS();
    }
}