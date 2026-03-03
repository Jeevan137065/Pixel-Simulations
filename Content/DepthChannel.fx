#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

int Channel; // 0 = Red (Altitude), 1 = Blue (Draw Depth)

Texture2D ScreenTexture;
sampler ScreenSampler = sampler_state{ Texture = <ScreenTexture>; AddressU = Clamp; AddressV = Clamp; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 tex = tex2D(ScreenSampler, texCoord);

    if (Channel == 0)
        return float4(tex.r, 0.0, 0.0, 1.0); // Show Red gradient
    else
        return float4(tex.b, tex.b, tex.b, 1.0); // Show Blue channel as a grayscale map
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }