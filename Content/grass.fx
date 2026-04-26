float4x4 WorldViewProjection;
float Time;
float2 PlayerPos;
float WindSpeed, WindIntensity, Stiffness;
float PlayerPushStrength, PlayerPushRadius;
float Segments, RestingCurvature, BladeThickness, BladeTaper;

float2 ScreenResolution;
Texture2D ObjectDepthTexture;
sampler2D DepthSampler = sampler_state{ Texture = <ObjectDepthTexture>; AddressU = Clamp; AddressV = Clamp; };

struct VertexInput {
    float4 RootPos : POSITION0;
    float2 T_Side : TEXCOORD0;
    float2 Wind_Height : TEXCOORD1;
    float Variation : TEXCOORD2;
    float4 Color : COLOR0;
};

struct PixelInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float DepthValue : TEXCOORD1;
    float4 ScreenPos : TEXCOORD2;
    float2 RootUV : TEXCOORD3; // --- NEW: Track where the root is on screen ---
};

struct PixelShaderOutput {
    float4 Color : COLOR0;
    float4 Normal : COLOR1;
};

float2 GetBezier(float2 p0, float2 p1, float2 p2, float t) {
    float invT = 1.0 - t;
    return invT * invT * p0 + 2.0 * invT * t * p1 + t * t * p2;
}

PixelInput MainVS(VertexInput input) {
    PixelInput output;

    float t = input.T_Side.x;
    float side = input.T_Side.y;
    float2 p0 = input.RootPos.xy;
    float lean = input.RootPos.z;
    float windOffset = input.Wind_Height.x;
    float heightScale = input.Wind_Height.y;

    float height = 10.0 * heightScale;
    float sway = sin(Time * WindSpeed + windOffset) * (WindIntensity * height);

    float dist = distance(p0, PlayerPos);
    float2 push = float2(0, 0);

    if (dist < PlayerPushRadius) {
        float force = (PlayerPushRadius - dist) / PlayerPushRadius;
        push = normalize(p0 - PlayerPos) * (force * 18.0 * PlayerPushStrength);
    }

    float stepT = floor(t * Segments) / Segments;
    float droop = RestingCurvature * (height / 10.0);
    float2 p2 = p0 + float2(lean + sway + push.x + droop, -height + abs(push.y * 0.5));
    float2 p1 = lerp(p0, p2, Stiffness) + float2(droop * 0.2, 2.0);

    float2 pos = GetBezier(p0, p1, p2, stepT);
    pos.x += side * (1.0 - (t * BladeTaper)) * BladeThickness;

    output.Position = mul(float4(pos, 0, 1), WorldViewProjection);
    output.ScreenPos = output.Position;

    // --- NEW: Calculate the Screen UV specifically for the ROOT of the grass! ---
    float4 rootScreenPos = mul(float4(p0, 0, 1), WorldViewProjection);
    output.RootUV = (rootScreenPos.xy / rootScreenPos.w) * 0.5 + 0.5;
    output.RootUV.y = 1.0 - output.RootUV.y;

    // Depth matching DepthUtil.MAX_WORLD_HEIGHT (32768.0)
    output.DepthValue = p0.y / 32768.0;

    float4 grassColor = input.Color;
    if (input.Variation > 0.8) grassColor.rgb += float3(0.05, 0.1, -0.05);

    float playerShadow = saturate(dist / (PlayerPushRadius * 0.8));
    float4 finalGrassColor = lerp(grassColor * 0.1, grassColor, playerShadow);

    output.Color = lerp(finalGrassColor * 0.3, finalGrassColor, t);

    return output;
}

PixelShaderOutput MainPS(PixelInput input) {
    PixelShaderOutput output;
    clip(input.Color.a - 0.5);

    // Calculate Screen Space UV for reading Depth
    float2 screenUV = (input.ScreenPos.xy / input.ScreenPos.w) * 0.5 + 0.5;
    screenUV.y = 1.0 - screenUV.y;

    // --- 1. REJECT GRASS ON TOP OF OBJECTS ---
    // Check the Altitude of the pixel where the grass ROOT is touching the ground
    float rootAltitude = tex2D(DepthSampler, input.RootUV).r;

    // If the root is sitting higher than 0.03 altitude, it is spawning inside a tree trunk/rock!
    if (rootAltitude > 0.03) discard;

    // --- 2. REJECT GRASS BEHIND OBJECTS ---
    // Read the Object's physical Y-Depth from the Green channel
    float objDepth = tex2D(DepthSampler, screenUV).g;

    // If an object is blocking this pixel, and the grass is further back (smaller depth), discard!
    // Adding a tiny bias (0.0005) prevents floating-point Z-fighting on exact edges.
    if (objDepth > 0.001 && input.DepthValue < objDepth - 0.0005) discard;

    output.Color = input.Color;
    output.Normal = float4(0.5, 0.4, 1.0, 0.001);

    return output;
}

technique GrassDraw{ pass P0 { VertexShader = compile vs_3_0 MainVS(); PixelShader = compile ps_3_0 MainPS(); } }