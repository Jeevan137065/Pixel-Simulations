#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;

// The state of the particles calculated by ParticleUpdate.fx
Texture2D StateTexture;
sampler StateSampler = sampler_state{ Texture = <StateTexture>; Filter = Point; AddressU = Clamp; AddressV = Clamp; };

// Your 96x16 Particle Atlas
Texture2D AtlasTexture;
sampler AtlasSampler = sampler_state{ Texture = <AtlasTexture>; Filter = Point; AddressU = Clamp; AddressV = Clamp; };

// --- Particle Settings Passed from C# ---
float2 SpriteSize;       // World size (e.g., 8x16 for rain, 8x8 for snow)
float4 AtlasRect;        // UV Rect for the current weather (X, Y, Width, Height)
float2 AtlasGrid;        // How many sprites in the rect (Columns, Rows). e.g., (4, 1)
float2 WindDirection;    // For rotating Rain/Snow
float3 LeafColor1;
float3 LeafColor2;
int WeatherType; // 0=Rain, 1=Snow, 2=Dust, 3=Leaf, 4=Hail
// Input from C# Vertex Buffer
struct VertexShaderInput
{
    float ParticleID : POSITION0;   // 0, 1, 2, 3, etc.
    float2 Corner : TEXCOORD0;   // (0,0), (1,0), (0,1), (1,1)
};

struct PixelShaderInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float Alpha : COLOR0;
};

PixelShaderInput MainVS(VertexShaderInput input)
{
    PixelShaderInput output = (PixelShaderInput)0;

    // 1. Find this particle's position in the State Texture
    float texSize = 100.0; // Must match _stateTextureSize in C# (sqrt of 10000)
    float2 stateTexCoord = float2(frac(input.ParticleID / texSize), floor(input.ParticleID / texSize) / texSize);
    stateTexCoord += 0.5 / texSize; // Half pixel offset for perfect sampling

    // Read Position and Lifetime from the state texture
    float4 particleState = float4(stateTexCoord, 0, 0);
    float2 worldPos = particleState.xy;
    float lifetime = particleState.z;

    // Hide dead particles
    if (lifetime < 0.0) worldPos = float2(-99999, -99999);

    // 2. Calculate Rotation (Only slant Rain/Snow, don't slant Dew or Fog)
    float angle = atan2(normalize(WindDirection).y, normalize(WindDirection).x) + 3.14159f / 2.0f;
    float s, c;
    sincos(angle, s, c);

    // 3. Offset corners to create the sprite
    float2 offset = (input.Corner - 0.5) * SpriteSize;
    offset = mul(offset, float2x2(c, -s, s, c)); // Rotate

    output.Position = mul(float4(worldPos + offset, 0, 1), MatrixTransform);

    // 4. ATLAS UV MAPPING
    // Pick a random variant based on ID
    float variantIndex = floor(fmod(input.ParticleID, AtlasGrid.x * AtlasGrid.y));
    float varX = fmod(variantIndex, AtlasGrid.x);
    float varY = floor(variantIndex / AtlasGrid.x);

    // Calculate UVs based on the specific region passed from C#
    float2 spriteUVSize = float2(AtlasRect.z / AtlasGrid.x, AtlasRect.w / AtlasGrid.y);
    float2 uvStart = float2(AtlasRect.x, AtlasRect.y) + float2(varX * spriteUVSize.x, varY * spriteUVSize.y);

    output.TexCoord = uvStart + (input.Corner * spriteUVSize);

    // Fade out based on lifetime (optional, good for splashes)
    output.Alpha = saturate(lifetime * 5.0);

    return output;
}

float4 MainPS(PixelShaderInput input) : SV_TARGET
{
float4 color = tex2D(AtlasSampler, input.TexCoord);

// If this is a LEAF (WeatherType == 3), we tint the grayscale atlas texture
if (WeatherType == 3)
{
    // Use the particle's X position to randomly assign Color1 or Color2
    float randomBlend = frac(input.Position.x * 0.123);
    float3 targetColor = lerp(LeafColor1, LeafColor2, randomBlend);

    // Tint the white texture
    color.rgb *= targetColor;
}
// If HAIL (WeatherType == 4), make it pure white and slightly opaque
else if (WeatherType == 4)
{
    color.rgb = float3(1.0, 1.0, 1.0);
    color.a *= 0.9;
}

color.a *= input.Alpha;
return color;
}

technique Basic{ pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }