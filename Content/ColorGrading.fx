#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float Saturation = 1.0;
float Brightness = 1.0;
float Contrast = 1.0;
float3 TintColor = float3(1.0, 1.0, 1.0);

Texture2D ScreenTexture;
sampler ScreenSampler = sampler_state{ Texture = <ScreenTexture>; AddressU = Clamp; AddressV = Clamp; };

// Notice we added MonoGame's default SpriteBatch parameters here
float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 texColor = tex2D(ScreenSampler, texCoord);
    texColor.rgb = ((texColor.rgb - 0.5) * max(Contrast, 0.0)) + 0.5;
    texColor.rgb *= Brightness;
    float luminance = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
    texColor.rgb = lerp(float3(luminance, luminance, luminance), texColor.rgb, Saturation);
    texColor.rgb *= TintColor;
    return texColor;
}

technique Basic
{
    pass P0
    {
        // WE REMOVED THE VERTEX SHADER! SpriteBatch will handle it now.
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}