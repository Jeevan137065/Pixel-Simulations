sampler2D SourceTexture : register(s0);
int Channel; // 0=R, 1=G, 2=B, 3=A

float4 PixelShaderFunction(float2 uv : TEXCOORD0) : COLOR0
{
    float4 col = tex2D(SourceTexture, uv);

// Remap HalfVector4 (0-1) to Visual Color (0-255)
if (Channel == 0) return float4(col.r, col.r, col.r, 1.0);
if (Channel == 1) return float4(col.g, col.g, col.g, 1.0);
if (Channel == 2) return float4(col.b, col.b, col.b, 1.0);
return float4(col.aaa, 1.0);
}

technique Debug{ pass P0 { PixelShader = compile ps_3_0 PixelShaderFunction(); } }