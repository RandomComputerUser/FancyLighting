sampler TextureSampler : register(s0);
sampler LightSampler : register(s8);
sampler GlowSampler : register(s9);
sampler DitherSampler : register(s10);

#define DITHER_TEXTURE_SIZE 32

float4x4 MatrixTransform;

float4x4 LightMapMatrixTransform;
float Zoom;

float NormalMapResolution;
float NormalMapGradientMult;
float NormalMapStrength;
float2 SkyLightGradient;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TileTexCoord : TEXCOORD0;
};

struct PixelShaderInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TileTexCoord : TEXCOORD0;
    float2 LightTexCoord : TEXCOORD1;
};

struct GlowPixelShaderInput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TileTexCoord : TEXCOORD0;
    float2 LightTexCoord : TEXCOORD1;
    float2 GlowTexCoord : TEXCOORD2;
};

/* Helper functions *********************************************************************/

float Square(float x)
{
    return x * x;
}

float3 Square(float3 x)
{
    return x * x;
}

float Luma(float3 color)
{
    return sqrt(dot(Square(color), float3(0.2126, 0.7152, 0.0722)));
}

// Not technically correct because it ignores gamma, but cheap and decent quality
float3 Dithered(float2 position, float3 color)
{
    float noise
        = (1.0 / 256) * tex2D(DitherSampler, (1.0 / DITHER_TEXTURE_SIZE) * position).r
        - 0.5 / 255;
    
    color += noise;
    
    return color;
}

struct SamplingTransform
{
    float2 TexelSize;
    float2 TextureSize;
    float2x2 ScalingAndRotation;
};

// Assumes only rotation and/or flipping and no scaling or stretching
SamplingTransform CalculateSamplingTransform(float2 tileCoord)
{
    SamplingTransform output;

    float2 partialX = Zoom * ddx(tileCoord);
    float2 partialY = Zoom * ddy(tileCoord);
    
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

float SampleForNormal(float2 tileCoord, float fallback)
{
    float4 color = tex2D(TextureSampler, tileCoord);
    return color.a < 1 ? fallback : saturate(Luma(color.rgb));
}

float3 NormalsSurfaceGradientAndMult(float2 tileCoord, float2 diff, float4 tileColor)
{
    float luma = saturate(Luma(tileColor.rgb));
    
    float leftLuma = SampleForNormal(tileCoord - float2(diff.x, 0), luma);
    float rightLuma = SampleForNormal(tileCoord + float2(diff.x, 0), luma);
    float upLuma = SampleForNormal(tileCoord - float2(0, diff.y), luma);
    float downLuma = SampleForNormal(tileCoord + float2(0, diff.y), luma);
    float positiveDiagonal
        = SampleForNormal(tileCoord - diff, luma) // up left
        - SampleForNormal(tileCoord + diff, luma); // down right
    float negativeDiagonal
        = SampleForNormal(tileCoord - float2(diff.x, -diff.y), luma) // down left
        - SampleForNormal(tileCoord + float2(diff.x, -diff.y), luma); // up right

    float horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (leftLuma - rightLuma);
    float verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (upLuma - downLuma);

    float maxLuma = max(
        luma,
        max(max(leftLuma, rightLuma), max(upLuma, downLuma))
    );
    float mult = 1 - maxLuma;
    return float3(
        mult * Gradient(horizontalColorDiff, verticalColorDiff),
        tileColor.a < 1.0 ? 0.0 : 1.0
    );
}

float2 NormalsLightGradient(float luma)
{
    return NormalMapGradientMult * float2(ddx(luma), ddy(luma));
}

float4 NormalsLightGradientFancySky(float luma, float alpha)
{
    return float4(
        NormalMapGradientMult * float2(ddx(luma), ddy(luma)),
        SkyLightGradient * alpha
    );
}

float NormalsMultiplier(float2 tileCoord, float4 tileColor, float3 lightColor)
{
    SamplingTransform samplingTransform = CalculateSamplingTransform(tileCoord);
    float2 diff = samplingTransform.TexelSize * NormalMapResolution;
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(
        tileCoord, diff, tileColor
    );
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    
    float luma = Luma(lightColor);
    float2 lightGradient = NormalsLightGradient(luma);
    lightGradient = mul(lightGradient, samplingTransform.ScalingAndRotation);
    float lightGradientLength = length(lightGradient);
    
    if (luma <= 0 || surfaceGradientLength == 0 || lightGradientLength == 0)
    {
        return 1.0;
    }
    
    surfaceGradient = lerp(
        surfaceGradient,
        surfaceGradient / surfaceGradientLength,
        saturate(16.0 * surfaceGradientLength)
    );
    surfaceGradient *= surfaceGradientAndMult.z;
    lightGradient /= lightGradientLength;
    lightGradientLength /= (luma + 0.1);
    
    float lightMult = 1.0 + clamp(
        NormalMapStrength * dot(lightGradient, surfaceGradient),
        -0.9,
        0.9
    );
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (16.0 * lightGradientLength + 1))
    );
}

