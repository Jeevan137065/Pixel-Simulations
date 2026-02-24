// File: SnowEffect.fx
cbuffer SnowProperties : register(b0)
{
    float2 u_Resolution;
    float  u_Time;
    float  u_Spread;
    float  u_Size;
    float  u_Speed;
    float  u_Wind;
    float  u_SnowTransparency;
    int    u_NumLayers;
};

struct VS_INPUT { float4 Position : POSITION; float2 TC : TEXCOORD0; };
struct PS_INPUT { float4 Position : SV_POSITION; float2 TC : TEXCOORD0; };

PS_INPUT VSMain(VS_INPUT i)
{
    PS_INPUT o;
    float normX = i.Position.x / u_Resolution.x;
    o.Position.x = normX * 2.0f - 1.0f;
    float normY = i.Position.y / u_Resolution.y;
    o.Position.y = 1.0f - normY * 2.0f;
    o.Position.z = 0.0f;
    o.Position.w = 1.0f;
    //o.Position = i.Position;
    o.TC = i.TC;
    return o;
}
// File: SnowEffect.fx
#define MAX_LAYERS 60

float4 PSMain(PS_INPUT inp) : COLOR0
{
    float2 uv = inp.TC * u_Resolution / min(u_Resolution.x, u_Resolution.y);
    float3 acc = float3(0,0,0);

    for (int layer = 0; layer < u_NumLayers && layer < MAX_LAYERS; layer++)
    {
        float fi = layer;
        float scale = 1 + fi * 0.5 / (max(u_Size, 0.01) * 2);
        float2 sc = uv * scale;
        float2 mv = float2(
            sc.y * (u_Spread * fmod(fi * 7.2389, 1.0) - u_Spread * 0.5) - u_Wind * u_Speed * u_Time * 0.5,
           -u_Speed * u_Time / (1 + fi * 0.5 * 0.03)
        );
        float2 fc = sc + mv;

        // Simple per-component hash & frac
        float hx = frac(fc.x + fi * 12.9898);
        float hy = frac(fc.y + fi * 78.233);
        float rnd = frac(hx * hy * 43758.5453);

        float2 shp = abs(fmod(fc, 1.0) - 0.5 + 0.9 * float2(hx, hy) - 0.45);
        float edge = 0.005 + 0.05 * min(0.5 * abs(fi - 5 - 5 * sin(u_Time * 0.1)), 1);
        float sv = max(shp.x - shp.y, shp.x + shp.y);
        float s = smoothstep(edge, -edge, sv);

        acc += rnd * s / (1 + 0.02 * fi * 0.5);
    }

    float intensity = clamp(length(acc) * 5, 0, 1);
    return float4(1,1,1, intensity * u_SnowTransparency);
}

technique Snow
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMain();
    }
}
