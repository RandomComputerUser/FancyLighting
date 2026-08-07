sampler TextureSampler : register(s0);
sampler PrevLightSampler : register(s8);
sampler CurrLightSampler : register(s9);

float4x4 PrevLightMapMatrixTransform;
float4x4 CurrLightMapMatrixTransform;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelShaderInput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float2 PrevLightTexCoord : TEXCOORD1;
    float2 CurrLightTexCoord : TEXCOORD2;
};

PixelShaderInput SyncHdr_VS(VertexShaderInput input)
{
    PixelShaderInput output;
    
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    float4 homogeneousTexCoord = float4(input.TexCoord, 0, 1);
    output.PrevLightTexCoord = mul(homogeneousTexCoord, PrevLightMapMatrixTransform).xy;
    output.CurrLightTexCoord = mul(homogeneousTexCoord, CurrLightMapMatrixTransform).xy;
    
    return output;
}

float4 SyncHdr_PS(PixelShaderInput input) : COLOR0
{
    float4 color = tex2D(TextureSampler, input.TexCoord);
    float3 prevLightColor = tex2D(PrevLightSampler, input.PrevLightTexCoord).rgb;
    float3 currLightColor = tex2D(CurrLightSampler, input.CurrLightTexCoord).rgb;
    color.rgb *= max(prevLightColor, 1) / max(currLightColor, 1);
    return color;
}

technique SyncHdr
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 SyncHdr_VS();
        PixelShader = compile ps_3_0 SyncHdr_PS();
    }
}
