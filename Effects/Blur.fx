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

    float4 a = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y + 2*dy));
    float4 b = tex2D(TextureSampler, float2(coords.x,        coords.y + 2*dy));
    float4 c = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y + 2*dy));

    float4 d = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y));
    float4 e = tex2D(TextureSampler, float2(coords.x,        coords.y));
    float4 f = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y));

    float4 g = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y - 2*dy));
    float4 h = tex2D(TextureSampler, float2(coords.x,        coords.y - 2*dy));
    float4 i = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y - 2*dy));

    float4 j = tex2D(TextureSampler, float2(coords.x - dx,   coords.y + dy));
    float4 k = tex2D(TextureSampler, float2(coords.x + dx,   coords.y + dy));
    float4 l = tex2D(TextureSampler, float2(coords.x - dx,   coords.y - dy));
    float4 m = tex2D(TextureSampler, float2(coords.x + dx,   coords.y - dy));
    
    float4 downsample
        = 0.125 * (e + j + k + l + m)
        + 0.03125 * (a + c + g + i)
        + 0.0625 * (b + d + f + h);
    return downsample;
}

// Input must be in linear space
float4 DownsampleKaris(float2 coords : TEXCOORD0) : COLOR0
{
    float dx = FilterSize.x;
    float dy = FilterSize.y;
    
    float4 a = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y + 2*dy));
    float4 b = tex2D(TextureSampler, float2(coords.x,        coords.y + 2*dy));
    float4 c = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y + 2*dy));
    
    float4 d = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y));
    float4 e = tex2D(TextureSampler, float2(coords.x,        coords.y));
    float4 f = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y));
    
    float4 g = tex2D(TextureSampler, float2(coords.x - 2*dx, coords.y - 2*dy));
    float4 h = tex2D(TextureSampler, float2(coords.x,        coords.y - 2*dy));
    float4 i = tex2D(TextureSampler, float2(coords.x + 2*dx, coords.y - 2*dy));
    
    float4 j = tex2D(TextureSampler, float2(coords.x - dx,   coords.y + dy));
    float4 k = tex2D(TextureSampler, float2(coords.x + dx,   coords.y + dy));
    float4 l = tex2D(TextureSampler, float2(coords.x - dx,   coords.y - dy));
    float4 m = tex2D(TextureSampler, float2(coords.x + dx,   coords.y - dy));
    
    float4 groups[5];
    groups[0] = (a+b+d+e) / 4.0;
    groups[1] = (b+c+e+f) / 4.0;
    groups[2] = (d+e+g+h) / 4.0;
    groups[3] = (e+f+h+i) / 4.0;
    groups[4] = (j+k+l+m) / 4.0;
    float kw0 = KarisAverage(groups[0].rgb);
    float kw1 = KarisAverage(groups[1].rgb);
    float kw2 = KarisAverage(groups[2].rgb);
    float kw3 = KarisAverage(groups[3].rgb);
    float kw4 = KarisAverage(groups[4].rgb);
    
    float4 downsample = (
        kw0 * groups[0]
        + kw1 * groups[1]
        + kw2 * groups[2]
        + kw3 * groups[3]
        + kw4 * groups[4]
    ) / (kw0 + kw1 + kw2 + kw3 + kw4);
    return downsample;
}

// Input must be in linear space
float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float dx = FilterSize.x;
    float dy = FilterSize.y;
    
    float4 a = tex2D(TextureSampler, float2(coords.x - dx, coords.y + dy));
    float4 b = tex2D(TextureSampler, float2(coords.x,      coords.y + dy));
    float4 c = tex2D(TextureSampler, float2(coords.x + dx, coords.y + dy));

    float4 d = tex2D(TextureSampler, float2(coords.x - dx, coords.y));
    float4 e = tex2D(TextureSampler, float2(coords.x,      coords.y));
    float4 f = tex2D(TextureSampler, float2(coords.x + dx, coords.y));

    float4 g = tex2D(TextureSampler, float2(coords.x - dx, coords.y - dy));
    float4 h = tex2D(TextureSampler, float2(coords.x,      coords.y - dy));
    float4 i = tex2D(TextureSampler, float2(coords.x + dx, coords.y - dy));

    float4 upsample
        = 0.25 * e
        + 0.125 * (b + d + f + h)
        + 0.0625 * (a + c + g + i);
    return upsample;
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
