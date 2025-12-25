float4x4 MatrixTransform;
texture NormalTexture; // The _n texture
sampler2D NormalSampler = sampler_state{ Texture = <NormalTexture>; MinFilter = Point; MagFilter = Point; };

// Input/Output structs similar to above...
struct VSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput {
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input) {
    VSOutput output;
    output.Position = mul(input.Position, MatrixTransform);


    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(VSOutput input) : COLOR{
 
    float4 normalColor = tex2D(NormalSampler, input.TexCoord);

    if (normalColor.a < 0.1) discard;

    return normalColor;
}

technique Depth{ pass P0 { VertexShader = compile vs_3_0 MainVS(); PixelShader = compile ps_3_0 MainPS(); } }