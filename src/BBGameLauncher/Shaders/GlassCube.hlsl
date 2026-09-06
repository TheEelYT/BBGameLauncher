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
Texture2D SceneColor : register(t3);
SamplerState SceneColorSampler : register(s3);

struct FullscreenOutput
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

FullscreenOutput VSFullscreen(uint vertexId : SV_VertexID)
{
    FullscreenOutput output;
    float2 position = vertexId == 0 ? float2(-1, -1) : (vertexId == 1 ? float2(-1, 3) : float2(3, -1));
    output.Position = float4(position, 0, 1);
    output.Uv = position * float2(0.5, -0.5) + 0.5;
    return output;
}

float Nebula(float2 uv, float2 center, float radius)
{
    float2 d = uv - center;
    d.x *= 1.9;
    return exp(-dot(d, d) / (radius * radius));
}

float4 PSBackground(FullscreenOutput input) : SV_TARGET
{
    float vertical = 1.0 - input.Uv.y;
    float3 color = lerp(float3(0.002, 0.006, 0.018), float3(0.008, 0.018, 0.055), vertical);
    color += Nebula(input.Uv, float2(0.20, 0.47), 0.30) * float3(0.06, 0.055, 0.21);
    color += Nebula(input.Uv, float2(0.79, 0.23), 0.22) * float3(0.012, 0.055, 0.12);
    color += Nebula(input.Uv, float2(0.67, 0.73), 0.18) * float3(0.006, 0.035, 0.09);
    return float4(color, 1);
}

float4 PSComposite(FullscreenOutput input) : SV_TARGET
{
    return SceneColor.Sample(SceneColorSampler, input.Uv);
}

struct StarInput
{
    float3 Position : POSITION;
    float3 Color : COLOR;
    float2 Corner : TEXCOORD0;
    float Size : TEXCOORD1;
};

struct StarOutput
{
    float4 Position : SV_POSITION;
    float3 Color : COLOR;
    float2 Corner : TEXCOORD0;
};

StarOutput VSStar(StarInput input)
{
    StarOutput output;
    float3 worldPosition = input.Position + float3(input.Corner * input.Size, 0);
    output.Position = mul(float4(worldPosition, 1), ViewProjection);
    output.Color = input.Color;
    output.Corner = input.Corner;
    return output;
}

float4 PSStar(StarOutput input) : SV_TARGET
{
    float r = length(input.Corner);
    float intensity = exp(-r * r * 3.2);
    return float4(input.Color * intensity, intensity * 0.82);
}

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

float BoxExitDistance(float3 origin, float3 direction)
{
    float exitDistance = 100000.0;
    if (abs(direction.x) > 0.00001)
    {
        float axisDistance = ((direction.x > 0.0 ? 1.0 : -1.0) - origin.x) / direction.x;
        if (axisDistance > 0.0001) exitDistance = min(exitDistance, axisDistance);
    }
    if (abs(direction.y) > 0.00001)
    {
        float axisDistance = ((direction.y > 0.0 ? 1.0 : -1.0) - origin.y) / direction.y;
        if (axisDistance > 0.0001) exitDistance = min(exitDistance, axisDistance);
    }
    if (abs(direction.z) > 0.00001)
    {
        float axisDistance = ((direction.z > 0.0 ? 1.0 : -1.0) - origin.z) / direction.z;
        if (axisDistance > 0.0001) exitDistance = min(exitDistance, axisDistance);
    }
    return exitDistance;
}

float2 ProjectToUv(float3 worldPosition)
{
    float4 clip = mul(float4(worldPosition, 1), ViewProjection);
    return clip.xy / clip.w * float2(0.5, -0.5) + 0.5;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    const float glassIor = 1.50;
    float3 incident = normalize(input.WorldPosition - Camera.xyz);
    float3 entryNormal = normalize(input.WorldNormal);

    // Shade only the first surface. The rest of the cube is traversed below as
    // one glass volume, so its rear faces cannot appear as detached panes.
    if (dot(entryNormal, -incident) <= 0) discard;

    float3 glassDirection = refract(incident, entryNormal, 1.0 / glassIor);
    float3 localDirection = normalize(mul(float4(glassDirection, 0), InverseWorld).xyz);
    float3 localEntry = input.LocalPosition - input.LocalNormal * 0.001;
    float exitDistance = BoxExitDistance(localEntry, localDirection);
    float3 localExit = localEntry + localDirection * exitDistance;
    float3 localExitNormal = BoxNormal(localExit);
    float3 worldExit = mul(float4(localExit, 1), World).xyz;
    float3 exitNormal = normalize(mul(float4(localExitNormal, 0), World).xyz);

    // Bend the ray back into air at the opposite face. Total internal
    // reflection falls back to a reflected internal ray instead of vanishing.
    float3 outgoing = refract(glassDirection, -exitNormal, glassIor);
    float transmittedRay = step(0.00001, dot(outgoing, outgoing));
    outgoing = normalize(lerp(reflect(glassDirection, exitNormal), outgoing, transmittedRay));

    // Project the actual exit ray into the already-rendered 3D starfield.
    // This keeps stars localized instead of stretching one texture over a face.
    float3 samplePoint = worldExit + outgoing * Material.z * 1.35;
    float2 refractedUv = saturate(ProjectToUv(samplePoint));
    float2 dispersion = (refractedUv - ProjectToUv(worldExit)) * 0.035;
    float3 transmission;
    transmission.r = SceneBackdrop.Sample(SceneSampler, saturate(refractedUv + dispersion)).r;
    transmission.g = SceneBackdrop.Sample(SceneSampler, refractedUv).g;
    transmission.b = SceneBackdrop.Sample(SceneSampler, saturate(refractedUv - dispersion)).b;

    float3 reflectedDirection = reflect(incident, entryNormal);
    float3 reflection = EnvironmentMap.Sample(EnvironmentSampler, reflectedDirection).rgb;
    float3 internalReflection = EnvironmentMap.Sample(EnvironmentSampler, reflect(glassDirection, exitNormal)).rgb;
    float facing = saturate(dot(-incident, entryNormal));
    float fresnel = 0.035 + 0.965 * pow(1.0 - facing, 5.0);
    float exitFacing = saturate(dot(glassDirection, exitNormal));
    float exitFresnel = 0.035 + 0.965 * pow(1.0 - exitFacing, 5.0);
    float thickness = saturate(exitDistance / 3.464);
    float3 absorption = exp(-float3(0.13, 0.055, 0.018) * exitDistance);

    float3 glass = transmission * absorption;
    glass = lerp(glass, reflection * 1.15, fresnel);
    glass += internalReflection * (0.08 + exitFresnel * 0.24);
    glass += fresnel * float3(0.20, 0.48, 0.82);

    // The selected light is evaluated along the ray segment inside the cube.
    // It therefore occupies the geometric centre without a separate sphere or
    // a face-aligned glow texture.
    float closestDistance = clamp(dot(-localEntry, localDirection), 0.0, exitDistance);
    float3 closestPoint = localEntry + localDirection * closestDistance;
    float coreGlow = Material.x * exp(-dot(closestPoint, closestPoint) * 7.5) * saturate(exitDistance * 0.55);
    glass += coreGlow * float3(0.015, 0.34, 1.25);

    float opacity = Material.y * (0.24 + fresnel * 0.36 + thickness * 0.10);
    return float4(glass, opacity);
}
