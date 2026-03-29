// For some reason #include isn't working with EasyXnb
// So instead, more code is included in this file
/* BEGIN Gamut Clipping *****************************************************************/

// Code adapted from https://bottosson.github.io/posts/gamutclipping/

/*
Copyright (c) 2021 Björn Ottosson

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#define FLT_MAX 3.402823466e+38f

float3 linear_srgb_to_oklab(float3 c)
{
    float3 lms = mul(
        float3x3(
            0.4122214708f, 0.5363325363f, 0.0514459929f,
            0.2119034982f, 0.6806995451f, 0.1073969566f,
            0.0883024619f, 0.2817188376f, 0.6299787005f
        ),
        c
    );

    float3 lms_ = pow(lms, 1.f / 3);

    return mul(
        float3x3(
            0.2104542553f,  0.7936177850f, -0.0040720468f,
            1.9779984951f, -2.4285922050f,  0.4505937099f,
            0.0259040371f,  0.7827717662f, -0.8086757660f
        ),
        lms_
    );
}

float3 oklab_to_linear_srgb(float3 c)
{
    float3 lms_ = mul(
        float3x3(
            1.f,  0.3963377774f,  0.2158037573f,
            1.f, -0.1055613458f, -0.0638541728f,
            1.f, -0.0894841775f, -1.2914855480f
        ),
        c
    );
    
    float3 lms = lms_ * lms_ * lms_;

    return mul(
        float3x3(
             4.0767416621f, -3.3077115913f,  0.2309699292f,
            -1.2684380046f,  2.6097574011f, -0.3413193965f,
            -0.0041960863f, -0.7034186147f,  1.7076147010f
        ),
        lms
    );
}

// Finds the maximum saturation possible for a given hue that fits in sRGB
// Saturation here is defined as S = C/L
// a and b must be normalized so a^2 + b^2 == 1
float compute_max_saturation(float2 ab)
{
    // Max saturation will be when one of r, g or b goes below zero.

    // Select different coefficients depending on which component goes below zero first
    float k0, k1, k2, k3, k4, wl, wm, ws;

    if (dot(float2(-1.88170328f, -0.80936493f), ab) > 1)
    {
        // Red component
        k0 = +1.19086277f; k1 = +1.76576728f; k2 = +0.59662641f; k3 = +0.75515197f; k4 = +0.56771245f;
        wl = +4.0767416621f; wm = -3.3077115913f; ws = +0.2309699292f;
    }
    else if (dot(float2(1.81444104f, -1.19445276f), ab) > 1)
    {
        // Green component
        k0 = +0.73956515f; k1 = -0.45954404f; k2 = +0.08285427f; k3 = +0.12541070f; k4 = +0.14503204f;
        wl = -1.2684380046f; wm = +2.6097574011f; ws = -0.3413193965f;
    }
    else
    {
        // Blue component
        k0 = +1.35733652f; k1 = -0.00915799f; k2 = -1.15130210f; k3 = -0.50559606f; k4 = +0.00692167f;
        wl = -0.0041960863f; wm = -0.7034186147f; ws = +1.7076147010f;
    }

    // Approximate max saturation using a polynomial:
    float S = k0 + k1 * ab.x + k2 * ab.y + k3 * ab.x * ab.x + k4 * ab.x * ab.y;

    // Do one step Halley's method to get closer
    // this gives an error less than 10e6, except for some blue hues where the dS/dh is close to infinite
    // this should be sufficient for most applications, otherwise do two/three steps 

	float3 k_lms = mul(
		float3x2(
			 0.3963377774f,  0.2158037573f,
			-0.1055613458f, -0.0638541728f,
			-0.0894841775f, -1.2914855480f
		),
		ab
	);

	{
		float3 lms_ = 1.f + S * k_lms;
    	float3 lms = lms_ * lms_ * lms_;
    	float3 lms_dS = 3.f * k_lms * lms_ * lms_;
    	float3 lms_dS2 = 6.f * k_lms * k_lms * lms_;

    	float f = dot(float3(wl, wm, ws), lms);
    	float f1 = dot(float3(wl, wm, ws), lms_dS);
    	float f2 = dot(float3(wl, wm, ws), lms_dS2);

    	S = S - f * f1 / (f1*f1 - 0.5f * f * f2);
	}

    return S;
}

// finds L_cusp and C_cusp for a given hue
// a and b must be normalized so a^2 + b^2 == 1
float2 find_cusp(float2 ab)
{
	// First, find the maximum saturation (saturation S = C/L)
	float S_cusp = compute_max_saturation(ab);

	// Convert to linear sRGB to find the first point where at least one of r,g or b >= 1:
	float3 rgb_at_max = oklab_to_linear_srgb(float3(1, S_cusp * ab));
	float L_cusp = pow(1.f / max(max(rgb_at_max.r, rgb_at_max.g), rgb_at_max.b), 1.f / 3);
	float C_cusp = L_cusp * S_cusp;

	return float2(L_cusp, C_cusp);
}

// Finds intersection of the line defined by 
// L = L0 * (1 - t) + t * L1;
// C = t * C1;
// a and b must be normalized so a^2 + b^2 == 1
float find_gamut_intersection(float2 ab, float L1, float C1, float L0)
{
	// Find the cusp of the gamut triangle
	float2 cusp = find_cusp(ab);

	// Find the intersection for upper and lower half separately
	float t;
	if (((L1 - L0) * cusp.y - (cusp.x - L0) * C1) <= 0.f)
	{
		// Lower half

		t = cusp.y * L0 / (C1 * cusp.x + cusp.y * (L0 - L1));
	}
	else
	{
		// Upper half

		// First intersect with triangle
		t = cusp.y * (L0 - 1.f) / (C1 * (cusp.x - 1.f) + cusp.y * (L0 - L1));

		// Then one-step Halley's method
		{
			float dL = L1 - L0;
			float dC = C1;

			float3 k_lms = mul(
				float3x2(
					 0.3963377774f,  0.2158037573f,
					-0.1055613458f, -0.0638541728f,
					-0.0894841775f, -1.2914855480f
				),
				ab
			);

			float3 lms_dt = dL + dC * k_lms;
			
			// If higher accuracy is required, 2 or 3 iterations of the following block can be used:
			{
				float L = L0 * (1.f - t) + t * L1;
				float C = t * C1;

				float3 lms_ = L + C * k_lms;
				float3 lms = lms_ * lms_ * lms_;
				float3 lmsdt = 3 * lms_dt * lms_ * lms_;
				float3 lmsdt2 = 6 * lms_dt * lms_dt * lms_;

				float3 rgb = mul(
					float3x3(
						 4.0767416621f, -3.3077115913f,  0.2309699292f,
						-1.2684380046f,  2.6097574011f, -0.3413193965f,
						-0.0041960863f, -0.7034186147f,  1.7076147010f
					),
					lms
				) - 1;
				float3 rgb1 = mul(
					float3x3(
                         4.0767416621f, -3.3077115913f,  0.2309699292f,
                        -1.2684380046f,  2.6097574011f, -0.3413193965f,
                        -0.0041960863f, -0.7034186147f,  1.7076147010f
                    ),
					lmsdt
				);
				float3 rgb2 = mul(
					float3x3(
                         4.0767416621f, -3.3077115913f,  0.2309699292f,
                        -1.2684380046f,  2.6097574011f, -0.3413193965f,
                        -0.0041960863f, -0.7034186147f,  1.7076147010f
                    ),
					lmsdt2
				);

				float3 u_rgb = rgb1 / (rgb1 * rgb1 - 0.5f * rgb * rgb2);
				float3 t_rgb = -rgb * u_rgb;
				t_rgb = lerp(FLT_MAX, t_rgb, step(0.f, u_rgb));

				t += min(t_rgb.r, min(t_rgb.g, t_rgb.b));
			}
		}
	}

	return t;
}

float3 gamut_clip_adaptive_L0_0_5(float3 rgb, float alpha = 0.05f)
{
	if (rgb.r < 1 && rgb.g < 1 && rgb.b < 1 && rgb.r > 0 && rgb.g > 0 && rgb.b > 0)
		return rgb;

	float3 lab = linear_srgb_to_oklab(rgb);

	float L = lab.x;
	float eps = 0.00001f;
	float C = max(eps, sqrt(dot(lab.yz, lab.yz)));
	float2 ab_ = lab.yz / C;

	float Ld = L - 0.5f;
	float e1 = 0.5f + abs(Ld) + alpha * C;
	float L0 = 0.5f*(1.f + sign(Ld)*(e1 - sqrt(e1*e1 - 2.f *abs(Ld))));

	float t = find_gamut_intersection(ab_, L, C, L0);
	float L_clipped = L0 * (1.f - t) + t * L;
	float C_clipped = t * C;

	return oklab_to_linear_srgb(float3(L_clipped, C_clipped * ab_));
}

float3 GamutClipLinearSrgb(float3 x)
{
	return saturate(gamut_clip_adaptive_L0_0_5(x, 0.5f));
}

/* END Gamut Clipping *******************************************************************/

sampler ScreenSampler : register(s0);
sampler DitherSampler : register(s4);
sampler BloomBlurSampler : register(s4);

float2 DitherCoordMult;
float GammaRatio;
float OutputGamma;
float Exposure;
float BloomStrength;

// https://www.colour-science.org/apps/

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
    float c1 = 1.6;
    float c2 = 3.0;
    return saturate(
        c1 * (x / (x + c2))
    );
}

float4 ToneMap1(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(ScreenSampler, coords);
    color.rgb = mul(P3ToAcescg, color.rgb);
    color.rgb = ToneMapColor1(color.rgb);
    color.rgb = GamutClipLinearSrgb(mul(AcescgToSrgb, color.rgb));
    return color;
}

float3 ToneMapColor2(float3 x)
{
    float c1 = 1.46666666667;
    float c2 = 0.363636363636;
    float c3 = 256;
    float c4 = 2;
    float c5 = 2.33333333333;
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
