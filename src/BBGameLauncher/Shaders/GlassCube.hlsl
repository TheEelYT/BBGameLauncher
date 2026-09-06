cbuffer Frame : register(b0)
{
    row_major float4x4 ViewProjection;
    float4 Camera;
    float4 Viewport;
};

cbuffer Object : register(b1)
{
    row_major float4x4 World;
    row_major float4x4 InverseWorld;
    float4 Material; // selected, opacity, cube size, unused
};

TextureCube EnvironmentMap : register(t0);
SamplerState EnvironmentSampler : register(s0);
Texture2D SceneBackdrop : register(t1);
SamplerState SceneSampler : register(s1);
Texture2D ExitPosition : register(t2);
SamplerState ExitSampler : register(s2);

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float3 LocalPosition : TEXCOORD2;
    float3 LocalNormal : TEXCOORD3;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float4 worldPosition = mul(float4(input.Position, 1), World);
    output.Position = mul(worldPosition, ViewProjection);
    output.WorldPosition = worldPosition.xyz;
    output.WorldNormal = normalize(mul(float4(input.Normal, 0), World).xyz);
    output.LocalPosition = input.Position;
    output.LocalNormal = input.Normal;
    return output;
}

float3 BoxNormal(float3 cubePosition)
{
    float3 a = abs(cubePosition);
    if (a.x > a.y && a.x > a.z) return float3(sign(cubePosition.x), 0, 0);
    if (a.y > a.z) return float3(0, sign(cubePosition.y), 0);
    return float3(0, 0, sign(cubePosition.z));
}

float4 PSBack(PSInput input) : SV_TARGET
{
    float3 incident = normalize(input.WorldPosition - Camera.xyz);
    // Keep only physical rear faces. Depth testing then leaves the visible
    // exit surface at every pixel for the front-face volume pass.
    if (dot(normalize(input.WorldNormal), -incident) > 0) discard;
    return float4(input.LocalPosition, 1);
}

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 incident = normalize(input.WorldPosition - Camera.xyz);
    float3 normal = normalize(input.WorldNormal);

    // Back faces are reached by the volume ray below. Blending them again as
    // independent glass panes is what made the old result read as hollow.
    if (dot(normal, -incident) <= 0) discard;

    float3 entry = input.LocalPosition;
    float3 entryNormal = normalize(input.LocalNormal);
    float2 screenUv = input.Position.xy * Viewport.zw;
    float4 exitSample = ExitPosition.Sample(ExitSampler, screenUv);
    // Some D3D9/WPF interop drivers do not preserve the alpha channel of the
    // intermediate half-float target. Fall back to the opposite cube surface
    // instead of discarding the entire object while that target is unavailable.
    float3 exitPoint = exitSample.a < 0.5 ? -entry : exitSample.xyz;
    float3 insideRay = normalize(exitPoint - entry);
    float travel = length(exitPoint - entry);
    float3 exitNormal = BoxNormal(exitPoint);
    float3 exitRay = refract(insideRay, exitNormal, 1.33);
    if (dot(exitRay, exitRay) < 0.001) exitRay = reflect(insideRay, exitNormal);

    float3 reflectedDirection = reflect(incident, normal);
    float3 transmissionDirection = normalize(mul(float4(exitRay, 0), World).xyz);
    float3 internalBounce = reflect(reflect(insideRay, exitNormal), entryNormal);
    internalBounce = normalize(mul(float4(internalBounce, 0), World).xyz);
    float3 reflection = EnvironmentMap.Sample(EnvironmentSampler, reflectedDirection).rgb;
    float3 cubemapTransmission = EnvironmentMap.Sample(EnvironmentSampler, transmissionDirection).rgb;
    float4 exitWorld = mul(float4(exitPoint, 1), World);
    float4 exitClip = mul(exitWorld, ViewProjection);
    float2 refractionUv = exitClip.xy / exitClip.w * float2(0.5, -0.5) + 0.5;
    float3 transmission = SceneBackdrop.Sample(SceneSampler, saturate(refractionUv)).rgb;
    float3 trappedReflection = EnvironmentMap.Sample(EnvironmentSampler, internalBounce).rgb;
    float facing = saturate(dot(-incident, normal));
    float fresnel = 0.035 + 0.965 * pow(1.0 - facing, 5.0);
    float3 absorption = exp(-travel * float3(0.14, 0.055, 0.015));
    float3 glass = lerp(transmission, cubemapTransmission, 0.16) * absorption;
    glass = lerp(glass, reflection, fresnel);
    glass += trappedReflection * (0.12 + fresnel * 0.18) * absorption;

    // Find the blue emitter's distance from the internal optical path. It is a
    // real center-volume contribution, not a radial texture on each cube face.
    float3 path = exitPoint - entry;
    float pathLengthSq = max(dot(path, path), 0.0001);
    float pathT = saturate(dot(-entry, path) / pathLengthSq);
    float centerDistance = length(entry + path * pathT);
    float blueCore = Material.x * exp(-centerDistance * centerDistance * 13.0) * exp(-travel * 0.24);
    glass += blueCore * float3(0.025, 0.44, 1.25);
    glass += fresnel * float3(0.16, 0.27, 0.40);

    float opacity = Material.y * lerp(0.54, 0.68, fresnel);
    return float4(glass * opacity, opacity);
}
