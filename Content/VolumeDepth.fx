#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float MaxAltitude = 350.0;

Texture2D SpriteTexture;
sampler2D SpriteSampler = sampler_state{ Texture = <SpriteTexture>; AddressU = Clamp; AddressV = Clamp; };

struct VSInput {
    float3 Position : POSITION0; // Z holds the BaseWorldY
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float Altitude : TEXCOORD1;
};

VSOutput MainVS(VSInput input) {
    VSOutput output;

    // Multiply X and Y by matrix, but force Z=0 so Ortho projection doesn't clip it out of bounds!
    output.Position = mul(float4(input.Position.x, input.Position.y, 0.0, 1.0), MatrixTransform);

    // Altitude = (Bottom of Sprite Y - Current Vertex Y)
    output.Altitude = clamp((input.Position.z - input.Position.y) / MaxAltitude, 0.0, 1.0);

    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(VSOutput input) : COLOR{
    float4 texColor = tex2D(SpriteSampler, input.TexCoord);
    if (texColor.a < 0.1) discard;

    // Red = Altitude, Green = Y-Sort Depth (passed via vertex color in C#)
    return float4(input.Altitude, input.Color.g, 0, texColor.a);
}

technique Basic{ pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }