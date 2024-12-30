sampler OccluderSampler : register(s0);

float2 BlurSize;
float BlurPower;
float BlurMult;

float4 ExtractInverseAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - saturate(tex2D(OccluderSampler, coords).a);
    return float4(0, 0, 0, brightness);
}

float4 ExtractInverseMultipliedAlpha(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = -0.8 * saturate(tex2D(OccluderSampler, coords).a) + 1;
    return float4(0, 0, 0, brightness);
}

float4 ToneMapping(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = tex2D(OccluderSampler, coords).a;
    brightness = 1 - BlurMult * (1 - pow(brightness, BlurPower));

    return float4(0, 0, 0, brightness);
}

technique Technique1
{
    pass ExtractInverseAlpha
    {
        PixelShader = compile ps_3_0 ExtractInverseAlpha();
    }

    pass ExtractInverseMultipliedAlpha
    {
        PixelShader = compile ps_3_0 ExtractInverseMultipliedAlpha();
    }

    pass ToneMapping
    {
        PixelShader = compile ps_3_0 ToneMapping();
    }
}
