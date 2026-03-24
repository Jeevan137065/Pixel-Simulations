// Parameters
float4x4 WorldViewProjection;
float Time;
float2 PlayerPos;
float WindSpeed, WindIntensity, Stiffness;
float PlayerPushStrength, PlayerPushRadius;
float Segments, RestingCurvature, BladeThickness, BladeTaper;

struct VertexInput {
    float4 RootPos : POSITION0;     // xyz
    float2 T_Side : TEXCOORD0;      // x: T (0.0 to 1.0), y: Side (-1.0 to 1.0)
    float2 Wind_Height : TEXCOORD1; // x: WindOffset, y: HeightScale
    float Variation : TEXCOORD2;    // Seed for color
    float4 Color : COLOR0;
};

struct PixelInput {
    float4 Position : POSITION; // Use POSITION semantic for vs_3_0 compatibility
    float4 Color : COLOR0;
    float DepthValue : TEXCOORD1;
};

struct PixelShaderOutput {
    float4 Color : COLOR0;  // Goes to RT_Dynamic
    float4 Normal : COLOR1; // Goes to RT_Normal
    float4 Depth : COLOR2;   // Goes to RT_DepthOnly (R channel)
};

// Quadratic Bezier Function
float2 GetBezier(float2 p0, float2 p1, float2 p2, float t) {
    float invT = 1.0 - t;
    return invT * invT * p0 + 2.0 * invT * t * p1 + t * t * p2;
}

PixelInput MainVS(VertexInput input) {
    PixelInput output;

    // 1. Unpack data
    float t = input.T_Side.x;
    float side = input.T_Side.y;
    float2 p0 = input.RootPos.xy;
    float lean = input.RootPos.z;
    float windOffset = input.Wind_Height.x;
    float heightScale = input.Wind_Height.y;

    // 2. Physics Logic
    float height = 10.0 * heightScale;
    float sway = sin(Time * WindSpeed + windOffset) * (WindIntensity * height);

    // Player Interaction (Push)
    float dist = distance(p0, PlayerPos);
    float2 push = float2(0, 0);
    float trample = 0;

    if (dist < PlayerPushRadius) {
        float force = (PlayerPushRadius - dist) / PlayerPushRadius;
        push = normalize(p0 - PlayerPos) * (force * 18.0 * PlayerPushStrength);
        trample = force * 8.0 * PlayerPushStrength;
    }

    // 3. Define Bezier Points
    float stepT = floor(t * Segments) / Segments;
    float droop = RestingCurvature * (height / 10.0);
    // P0: Root (Static)
    // P1: Mid-point (Affected by Stiffness)
    // P2: Tip (Full sway and push)
    float2 p2 = p0 + float2(lean + sway + push.x + droop, -height + abs(push.y * 0.5));
    float2 p1 = lerp(p0, p2, Stiffness) + float2(droop * 0.2, 2.0);
    // 4. Calculate Vertex Position on Curve
    float2 pos = GetBezier(p0, p1, p2, stepT);
    pos.x += side * (1.0 - (t * BladeTaper)) * BladeThickness;

    // 6. Final Snapping & Transformation
    
    output.Position = mul(float4(pos, 0, 1), WorldViewProjection);
    float z = 1.0 - (p0.y / 10000.0);
    output.Position.z = z;
    output.Position.w = 1.0;
    output.DepthValue = z;

    // 8. Color Gradient (Goal C)
    // Darker at root, full color at tip
    float4 grassColor = input.Color;
    if (input.Variation > 0.8) grassColor.rgb += float3(0.05, 0.1, -0.05);

    float playerShadow = saturate(dist / (PlayerPushRadius * 0.8));
    float4 finalGrassColor = lerp(grassColor * 0.1, grassColor, playerShadow);

    // Apply vertical gradient as before
    output.Color = lerp(finalGrassColor * 0.3, finalGrassColor, t);

    return output;
}

PixelShaderOutput MainPS(PixelInput input) {
    PixelShaderOutput output;
    float4 color = input.Color;
    clip(color.a - 0.5);

    output.Color = color;
    output.Normal = float4(0.5, 0.4, 1.0, 0.001);

    // NEW: Aligning perfectly with VolumeDepth format!
    // Red Channel = Altitude (0.0 for flat ground objects)
    // Blue Channel = DrawDepth (Y-Sort)
    output.Depth = float4(0.0, 0.0, input.DepthValue, 1.0);
    return output;
}

technique GrassDraw{
    pass P0 {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
};