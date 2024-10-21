sampler LightSampler : register(s0);
sampler DitherSampler : register(s4);

float2 LightMapSize;
float2 PixelSize;
float2 DitherCoordMult;

#define CUBIC_MULT 0.5503212081491045 // 1 / cbrt(6)

// https://stackoverflow.com/a/42179924
// https://web.archive.org/web/20180927181721/http://www.java-gaming.org/index.php?topic=35123.0
float4 Cubic(float v)
{
    float3 n = float3(1.0 * CUBIC_MULT, 2.0 * CUBIC_MULT, 3.0 * CUBIC_MULT) - v;
    n *= n * n;
    float x = n.x;
    float y = n.y - 4.0 * n.x;
    float z = n.z - 4.0 * n.y + 6.0 * n.x;
    float w = 1.0 - x - y - z;
    return float4(x, y, z, w);
}

float3 BicubicColor(float2 coords)
{
    float2 texCoords = LightMapSize * coords - 0.5;

    float2 fxy = frac(texCoords);
    texCoords -= fxy;
    fxy *= CUBIC_MULT;

    float4 xcubic = Cubic(fxy.x);
    float4 ycubic = Cubic(fxy.y);

    float4 c = texCoords.xxyy + float2(-0.5, 1.5).xyxy;

    float4 s = float4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
    float4 offset = c + float4(xcubic.yw, ycubic.yw) / s;

    offset *= PixelSize.xxyy;

    float3 sample0 = tex2D(LightSampler, offset.xz).rgb;
    float3 sample1 = tex2D(LightSampler, offset.yz).rgb;
    float3 sample2 = tex2D(LightSampler, offset.xw).rgb;
    float3 sample3 = tex2D(LightSampler, offset.yw).rgb;

    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);

    return lerp(lerp(sample3, sample2, sx), lerp(sample1, sample0, sx), sy);
}

float4 BicubicDither(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = BicubicColor(coords);

    // Dithering
    color += (tex2D(DitherSampler, coords * DitherCoordMult).rgb - 128 / 255.0) * (0.5 / 128);

    return float4(color, 1);
}

float4 BicubicNoDitherHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = BicubicColor(coords);

    // No dithering
    // Dithering is done in the light rendering HiDef shaders

    return float4(color, 1);
}

float4 NoFilter(float2 coords : TEXCOORD0) : COLOR0
{
    return tex2D(LightSampler, coords);
}

technique Technique1
{
    pass BicubicDither
    {
        PixelShader = compile ps_2_0 BicubicDither();
    }

    pass BicubicNoDitherHiDef
    {
        PixelShader = compile ps_3_0 BicubicNoDitherHiDef();
    }

    pass NoFilter
    {
        PixelShader = compile ps_2_0 NoFilter();
    }
}
