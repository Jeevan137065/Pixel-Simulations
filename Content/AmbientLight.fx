#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// The color of the environment's light (From TimeManager)
float3 AmbientColor;

Texture2D ScreenTexture;
sampler ScreenSampler = sampler_state
{
    Texture = <ScreenTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 texColor = tex2D(ScreenSampler, texCoord);

// Multiplicative blending for realistic lighting.
// If AmbientColor is (1,1,1), nothing changes. 
// If it is (0.1, 0.2, 0.4), the screen becomes dark and blue.
float3 finalColor = texColor.rgb * AmbientColor;

return float4(finalColor, texColor.a);
}

technique Basic
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}