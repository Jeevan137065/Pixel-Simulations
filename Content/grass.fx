#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float u_camViewTop;
float u_camViewHeight;
float2 u_viewportScale;
float4x4 WorldViewProjection;
float Time;
float2 PlayerPos;

// Geometry & Dynamics
float u_baseH, u_baseW, u_taper, u_curve, u_stiff;
float2 u_windDir;
float u_windSpeed, u_waveLen, u_gustStr, u_flutterStr;
float u_pushRad, u_pushStr;
float u_animFPS;
int u_pixelSnap;
int u_isFlower;

// Color Grading
float3 u_cRoot, u_cMid, u_cTip;
float3 u_cPetal, u_cCore, u_cCalyx;

Texture2D ObjectDepthTexture;
sampler2D DepthSampler = sampler_state{
    Texture = <ObjectDepthTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
};

struct VertexInput {
    float3 RootPos : POSITION0;
    float2 T_Side : TEXCOORD0;
    float2 Wind_Height : TEXCOORD1;
    float Variation : TEXCOORD2;
    float4 FloraData : TEXCOORD3; // x: type, y: size, z: stage, w: rotJitter
};

struct PixelInput {
    float4 Position : SV_POSITION;
    float DepthValue : TEXCOORD0;
    float4 ScreenPos : TEXCOORD1;
    float2 RootUV : TEXCOORD2;
    float v_t : TEXCOORD3;
    float v_var : TEXCOORD4;
    float2 v_uv : TEXCOORD5;
    float v_fType : TEXCOORD6;
    float v_fStage : TEXCOORD7;
};

float2 getBezier(float2 p0, float2 p1, float2 p2, float t) {
    float u = 1.0 - t;
    return u * u * p0 + 2.0 * u * t * p1 + t * t * p2;
}

float2 getBezierTangent(float2 p0, float2 p1, float2 p2, float t) {
    float2 tanVec = 2.0 * (1.0 - t) * (p1 - p0) + 2.0 * t * (p2 - p1);
    float len = length(tanVec);
    return len > 0.0001 ? (tanVec / len) : float2(0.0, -1.0);
}

PixelInput MainVS(VertexInput input) {
    PixelInput output = (PixelInput)0;

    float t = input.T_Side.x;
    float side = input.T_Side.y;
    float2 p0 = input.RootPos.xy;
    float lean = input.RootPos.z;
    float localPhase = input.Wind_Height.x;
    float hMod = input.Wind_Height.y;
    float actualH = u_baseH * hMod;

    output.v_var = input.Variation;
    output.v_fType = input.FloraData.x;
    float fSize = input.FloraData.y;
    output.v_fStage = input.FloraData.z;
    float rotJitter = input.FloraData.w;

    // --- ANIMATION CEL STEPPING (FIXES BLURRINESS & JITTER) ---
    float timeStepped = u_animFPS > 0.0 ? floor(Time * u_animFPS) / u_animFPS : Time;

    // --- TRAVELING WAVE WIND PHYSICS ---
    float spatialProj = dot(p0, u_windDir);
    float wavePhase = (spatialProj / max(u_waveLen, 1.0)) - (timeStepped * u_windSpeed);
    float gust = pow(sin(wavePhase) * 0.5 + 0.5, 1.8) * u_gustStr;
    float microFlutter = sin(timeStepped * 6.5 + localPhase) * u_flutterStr;
    float totalDisplacement = (gust + microFlutter) * actualH;

    float droop = u_curve * (actualH / 40.0);

    // --- PLAYER INTERACTION PUSH ---
    float dist = distance(p0, PlayerPos);
    float2 push = float2(0, 0);
    if (dist < u_pushRad && dist > 0.001) {
        float force = (u_pushRad - dist) / u_pushRad;
        push = normalize(p0 - PlayerPos) * (force * 18.0 * u_pushStr);
    }

    float2 tipOffset = (u_windDir * (lean + totalDisplacement + droop)) + push;
    float2 p2 = p0 + float2(tipOffset.x, -actualH + abs(tipOffset.y * 0.25));
    float2 p1 = p0 + float2((p2.x - p0.x) * (1.0 - u_stiff) + (u_windDir.x * droop * 0.2), -actualH * 0.5);

    float2 finalPos;

    if (u_isFlower == 1) {
        output.v_uv = float2(side, t);
        float2 tangent = getBezierTangent(p0, p1, p2, 1.0);

        if (int(output.v_fType) == 5) {
            tangent = float2(0.0, 1.0); // Bluebell hanging down
        }

        // Apply angular tilt jitter to flower head
        float cosJ = cos(rotJitter);
        float sinJ = sin(rotJitter);
        tangent = float2(tangent.x * cosJ - tangent.y * sinJ, tangent.x * sinJ + tangent.y * cosJ);
        float2 normal = float2(-tangent.y, tangent.x);

        if (output.v_fStage < 0.5) fSize *= 0.55;       // Stage 0: Bud
        else if (output.v_fStage < 1.5) fSize *= 0.80;  // Stage 1: Sprout

        float2 headCenter = p2 + (tangent * (fSize * 0.6));
        finalPos = headCenter + (normal * side * (fSize * 0.5)) + (tangent * (t - 0.5) * fSize);
    }
    else {
        output.v_t = t;
        finalPos = getBezier(p0, p1, p2, t);
        float2 tangent = getBezierTangent(p0, p1, p2, t);
        float2 normal = float2(-tangent.y, tangent.x);

        float w = u_baseW * (1.0 - (t * u_taper));
        finalPos += normal * side * (w * 0.5);
    }

    if (u_pixelSnap == 1) {
        finalPos = floor(finalPos) + 0.5;
    }

    output.Position = mul(float4(finalPos, 0.0, 1.0), WorldViewProjection);
    output.ScreenPos = output.Position;

    float4 rootScreenPos = mul(float4(p0, 0.0, 1.0), WorldViewProjection);
    output.RootUV = (rootScreenPos.xy / rootScreenPos.w) * 0.5 + 0.5;
    output.RootUV.y = 1.0 - output.RootUV.y;
    output.DepthValue = saturate((p0.y - u_camViewTop) / max(u_camViewHeight, 1.0));

    return output;
}

