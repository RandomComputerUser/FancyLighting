sampler TextureSampler : register(s0);

float4x4 MatrixTransform;

float PixelSize;
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

struct SamplingTransform
{
    float2 TexelSize;
    float2 TextureSize;
    float2x2 ScalingAndRotation;
};

// Assumes only rotation and/or flipping and no scaling or stretching
SamplingTransform CalculateSamplingTransform(float2 texCoord)
{
    SamplingTransform output;

    float2 partialX = ddx(texCoord);
    float2 partialY = ddy(texCoord);
    
    float2 texelSize = float2(
        length(float2(partialX.x, partialY.x)),
        length(float2(partialX.y, partialY.y))
    );
    float2 textureSize = 1.0 / texelSize;
    
    output.TexelSize = texelSize;
    output.TextureSize = textureSize;
    output.ScalingAndRotation = float2x2(
        partialX * textureSize,
        partialY * textureSize
    );
    
    return output;
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

float SampleForNormal(float2 texCoord)
{
    float4 color = tex2D(TextureSampler, texCoord);
    return saturate(Luma(color.rgb));
}

float2 NormalsSurfaceGradient(float2 texCoord, float2 diff)
{
    float4 color = tex2D(TextureSampler, texCoord);
    float luma = saturate(Luma(color.rgb));
    
    float leftLuma = SampleForNormal(texCoord - float2(diff.x, 0));
    float rightLuma = SampleForNormal(texCoord + float2(diff.x, 0));
    float upLuma = SampleForNormal(texCoord - float2(0, diff.y));
    float downLuma = SampleForNormal(texCoord + float2(0, diff.y));
    float positiveDiagonal
        = SampleForNormal(texCoord - diff) // up left
        - SampleForNormal(texCoord + diff); // down right
    float negativeDiagonal
        = SampleForNormal(texCoord - float2(diff.x, -diff.y)) // down left
        - SampleForNormal(texCoord + float2(diff.x, -diff.y)); // up right

    float horizontalColorDiff = 0.7071068 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.7071068 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return mult * Gradient(horizontalColorDiff, verticalColorDiff);
}


float NormalsMultiplierFancySky(float2 texCoord)
{
    SamplingTransform samplingTransform = CalculateSamplingTransform(texCoord);
    float2 diff = samplingTransform.TexelSize * PixelSize;
    
    float2 lightGradient = SkyLightGradient;
    lightGradient = mul(lightGradient, samplingTransform.ScalingAndRotation);
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float2 surfaceGradient = NormalsSurfaceGradient(texCoord, diff);
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

float4 CloudShadingPS(in VertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TexCoord);

    return lightColor * lerp(texColor, float4(mult.xxx, 1) * texColor.a, SkyLightMult);
}

technique CloudShading
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 CloudShadingPS();
    }
}
