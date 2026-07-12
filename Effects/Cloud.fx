sampler TextureSampler : register(s0);

float4x4 MatrixTransform;

float Scale;
float2 NormalMapGradientMult;
float2 SkyLightGradient;
float SkyLightMult;

const float NormalMapStrength = 0.9;

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float Square(float x)
{
    return x * x;
}

float Luma(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float2 Gradient(
    float horizontalColorDiff,
    float verticalColorDiff
)
{
    float2 gradient = float2(horizontalColorDiff, verticalColorDiff);
    gradient *= 0.5;
    return gradient;
}

// Intentionally use gamma-encoded values for simulating normal maps

float4 SampleTexture(float2 texCoord, bool wrap)
{
    float4 color = tex2D(TextureSampler, texCoord);
    if (!wrap)
    {
        color *= any(texCoord <= 0.0 || texCoord >= 1.0) ? 0 : 1;
    }
    
    return color;
}

float SampleForNormal(float2 texCoord, bool wrap)
{
    float4 color = SampleTexture(texCoord, wrap);
    return saturate(Luma(color.rgb));
}

float2 NormalsSurfaceGradient(float2 texCoord, float4 diff, bool wrap)
{
    float4 color = SampleTexture(texCoord, wrap);
    float luma = saturate(Luma(color.rgb));
    
    float leftLuma = SampleForNormal(texCoord - diff.xy, wrap);
    float rightLuma = SampleForNormal(texCoord + diff.xy, wrap);
    float upLuma = SampleForNormal(texCoord - diff.zw, wrap);
    float downLuma = SampleForNormal(texCoord + diff.zw, wrap);
    float positiveDiagonal
        = SampleForNormal(texCoord - diff.xy - diff.zw, wrap) // up left
        - SampleForNormal(texCoord + diff.zy + diff.zw, wrap); // down right
    float negativeDiagonal
        = SampleForNormal(texCoord - diff.xy + diff.zw, wrap) // down left
        - SampleForNormal(texCoord + diff.xy - diff.zw, wrap); // up right

    float horizontalColorDiff = 0.7071068 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.7071068 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return mult * Gradient(horizontalColorDiff, verticalColorDiff);
}


float NormalsMultiplierFancySky(float2 texCoord, bool wrap)
{
    float4 diff = Scale * float4(ddx(texCoord), ddy(texCoord));
    
    float2 lightGradient = SkyLightGradient;
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float2 surfaceGradient = NormalsSurfaceGradient(texCoord, diff, wrap);
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0
        ? 0 
        : surfaceGradient / surfaceGradientLength;
    
    float lightMult = 1.0 + dot(lightGradient, surfaceGradient);
    return lerp(
        1.0,
        lightMult,
        NormalMapStrength
            * sqrt(surfaceGradientLength)
            * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float4 CloudShadingColor(in VertexShaderOutput input, bool wrap)
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TexCoord, wrap);

    return lightColor * lerp(texColor, float4(mult.xxx, 1) * texColor.a, SkyLightMult);
}

float4 CloudShadingPS(in VertexShaderOutput input) : COLOR0
{
    return CloudShadingColor(input, false);
}

float4 CloudShadingWrapPS(in VertexShaderOutput input) : COLOR0
{
    return CloudShadingColor(input, true);
}

technique CloudShading
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 CloudShadingPS();
    }
}

technique CloudShadingWrap
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 CloudShadingWrapPS();
    }
}