float4 MainPS(PixelInput input) : COLOR0{
    // --- DEPTH SORTING & OCCLUSION ---
    float2 screenUV = (input.ScreenPos.xy / input.ScreenPos.w) * 0.5 + 0.5;
    screenUV.y = 1.0 - screenUV.y;

    // Scale screenUV to match active SimViewportRect inside MASTER_SIM target
    float2 depthUV = screenUV * u_viewportScale;

    float entityDepth = tex2D(DepthSampler, depthUV).r;
    // If an entity (Player, Tree, Prop) occupies this pixel:
    if (entityDepth > 0.001) {
        // If the grass root Y is HIGHER on screen than entity feet -> Grass is BEHIND entity!
        if (input.DepthValue < entityDepth - 0.0002) {
            clip(-1); // Discard grass pixel so Player / Tree draws in front!
        }
    }

    float4 outColor = float4(0, 0, 0, 1);

    if (u_isFlower == 1) {
        int type = int(input.v_fType);
        if (type <= 0) clip(-1);

        float2 uv = input.v_uv;
        float3 col = u_cPetal;
        float alpha = 0.0;
        int stage = int(input.v_fStage + 0.5);

        // Calyx / Sepal base connection
        if (uv.y < 0.25 && abs(uv.x) < 0.45) {
            return float4(u_cCalyx, 1.0);
        }

        // STAGE 0: CLOSED BUD
        if (stage == 0) {
            float dist = length(float2(uv.x * 1.5, (uv.y - 0.5) * 1.8));
            if (dist < 0.65) {
                alpha = 1.0;
                col = lerp(u_cCalyx, u_cPetal, uv.y * 1.2);
            }
        }
        else {
            if (type == 1) { // Dandelion
                float dist = length(float2(uv.x, (uv.y - 0.5) * 1.5));
                float maxR = (stage == 3) ? 0.95 : 0.85;
                if (dist < maxR) {
                    alpha = 1.0;
                    if (stage == 3) col = float3(0.92, 0.94, 0.92);
                    else col = lerp(u_cCore, u_cPetal, step(0.35, dist));
                }
            }
            else if (type == 2) { // Cattail
                if (abs(uv.x) < 0.45 && uv.y > 0.15 && uv.y < 0.85) {
                    alpha = 1.0;
                    col = lerp(u_cCore, u_cPetal, abs(uv.x) * 2.0);
                }
 else if (abs(uv.x) < 0.1 && uv.y >= 0.85) {
  alpha = 1.0; col = u_cCalyx;
}
}
else if (type == 3) { // Lavender
    float tier = sin(uv.y * 22.0) * 0.18;
    float width = (1.0 - uv.y * 0.6) * 0.4 + tier;
    if (abs(uv.x) < width && uv.y > 0.15) {
        alpha = 1.0;
        float bead = step(0.5, frac(uv.y * 11.0));
        col = lerp(u_cPetal, u_cCore, bead * 0.35);
    }
}
else if (type == 4) { // Wheat
    float zigzag = abs(frac(uv.y * 6.0) - 0.5);
    if (abs(uv.x) < 0.25 + zigzag * 0.35 && uv.y > 0.1) {
        alpha = 1.0;
        col = lerp(u_cCore, u_cPetal, zigzag * 1.6);
    }
}
else if (type == 5) { // Bluebell
    float bellW = uv.y * 0.6;
    if (abs(uv.x) < bellW && uv.y > 0.15 && uv.y < 0.85) {
        alpha = 1.0;
        col = lerp(u_cCore, u_cPetal, uv.y);
    }
}
else if (type == 6) { // Daisy
    float2 c = float2(uv.x, (uv.y - 0.5) * 2.0);
    float a = atan2(c.y, c.x);
    float shape = 0.5 + 0.45 * cos(a * 6.0);
    if (length(c) < shape) {
        alpha = 1.0;
        col = length(c) < 0.28 ? u_cCore : u_cPetal;
    }
}
else if (type == 7) { // Clover
    float2 c = float2(uv.x, (uv.y - 0.5) * 2.0);
    float a = atan2(c.y, c.x);
    float shape = 0.4 + 0.4 * cos(a * 3.0);
    if (length(c) < shape) {
        alpha = 1.0;
        col = lerp(u_cPetal, u_cCore, length(c) * 1.2);
    }
}
else if (type == 8) { // Thistle
    float2 c = float2(uv.x, (uv.y - 0.5) * 2.0);
    float sp = abs(frac(atan2(c.y, c.x) * 2.5) - 0.5) * 0.4;
    if (length(c) < 0.55 + sp) {
        alpha = 1.0;
        col = length(c) < 0.25 ? u_cCore : u_cPetal;
    }
}
}

if (alpha < 0.5) clip(-1);
outColor = float4(col, 1.0);
}
else {
        float3 col;
        if (input.v_t < 0.45) col = lerp(u_cRoot, u_cMid, input.v_t / 0.45);
        else col = lerp(u_cMid, u_cTip, (input.v_t - 0.45) / 0.55);

        if (input.v_var > 0.8) col += float3(0.04, 0.06, -0.02);
        else if (input.v_var < 0.2) col -= float3(0.03, 0.02, 0.04);

        outColor = float4(col, 1.0);
    }

    return outColor;
}

technique GrassDraw{
    pass P0 {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}