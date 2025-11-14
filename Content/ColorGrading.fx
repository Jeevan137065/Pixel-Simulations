Texture2D ScreenTexture;
sampler ScreenSampler = sampler_state{ Texture = <ScreenTexture>; };

// Parameters to be set from our JSON preset
float Desaturation;

// THE FIX - Part 1: We now expect color values in the 0-255 range.
// We rename it to make it clear.
float3 TintColor255;

float4 main(float2 texCoord : TEXCOORD0) : SV_TARGET
{
    float4 originalColor = tex2D(ScreenSampler, texCoord);
    float grayscale = dot(originalColor.rgb, float3(0.299, 0.587, 0.114));
    float3 desaturatedColor = lerp(originalColor.rgb, grayscale.xxx, Desaturation);

    // THE FIX - Part 2: The shader normalizes the color itself.
    // This is robust and removes any ambiguity.
    float3 normalizedTintColor = TintColor255 / 255.0;

    float3 finalColor = desaturatedColor * normalizedTintColor;
    return float4(finalColor, originalColor.a);
}

technique Basic{ pass P0 { PixelShader = compile ps_3_0 main(); } }