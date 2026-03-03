#if OPENGL
	#define PS_SHADERMODEL ps_3_0
#else
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- NEW PARAMETERS ---
float SpriteTopY;      // The World Y coordinate of the very top of the sprite image
float SpriteBottomY;   // The World Y coordinate of the very bottom of the sprite image
float BaseWorldY;      // The World Y coordinate where the object touches the ground
float DrawDepth;       // 0.0 to 1.0 (for the Blue Channel)
float MaxAltitude;     // Normalizer (e.g., 150.0)

Texture2D SpriteTexture;
sampler SpriteSampler = sampler_state { Texture = <SpriteTexture>; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 texColor = tex2D(SpriteSampler, texCoord);
    if (texColor.a < 0.1) discard; 

    // 1. Calculate EXACT World Y using the UV coordinates (Camera Independent!)
    // texCoord.y goes from 0.0 (top) to 1.0 (bottom)
    float pixelWorldY = lerp(SpriteTopY, SpriteBottomY, texCoord.y);

    // 2. Altitude is how far the pixel is above the base of the object.
    float altitude = max(0.0, BaseWorldY - pixelWorldY);
    
    // 3. Normalize to 0.0 -> 1.0
    float normalizedAltitude = saturate(altitude / MaxAltitude);

    // Red = Altitude Map, Blue = Draw Depth Map
    return float4(normalizedAltitude, 0.0, DrawDepth, 1.0);
}

technique Basic { pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }