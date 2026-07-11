#define VS_SHADER_MODEL vs_3_0
#define PS_SHADER_MODEL ps_3_0

float4x4 MatrixTransform;

// Graphics Samplers
sampler2D AlbedoSampler : register(s0);    // s0: Background riverbed/terrain
sampler2D WaterDataSampler : register(s1); // s1: Water Surface Data (R: Flow, G: Foam)
sampler2D MaskSampler : register(s2);      // s2: Terrain Mask (B: Ground Elevation)

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.UV = input.UV;
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    // 1. Read the raw terrain elevation from the Blue channel of the Terrain Mask
    float4 maskData = tex2D(MaskSampler, input.UV);
    float terrainElevation = maskData.b; // Ground height is stored in B

    // 2. Set the baseline water surface height (matches shore level 180 / 255.0)
    float waterSurfaceLevel = 180.0 / 255.0;

    // 3. Depth is the difference between water surface and ground elevation
    float depth = max(0.0, waterSurfaceLevel - terrainElevation);

    // If outside water bounds, cleanly discard the pixel
    if (depth <= 0.0)
    {
        clip(-1);
        return float4(0, 0, 0, 0);
    }

    // Read underlying riverbed color
    float4 bedColor = tex2D(AlbedoSampler, input.UV);

    // Exponential light absorption (Beer-Lambert)
    float absorption = 1.0 - exp(-depth * 4.5);

    // Smooth depth color shift
    float4 shallowColor = float4(0.08, 0.72, 0.65, 0.5); // Semi-transparent teal
    float4 deepColor = float4(0.06, 0.16, 0.35, 0.75);   // Semi-transparent deep blue
    float4 volumeColor = lerp(shallowColor, deepColor, absorption);

    return lerp(bedColor, volumeColor, absorption);
}

technique SimpleWater
{
    pass Pass1
    {
        VertexShader = compile VS_SHADER_MODEL VertexShaderFunction();
        PixelShader = compile PS_SHADER_MODEL PixelShaderFunction();
    }
}