#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- C# PARAMETERS ---
float4x4 WorldViewProjection;
float2 ScreenResolution; // e.g. 960x540
float Time;
float BlurAmount = 1.0;
float RippleSpeed = 1.0;
float ReflectionOffset = 0.0;
float IsNegative = 1.0; // 1.0 = Floor/River, 0.0 = Wall Mirror
float4 TintColor = float4(0.0, 0.5, 1.0, 0.5);

// The dynamic texture (Player, Props, Grass)
Texture2D DynamicTexture;
sampler2D DynamicSampler = sampler_state
{
    Texture = <DynamicTexture>;
    AddressU = Clamp; // Crucial: Don't wrap the screen!
    AddressV = Clamp;
};

// --- STRUCTS ---
// Use TEXCOORD0 for passing position to pixel shader in ps_3_0
struct VertexInput { float4 Position : POSITION; float4 Color : COLOR0; };
struct VertexOutput { float4 Position : SV_POSITION; float4 ScreenPos : TEXCOORD0; float4 Color : COLOR0; };

// --- VERTEX SHADER ---
VertexOutput MainVS(in VertexInput input)
{
    VertexOutput output;
    output.Position = mul(input.Position, WorldViewProjection);
    output.ScreenPos = output.Position; // Pass screen position via TEXCOORD0
    output.Color = input.Color;
    return output;
}

// --- PIXEL SHADER ---
float4 MainPS(VertexOutput input) : COLOR
{
    // Use input.ScreenPos instead of input.Position for screen pixel position
    float2 screenUV = input.ScreenPos.xy / ScreenResolution;

    float2 refUV = screenUV;

    if (IsNegative > 0.5)
    {
        refUV.y = 1.0 - refUV.y;
        refUV.y += ReflectionOffset;
        float ripple = sin((refUV.y * 50.0) + (Time * RippleSpeed)) * 0.005;
        refUV.x += ripple;
    }
    else
    {
        refUV.x = 1.0 - refUV.x;
        refUV.x += ReflectionOffset;
    }

    if (refUV.x < 0.0 || refUV.x > 1.0 || refUV.y < 0.0 || refUV.y > 1.0) return float4(0,0,0,0);

    float4 color = tex2D(DynamicSampler, refUV);

    // ... apply blur and tint ...
    color *= TintColor * input.Color;

    if (color.a < 0.05) discard;

    return color;
}

technique Reflection
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};