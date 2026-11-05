#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D ScreenTexture;
sampler2D ScreenSampler = sampler_state
{
    Texture = <ScreenTexture>;
    Filter = Linear; // Linear sampling is required for sub-pixel boundary blending
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 SourceResolution; // e.g. float2(960.0, 540.0)

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

// Anti-aliased Box-Filtered Point Sampling
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TexCoord * SourceResolution;

// Determine fractional position within the current source pixel
float2 seam = floor(uv + 0.5);
float2 dUV = fwidth(uv);

// Calculate sub-pixel interpolation box
float2 box = clamp((uv - seam) / dUV + 0.5, 0.0, 1.0);

// Scale back to normalized [0, 1] texture coordinates
float2 smoothUV = (seam + box - 0.5) / SourceResolution;

return tex2D(ScreenSampler, smoothUV) * input.Color;
}

technique CleanZoom
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}