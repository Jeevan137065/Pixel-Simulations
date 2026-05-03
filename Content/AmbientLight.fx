#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float3 AmbientColorTop;
float3 AmbientColorBottom;
int GradientStyle; // 0:Linear, 1:LinearRev, 2:Horiz, 3:Radial, 4:RadialRev

Texture2D ScreenTexture;
sampler ScreenSampler = sampler_state{ Texture = <ScreenTexture>; AddressU = Clamp; AddressV = Clamp; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 texColor = tex2D(ScreenSampler, texCoord);
    float blendFactor = texCoord.y;

    if (GradientStyle == 1) blendFactor = 1.0 - texCoord.y; // Linear Reverse
    else if (GradientStyle == 2) blendFactor = texCoord.x;  // Horizontal
    else if (GradientStyle == 3) // Radial
    {
        float dist = length(texCoord - float2(0.5, 0.5)) * 1.414; // Distance from center
        blendFactor = saturate(dist);
    }
    else if (GradientStyle == 4) // Radial Reverse
    {
        float dist = length(texCoord - float2(0.5, 0.5)) * 1.414;
        blendFactor = 1.0 - saturate(dist);
    }

    float3 gradientAmbient = lerp(AmbientColorTop, AmbientColorBottom, blendFactor);
    return float4(texColor.rgb * gradientAmbient, texColor.a);
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }