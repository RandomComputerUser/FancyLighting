sampler OccluderSampler : register(s0);

float2 BlurSize;
float BlurPower;
float BlurMult;

const float CenterWeight = 0.20947266;
const float GaussianKernel[7] = {
    0.18328857, 0.12219238, 0.06109619, 0.022216797, 0.005554199, 0.0008544922, 6.1035156e-05
};
const float BilinearCenterWeight = 0.17619705;
const float BilinearGaussianWeights[5] = {
    0.2803135, 0.11089325, 0.019406319, 0.0012683868, 2.002716e-05
};
const float BilinearGaussianOffsets[5] = {
    1.4285715, 3.3333333, 5.2380953, 7.142857, 9.047619
};

float BlurBrightness(float2 coords)
{
    float brightness = CenterWeight * tex2D(OccluderSampler, coords).r;

    [unroll]
    float2 offset = 0;
    for (int i = 0; i < 7; ++i)
    {
        offset += BlurSize;
        brightness += GaussianKernel[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );
    }

    return brightness;
}

float BilinearBlurBrightness(float2 coords)
{
    float brightness = BilinearCenterWeight * tex2D(OccluderSampler, coords).r;

    [unroll]
    for (int i = 0; i < 5; ++i)
    {
        float2 offset = BilinearGaussianOffsets[i] * BlurSize;
        brightness += BilinearGaussianWeights[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );
    }

    return brightness;
}

float4 AlphaToRed(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - saturate(tex2D(OccluderSampler, coords).a);
    return float4(brightness.x, 0, 0, 0);
}

float4 AlphaToLightRed(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = -0.8 * saturate(tex2D(OccluderSampler, coords).a) + 1;
    return float4(brightness.x, 0, 0, 0);
}

float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BlurBrightness(coords);

    return float4(brightness.x, 0, 0, 0);
}

float4 BilinearBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BilinearBlurBrightness(coords);

    return float4(brightness.x, 0, 0, 0);
}

float4 FinalBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BilinearBlurBrightness(coords);
    brightness = 1 - BlurMult * (1 - pow(brightness, BlurPower));

    return float4(brightness.xxx, 1);
}

technique Technique1
{
    pass AlphaToRed
    {
        PixelShader = compile ps_3_0 AlphaToRed();
    }

    pass AlphaToLightRed
    {
        PixelShader = compile ps_3_0 AlphaToLightRed();
    }

    pass Blur
    {
        PixelShader = compile ps_3_0 Blur();
    }

    pass BilinearBlur
    {
        PixelShader = compile ps_3_0 BilinearBlur();
    }

    pass FinalBlur
    {
        PixelShader = compile ps_3_0 FinalBlur();
    }
}
