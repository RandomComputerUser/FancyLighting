sampler TextureSampler : register(s0);
sampler PrevLightSampler : register(s4);
sampler CurrLightSampler : register(s5);

float4x4 MatrixTransform;

float4x4 PrevLightMapMatrixTransform;
float4x4 CurrLightMapMatrixTransform;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float2 PrevLightMapTexCoord : TEXCOORD1;
    float2 CurrLightMapTexCoord : TEXCOORD2;
};

VertexShaderOutput SyncHdrVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    
    output.Position = mul(input.Position, MatrixTransform);
    output.TexCoord = input.TexCoord;
    float4 homogeneousTexCoord = float4(input.TexCoord, 0, 1);
    output.PrevLightMapTexCoord = mul(homogeneousTexCoord, PrevLightMapMatrixTransform).xy;
    output.CurrLightMapTexCoord = mul(homogeneousTexCoord, CurrLightMapMatrixTransform).xy;
    
    return output;
}

float4 SyncHdrPS(in VertexShaderOutput input) : COLOR0
{
    float4 color = tex2D(TextureSampler, input.TexCoord);
    float3 prevLightColor = tex2D(PrevLightSampler, input.PrevLightMapTexCoord).rgb;
    float3 currLightColor = tex2D(CurrLightSampler, input.CurrLightMapTexCoord).rgb;
    color.rgb *= max(prevLightColor, 1) / max(currLightColor, 1);
    return color;
}

technique SyncHdr
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SyncHdrVS();
        PixelShader = compile ps_3_0 SyncHdrPS();
    }
}
