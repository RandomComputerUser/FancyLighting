sampler TextureSampler : register(s0);

float4x4 MatrixTransform;

float PixelSize;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput CloudShadingVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    
    return output;
}

float4 CloudShadingPS(in VertexShaderOutput input) : COLOR0
{
    input.Color *= float4(1, 0, 1, 1);
    input.TexCoord.y = 1 - input.TexCoord.y;
    return input.Color * tex2D(TextureSampler, input.TexCoord);
}

technique CloudShading
{
    pass Pass1
    {
        // VertexShader = compile vs_3_0 CloudShadingVS();
        PixelShader = compile ps_3_0 CloudShadingPS();
    }
}
