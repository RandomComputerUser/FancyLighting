sampler ScreenSampler : register(s0);
sampler TextureSampler : register(s0);
sampler BackgroundSampler : register(s8);
sampler DitherSampler : register(s8);
sampler BloomBlurSampler : register(s8);

#define DITHER_TEXTURE_SIZE 32

float4x4 MatrixTransform;

float BrightnessMult;
float GammaRatio;
float BackgroundGamma;
float OutputGamma;
float Exposure;
float BackgroundExposure;
float BloomStrength;

float4 VibranceBoostParams1;
float2 VibranceBoostParams2;
     
static const float3x3 SrgbToCustomWcg =
{
    0.60, 0.13, 0.02,
    0.39, 0.85, 0.10,
    0.01, 0.02, 0.88
};

static const float3x3 CustomWcgToSrgb =
{
     1.85065741e+00, -2.82808236e-01, -9.92309601e-03,
    -8.48920863e-01,  1.30935252e+00, -1.29496403e-01,
    -1.73654180e-03, -2.65442818e-02,  1.13941950e+00
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

float3 GammaToLinearColor(float3 color, float exposure, float gamma)
{
    color.rgb = max(color.rgb, 0); // prevent NaN and negative numbers
    color.rgb = pow(color.rgb, gamma);
    color.rgb = min(color.rgb, 10000); // prevent infinity
    color.rgb *= exposure;
    return color;
}

float SaturationCurve(float x)
{
    x = VibranceBoostParams1.x + VibranceBoostParams1.y * sqrt(
        VibranceBoostParams1.z + VibranceBoostParams1.w * x
    );
    return saturate(VibranceBoostParams2.x * x * (VibranceBoostParams2.y + x));
}

float3 ColorGrade(float3 x)
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
	return max(lerp(luminance.xxx, x, mult), 0.0);
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

void Brighten_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    inout float4 color : COLOR0,
    out float4 screenPos : SV_Position
)
{
    screenPos = mul(position, MatrixTransform);
    color.rgb *= BrightnessMult;
}

/* Pixel shaders ************************************************************************/

float4 SpriteBatch_PS(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    return color * tex2D(TextureSampler, coords);
}

float4 GammaToLinear_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = GammaToLinearColor(color.rgb, Exposure, GammaRatio);
    return color;
}

float4 GammaToLinearColorGraded_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ColorGrade(GammaToLinearColor(color.rgb, Exposure, GammaRatio));
    return color;
}

float4 CombineLayersGammaToLinearColor(float4 foregroundColor, float4 backgroundColor)
{
    foregroundColor.rgb = GammaToLinearColor(foregroundColor.rgb, Exposure, GammaRatio);
    backgroundColor.rgb = GammaToLinearColor(backgroundColor.rgb, BackgroundExposure, BackgroundGamma);
    return (1 - foregroundColor.a) * backgroundColor + foregroundColor;
}

float4 CombineLayersGammaToLinear_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    return CombineLayersGammaToLinearColor(foregroundColor, backgroundColor);
}

float4 CombineLayersGammaToLinearColorGraded_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    float4 color = CombineLayersGammaToLinearColor(foregroundColor, backgroundColor);
    color.rgb = ColorGrade(color.rgb);
    return color;
}

float4 CombineLayers_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 foregroundColor = tex2D(ScreenSampler, coords);
    float4 backgroundColor = tex2D(BackgroundSampler, coords);
    return (1 - foregroundColor.a) * backgroundColor + foregroundColor;
}

float4 GammaToGammaDither_PS(
    float2 coords : TEXCOORD0, float2 position : SV_Position
) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = Dither(pow(color.rgb, GammaRatio), position);
    return color;
}

float4 GammaToGammaNoDither_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = pow(color.rgb, GammaRatio);
    return color;
}

float4 GammaToSrgbDither_PS(
    float2 coords : TEXCOORD0, float2 position : SV_Position
) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = LinearToSrgb(pow(color.rgb, GammaRatio)) + DitherNoise(position);
    return color;
}

float4 GammaToSrgbNoDither_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = LinearToSrgb(pow(color.rgb, GammaRatio));
    return color;
}

float4 BloomComposite_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    float3 bloomColor = tex2D(BloomBlurSampler, coords).rgb;
    color.rgb += BloomStrength * bloomColor;
    return color;
}

float3 ToneMapColorBrilliant(float3 x)
{
    const float c1 = 2.05;
    const float c2 = 5.25;
    x = mul(x, SrgbToCustomWcg);
    x = saturate(c1 * (x / (x + c2)));
    return saturate(mul(x, CustomWcgToSrgb));
}

float4 ToneMapBrilliant_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColorBrilliant(color.rgb);
    return color;
}

float3 ToneMapColorVibrant(float3 x)
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

float4 ToneMapVibrant_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = ToneMapColorVibrant(color.rgb);
    return color;
}

/* Techniques ***************************************************************************/

technique Brighten
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Brighten_VS();
        PixelShader = compile ps_3_0 SpriteBatch_PS();
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

technique GammaToLinearColorGraded
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToLinearColorGraded_PS();
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

technique CombineLayersGammaToLinearColorGraded
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 CombineLayersGammaToLinearColorGraded_PS();
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

technique GammaToGammaDither
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToGammaDither_PS();
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

technique GammaToSrgbDither
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToSrgbDither_PS();
    }
}

technique GammaToSrgbNoDither
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 GammaToSrgbNoDither_PS();
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

technique ToneMapBrilliant
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneMapBrilliant_PS();
    }
}

technique ToneMapVibrant
{    
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneMapVibrant_PS();
    }
}