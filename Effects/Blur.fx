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

float4 BlurDownsampleAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float sum = tex2D(TextureSampler, coords).a * 4.0;
    sum += tex2D(TextureSampler, coords - PixelSize).a;
    sum += tex2D(TextureSampler, coords + PixelSize).a;
    sum += tex2D(TextureSampler, coords + float2(PixelSize.x, -PixelSize.y)).a;
    sum += tex2D(TextureSampler, coords - float2(PixelSize.x, -PixelSize.y)).a;
    return float4(0, 0, 0, (1.0 / 8) * sum);
}

float4 BlurUpsampleAlpha(float2 coords : TEXCOORD0) : COLOR0
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
    
    pass BlurDownsampleAlpha
    {
        PixelShader = compile ps_3_0 BlurDownsampleAlpha();
    }

    pass BlurUpsampleAlpha
    {
        PixelShader = compile ps_3_0 BlurUpsampleAlpha();
    }
}
