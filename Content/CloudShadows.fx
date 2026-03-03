#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 CameraPosition;
float2 ViewportSize;
float Time;
float2 WindDirection;

// 0.0 = Clear Sky, 1.0 = Completely Overcast
float CloudCover;

Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state{ Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    // 1. World Position
    float2 worldPos = CameraPosition + (texCoord * ViewportSize);

// 2. Scale the noise massively so a single cloud covers multiple screens
float2 uv = worldPos / 2500.0;

// 3. Clouds move high in the atmosphere. We use the wind direction, 
// but they move smoothly and slowly across the map.
float2 offset = WindDirection * Time * 0.02;

// 4. Two-Layer FBM for fluffy cloud edge shapes
float n1 = tex2D(NoiseSampler, uv - offset).r;
float n2 = tex2D(NoiseSampler, (uv * 2.0) - (offset * 1.5)).r;
float rawNoise = (n1 * 0.7) + (n2 * 0.3);

// 5. Apply Cloud Cover Threshold
// If CloudCover is low, only the absolute highest peaks of noise become shadows.
// If CloudCover is high, almost all noise becomes shadow.
float threshold = 1.0 - CloudCover;

// Smoothstep gives the shadows soft, blurry edges on the ground
float shadowAlpha = smoothstep(threshold, threshold + 0.3, rawNoise);

// 6. Max Darkness
// Clouds don't cast pitch-black shadows. We cap the darkness (e.g., 40% opacity max)
float maxDarkness = 0.4;
shadowAlpha *= maxDarkness;

// Output pure black with the calculated alpha
return float4(0.0, 0.0, 0.0, shadowAlpha);
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }