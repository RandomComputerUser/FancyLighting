sampler TextureSampler : register(s0);
sampler LightSampler : register(s4);

float4x4 MatrixTransform;

float4x4 LightMapMatrixTransform;
float Zoom;

float NormalMapResolution;
float2 NormalMapGradientMult;
float NormalMapStrength;
float2 SkyLightGradient;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct NormalsVertexShaderOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float2 LightMapTexCoord : TEXCOORD1;
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

// Assumes only rotation and no scaling or stretching
SamplingTransform CalculateSamplingTransform(float2 texCoord)
{
    SamplingTransform output;

    float2 partialX = Zoom * ddx(texCoord);
    float2 partialY = Zoom * ddy(texCoord);
    
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

float SampleForNormal(float2 texCoord, float fallback)
{
    float4 color = tex2D(TextureSampler, texCoord);
    return color.a < 1 ? fallback : saturate(Luma(color.rgb));
}

float3 NormalsSurfaceGradientAndMult(float2 texCoord, float2 diff)
{
    float4 color = tex2D(TextureSampler, texCoord);
    float luma = saturate(Luma(color.rgb));
    
    float leftLuma = SampleForNormal(texCoord - float2(diff.x, 0), luma);
    float rightLuma = SampleForNormal(texCoord + float2(diff.x, 0), luma);
    float upLuma = SampleForNormal(texCoord - float2(0, diff.y), luma);
    float downLuma = SampleForNormal(texCoord + float2(0, diff.y), luma);
    float positiveDiagonal
        = SampleForNormal(texCoord - diff, luma) // up left
        - SampleForNormal(texCoord + diff, luma); // down right
    float negativeDiagonal
        = SampleForNormal(texCoord - float2(diff.x, -diff.y), luma) // down left
        - SampleForNormal(texCoord + float2(diff.x, -diff.y), luma); // up right

    float horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return float3(
        mult * Gradient(horizontalColorDiff, verticalColorDiff),
        color.a < 1.0 ? 0.0 : 1.0
    );
}

float2 NormalsLightGradient(float2 lightMapTexCoord)
{
    float3 light = tex2D(LightSampler, lightMapTexCoord).rgb;
    float luma = Luma(light);
    return NormalMapGradientMult * float2(ddx(luma), ddy(luma));
}

float4 NormalsLightGradientFancySky(float2 lightMapTexCoord)
{
    float4 light = tex2D(LightSampler, lightMapTexCoord);
    float luma = Luma(light.rgb);
    return float4(
        NormalMapGradientMult * float2(ddx(luma), ddy(luma)),
        SkyLightGradient * light.a
    );
}

float NormalsMultiplier(float2 texCoord, float2 lightMapTexCoord)
{
    SamplingTransform samplingTransform = CalculateSamplingTransform(texCoord);
    float2 diff = samplingTransform.TexelSize * NormalMapResolution;
    
    float2 lightGradient = NormalsLightGradient(lightMapTexCoord);
    lightGradient = mul(lightGradient, samplingTransform.ScalingAndRotation);
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(texCoord, diff);
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0
        ? 0
        : surfaceGradient / surfaceGradientLength;
    surfaceGradient *= surfaceGradientAndMult.z;
    
    float lightMult = 1.0 + NormalMapStrength * dot(lightGradient, surfaceGradient);
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float NormalsMultiplierFancySky(float2 texCoord, float2 lightMapTexCoord)
{
    SamplingTransform samplingTransform = CalculateSamplingTransform(texCoord);
    float2 diff = samplingTransform.TexelSize * NormalMapResolution;
    
    float4 lightAndSkyLightGradient = NormalsLightGradientFancySky(lightMapTexCoord);
    
    float2 lightGradient = lightAndSkyLightGradient.xy + lightAndSkyLightGradient.zw;
    lightGradient = mul(lightGradient, samplingTransform.ScalingAndRotation);
    float lightGradientLength = length(lightGradient);
    
    if (lightGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float skyLightGradientLength = length(lightAndSkyLightGradient.zw);
    float shininess = skyLightGradientLength / (
        skyLightGradientLength + length(lightAndSkyLightGradient.xy)
    );
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(texCoord, diff);
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    surfaceGradient = surfaceGradientLength == 0
        ? 0 
        : surfaceGradient / surfaceGradientLength;
    surfaceGradient *= surfaceGradientAndMult.z;
    
    float lightMult = dot(lightGradient, surfaceGradient);
    lightMult += (0.333 + 0.1 * lightMult) * shininess * Square(lightMult);
    lightMult = 1.0 + NormalMapStrength * lightMult;
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (32.0 * lightGradientLength + 1))
    );
}

float4 NormalsPS(in NormalsVertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplier(input.TexCoord, input.LightMapTexCoord);

    return lightColor * float4(mult.xxx, 1) * texColor;
}

float4 NormalsFancySkyPS(in NormalsVertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TexCoord, input.LightMapTexCoord);

    return lightColor * float4(mult.xxx, 1) * texColor;
}

float4 NormalsLightOnlyPS(in NormalsVertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplier(input.TexCoord, input.LightMapTexCoord);
    
    return lightColor * float4(mult.xxx, 1) * texColor.a;
}

float4 NormalsLightOnlyFancySkyPS(in NormalsVertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TexCoord, input.LightMapTexCoord);
    
    return lightColor * float4(mult.xxx, 1) * texColor.a;
}

NormalsVertexShaderOutput NormalsVS(in VertexShaderInput input)
{
    NormalsVertexShaderOutput output;
    
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    output.LightMapTexCoord = mul(input.Position, LightMapMatrixTransform);
    
    return output;
}

float4 LightOnlyPS(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    return color * tex2D(TextureSampler, texCoord).a;
}

technique Normals
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 NormalsVS();
        PixelShader = compile ps_3_0 NormalsPS();
    }
}

technique NormalsFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 NormalsVS();
        PixelShader = compile ps_3_0 NormalsFancySkyPS();
    }
}

technique NormalsLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 NormalsVS();
        PixelShader = compile ps_3_0 NormalsLightOnlyPS();
    }
}

technique NormalsLightOnlyFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 NormalsVS();
        PixelShader = compile ps_3_0 NormalsLightOnlyFancySkyPS();
    }
}

technique LightOnly
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 LightOnlyPS();
    }
}
