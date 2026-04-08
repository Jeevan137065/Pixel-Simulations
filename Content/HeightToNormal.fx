#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 TextureSize;
float HeightScale = 15.0;

// The Master Mask Texture we are converting!
Texture2D MaskTexture;
sampler2D MaskSampler = sampler_state
{
    Texture = <MaskTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VertexOutput MainVS(in VertexInput input)
{
    VertexOutput output;
    // Pass the exact coordinates from the CPU (which will be -1.0 to 1.0 Clip Space)
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

float GetHeight(float2 uv)
{
    // STRICTLY read only the Blue channel (Elevation)
    return tex2D(MaskSampler, uv).b;
}

float4 MainPS(VertexOutput input) : COLOR
{
    float2 uv = input.TexCoord;

// 1. Read Current Height
float currentHeight = GetHeight(uv);

// 2. If it's pure black/transparent, output transparent so we don't draw normals over the void!
if (currentHeight <= 0.001) return float4(0, 0, 0, 0);

// 3. Step Size
float2 texel = 1.0 / TextureSize;

// 4. Sample Neighbors
float hL = GetHeight(uv + float2(-texel.x, 0));
float hR = GetHeight(uv + float2(texel.x, 0));
float hU = GetHeight(uv + float2(0, -texel.y));
float hD = GetHeight(uv + float2(0, texel.y));

// 5. Calculate Slopes
float dX = (hR - hL) * HeightScale;
float dY = (hD - hU) * HeightScale;

// 6. Generate Normal Vector (Z is 1.0 for pointing UP out of the ground)
float3 normal = normalize(float3(-dX, -dY, 1.0));

// 7. Convert Vector (-1 to 1) to RGB Color (0 to 1)
// Red = X-Axis, Green = Z-Axis (Up), Blue = Y-Axis
// Because this is ground, the Z axis maps to the Green color channel!
float3 normalColor = float3(
    (normal.x * 0.5) + 0.5,
    normal.z,
    (normal.y * 0.5) + 0.5
);

// Output Solid Alpha so it saves to PNG properly
return float4(normalColor, 1.0);
}

technique Basic{ pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }