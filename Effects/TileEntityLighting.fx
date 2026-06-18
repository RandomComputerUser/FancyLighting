sampler TextureSampler : register(s0);

float4 LightOnlyPS(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    return color * tex2D(TextureSampler, coords).a;
}

technique LightOnly
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 LightOnlyPS();
    }
}
