// --- UNIFORMS / PARAMETERS ---
float2 uResolution;
float4x4 MatrixTransform;
float uTime;
int uCountPrimary;
int uCountSecondary;
float uSlantPrimary;
float uSlantSecondary;
float uSpeedPrimary;
float uSpeedSecondary;
float uBlurPrimary;
float uBlurSecondary;
float2 uSizePrimary;
float2 uSizeSecondary;
float3 uRainColor;
float uAlpha;

float2 uWorldTileSize = float2(128.0, 128.0);
float4 uViewBounds;

// --- Input/Output Structs (Verified from our debug shader) ---
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};
struct PixelShaderInput
{
    float4 Position : SV_POSITION;
    float2 WorldPos : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
};


// --- VERTEX SHADER (Verified) ---
PixelShaderInput MainVS(VertexShaderInput input)
{
    PixelShaderInput output;

    // For a screen-filling effect, the vertex position is passed through directly.
    // The C# DrawUserPrimitives call handles placing it correctly on the screen.
    // ** We DO NOT multiply by a camera matrix here. **
    output.Position = input.Position;

    // Interpolate the world position based on which corner of the screen this vertex is.
    // uViewBounds.xy is the world coordinate of the top-left of the screen.
    // uViewBounds.zw is the world coordinate of the bottom-right.
    output.WorldPos = lerp(uViewBounds.xy, uViewBounds.zw, input.TexCoord);
    output.TexCoord = input.TexCoord;

    return output;

}

// --- HELPER FUNCTIONS (Unchanged) ---
float Hash(float x) { return frac(sin(x * 18.34) * 51.78); }
float Hash2(float x) { return frac(sin(x * 25.42) * 21.24); }
float line_sdf(float2 p, float2 s) { float2 d = abs(p) - s; return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)); }

// --- PIXEL SHADER (With fixes) ---
float4 MainPS(PixelShaderInput input) : SV_TARGET
{ 
    float2 worldUV = frac(input.WorldPos / uWorldTileSize);
// Screen UV is already in 0-1 range from input.TexCoord
float2 screenUV = input.TexCoord;

// Normalize to aspect ratio for uniform distribution
screenUV.x *= uResolution.x / uResolution.y; // Apply aspect ratio correction

// Use screenUV directly or blend minimally with worldUV
float2 uv = input.TexCoord * float2(0.5, 0.5);
//float2 uv = input.TexCoord;
uv.y = (1.0 - uv.y); // Flip Y-axis for correct rain direction
    float time = uTime*0.5;

// === PRIMARY RAIN LAYER (misty) ===
float rainPrimary = 0.0;
[loop] for (int i = 0; i < 200; i++)
{
    if (i >= uCountPrimary) break;
    float fi = float(i + 1);
    float h1 = Hash(fi);
    float h2 = Hash2(fi);
    float sl = h1 * uv.y * -uSlantPrimary;
    float pos_mod_x = h1;
    float dropSpeed = uSpeedPrimary + (h2 - 0.5) * 0.5;  // Reduced speed variation
    float2 position = float2(pos_mod_x + sl, fmod(dropSpeed * time, 1.0));  // Simplified motion
    float sdf = line_sdf(uv - position, uSizePrimary);  // Removed 0.1 multiplier
    rainPrimary += clamp(-sdf / max(0.001, uBlurPrimary), 0.0, 1.0);  // Prevent division by zero
}

// === SECONDARY RAIN LAYER (larger streaks) ===
float rainSecondary = 0.0;
// --- FIX #2: Reduce loop size here as well ---
[loop] for (int j = 0; j < 200; j++)
{
    if (j >= uCountSecondary) break;
    float fi = float(j + 1) + 50.0;
    float h1 = Hash(fi);
    float h2 = Hash2(fi);
    float sl = h1 * uv.y * -uSlantSecondary;
    float pos_mod_x = h1 * 1.2;
    float dropSpeed = uSpeedSecondary + (h2 - 0.5) * 0.5;  // Reduced speed variation
    float2 position = float2(pos_mod_x + sl, fmod(dropSpeed * time, 1.0));  // Simplified motion
    float sdf = line_sdf(uv - position, uSizeSecondary);  // Removed 0.1 multiplier
    rainSecondary += clamp(-sdf / max(0.001, uBlurSecondary), 0.0, 1.0);  // Prevent division by zero
}

float combined = saturate(rainPrimary * 0.5 + rainSecondary);
float3 finalColor = uRainColor * combined;
//float3 finalColor = float3(1,1,1) TESTING;

return float4(finalColor, uAlpha * combined);
}

technique Basic{
    pass P0 {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}