#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float MaxAltitude = 350.0; // The maximum height anything can reach before clipping the fog mask

Texture2D SpriteTexture;
sampler2D SpriteSampler = sampler_state{ Texture = <SpriteTexture>; AddressU = Clamp; AddressV = Clamp; };

struct VSInput {
    float3 Position : POSITION0; // Z = Relative Height of this vertex (e.g. 320 for the top, 0 for the bottom)
    float4 Color : COLOR0;       // R = DrawDepth, G = GroundY
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float Altitude : TEXCOORD1; // Passed to Pixel Shader
};

VSOutput MainVS(VSInput input) {
    VSOutput output;

    // Force Z to 0 for the actual screen-space projection
    output.Position = mul(float4(input.Position.x, input.Position.y, 0.0, 1.0), MatrixTransform);

    // Altitude is just the relative height divided by MaxAltitude!
    // This perfectly isolates the object from the global map depth.
    output.Altitude = clamp(input.Position.z / MaxAltitude, 0.0, 1.0);

    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(VSOutput input) : COLOR{
    float4 texColor = tex2D(SpriteSampler, input.TexCoord);
    if (texColor.a < 0.1) discard;

    // Red = Altitude (Used by Rolling Fog to cull fog at treetops)
    // Green = Ground Y (Used by Parallax to cull blades of grass behind the object)
    return float4(input.Altitude, input.Color.g, 0, texColor.a);
}

technique Basic{ pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }