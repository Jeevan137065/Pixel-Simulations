#if OPENGL
#define SV_Position POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define SV_Position SV_POSITION
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

//-----------------------------------------------------------------------------
// PARAMETERS
//-----------------------------------------------------------------------------
Texture2D NormalMap;
int ShadingMode;
float2 LightPosition;
float3 LightColor;
float LightRadius;
float LightIntensity;
float3 Attenuation;
float2 ScreenDimensions;
float2 QuadTopLeft;
float2 QuadSize;
uniform float4x4 MatrixTransform;

// --- New Drama Parameters ---
float3 CoreColor;
float BandSmoothness;
float3 RimColor;
float RimIntensity;
float Time; // For animations

uniform int LightType;

//-----------------------------------------------------------------------------
// SAMPLERS & VERTEX SHADER (Unchanged)
//-----------------------------------------------------------------------------
sampler NormalMapSampler = sampler_state{ Texture = <NormalMap>; MinFilter = Linear; MagFilter = Linear; AddressU = Clamp; AddressV = Clamp; };
struct VertexShaderInput { float4 Position : POSITION0; float2 TexCoord : TEXCOORD0; };
struct VertexShaderOutput { float4 Position : SV_Position; float2 TexCoord : TEXCOORD0; };

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.TexCoord = input.TexCoord;
    return output;
}

//-----------------------------------------------------------------------------
// PIXEL SHADER (NEW DRAMATIC LOGIC)
//-----------------------------------------------------------------------------
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float2 screenCoord = QuadTopLeft + (input.TexCoord * QuadSize);
    float2 delta = LightPosition - screenCoord;
    float distance = length(delta);

    if (distance > LightRadius) { return float4(0, 0, 0, 0); }

    // --- Common Calculations ---
    float2 uv = screenCoord / ScreenDimensions;
    float3 surfaceNormal = tex2D(NormalMapSampler, uv).rgb * 2.0 - 1.0;
    float3 lightDir = float3(0, 0, 1);
    float diffuse = saturate(dot(surfaceNormal, lightDir));
    float3 finalColor = float3(0, 0, 0); // Start with black

    // --- Style-Specific Logic ---
    if (ShadingMode == 1) // Pow - White Hot Core
    {
        float shadingFactor = pow(diffuse, 8.0);
        // Create a blend factor for the core. This is 1.0 at the very center, fading to 0.0.
        float coreBlend = smoothstep(0.9, 1.0, diffuse);
        // Mix the main light color with the "white hot" core color.
        float3 blendedColor = lerp(LightColor, CoreColor, coreBlend);
        finalColor = blendedColor * shadingFactor;
    }
    else if (ShadingMode == 2) // Bands - Sharper and Smoother
    {
        // Define thresholds for our bands
        float highlightThreshold = 0.8;
        float midtoneThreshold = 0.5;
        float shadowBrightness = 0.1;

        // Use smoothstep to create controllable transitions between bands
        float midtone = smoothstep(midtoneThreshold, midtoneThreshold + BandSmoothness, diffuse);
        float highlight = smoothstep(highlightThreshold, highlightThreshold + BandSmoothness, diffuse);

        // Combine the bands. Each one adds brightness.
        float shadingFactor = shadowBrightness + (midtone * 0.6) + (highlight * 0.5);
        finalColor = LightColor * shadingFactor;
    }
    else if (ShadingMode == 3) // Rim - Electric Flicker
    {
        // Soft base lighting so the object isn't black
        finalColor = LightColor * pow(diffuse, 2.0);

        // Standard rim calculation
        float3 viewDir = float3(0, 0, 1);
        float rim = pow(1.0 - saturate(dot(surfaceNormal, viewDir)), 10.0);

        // Add a chaotic, electric flicker using time
        float flicker = saturate(sin(Time * 20.0) * sin(Time * 35.7)); // Multiplied sines are less regular

        // Add the flickering rim light on top of the base light
        finalColor += RimColor * rim * flicker * RimIntensity;
    }
    else // Smooth (mode 0)
    {
        finalColor = LightColor * diffuse;
    }

    // --- Final Assembly ---
    float attenuation = 1.0 / (Attenuation.x + Attenuation.y * distance + Attenuation.z * distance * distance);
    return float4(finalColor * LightIntensity * attenuation, 1.0* attenuation);
}

//-----------------------------------------------------------------------------
// TECHNIQUE (Unchanged)
//-----------------------------------------------------------------------------
technique BasicColorDrawing{ 
    pass P0 
    { 
        VertexShader = compile VS_SHADERMODEL MainVS(); 
        PixelShader = compile PS_SHADERMODEL MainPS(); 
    } 
};