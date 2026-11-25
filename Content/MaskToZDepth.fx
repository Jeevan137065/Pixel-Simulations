#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D MaskTexture;
sampler2D MaskSampler = sampler_state{
    Texture = <MaskTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
};

// Maps:
// Source Red   (Biomes) -> Target Blue
// Source Green (Spawns) -> Target Green
// Target Red   (Z-Depth) -> Preserved / 0
float4 MainPS(float2 uv : TEXCOORD0) : COLOR0{
    float4 mask = tex2D(MaskSampler, uv);
    return float4(0.0, mask.g, mask.r, 1.0);
}

technique MaskRoute{
    pass P0 {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}