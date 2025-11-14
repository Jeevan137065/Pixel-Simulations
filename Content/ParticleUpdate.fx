Texture2D StateTexture;
sampler StateSampler = sampler_state{ Texture = <StateTexture>; };

float DeltaTime;
float Time;
float2 WindDirection;
float WindSpeed;

// NEW: The size of our wrapping simulation box in world pixels
float2 SimulationBounds;

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
        if (lifetime <= 0.0) { type = 0.0; pos = float2(-9999, -9999); lifetime = -1; } // Effectively kill it
    }

    // If particle is "dead", or has gone outside the wrap bounds, reset it.
    if (lifetime < 0.0 || pos.x < 0 || pos.x > SimulationBounds.x || pos.y < 0 || pos.y > SimulationBounds.y)
    {
        type = 0.0;
        float seedX = rand(seed * 99.1);
        float seedY = rand(seed * 55.4);
        float seedLife = rand(seed * 12.7);
        float seedSpeed = rand(seed * 33.3);

        pos = float2(seedX * SimulationBounds.x, seedY * SimulationBounds.y);
        float travelDistance = SimulationBounds.y * (0.5 + seedLife * 1.0);
        float speed = WindSpeed * lerp(0.4, 1.2, seedSpeed);
        lifetime = (speed > 0.1) ? travelDistance / speed : 99.0;
    }

    return float4(pos, lifetime, type);
}

technique Basic{ pass P0 { VertexShader = compile vs_3_0 MainVS(); PixelShader = compile ps_3_0 MainPS(); } }