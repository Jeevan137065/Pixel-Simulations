#if OPENGL
#define SV_Position POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define SV_Position SV_POSITION
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

//-----------------------------------------------------------------------------
// PARAMETERS
//-----------------------------------------------------------------------------
Texture2D AlbedoMap;    // Will be bound to texture slot 0
Texture2D LightMaskMap; // Will be bound to texture slot 1
float4 AmbientColor;

uniform float4x4 MatrixTransform;

//-----------------------------------------------------------------------------
// SAMPLERS
//-----------------------------------------------------------------------------

// **THE FIX**
// This sampler gets its state from GraphicsDevice.SamplerStates[0] (PointClamp)
sampler AlbedoSampler : register(s0) = sampler_state
{
    Texture = <AlbedoMap>;
};

// This sampler gets its state from GraphicsDevice.SamplerStates[1] (LinearClamp)
sampler LightMaskSampler : register(s1) = sampler_state
{
    Texture = <LightMaskMap>;
};

//-----------------------------------------------------------------------------
// DATA STRUCTURES & VERTEX SHADER (Unchanged from robust version)
//-----------------------------------------------------------------------------
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.TexCoord = input.TexCoord;
    return output;
}

//-----------------------------------------------------------------------------
// PIXEL SHADER (Unchanged, but now uses the correct samplers)
//-----------------------------------------------------------------------------
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 albedo = tex2D(AlbedoSampler, input.TexCoord);
    float4 light = tex2D(LightMaskSampler, input.TexCoord);

    float3 finalColor = albedo.rgb * ((light.rgb) + AmbientColor.rgb);
    float alpha = albedo.a*(light.a + AmbientColor.a);
    return float4(finalColor, alpha);
}

//-----------------------------------------------------------------------------
// TECHNIQUE
//-----------------------------------------------------------------------------
technique Compose
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};