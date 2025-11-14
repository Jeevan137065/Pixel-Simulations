cbuffer LightProperties : register(b0)
{
    float3 u_LightPos;       // Light position in screen space
    float  u_LightRadius;    // Light influence radius
    float  u_LightIntensity; // Light intensity multiplier
    float3 u_LightColor;     // RGB color of the light
};

Texture2D u_AlbedoMap : register(t0);
Texture2D u_NormalMap : register(t1);
SamplerState u_Sampler : register(s0);

struct VS_INPUT
{
    float4 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

// VSMain: pass-through for full-screen quad
PS_INPUT VSMain(VS_INPUT input)
{
    PS_INPUT output;
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

// PSMain: normal mapping with diffuse and attenuation
float4 PSMain(PS_INPUT inp) : SV_Target
{
    float2 uv = inp.TexCoord;
    float4 albedo = u_AlbedoMap.Sample(u_Sampler, uv);
    float3 normal = normalize(u_NormalMap.Sample(u_Sampler, uv).xyz * 2.0 - 1.0);

    float2 pixelPos = inp.Position.xy;
    float3 lightDir = normalize(float3(u_LightPos.xy - pixelPos, 0));
    float diff = max(dot(normal, lightDir), 0);
    float dist = length(u_LightPos.xy - pixelPos);
    float atten = saturate(1.0 - dist / u_LightRadius) * u_LightIntensity;
    float3 shaded = albedo.rgb * diff * atten * u_LightColor;

    return float4(shaded, albedo.a);
}

technique Lighting
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMain();
    }
}