float NormalsMultiplierFancySky(float2 tileCoord, float4 tileColor, float4 lightColor)
{
    SamplingTransform samplingTransform = CalculateSamplingTransform(tileCoord);
    float2 diff = samplingTransform.TexelSize * NormalMapResolution;
    
    float3 surfaceGradientAndMult = NormalsSurfaceGradientAndMult(
        tileCoord, diff, tileColor
    );
    float2 surfaceGradient = surfaceGradientAndMult.xy;
    float surfaceGradientLength = length(surfaceGradient);
    
    float luma = Luma(lightColor.rgb);
    float4 lightAndSkyLightGradient = NormalsLightGradientFancySky(luma, lightColor.a);
    float2 lightGradient = lightAndSkyLightGradient.xy + lightAndSkyLightGradient.zw;
    lightGradient = mul(lightGradient, samplingTransform.ScalingAndRotation);
    float lightGradientLength = length(lightGradient);
    
    if (luma <= 0 || surfaceGradientLength == 0 || lightGradientLength == 0)
    {
        return 1.0;
    }
    
    surfaceGradient = lerp(
        surfaceGradient,
        surfaceGradient / surfaceGradientLength,
        saturate(16.0 * surfaceGradientLength)
    );
    surfaceGradient *= surfaceGradientAndMult.z;
    lightGradient /= lightGradientLength;
    lightGradientLength /= (luma + 0.1);
    
    float lightMult = 1.0 + clamp(
        NormalMapStrength * dot(lightGradient, surfaceGradient),
        -0.9,
        0.9
    );
    return lerp(
        1.0,
        lightMult,
        sqrt(surfaceGradientLength) * Square(1 - 1.0 / (16.0 * lightGradientLength + 1))
    );
}

float4 SmoothLightingColor(
    GlowPixelShaderInput input,
    float4 tileColor,
    float3 lightColor,
    float lightMult
)
{
    lightColor = input.Color.a * lightMult * saturate(lightColor);
    
    float3 smoothColor = lightColor * tileColor.rgb;
    float3 selector = tex2D(GlowSampler, input.GlowTexCoord).rgb;
    float4 glow = input.Color * tileColor;
    float3 bright = max(smoothColor, glow.rgb);
    
    return (input.Color.a <= 0 && max(input.Color.r, max(input.Color.g, input.Color.b)) > 0)
        ? input.Color * tileColor
        : float4(
            lerp(smoothColor.rgb, bright, step(2.5 / 255, selector)),
            glow.a
        );
}

float4 SmoothLightingColorDithered(
    GlowPixelShaderInput input,
    float4 tileColor,
    float3 lightColor,
    float lightMult
)
{
    lightColor = input.Color.a * lightMult * saturate(lightColor);
    
    float3 smoothColor = Dithered(input.Position, lightColor * tileColor.rgb);
    float3 selector = tex2D(GlowSampler, input.GlowTexCoord).rgb;
    float4 glow = input.Color * tileColor;
    float3 bright = max(smoothColor, glow.rgb);
    
    return (input.Color.a <= 0 && max(input.Color.r, max(input.Color.g, input.Color.b)) > 0)
        ? input.Color * tileColor
        : float4(
            lerp(smoothColor.rgb, bright, step(2.5 / 255, selector)),
            glow.a
        );
}

float4 SmoothLightingColorLightOnly(
    PixelShaderInput input,
    float4 tileColor,
    float3 lightColor,
    float lightMult
)
{
    float alpha = tileColor.a;
    lightColor = input.Color.a * lightMult * saturate(lightColor);
    
    return (input.Color.a <= 0 && max(input.Color.r, max(input.Color.g, input.Color.b)) > 0)
        ? input.Color * alpha
        : float4(lightColor, input.Color.a) * alpha;
}

float4 SmoothLightingColorDitheredLightOnly(
    PixelShaderInput input,
    float4 tileColor,
    float3 lightColor,
    float lightMult
)
{
    float alpha = tileColor.a;
    lightColor = input.Color.a * lightMult * saturate(lightColor);
    
    float4 result = float4(lightColor, input.Color.a) * alpha;
    result.rgb = Dithered(input.Position, result.rgb);
    return (input.Color.a <= 0 && max(input.Color.r, max(input.Color.g, input.Color.b)) > 0)
        ? input.Color * alpha
        : result;
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

PixelShaderInput SmoothLighting_VS(VertexShaderInput input)
{
    PixelShaderInput output;
    
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TileTexCoord = input.TileTexCoord;
    output.LightTexCoord = mul(output.Position, LightMapMatrixTransform);
    
    return output;
}

GlowPixelShaderInput SmoothLightingGlow_VS(VertexShaderInput input)
{
    GlowPixelShaderInput output;
    
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TileTexCoord = input.TileTexCoord;
    output.LightTexCoord = mul(output.Position, LightMapMatrixTransform);
    output.GlowTexCoord = float2(0.5, -0.5) * output.Position + 0.5;
    
    return output;
}

/* Pixel shaders ************************************************************************/

float4 LightOnly_PS(float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    return color * tex2D(TextureSampler, texCoord).a;
}

float4 Normals_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplier(input.TileTexCoord, tileColor, lightColor.rgb);

    return lightColor * float4(mult.xxx, 1) * tileColor;
}

