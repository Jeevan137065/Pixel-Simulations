// --- RollingFog.fx ---
#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 ViewportSize;
float Time;
float2 WindDirection;

// Fog Controls
float FogDensity;
float3 FogColor;

// Depth Controls
float FogTopAltitude; // E.g., 100.0
float MaxAltitude;    // Must match VolumeDepth pass (350.0)

Texture2D NoiseTextureA;
sampler NoiseSamplerA = sampler_state{ Texture = <NoiseTextureA>; AddressU = Wrap; AddressV = Wrap; };

Texture2D NoiseTextureB;
sampler NoiseSamplerB = sampler_state{ Texture = <NoiseTextureB>; AddressU = Wrap; AddressV = Wrap; };

Texture2D VolumeDepthTexture;
sampler VolumeDepthSampler = sampler_state{ Texture = <VolumeDepthTexture>; AddressU = Clamp; AddressV = Clamp; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    // 1. Dual Noise coordinates (different scales and scroll speeds)
    float2 windOffset = WindDirection * Time * 0.05;

    float2 uvA = (texCoord * 2.0) + windOffset;
    float2 uvB = (texCoord * 3.2) + (windOffset * 1.5) + float2(0.5, 0.5);

    float nA = tex2D(NoiseSamplerA, uvA).r;
    float nB = tex2D(NoiseSamplerB, uvB).r;

    // Multiply and boost to get patchy, organic clouds
    float patchyNoise = (nA * nB) * 2.0;

    // 2. Volumetric Depth Masking
    // normalizedAltitude = 0.0 (Floor), 1.0 (Sky/High objects)
    float normalizedAltitude = tex2D(VolumeDepthSampler, texCoord).r;
    float pixelAltitude = normalizedAltitude * MaxAltitude;

    // Fade fog out gracefully as it reaches the tops of trees/buildings
    float depthMask = 1.0 - smoothstep(max(0.0, FogTopAltitude - 40.0), FogTopAltitude, pixelAltitude);

    // 3. Final Alpha
    float finalAlpha = patchyNoise * FogDensity * depthMask;

    return float4(FogColor * finalAlpha, finalAlpha); // Premultiplied Alpha
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }