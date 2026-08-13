sampler TextureSampler : register(s0);

float4x4 MatrixTransform;

float Scale;
float2 SkyLightGradient;
float SkyLightMult;
float ShadingStrength;

struct PixelShaderInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

/* Helper functions *********************************************************************/

float Square(float x)
{
    return x * x;
}

float Luma(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float SampleTexture(float2 texCoord, bool wrap)
{
    float3 color = tex2D(TextureSampler, texCoord).rgb;
    if (!wrap)
    {
        color *= all(texCoord == saturate(texCoord));
    }
    
    const float MIN_LUMA = 0.5;
    const float MULT = 1.0 / (1.0 - MIN_LUMA);
    return saturate(MULT * Luma(color) - MULT * MIN_LUMA);
}

float2 NormalsSurfaceGradient(float2 texCoord, float4 diff, bool wrap)
{
    float center = SampleTexture(texCoord, wrap);
    float2 sum = 0;
    [unroll]
    for (int dy = -3; dy <= 3; ++dy)
    {
        [unroll]
        for (int dx = -3; dx <= 3; ++dx)
        {
            if (abs(dx) + abs(dy) > 4 || dx == 0 && dy == 0)
            {
                continue;
            }
        
            float2 direction = float2(dx, dy);
            sum += (SampleTexture(
                texCoord + dx * diff.xy + dy * diff.zw, wrap
            ) - center) * direction / dot(direction, direction);
        }
    }
    
    sum *= -0.09375;
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

float4 CloudShadingColor(PixelShaderInput input, bool wrap)
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TexCoord, wrap);

    return lightColor * lerp(
        texColor, 
        float4(mult * float3(196 / 255.0, 223 / 255.0, 244 / 255.0), 1) * texColor.a, 
        SkyLightMult
    );
}

/* Vertex shaders ***********************************************************************/

void SpriteBatch_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    inout float4 color : COLOR0,
    out float4 screenPos : SV_Position
)
{
    screenPos = mul(position, MatrixTransform);
}

/* Pixel shaders ************************************************************************/

float4 CloudShading_PS(PixelShaderInput input) : COLOR0
{
    return CloudShadingColor(input, false);
}

float4 CloudShadingWrap_PS(PixelShaderInput input) : COLOR0
{
    return CloudShadingColor(input, true);
}

/* Techniques ***************************************************************************/

technique CloudShading
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SpriteBatch_VS();
        PixelShader = compile ps_3_0 CloudShading_PS();
    }
}

technique CloudShadingWrap
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SpriteBatch_VS();
        PixelShader = compile ps_3_0 CloudShadingWrap_PS();
    }
}
