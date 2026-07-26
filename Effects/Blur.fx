// https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf

sampler TextureSampler : register(s0);

float2 PixelSize;

void Blit_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
}

float4 BlurDownsample_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 sum = tex2D(TextureSampler, coords) * 4.0;
    sum += tex2D(TextureSampler, coords - PixelSize);
    sum += tex2D(TextureSampler, coords + PixelSize);
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y));
    sum += tex2D(TextureSampler, coords - float2(PixelSize.x, -PixelSize.y));
    return (1.0 / 8) * sum;
}

float4 BlurUpsample_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float4 sum = tex2D(TextureSampler, coords + float2(-PixelSize.x * 2.0, 0.0));
    sum += tex2D(TextureSampler, coords + float2(-PixelSize.x, PixelSize.y)) * 2.0;
    sum += tex2D(TextureSampler, coords + float2(0.0, PixelSize.y * 2.0));
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, PixelSize.y)) * 2.0;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x * 2.0, 0.0));
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y)) * 2.0;
    sum += tex2D(TextureSampler, coords + float2(0.0, -PixelSize.y * 2.0));
    sum += tex2D(TextureSampler, coords + float2(-PixelSize.x, -PixelSize.y)) * 2.0;
    return (1.0 / 12) * sum;
}

float4 BlurDownsampleAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float sum = tex2D(TextureSampler, coords).a * 4.0;
    sum += tex2D(TextureSampler, coords - PixelSize).a;
    sum += tex2D(TextureSampler, coords + PixelSize).a;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y)).a;
    sum += tex2D(TextureSampler, coords - float2(PixelSize.x, -PixelSize.y)).a;
    return float4(0, 0, 0, (1.0 / 8) * sum);
}

float4 BlurUpsampleAlpha_PS(float2 coords : TEXCOORD0) : COLOR0
{
    float sum = tex2D(TextureSampler, coords + float2(-PixelSize.x * 2.0, 0.0)).a;
    sum += tex2D(TextureSampler, coords + float2(-PixelSize.x, PixelSize.y)).a * 2.0;
    sum += tex2D(TextureSampler, coords + float2(0.0, PixelSize.y * 2.0)).a;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, PixelSize.y)).a * 2.0;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x * 2.0, 0.0)).a;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y)).a * 2.0;
    sum += tex2D(TextureSampler, coords + float2(0.0, -PixelSize.y * 2.0)).a;
    sum += tex2D(TextureSampler, coords + float2(-PixelSize.x, -PixelSize.y)).a * 2.0;
    return float4(0, 0, 0, (1.0 / 12) * sum);
}

technique BlurDownsample
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 BlurDownsample_PS();
    }
}

technique BlurUpsample
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 BlurUpsample_PS();
    }
}
  
technique BlurDownsampleAlpha
{  
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 BlurDownsampleAlpha_PS();
    }
}

technique BlurUpsampleAlpha
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 Blit_VS();
        PixelShader = compile ps_3_0 BlurUpsampleAlpha_PS();
    }
}
