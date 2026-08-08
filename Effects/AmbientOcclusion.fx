sampler OccluderSampler : register(s0);

sampler TileSampler : register(s0);
sampler Tile2Sampler : register(s8);

float4x4 MatrixTransform;
float4x4 MatrixTransform2;

float2 BlurSize;
float BlurPower;
float BlurMult;

#define NONSOLID_OCCLUSION_MULT 0.7

/* Vertex shaders ***********************************************************************/

void Blit_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
}

void TileEntity_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    inout float4 color : COLOR0,
    out float4 screenPos : SV_Position
)
{
    screenPos = mul(position, MatrixTransform);
    color.a *= NONSOLID_OCCLUSION_MULT;
}

void Tiles_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    texCoord = mul(float4(texCoord, 0, 1), MatrixTransform).xy;
}

void TilesAndTiles2_VS(
    float4 position : POSITION0,
    float2 texCoord : TEXCOORD0,
    out float2 tileCoord : TEXCOORD0,
    out float2 tile2Coord : TEXCOORD1,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    float4 homogeneousTexCoord = float4(texCoord, 0, 1);
    tileCoord = mul(homogeneousTexCoord, MatrixTransform).xy;
    tile2Coord = mul(homogeneousTexCoord, MatrixTransform2).xy;
}

/* Pixel shaders ************************************************************************/

float4 Tiles_PS(float2 tileCoord : TEXCOORD0) : COLOR0
{
    float brightness = tex2D(TileSampler, tileCoord).a;
    return float4(0, 0, 0, brightness);
}

float4 TilesAndTiles2_PS(float2 tileCoord : TEXCOORD0, float2 tile2Coord : TEXCOORD1) : COLOR0
{
    float brightness = max(
        tex2D(TileSampler, tileCoord).a,
        NONSOLID_OCCLUSION_MULT * tex2D(Tile2Sampler, tile2Coord).a
    );
    return float4(0, 0, 0, brightness);
}

float4 TileEntity_PS(float2 tileCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float brightness = color.a * tex2D(TileSampler, tileCoord).a;
    return float4(0, 0, 0, brightness);
}

float4 ToneCurve_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float brightness = 1.0 - tex2D(OccluderSampler, texCoord).a;
    brightness = pow((1 - BlurMult) + BlurMult * pow(brightness, BlurPower), 1 / 2.2);

    return float4(0, 0, 0, brightness);
}

float4 ToneCurveHiDef_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float brightness = 1.0 - tex2D(OccluderSampler, texCoord).a;
    brightness = (1 - BlurMult) + BlurMult * pow(brightness, BlurPower);

    return float4(0, 0, 0, brightness);
}

float4 ToneCurveDefault_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float brightness = 1.0 - tex2D(OccluderSampler, texCoord).a;
    brightness *= brightness;
    brightness = pow((1 - BlurMult) + BlurMult * brightness, 1 / 2.2);

    return float4(0, 0, 0, brightness);
}

float4 ToneCurveDefaultHiDef_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float brightness = 1.0 - tex2D(OccluderSampler, texCoord).a;
    brightness *= brightness;
    brightness = (1 - BlurMult) + BlurMult * brightness;

    return float4(0, 0, 0, brightness);
}

/* Techniques ***************************************************************************/

technique Tiles
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Tiles_VS();
        PixelShader = compile ps_3_0 Tiles_PS();
    }
}

technique TilesAndTiles2
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 TilesAndTiles2_VS();
        PixelShader = compile ps_3_0 TilesAndTiles2_PS();
    }
}

technique TileEntity
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 TileEntity_VS();
        PixelShader = compile ps_3_0 TileEntity_PS();
    }
}

technique ToneCurve
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneCurve_PS();
    }
}

technique ToneCurveHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneCurveHiDef_PS();
    }
}

technique ToneCurveDefault
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneCurveDefault_PS();
    }
}

technique ToneCurveDefaultHiDef
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 ToneCurveDefaultHiDef_PS();
    }
}
