float4x4 MatrixTransform;
texture SpriteTexture;
sampler2D TextureSampler = sampler_state{ Texture = <SpriteTexture>; MinFilter = Point; MagFilter = Point; };

// Parameters
float WorldHeight;

struct VSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput {
    float4 Position : POSITION0;
    float Depth : TEXCOORD1; // Pass calculated depth
    float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input) {
    VSOutput output;
    output.Position = mul(input.Position, MatrixTransform);

    // Calculate normalized depth (0.0 at Top, 1.0 at Bottom) based on Y position
    // NOTE: Input Y is usually inverted in Screen Space, adjust logic if needed.
    // Assuming input.Position.y is World Y here.
    output.Depth = input.Position.y / WorldHeight;

    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(VSOutput input) : COLOR{
    float4 color = tex2D(TextureSampler, input.TexCoord);

// Alpha Cutout: Discard transparent pixels
if (color.a < 0.1) discard;

// Output White * Depth
// R=1, G=1, B=1, A=Depth
// Or strictly R=Depth for debugging/logic
//return float4(1, 1, 1, input.Depth);
return float4(input.Depth, input.Depth, input.Depth, 1);
}

technique Depth{ pass P0 { VertexShader = compile vs_3_0 MainVS(); PixelShader = compile ps_3_0 MainPS(); } }