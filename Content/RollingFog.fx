#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 CameraPosition;
float2 ViewportSize;
float Time;
float2 WindDirection;

// --- Fog Controls ---
float FogDensity;    // Overall Opacity (0.0 to 1.0)
float3 FogColor;     // Color of the fog

// --- Volumetric Controls ---
float FogTopAltitude; // How high does the fog bank reach?
float MaxAltitude;    // MUST MATCH the Depth Renderer (e.g., 150.0)

Texture2D NoiseTexture;
sampler NoiseSampler = sampler_state{ Texture = <NoiseTexture>; AddressU = Wrap; AddressV = Wrap; };

Texture2D VolumeDepthTexture;
sampler VolumeDepthSampler = sampler_state{ Texture = <VolumeDepthTexture>; AddressU = Clamp; AddressV = Clamp; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    // 1. Find World Position
    float2 worldPos = CameraPosition + (texCoord * ViewportSize);

// Scale the noise so it's large and fluffy. 
// Tweak 1000.0 to make the patches bigger or smaller.
float2 noiseUV = worldPos / 1000.0;

// 2. Scroll the noise with the wind
noiseUV += WindDirection * Time * 0.05;

// 3. Sample and SQUARE the noise (Your request!)
// This makes the darks darker and keeps the brights bright, creating patches.
float rawNoise = tex2D(NoiseSampler, noiseUV).r;
float patchyNoise = rawNoise * rawNoise;

// 4. Sample the Altitude Depth Map
float normalizedAltitude = tex2D(VolumeDepthSampler, texCoord).r;
float pixelAltitude = normalizedAltitude * MaxAltitude;

// 5. Volumetric Mask
// If the object's altitude is greater than the fog's top, mask = 0 (no fog).
// We use a 20-pixel gradient so it fades softly around the trunks of the trees.
float depthMask = 1.0 - smoothstep(max(0.0, FogTopAltitude - 20.0), FogTopAltitude, pixelAltitude);

// 6. Final Alpha Calculation
float finalAlpha = patchyNoise * FogDensity * depthMask;

// 7. Output PREMULTIPLIED Alpha. 
// This guarantees it blends perfectly when drawn over the final game world.
return float4(FogColor, finalAlpha);
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }