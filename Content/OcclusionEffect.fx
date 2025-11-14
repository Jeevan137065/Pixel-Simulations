#if OPENGL
#define SV_Position POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define SV_Position SV_POSITION
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

// --- Parameters from C# ---
uniform float4x4 MatrixTransform;
float2 LightPosition;
float ShadowLength; // How far the shadow should stretch

// --- Vertex Shader ---
struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : SV_Position;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    // Get the object's vertex position in screen space
    float2 vertexPos = mul(input.Position, MatrixTransform).xy;

    // Calculate the direction from the light to this vertex
    float2 direction = normalize(vertexPos - LightPosition);

    // Push the vertex away from the light source to create the shadow
    float2 newPos = vertexPos + direction * ShadowLength;

    // Rebuild the output, keeping the original Z and W values
    VertexShaderOutput output;
    float4 originalPos = mul(input.Position, MatrixTransform);
    output.Position = float4(newPos, originalPos.z, originalPos.w);

    return output;
}

// --- Pixel Shader ---
// This shader is extremely simple: it just draws black.
float4 MainPS() : COLOR0
{
    return float4(0, 0, 0, 1); // Black, fully opaque shadow
}

// --- Technique ---
technique ShadowCasting
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};