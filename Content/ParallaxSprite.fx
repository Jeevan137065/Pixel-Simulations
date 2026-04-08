#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float2 CameraPosition;
float ParallaxAmount;
float EnableLighting;
bool IsDepthPass; // NEW: True if rendering VolumeDepth, False if rendering Albedo

Texture2D SpriteTexture;
sampler SpriteSampler = sampler_state{ Texture = <SpriteTexture>; AddressU = Clamp; AddressV = Clamp; MagFilter = Point; MinFilter = Point; MipFilter = Point; };

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float NormalizedHeight : TEXCOORD1;
    float ParallaxMask : TEXCOORD2; // NEW!
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

    float2 offset = input.Position.xy - CameraPosition;

    // MULTIPLY BY MASK: If ParallaxMask is 0.0 (Player), this becomes + 0.0 (No shift!)
    float2 finalPos = input.Position.xy + (offset * ParallaxAmount * input.NormalizedHeight * input.ParallaxMask);

    output.Position = mul(float4(finalPos, input.Position.zw), MatrixTransform);

    output.Color = input.Color;
    float lightMultiplier = 1.0 + (input.NormalizedHeight * ParallaxAmount * 1.5 * input.ParallaxMask);
    output.Color.rgb *= lerp(1.0, lightMultiplier, EnableLighting);

    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(PSInput input) : SV_TARGET
{
    float4 texColor = tex2D(SpriteSampler, input.TexCoord);

    if (texColor.a < 0.05) discard;

    // If we are doing the VolumeDepth pass, output ONLY the Vertex Color (which holds the Depth Value in C#)
    if (IsDepthPass)
    {
        return input.Color;
    }

    return texColor * input.Color;
}

technique Basic
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}