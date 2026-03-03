#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state{ Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

// --- NEW: Data structures for the Quad ---
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelShaderInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

// --- NEW: Pass-through Vertex Shader ---
PixelShaderInput MainVS(VertexShaderInput input)
{
    PixelShaderInput output;
    // Our quad is defined from -1 to 1, which perfectly matches screen space!
    // No camera matrix needed.
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

// --- PIXEL SHADER ---
float4 MainPS(PixelShaderInput input) : SV_TARGET
{
    // Use the TexCoord passed from the Vertex Shader
    float noiseValue = tex2D(NoiseSampler, input.TexCoord).r;
    return float4(noiseValue, noiseValue, noiseValue, noiseValue); // Opaque noise
}

technique Basic
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}