float4 NormalsLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplier(input.TileTexCoord, tileColor, lightColor.rgb);
    
    return lightColor * float4(mult.xxx, 1) * tileColor.a;
}

float4 NormalsFancySky_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TileTexCoord, tileColor, lightColor);

    return lightColor * float4(mult.xxx, 1) * tileColor;
}

float4 NormalsFancySkyLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = input.Color;
    float mult = NormalsMultiplierFancySky(input.TileTexCoord, tileColor, lightColor);
    
    return lightColor * float4(mult.xxx, 1) * tileColor.a;
}

float4 Smooth_PS(GlowPixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    return SmoothLightingColor(input, tileColor, lightColor.rgb, 1);
}

float4 SmoothLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    return SmoothLightingColorLightOnly(input, tileColor, lightColor.rgb, 1);
}

float4 SmoothNormals_PS(GlowPixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplier(input.TileTexCoord, tileColor, lightColor.rgb);
    return SmoothLightingColor(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothNormalsLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplier(input.TileTexCoord, tileColor, lightColor.rgb);
    return SmoothLightingColorLightOnly(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothNormalsFancySky_PS(GlowPixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplierFancySky(input.TileTexCoord, tileColor, lightColor);
    return SmoothLightingColor(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothNormalsFancySkyLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplierFancySky(input.TileTexCoord, tileColor, lightColor);
    return SmoothLightingColorLightOnly(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothDithered_PS(GlowPixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    return SmoothLightingColorDithered(input, tileColor, lightColor.rgb, 1);
}

float4 SmoothDitheredLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    return SmoothLightingColorDitheredLightOnly(input, tileColor, lightColor.rgb, 1);
}

float4 SmoothDitheredNormals_PS(GlowPixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplier(input.TileTexCoord, tileColor, lightColor.rgb);
    return SmoothLightingColorDithered(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothDitheredNormalsLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplier(input.TileTexCoord, tileColor, lightColor.rgb);
    return SmoothLightingColorDitheredLightOnly(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothDitheredNormalsFancySky_PS(GlowPixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplierFancySky(input.TileTexCoord, tileColor, lightColor);
    return SmoothLightingColorDithered(input, tileColor, lightColor.rgb, mult);
}

float4 SmoothDitheredNormalsFancySkyLightOnly_PS(PixelShaderInput input) : COLOR0
{
    float4 tileColor = tex2D(TextureSampler, input.TileTexCoord);
    float4 lightColor = tex2D(LightSampler, input.LightTexCoord);
    float mult = NormalsMultiplierFancySky(input.TileTexCoord, tileColor, lightColor);
    return SmoothLightingColorDitheredLightOnly(input, tileColor, lightColor.rgb, mult);
}

/* Techniques ***************************************************************************/

technique LightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SpriteBatch_VS();
        PixelShader = compile ps_3_0 LightOnly_PS();
    }
}

technique Normals
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 Normals_PS();
    }
}

technique NormalsLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsLightOnly_PS();
    }
}

technique NormalsFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsFancySky_PS();
    }
}

technique NormalsFancySkyLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 NormalsFancySkyLightOnly_PS();
    }
}

technique Smooth
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLightingGlow_VS();
        PixelShader = compile ps_3_0 Smooth_PS();
    }
}

technique SmoothLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothLightOnly_PS();
    }
}

technique SmoothNormals
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLightingGlow_VS();
        PixelShader = compile ps_3_0 SmoothNormals_PS();
    }
}

technique SmoothNormalsLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothNormalsLightOnly_PS();
    }
}

technique SmoothNormalsFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLightingGlow_VS();
        PixelShader = compile ps_3_0 SmoothNormalsFancySky_PS();
    }
}

technique SmoothNormalsFancySkyLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothNormalsFancySkyLightOnly_PS();
    }
}

technique SmoothDithered
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLightingGlow_VS();
        PixelShader = compile ps_3_0 SmoothDithered_PS();
    }
}

technique SmoothDitheredLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredLightOnly_PS();
    }
}

technique SmoothDitheredNormals
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLightingGlow_VS();
        PixelShader = compile ps_3_0 SmoothDitheredNormals_PS();
    }
}

technique SmoothDitheredNormalsLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredNormalsLightOnly_PS();
    }
}

technique SmoothDitheredNormalsFancySky
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLightingGlow_VS();
        PixelShader = compile ps_3_0 SmoothDitheredNormalsFancySky_PS();
    }
}
technique SmoothDitheredNormalsFancySkyLightOnly
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SmoothLighting_VS();
        PixelShader = compile ps_3_0 SmoothDitheredNormalsFancySkyLightOnly_PS();
    }
}
