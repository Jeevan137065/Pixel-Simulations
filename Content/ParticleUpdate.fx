Texture2D StateTexture;
sampler StateSampler = sampler_state{ Texture = <StateTexture>; Filter = Point; AddressU = Clamp; AddressV = Clamp; };

float DeltaTime;
float Time;
float2 WindDirection;
float WindSpeed;
float FallSpeed;
float SplashDuration; // NEW: How long the particle lives after hitting the ground

float2 CameraPosition;
float2 ViewportSize;

float rand(float2 s) { return frac(sin(dot(s, float2(12.9898, 78.233))) * 43758.5453); }

struct VS_IN { float4 P : POSITION0; float2 TC : TEXCOORD0; };
struct PS_IN { float4 P : SV_POSITION; float2 TC : TEXCOORD0; };
PS_IN MainVS(VS_IN i) { PS_IN o; o.P = i.P; o.TC = i.TC; return o; }

float4 MainPS(PS_IN input) : SV_TARGET
{
    float4 data = tex2D(StateSampler, input.TC);
    float groundX = data.x;
    float groundY = data.y;
    float heightZ = data.z;
    float state = data.w;

    float2 staticSeed = input.TC;
    float2 dynamicSeed = input.TC + Time;

    // --- PHYSICS ---
    if (state <= 0.5) // FALLING
    {
        float2 windVel = normalize(WindDirection) * WindSpeed;
        groundX += windVel.x * DeltaTime;
        groundY += windVel.y * DeltaTime;

        float speed = FallSpeed * lerp(0.8, 1.2, rand(staticSeed));
        heightZ -= speed * DeltaTime;

        if (heightZ <= 0.0)
        {
            heightZ = 0.0;
            state = 1.0; // Change to Splash
        }
    }
    else // SPLASHING / ON GROUND
    {
        // heightZ counts down from 0.0 to -1.0 based on the SplashDuration
        heightZ -= (DeltaTime / SplashDuration);
    }

    // --- CULLING & RESPAWNING ---
    float buffer = 200.0;
    float left = CameraPosition.x - buffer;
    float right = CameraPosition.x + ViewportSize.x + buffer;
    float top = CameraPosition.y - buffer;
    float bottom = CameraPosition.y + ViewportSize.y + buffer;

    // Respawn if splash timer is done (heightZ <= -1) OR if blown off screen
    if ((state > 0.5 && heightZ <= -1.0) || groundX < left || groundX > right || groundY < top || groundY > bottom)
    {
        state = 0.0;
        float seedX = rand(dynamicSeed * 99.1);
        float seedY = rand(dynamicSeed * 55.4);
        float seedZ = rand(dynamicSeed * 12.7);

        groundX = left + (seedX * (right - left));
        groundY = top + (seedY * (bottom - top));
        heightZ = 300.0 + (seedZ * 300.0);
    }

    return float4(groundX, groundY, heightZ, state);
}
technique Basic{ pass P0 { VertexShader = compile vs_3_0 MainVS(); PixelShader = compile ps_3_0 MainPS(); } }