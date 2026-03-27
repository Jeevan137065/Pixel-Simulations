#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
sampler2D SpriteSampler = sampler_state{ Texture = <SpriteTexture>; MinFilter = Point; MagFilter = Point; };

float WaterLevel; // Passed from C#. e.g., 80.0 / 255.0
float4 WaterColor; // The color of the water

float4 MainPS(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 depthData = tex2D(SpriteSampler, texCoord);

    float blueElevation = depthData.b;

    // Test: If elevation is greater than 0 and less than our WaterLevel
    if (blueElevation > 0.01 && blueElevation <= WaterLevel)
    {
        // Output solid water color. We will use AlphaBlend in SpriteBatch to make it translucent.
        return WaterColor;
    }

    // Output completely transparent for all other pixels
    return float4(0, 0, 0, 0);
}

technique Basic{ pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } }