#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 WorldViewProjection;
float Time;
float2 PlayerPos;

// Params
float u_baseH, u_baseW, u_taper, u_curve;
float u_wSpd, u_wInt, u_stiff;
float u_pushRad, u_pushStr;
float3 u_cRoot, u_cTip, u_cFlower, u_cFlower2;
int u_isFlower;

Texture2D ObjectDepthTexture;
sampler2D DepthSampler = sampler_state{ Texture = <ObjectDepthTexture>; AddressU = Clamp; AddressV = Clamp; };

struct VertexInput {
    float3 RootPos : POSITION0;
    float2 T_Side : TEXCOORD0;
    float2 Wind_Height : TEXCOORD1;
    float Variation : TEXCOORD2;
    float4 Color : COLOR0;
    float4 FloraData : TEXCOORD3; // x=Type, y=Size
};

struct PixelInput {
    float4 Position : SV_POSITION;
    float DepthValue : TEXCOORD0;
    float4 ScreenPos : TEXCOORD1;
    float2 RootUV : TEXCOORD2;

    // Extracted pass-throughs
    float v_t : TEXCOORD3;
    float v_var : TEXCOORD4;
    float2 v_uv : TEXCOORD5;
    float v_fType : TEXCOORD6;
};

float2 getBezier(float2 p0, float2 p1, float2 p2, float t) {
    float u = 1.0 - t;
    return u * u * p0 + 2.0 * u * t * p1 + t * t * p2;
}

PixelInput MainVS(VertexInput input) {
    PixelInput output = (PixelInput)0;

    float t = input.T_Side.x;
    float side = input.T_Side.y;
    float2 p0 = input.RootPos.xy;
    float lean = input.RootPos.z;
    float windOff = input.Wind_Height.x;
    float hMod = input.Wind_Height.y;

    output.v_var = input.Variation;
    output.v_fType = input.FloraData.x;
    float fSize = input.FloraData.y;

    float actualH = u_baseH * hMod;
    float sway = sin(Time * u_wSpd + windOff) * (u_wInt * actualH);
    float droop = u_curve * (actualH / 100.0);

    float dist = distance(p0, PlayerPos);
    float2 push = float2(0, 0);
    if (dist < u_pushRad) {
        float force = (u_pushRad - dist) / u_pushRad;
        push = normalize(p0 - PlayerPos) * (force * 18.0 * u_pushStr);
    }

    float2 p2 = p0 + float2(lean + sway + push.x + droop, -actualH + abs(sway * 0.1));
    float2 p1 = p0 + float2(((p2.x - p0.x) * (1.0 - u_stiff)) + (droop * 0.2), -(actualH * 0.5));

    float2 finalPos;

    if (u_isFlower == 1) {
        output.v_uv = float2(side, t);
        finalPos = p2 + float2(side * fSize, (t - 0.5) * fSize * 2.0);
    }
    else {
        output.v_t = t;
        finalPos = getBezier(p0, p1, p2, t);
        float w = u_baseW * (1.0 - (t * u_taper));
        finalPos.x += side * (w / 2.0);
    }

    output.Position = mul(float4(finalPos, 0.0, 1.0), WorldViewProjection);
    output.ScreenPos = output.Position;

    float4 rootScreenPos = mul(float4(p0, 0.0, 1.0), WorldViewProjection);
    output.RootUV = (rootScreenPos.xy / rootScreenPos.w) * 0.5 + 0.5;
    output.RootUV.y = 1.0 - output.RootUV.y;
    output.DepthValue = p0.y / 32768.0;

    return output;
}

float4 MainPS(PixelInput input) : COLOR0{

    // Depth Sorting
    float2 screenUV = (input.ScreenPos.xy / input.ScreenPos.w) * 0.5 + 0.5;
    screenUV.y = 1.0 - screenUV.y;

    float rootAltitude = tex2D(DepthSampler, input.RootUV).r;
    if (rootAltitude > 0.03) clip(-1);

    float objDepth = tex2D(DepthSampler, screenUV).g;
    if (objDepth > 0.001 && input.DepthValue < objDepth - 0.0005) clip(-1);

    float4 outColor = float4(0,0,0,0);

    if (u_isFlower == 1) {
        int type = (int)input.v_fType;
        if (type == 0) clip(-1);

        float alpha = 0.0;
        float2 c = input.v_uv;
        float mixFac = 0.0;

        if (type == 1) {
            alpha = length(c) < 1.0 ? 1.0 : 0.0;
            mixFac = step(0.5, length(c));
        }
 else if (type == 2) {
  float2 pod = c * float2(2.5, 0.8);
  alpha = length(max(abs(pod) - float2(0.0, 0.5), 0.0)) < 0.3 ? 1.0 : 0.0;
  mixFac = step(0.5, c.y);
}
else if (type == 3) {
 float clusters = sin(c.y * 15.0) * 0.2;
 alpha = abs(c.x) < 0.3 + clusters && abs(c.y) < 0.9 ? 1.0 : 0.0;
 mixFac = step(0.15, abs(c.x));
}
else if (type == 4) {
 float zigzag = abs(frac(c.y * 4.0) - 0.5);
 alpha = abs(c.x) < 0.2 + zigzag * 0.4 && abs(c.y) < 0.9 ? 1.0 : 0.0;
 mixFac = step(0.2, zigzag);
}
else if (type == 5) {
 alpha = (c.y > 0.2 && length(c - float2(sin(c.y * 3.0) * 0.3, 0.0)) < 0.6) ? 1.0 : 0.0;
 mixFac = step(0.5, c.y);
}
else if (type == 6) {
 float a = atan2(c.y, c.x);
 float r = 0.4 + 0.6 * cos(a * 5.0);
 alpha = length(c) < r ? 1.0 : 0.0;
 mixFac = step(0.5, length(c) / r);
}
else if (type == 7) {
 alpha = (1.0 - abs(c.x) * 4.0) > c.y + 0.5 ? 1.0 : 0.0;
 mixFac = step(0.3, abs(c.x) * 4.0);
}

if (alpha < 0.5) clip(-1);
outColor = float4(lerp(u_cFlower2, u_cFlower, mixFac), 1.0);

}
else {
 float3 tip = u_cTip;
 if (input.v_var > 0.8) tip += float3(0.05, 0.1, -0.05);

 float3 baseColor = lerp(u_cRoot, tip, input.v_t);
 float3 shadedColor = lerp(baseColor * 0.3, baseColor, input.v_t);
 outColor = float4(shadedColor, 1.0);
}

return outColor;
}

technique GrassDraw{ pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }