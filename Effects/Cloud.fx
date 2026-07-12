sampler TextureSampler : register(s0);

float4x4 MatrixTransform;

float Scale;
float2 SkyLightGradient;
float SkyLightMult;
float ShadingStrength;

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

float SampleTexture(float2 texCoord, bool wrap)
{
    float3 color = tex2D(TextureSampler, texCoord).rgb;
    if (!wrap)
    {
        color *= any(texCoord <= 0.0 || texCoord >= 1.0) ? 0 : 1;
    }
    
    return saturate((Luma(color) - 0.65) * (1.0 / (1.0 - 0.65)));
}

float2 NormalsSurfaceGradient(float2 texCoord, float4 diff, bool wrap)
{
    float center = SampleTexture(texCoord, wrap);
    float2 sum = 0;
    [unroll]
    for (int dy = -2; dy <= 2; ++dy)
    {
        [unroll]
        for (int dx = -2; dx <= 2; ++dx)
        {
            if (abs(dx) + abs(dy) > 3 || dx == 0 && dy == 0)
            {
                continue;
            }
        
            sum += (SampleTexture(
                texCoord + dx * diff.xy + dy * diff.zw, wrap
            ) - center) * float2(dx == 0 ? 0.0 : 1.0 / dx, dy == 0 ? 0.0 : 1.0 / dy);
        }
    }
    
    sum *= -0.5 / (5.0 + 3.0 / 2.0);
    return sum;
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
    
    float lightMult = dot(lightGradient, surfaceGradient);
    lightMult -= (0.3 - 0.1 * lightMult) * Square(lightMult);
    lightMult = 1.0 + ShadingStrength * lightMult;
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float4 CloudShadingColor(in VertexShaderOutput input, bool wrap)
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TexCoord, wrap);

    return lightColor * lerp(
        texColor, 
        float4(mult * float3(196 / 255.0, 223 / 255.0, 244 / 255.0), 1) * texColor.a, 
        SkyLightMult * lightColor.a
    );
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
