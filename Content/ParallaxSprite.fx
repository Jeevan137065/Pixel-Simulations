#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform; // View * Projection
float2 CameraPosition;
float ParallaxAmount;     // Strength of the 3D effect (e.g., 0.15)
float EnableLighting;     // 1.0 = True, 0.0 = False
Texture2D SpriteTexture;
sampler SpriteSampler = sampler_state{ Texture = <SpriteTexture>; AddressU = Clamp; AddressV = Clamp; MagFilter = Point; MinFilter = Point; MipFilter = Point; };

// Must match our C# struct perfectly!
struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float NormalizedHeight : TEXCOORD1;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

PSInput MainVS(VSInput input)
{
    PSInput output;

    // 1. Calculate Parallax Shift
    float2 offset = input.Position.xy - CameraPosition;

    // Bottom vertices (Height=0) stay anchored to the ground. 
    // Top vertices (Height=1) shift based on camera distance.
    float2 finalPos = input.Position.xy + (offset * ParallaxAmount * input.NormalizedHeight);

    // 2. Apply Camera Matrix
    output.Position = mul(float4(finalPos, input.Position.zw), MatrixTransform);

    // 3. Fake 3D Shading: Tops of trees catch more light
    output.Color = input.Color;
    float lightMultiplier = 1.0 + (input.NormalizedHeight * ParallaxAmount * 1.5);

    // If EnableLighting is 0.0, it multiplies by 1.0 (no change). 
    // If EnableLighting is 1.0, it multiplies by lightMultiplier.
    output.Color.rgb *= lerp(1.0, lightMultiplier, EnableLighting);

    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(PSInput input) : SV_TARGET
{
    float4 texColor = tex2D(SpriteSampler, input.TexCoord);

// Discard transparent pixels so they don't overwrite things behind them
if (texColor.a < 0.05) discard;

return texColor * input.Color;
}

technique Basic{ pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }