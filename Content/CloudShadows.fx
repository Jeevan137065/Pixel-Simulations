#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 CameraPosition;
float2 ViewportSize;
float Time;
float2 WindDirection;
float CloudCover; // 0.0 (Clear) to 1.0 (Overcast)

Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state{ Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    // Convert to world space to keep clouds anchored to the world, not the screen
    float2 worldPos = CameraPosition + (texCoord * ViewportSize);

// 1. Layer 1: Massive, slow base clouds
float2 uv1 = (worldPos / 3000.0) + (WindDirection * Time * 0.01);
float n1 = tex2D(NoiseSampler, uv1).r;

// 2. Layer 2: Medium, slightly faster detail clouds
float2 uv2 = (worldPos / 1500.0) + (WindDirection * Time * 0.02);
float n2 = tex2D(NoiseSampler, uv2).r;

// 3. Layer 3: Small, fast wispy clouds
float2 uv3 = (worldPos / 500.0) + (WindDirection * Time * 0.035);
float n3 = tex2D(NoiseSampler, uv3).r;

// Combine using FBM (Fractal Brownian Motion) ratios
float totalNoise = (n1 * 0.5) + (n2 * 0.3) + (n3 * 0.2);

// Map the CloudCover (0-1) to a threshold.
// If CloudCover is 1.0, threshold is low (everything is shadowed).
// If CloudCover is 0.1, threshold is high (only thickest clouds cast shadows).
float threshold = 1.0 - (CloudCover * 1.2);

// Smoothstep creates soft, blurry edges for the shadows
float shadowMask = smoothstep(threshold, threshold + 0.3, totalNoise);

// Max shadow opacity is 40% (0.4) so we don't pitch-black the game
float finalAlpha = shadowMask * 0.4;

return float4(0.0, 0.0, 0.0, finalAlpha);
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }