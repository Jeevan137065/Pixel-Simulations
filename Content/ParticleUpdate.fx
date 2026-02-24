Texture2D StateTexture;
sampler StateSampler = sampler_state{ Texture = <StateTexture>; Filter = Point; AddressU = Clamp; AddressV = Clamp; };

float DeltaTime;
float Time;
float2 WindDirection;
float WindSpeed;

// --- NEW: Camera Awareness ---
float2 CameraPosition;
float2 ViewportSize;

float rand(float2 s) { return frac(sin(dot(s, float2(12.9898, 78.233))) * 43758.5453); }
struct VS_IN { float4 P : POSITION0; float2 TC : TEXCOORD0; };
struct PS_IN { float4 P : SV_POSITION; float2 TC : TEXCOORD0; };
PS_IN MainVS(VS_IN i) { PS_IN o; o.P = i.P; o.TC = i.TC; return o; }

float4 MainPS(PS_IN input) : SV_TARGET
{
    float4 currentState = tex2D(StateSampler, input.TC);
    float2 pos = currentState.xy;
    float lifetime = currentState.z;
    float type = currentState.w;
    float2 seed = input.TC + Time;

    lifetime -= DeltaTime;

    if (type == 0.0) // Rain
    {
        float speed = WindSpeed * lerp(0.4, 1.2, rand(seed));
        float2 vel = normalize(WindDirection) * speed;
        pos += vel * DeltaTime;
        if (lifetime <= 0.0) { type = 0.5; lifetime = 0.3 + rand(seed.yx) * 0.2; }
    }
    else // Splash
    {
        if (lifetime <= 0.0) { type = 0.0; pos = float2(-9999, -9999); lifetime = -1.0; }
    }

    // --- NEW: Camera-Relative Culling & Spawning ---
    // Define a bounding box slightly larger than the screen
    float buffer = 400.0;
    float left = CameraPosition.x - buffer;
    float right = CameraPosition.x + ViewportSize.x + buffer;
    float top = CameraPosition.y - buffer;
    float bottom = CameraPosition.y + ViewportSize.y + buffer;

    // If particle is dead, OR it blew completely off the screen bounds
    if (lifetime < 0.0 || pos.x < left || pos.x > right || pos.y > bottom)
    {
        type = 0.0;

        float seedX = rand(seed * 99.1);
        float seedY = rand(seed * 55.4);
        float seedLife = rand(seed * 12.7);
        float seedSpeed = rand(seed * 33.3);

        // Spawn randomly within the camera bounds, but biased towards the top
        pos.x = left + (seedX * (right - left));
        pos.y = top + (seedY * 200.0); // Spawn near the top edge

        float travelDistance = ViewportSize.y * (0.8 + seedLife * 1.0);
        float speed = WindSpeed * lerp(0.4, 1.2, seedSpeed);

        // Ensure minimum speed so particles don't hover forever
        speed = max(speed, 50.0);
        lifetime = travelDistance / speed;
    }

    return float4(pos, lifetime, type);
}

technique Basic{ pass P0 { VertexShader = compile vs_3_0 MainVS(); PixelShader = compile ps_3_0 MainPS(); } }