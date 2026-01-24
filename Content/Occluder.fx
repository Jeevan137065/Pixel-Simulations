// Occluder.fx
float4x4 WorldViewProjection;
float2 PlayerPos;    // Base position for depth
float2 PlayerOrigin; // Size/Pivot

struct VertexInput {
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput {
    float4 Position : POSITION;
    float DepthValue : TEXCOORD0;
};

struct PixelShaderOutput {
    float4 Color : COLOR0; // Dynamic RT
    float4 Normal : COLOR1; // Normal RT
    float4 Data : COLOR2; // DepthOnly RT (HalfVector2)
};

PixelInput MainVS(VertexInput input) {
    PixelInput output;

    // Use input.Position as raw world coordinates
    output.Position = mul(float4(input.Position.xy, 0, 1), WorldViewProjection);

    // Calculate Depth based on the "Foot" (Y) of the building
    float z = 1.0 - (input.Position.z / 10000.0);
    output.Position.z = z;
    output.Position.w = 1.0;
    output.DepthValue = z;

    return output;
}

PixelShaderOutput MainPS(PixelInput input) {
    PixelShaderOutput output;

    // 1. Write NOTHING to the visible color buffer
    output.Color = float4(0, 0, 0, 0);

    // 2. Write a flat normal
    output.Normal = float4(0.5, 0.5, 1.0, 1.0);

    // 3. Write Depth and Material ID (1.0 = Building)
    output.Data = float4(input.DepthValue, 1.0, 0, 1);

    return output;
}

technique OccluderDraw{ pass P0 { 
    VertexShader = compile vs_3_0 MainVS(); 
    PixelShader = compile ps_3_0 MainPS(); 
} }