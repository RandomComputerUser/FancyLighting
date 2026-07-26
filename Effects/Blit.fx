sampler TextureSampler : register(s0);

void Blit_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
}
    
float4 Blit_PS(float2 texCoord : TEXCOORD0) : SV_Target0
{
    return tex2D(TextureSampler, texCoord);
}

technique Blit
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 Blit_PS();
    }
}
