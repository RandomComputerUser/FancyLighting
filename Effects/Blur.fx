// https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf

sampler TextureSampler : register(s0);

float2 PixelSize;

float4 BlurDownsample(float2 coords : TEXCOORD0) : COLOR0
{
    float4 sum = tex2D(TextureSampler, coords) * 4.0;
    sum += tex2D(TextureSampler, coords - PixelSize);
    sum += tex2D(TextureSampler, coords + PixelSize);
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y));
    sum += tex2D(TextureSampler, coords - float2(PixelSize.x, -PixelSize.y));
    return (1.0 / 8) * sum;
}

float4 BlurUpsample(float2 coords : TEXCOORD0) : COLOR0
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

float4 BlurDownsampleRed(float2 coords : TEXCOORD0) : COLOR0
{
    float sum = tex2D(TextureSampler, coords).r * 4.0;
    sum += tex2D(TextureSampler, coords - PixelSize).r;
    sum += tex2D(TextureSampler, coords + PixelSize).r;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y)).r;
    sum += tex2D(TextureSampler, coords - float2(PixelSize.x, -PixelSize.y)).r;
    return float4((1.0 / 8) * sum, 0, 0, 0);
}

float4 BlurUpsampleRed(float2 coords : TEXCOORD0) : COLOR0
{
    float sum = tex2D(TextureSampler, coords + float2(-PixelSize.x * 2.0, 0.0)).r;
    sum += tex2D(TextureSampler, coords + float2(-PixelSize.x, PixelSize.y)).r * 2.0;
    sum += tex2D(TextureSampler, coords + float2(0.0, PixelSize.y * 2.0)).r;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, PixelSize.y)).r * 2.0;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x * 2.0, 0.0)).r;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y)).r * 2.0;
    sum += tex2D(TextureSampler, coords + float2(0.0, -PixelSize.y * 2.0)).r;
    sum += tex2D(TextureSampler, coords + float2(-PixelSize.x, -PixelSize.y)).r * 2.0;
    return float4((1.0 / 12) * sum, 0, 0, 0);
}

technique Technique1
{
    pass BlurDownsample
    {
        PixelShader = compile ps_3_0 BlurDownsample();
    }

    pass BlurUpsample
    {
        PixelShader = compile ps_3_0 BlurUpsample();
    }
    
    pass BlurDownsampleRed
    {
        PixelShader = compile ps_3_0 BlurDownsampleRed();
    }

    pass BlurUpsampleRed
    {
        PixelShader = compile ps_3_0 BlurUpsampleRed();
    }
}
