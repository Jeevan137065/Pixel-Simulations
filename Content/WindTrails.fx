#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float Time;
float WindSpeed;
float Intensity; // 0.0 to 1.0
float2 WindDirection;

Texture2D NoiseTexture; // Pass Streak01.png or Swirl.png here
sampler NoiseSampler = sampler_state{ Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    // Stretch the UVs horizontally so the noise looks like long wind streaks
    float2 uv = texCoord * float2(1.0, 3.0);

// Scroll rapidly
uv -= WindDirection * Time * WindSpeed * 0.5;

float rawNoise = tex2D(NoiseSampler, uv).r;

// High threshold: only the brightest parts of the noise become wind trails
float trailMask = smoothstep(0.7, 0.9, rawNoise);

// Make the wind fade in and out organically over time (gusting effect)
float gusting = (sin(Time * 2.0) * 0.5 + 0.5) * 0.5 + 0.5;

float finalAlpha = trailMask * Intensity * gusting * 0.25; // 25% max opacity

return float4(1.0, 1.0, 1.0, finalAlpha); // Faint white streaks
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }