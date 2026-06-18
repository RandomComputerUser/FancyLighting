sampler TextureSampler : register(s0);

struct PixelShaderOutput
{
    float4 LightedColor : COLOR0;
    float4 FullbrightColor : COLOR1;
};

PixelShaderOutput ExtractFullbrightPS(float4 color : COLOR0, float2 texCoord : TEXCOORD0)
{
    float4 texColor = tex2D(TextureSampler, texCoord);
    
    PixelShaderOutput output;
    output.LightedColor = color * texColor;
    output.FullbrightColor = texColor;
    
    return output;
}

technique ExtractFullbright
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 ExtractFullbrightPS();
    }
}
