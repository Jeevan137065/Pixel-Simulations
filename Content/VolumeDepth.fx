#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float SpriteTopY;
float SpriteBottomY;
float BaseWorldY;
float MaxAltitude;

// NEW: To handle SpriteBatch Atlas coordinates
float VMin;
float VMax;

Texture2D SpriteTexture;
sampler2D SpriteSampler = sampler_state{ Texture = <SpriteTexture>; MinFilter = Point; MagFilter = Point; };

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR
{
    float4 texColor = tex2D(SpriteSampler, texCoord) * color;

// Discard transparent pixels
if (texColor.a < 0.1) discard;

// Normalize the V coordinate (0.0 at top of sprite, 1.0 at bottom)
float normalizedY = 0.0;
if (VMax - VMin > 0.0001)
{
    normalizedY = (texCoord.y - VMin) / (VMax - VMin);
}

// Interpolate the exact World Y of this pixel
float worldY = lerp(SpriteTopY, SpriteBottomY, normalizedY);

// Calculate altitude (0.0 = Ground, 1.0 = MaxAltitude)
float altitude = clamp((BaseWorldY - worldY) / MaxAltitude, 0.0, 1.0);

// Output Red channel.
return float4(altitude, 0, 0, texColor.a);
}

technique Basic{
    pass P0 {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}