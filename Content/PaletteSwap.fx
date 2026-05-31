#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4 ColorLight;
float4 ColorDark;

Texture2D SpriteTexture;
sampler2D SpriteSampler = sampler_state{ Texture = <SpriteTexture>; AddressU = Clamp; AddressV = Clamp; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 texColor = tex2D(SpriteSampler, texCoord);

// Skip fully transparent pixels
if (texColor.a < 0.05) discard;

// Calculate the brightness (luminance) of the original pixel
float lum = dot(texColor.rgb, float3(0.299, 0.587, 0.114));

// Smoothly lerp between the Dark color and the Light color based on brightness
float3 finalColor = lerp(ColorDark.rgb, ColorLight.rgb, lum);

// Keep the original alpha
return float4(finalColor, texColor.a);
}

technique Basic
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}