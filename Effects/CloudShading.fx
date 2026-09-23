sampler TextureSampler : register(s0);

sampler LuminanceSampler : register(s0);
sampler CloudSampler : register(s8);

float4x4 MatrixTransform;

float2 Scale;
float2 CloudScale;

float2 PixelSize;

float Gamma;
float InverseGamma;

float3 BaseColorSlope;
float3 BaseColorIntercept;
float ShadowLuminance;
float3 ShadowColorSlope;

float2 SkyLightGradient;
float SkyLightMult;
float NormalMapStrength;

/* Helper functions *********************************************************************/

float Luminance(float3 color)
{
    return dot(pow(max(color, 0), Gamma), float3(0.2126, 0.7152, 0.0722));
}

float2x2 CalculateRotationMatrix(float2 texCoord)
{
    float2 partialX = ddx(texCoord);
    float2 partialY = ddy(texCoord);
    
    float2 texelSize = float2(
        length(float2(partialX.x, partialY.x)),
        length(float2(partialX.y, partialY.y))
    );
    float2 textureSize = 1.0 / texelSize;
    
    return float2x2(
        partialX * textureSize,
        partialY * textureSize
    );
}

float NormalsMultiplierCloud(float2 surfaceGradient, float2 texCoord)
{
    float surfaceGradientLength = length(surfaceGradient);
    
    float2 lightGradient = SkyLightGradient;
    float2x2 rotationMatrix = CalculateRotationMatrix(texCoord);
    lightGradient = mul(lightGradient, rotationMatrix);
    float lightGradientLength = length(lightGradient);
    
    if (surfaceGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float lightMult = 1.0 + clamp(
        NormalMapStrength * dot(lightGradient, surfaceGradient),
        -0.9,
        0.9
    );
    return lerp(
        1.0,
        lightMult,
        saturate(surfaceGradientLength)
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

void ExtractLuminance_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    texCoord = Scale * (texCoord - 0.5) + 0.5;
}

void GenerateGradients_VS(
    float4 position : POSITION0,
    float2 texCoord : TEXCOORD0,
    out float2 luminanceTexCoord : TEXCOORD0,
    out float2 cloudTexCoord : TEXCOORD1,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    luminanceTexCoord = Scale * (texCoord - 0.5) + 0.5;
    cloudTexCoord = CloudScale * (texCoord - 0.5) + 0.5;
}

/* Pixel shaders ************************************************************************/

float4 ExtractLuminance_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, texCoord);
    if (
        texCoord.x < 0
        || texCoord.x > 1
        || texCoord.y < 0
        || texCoord.y > 1
    )
    {
        texColor = float4(0, 0, 0, 1);
    }
    float luminance = saturate(Luminance(texColor.rgb));
    return float4(luminance, 0, 0, 1);
}

float4 GenerateGradients_PS(
    float2 luminanceTexCoord : TEXCOORD0,
    float2 cloudTexCoord : TEXCOORD1
) : COLOR0
{
    float blurredLuminance = tex2D(LuminanceSampler, luminanceTexCoord).r;
    float4 cloudColor = tex2D(CloudSampler, cloudTexCoord);
    if (
        cloudTexCoord.x < 0
        || cloudTexCoord.x > 1
        || cloudTexCoord.y < 0
        || cloudTexCoord.y > 1
    )
    {
        cloudColor = float4(0, 0, 0, 0);
    }
    
    float2 luminanceGradient = 20.0 * float2(
        ddx(blurredLuminance),
        ddy(blurredLuminance)
    );
    luminanceGradient = smoothstep(-1.0, 1.0, luminanceGradient);
    
    float luminance;
    if (cloudColor.a <= 0.0)
    {
        luminance = 0.0;
        float totalAlpha = 0.0;
        float4 neighborColor;
        
        neighborColor = tex2D(CloudSampler, cloudTexCoord - float2(PixelSize.x, 0));
        luminance += Luminance(neighborColor);
        totalAlpha += neighborColor.a;
        neighborColor = tex2D(CloudSampler, cloudTexCoord + float2(PixelSize.x, 0));
        luminance += Luminance(neighborColor);
        totalAlpha += neighborColor.a;
        neighborColor = tex2D(CloudSampler, cloudTexCoord - float2(0, PixelSize.y));
        luminance += Luminance(neighborColor);
        totalAlpha += neighborColor.a;
        neighborColor = tex2D(CloudSampler, cloudTexCoord + float2(0, PixelSize.y));
        luminance += Luminance(neighborColor);
        totalAlpha += neighborColor.a;
        
        if (totalAlpha <= 0.0)
        {
            luminance = 0.0;
            totalAlpha = 0.0;
            
            neighborColor = tex2D(CloudSampler, cloudTexCoord - PixelSize);
            luminance += Luminance(neighborColor);
            totalAlpha += neighborColor.a;
            neighborColor = tex2D(CloudSampler, cloudTexCoord + PixelSize);
            luminance += Luminance(neighborColor);
            totalAlpha += neighborColor.a;
            neighborColor = tex2D(
                CloudSampler, cloudTexCoord + float2(PixelSize.x, -PixelSize.y)
            );
            luminance += Luminance(neighborColor);
            totalAlpha += neighborColor.a;
            neighborColor = tex2D(
                CloudSampler, cloudTexCoord + float2(-PixelSize.x, PixelSize.y)
            );
            luminance += Luminance(neighborColor);
            totalAlpha += neighborColor.a;
            
            luminance = totalAlpha <= 0.0 ? 0.0 : luminance / totalAlpha;
        }
        else
        {
            luminance /= totalAlpha;
        }
    }
    else
    {
        luminance = Luminance(cloudColor.rgb) / cloudColor.a;
    }
    
    return float4(luminanceGradient, luminance, cloudColor.a);
}

float4 CloudShading_PS(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, texCoord);
    float2 surfaceGradient = -2 * texColor.xy + 1;
    float mult = NormalsMultiplierCloud(surfaceGradient, texCoord);
    
    float baseLuminance = texColor.z;
    float shadedLuminance = 0.8 * mult;
    float mixedLuminance = lerp(baseLuminance, shadedLuminance, SkyLightMult);
    float3 mixedColor =
        mixedLuminance < ShadowLuminance
            ? ShadowColorSlope * mixedLuminance
        : mixedLuminance > 1.0
            ? mixedLuminance.xxx
        : BaseColorSlope * mixedLuminance + BaseColorIntercept;
    
    return color * texColor.a * float4(pow(mixedColor, InverseGamma), 1);
}

/* Techniques ***************************************************************************/

technique ExtractLuminance
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 ExtractLuminance_VS();
        PixelShader = compile ps_3_0 ExtractLuminance_PS();
    }
}

technique GenerateGradients
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 GenerateGradients_VS();
        PixelShader = compile ps_3_0 GenerateGradients_PS();
    }
}

technique CloudShading
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SpriteBatch_VS();
        PixelShader = compile ps_3_0 CloudShading_PS();
    }
}
