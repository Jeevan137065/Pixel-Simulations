#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float Time;
float2 WindVector;
float DistortionStrength;

Texture2D ScreenTexture;
sampler ScreenSampler = sampler_state{ Texture = <ScreenTexture>; AddressU = Clamp; AddressV = Clamp; };

Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state{ Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float2 noiseUV = texCoord + (WindVector * Time * 0.1);
    float2 distortion = tex2D(NoiseSampler, noiseUV).xy * 2.0 - 1.0;
    float2 finalUV = texCoord + (distortion * (DistortionStrength/1000));

    return tex2D(ScreenSampler, finalUV);
}

technique Basic
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}