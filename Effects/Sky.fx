sampler TextureSampler : register(s0);
sampler DitherSampler : register(s0);

#define DITHER_TEXTURE_SIZE 32

float4x4 MatrixTransform;

float HighSkyLevel;
float LowSkyLevel;
float3 HighSkyColor;
float3 LowSkyColor;

float Gamma;
float InverseGamma;

/* Helper functions *********************************************************************/

float Smootherstep(float t)
{
    t = saturate(t);
    return (t * t * t) * (t * (6 * t - 15) + 10);
}

// Not technically correct because it ignores gamma, but cheap and decent quality
float4 Dithered(float2 position, float4 color)
{
    float noise
        = (1.0 / 256) * tex2D(DitherSampler, (1.0 / DITHER_TEXTURE_SIZE) * position).r
        - 0.5 / 255;
    
    color.rgb += noise;
    
    return color;
}

float4 CalculateSkyColor(float2 coords)
{
    float t = Smootherstep(coords.y);
    return float4(pow(lerp(HighSkyColor, LowSkyColor, t), InverseGamma), 1);
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

void Sky_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    texCoord.y = (texCoord.y - HighSkyLevel) / (LowSkyLevel - HighSkyLevel);
}

/* Pixel shaders ************************************************************************/

float4 Sky_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    return CalculateSkyColor(texCoord);
}

float4 SkyDithered_PS(float2 texCoord : TEXCOORD0, float4 position : SV_Position) : COLOR0
{
    return Dithered(position, CalculateSkyColor(texCoord));
}

float4 Sun_PS(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    const float brightness = 1.1;

    float4 baseColor = tex2D(TextureSampler, texCoord);
    baseColor.rgb = pow(baseColor.rgb, Gamma);
    baseColor.rgb += (120 / brightness) * pow(baseColor.rgb, 12);
    
    // Desaturate
    float brightest = max(max(baseColor.r, baseColor.g), baseColor.b);
    baseColor.rgb = lerp(baseColor.rgb, brightest.xxx, 0.5);
    
    baseColor.rgb = pow(brightness * baseColor.rgb, InverseGamma);
    return color * baseColor;
}

float4 SunHiDef_PS(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 baseColor = tex2D(TextureSampler, texCoord);
    baseColor.rgb = pow(baseColor.rgb, Gamma);
    baseColor.rgb *= (
        1
        + 120 * pow(
            max(baseColor.r, max(baseColor.g, baseColor.b)), 
            12
        )
    );
    
    // Desaturate
    float brightest = max(max(baseColor.r, baseColor.g), baseColor.b);
    baseColor.rgb = lerp(baseColor.rgb, brightest.xxx, 0.45);
    
    baseColor.rgb = pow(baseColor.rgb, InverseGamma);
    return color * baseColor;
}

/* Techniques ***************************************************************************/

technique Sky
{   
    pass Pass1
    {
        VertexShader = compile vs_3_0 Sky_VS();
        PixelShader = compile ps_3_0 Sky_PS();
    }
}

technique SkyDithered
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Sky_VS();
        PixelShader = compile ps_3_0 SkyDithered_PS();
    }
}

technique Sun
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SpriteBatch_VS();
        PixelShader = compile ps_3_0 Sun_PS();
    }
}

technique SunHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SpriteBatch_VS();
        PixelShader = compile ps_3_0 SunHiDef_PS();
    }
}
