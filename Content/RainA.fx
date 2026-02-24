#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- Parameters ---
float Time;
float Intensity; // 0.0 to 1.0 (from WeatherSimulator)
float WindSlant; // Horizontal push based on wind

// The noise texture (e.g., noise_blue_256)
Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state
{
    Texture = <NoiseTexture>;
    AddressU = Wrap;
    AddressV = Wrap;
    Filter = Point; // Point filtering makes it look blocky/pixel-art style
};

struct VertexShaderInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

// Pass-through VS
VertexShaderInput MainVS(VertexShaderInput input)
{
    return input;
}

float4 MainPS(VertexShaderInput input) : SV_TARGET
{
    // --- Layer 1: Fast, foreground streaks ---
    // We stretch the UVs drastically on the Y-axis (e.g., * 0.05) to turn dots into long streaks.
    // We scale the X-axis up (e.g., * 4.0) to make the streaks thin.
    float2 uv1 = input.TexCoord * float2(4.0, 0.05);

// Pan the UVs downward (Y) and slant them (X) over time
uv1.y -= Time * 5.0;
uv1.x -= Time * WindSlant * 2.0;

// Sample the noise. 
float noise1 = tex2D(NoiseSampler, uv1).r;

// --- Layer 2: Slower, background streaks (creates parallax/depth) ---
float2 uv2 = input.TexCoord * float2(6.0, 0.08); // Slightly smaller, different scale
uv2.y -= Time * 3.5;
uv2.x -= Time * WindSlant * 1.5;
float noise2 = tex2D(NoiseSampler, uv2).r;

// --- Thresholding (The "Minecraft" look) ---
// Noise returns 0.0 to 1.0. We only want to draw a pixel if the noise is VERY high.
// As Intensity goes up, the threshold drops, allowing more pixels to become rain.
float threshold = lerp(0.98, 0.85, Intensity);

float rain1 = step(threshold, noise1); // Returns 1 if noise1 > threshold, else 0
float rain2 = step(threshold, noise2) * 0.5; // Background rain is half opacity

// Combine layers
float totalRain = saturate(rain1 + rain2);

// Minecraft rain color (light, translucent blue)
float3 rainColor = float3(0.4, 0.6, 1.0);

// The alpha is determined by the totalRain mask, scaled by overall intensity
float alpha = totalRain * lerp(0.2, 0.6, Intensity);

return float4(rainColor * alpha, alpha); // Premultiplied alpha
}

technique Basic
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}