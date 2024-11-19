// https://learnopengl.com/Guest-Articles/2022/Phys.-Based-Bloom

sampler TextureSampler : register(s0);

float2 FilterSize;

// Input must be in linear space
float Luma(float3 color)
{
    return dot(pow(color, 1 / 2.2), float3(0.2126, 0.7152, 0.0722));
}

// Input must be in linear space
float KarisAverage(float3 color)
{
    return 1.0 / (1.0 + Luma(color));
}

// Input must be in linear space
float4 Downsample(float2 coords : TEXCOORD0) : COLOR0
{
    float dx = FilterSize.x;
    float dy = FilterSize.y;

    float3 a = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y + 2*dy)).rgb;
    float3 b = tex2D(TextureSampler, float2(coords.x,        coords.y + 2*dy)).rgb;
    float3 c = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y + 2*dy)).rgb;

    float3 d = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y)).rgb;
    float3 e = tex2D(TextureSampler, float2(coords.x,        coords.y)).rgb;
    float3 f = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y)).rgb;

    float3 g = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y - 2*dy)).rgb;
    float3 h = tex2D(TextureSampler, float2(coords.x,        coords.y - 2*dy)).rgb;
    float3 i = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y - 2*dy)).rgb;

    float3 j = tex2D(TextureSampler, float2(coords.x - dx,   coords.y + dy)).rgb;
    float3 k = tex2D(TextureSampler, float2(coords.x + dx,   coords.y + dy)).rgb;
    float3 l = tex2D(TextureSampler, float2(coords.x - dx,   coords.y - dy)).rgb;
    float3 m = tex2D(TextureSampler, float2(coords.x + dx,   coords.y - dy)).rgb;
    
    float3 downsample
        = 0.125 * (e + j + k + l + m)
        + 0.03125 * (a + c + g + i)
        + 0.0625 * (b + d + f + h);
    return float4(downsample, 1);
}

// Input must be in linear space
float4 DownsampleKaris(float2 coords : TEXCOORD0) : COLOR0
{
    float dx = FilterSize.x;
    float dy = FilterSize.y;
    
    float3 a = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y + 2*dy)).rgb;
    float3 b = tex2D(TextureSampler, float2(coords.x,        coords.y + 2*dy)).rgb;
    float3 c = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y + 2*dy)).rgb;
    
    float3 d = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y)).rgb;
    float3 e = tex2D(TextureSampler, float2(coords.x,        coords.y)).rgb;
    float3 f = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y)).rgb;
    
    float3 g = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y - 2*dy)).rgb;
    float3 h = tex2D(TextureSampler, float2(coords.x,        coords.y - 2*dy)).rgb;
    float3 i = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y - 2*dy)).rgb;
    
    float3 j = tex2D(TextureSampler, float2(coords.x - dx,   coords.y + dy)).rgb;
    float3 k = tex2D(TextureSampler, float2(coords.x + dx,   coords.y + dy)).rgb;
    float3 l = tex2D(TextureSampler, float2(coords.x - dx,   coords.y - dy)).rgb;
    float3 m = tex2D(TextureSampler, float2(coords.x + dx,   coords.y - dy)).rgb;
    
    float3 groups[5];
    groups[0] = (a+b+d+e) / 4.0;
    groups[1] = (b+c+e+f) / 4.0;
    groups[2] = (d+e+g+h) / 4.0;
    groups[3] = (e+f+h+i) / 4.0;
    groups[4] = (j+k+l+m) / 4.0;
    float kw0 = KarisAverage(groups[0]);
    float kw1 = KarisAverage(groups[1]);
    float kw2 = KarisAverage(groups[2]);
    float kw3 = KarisAverage(groups[3]);
    float kw4 = KarisAverage(groups[4]);
    
    float3 downsample = (
        kw0 * groups[0]
        + kw1 * groups[1]
        + kw2 * groups[2]
        + kw3 * groups[3]
        + kw4 * groups[4]
    ) / (kw0 + kw1 + kw2 + kw3 + kw4);
    return float4(downsample, 1);
}

// Input must be in linear space
float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float dx = FilterSize.x;
    float dy = FilterSize.y;
    
    float3 a = tex2D(TextureSampler, float2(coords.x - dx, coords.y + dy)).rgb;
    float3 b = tex2D(TextureSampler, float2(coords.x,      coords.y + dy)).rgb;
    float3 c = tex2D(TextureSampler, float2(coords.x + dx, coords.y + dy)).rgb;

    float3 d = tex2D(TextureSampler, float2(coords.x - dx, coords.y)).rgb;
    float3 e = tex2D(TextureSampler, float2(coords.x,      coords.y)).rgb;
    float3 f = tex2D(TextureSampler, float2(coords.x + dx, coords.y)).rgb;

    float3 g = tex2D(TextureSampler, float2(coords.x - dx, coords.y - dy)).rgb;
    float3 h = tex2D(TextureSampler, float2(coords.x,      coords.y - dy)).rgb;
    float3 i = tex2D(TextureSampler, float2(coords.x + dx, coords.y - dy)).rgb;

    float3 upsample
        = 0.25 * e
        + 0.125 * (b + d + f + h)
        + 0.0625 * (a + c + g + i);
    return float4(upsample, 1);
}

technique Technique1
{
    pass Downsample
    {
        PixelShader = compile ps_3_0 Downsample();
    }

    pass DownsampleKaris
    {
        PixelShader = compile ps_3_0 DownsampleKaris();
    }

    pass Blur
    {
        PixelShader = compile ps_3_0 Blur();
    }
}
