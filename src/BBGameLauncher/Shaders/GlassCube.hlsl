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

float3 SampleSceneRay(float3 surfacePosition, float3 outgoingDirection, float cubeSize)
{
    // The captured scene represents geometry spread across the star field, not
    // a decal sitting immediately behind the cube. Trace the outgoing ray to a
    // distant scene plane before projecting it back into the captured image.
    // This keeps a star localized while still allowing a rotated block to bend
    // it, instead of stretching the same near-surface texels over an entire face.
    float2 surfaceUv = ProjectToUv(surfacePosition);
    float forwardRay = step(0.02, outgoingDirection.z);
    float3 sceneDirection = normalize(float3(outgoingDirection.xy, max(outgoingDirection.z, 0.02)));
    const float scenePlaneZ = 5200.0;
    float rayDistance = (scenePlaneZ - surfacePosition.z) / sceneDirection.z;
    rayDistance = clamp(rayDistance, cubeSize * 2.0, 7200.0);
    float2 rayUv = ProjectToUv(surfacePosition + sceneDirection * rayDistance);

    // Rays leaving the viewport see the environment rather than a clamped row
    // of pixels. Clamping was the source of the broad, smeared star streaks.
    float inside = step(0.0, rayUv.x) * step(rayUv.x, 1.0) *
                   step(0.0, rayUv.y) * step(rayUv.y, 1.0);
    float2 clampedUv = saturate(rayUv);

    // A small channel separation approximates crown-glass dispersion without
    // overwhelming the neutral transmission of an unselected cube.
    float2 bend = rayUv - surfaceUv;
    float2 dispersion = bend * 0.012;
    float3 scene;
    scene.r = SceneBackdrop.Sample(SceneSampler, saturate(clampedUv + dispersion)).r;
    scene.g = SceneBackdrop.Sample(SceneSampler, clampedUv).g;
    scene.b = SceneBackdrop.Sample(SceneSampler, saturate(clampedUv - dispersion)).b;
    float3 environment = EnvironmentMap.Sample(EnvironmentSampler, outgoingDirection).rgb;
    return lerp(environment, scene, inside * forwardRay);
}

float CenterEmission(float3 localCamera, float3 localSurface)
{
    // Evaluate a true object-space volume centred at the origin. Using the
    // already-refracted entry ray here made the light slide toward whichever
    // rear face that ray happened to hit, so it looked painted onto a surface.
    float3 viewDirection = normalize(localSurface - localCamera);
    float closestDistance = max(dot(-localCamera, viewDirection), 0.0);
    float3 closestPoint = localCamera + viewDirection * closestDistance;
    float distanceSquared = dot(closestPoint, closestPoint);

    float softVolume = exp(-distanceSquared * 22.0);
    float hotCore = exp(-distanceSquared * 86.0);
    return softVolume * 0.72 + hotCore * 0.58;
}

float SurfaceEdgeFactor(float3 localPosition)
{
    float3 coordinates = abs(localPosition);
    float largest = max(coordinates.x, max(coordinates.y, coordinates.z));
    float smallest = min(coordinates.x, min(coordinates.y, coordinates.z));
    float middle = coordinates.x + coordinates.y + coordinates.z - largest - smallest;
    return smoothstep(0.62, 0.96, middle);
}

float FresnelSchlick(float cosine)
{
    // Air-to-glass reflectance at normal incidence for IOR 1.50 is ~4%.
    return 0.04 + 0.96 * pow(1.0 - saturate(cosine), 5.0);
}

float4 PSMain(PSInput input) : SV_TARGET
{
    const float glassIor = 1.50;
    float3 incident = normalize(input.WorldPosition - Camera.xyz);
    float3 entryNormal = normalize(input.WorldNormal);
    float3 surfaceReflection = EnvironmentMap.Sample(EnvironmentSampler, reflect(incident, entryNormal)).rgb;

    float3 glassDirection = refract(incident, entryNormal, 1.0 / glassIor);
    float3 localDirection = normalize(mul(float4(glassDirection, 0), InverseWorld).xyz);
    float3 localEntry = input.LocalPosition - input.LocalNormal * 0.001;
    float exitDistance = BoxExitDistance(localEntry, localDirection);
    float3 localExit = localEntry + localDirection * exitDistance;
    float3 localExitNormal = BoxNormal(localExit);
    float3 worldExit = mul(float4(localExit, 1), World).xyz;
    float3 exitNormal = normalize(mul(float4(localExitNormal, 0), World).xyz);

    // First attempt to leave the block through the rear face.
    float3 primaryOutgoing = refract(glassDirection, -exitNormal, glassIor);
    float primaryTransmission = step(0.00001, dot(primaryOutgoing, primaryOutgoing));
    float3 safePrimaryOutgoing = normalize(primaryOutgoing + exitNormal * (1.0 - primaryTransmission));
    float3 primaryScene = SampleSceneRay(worldExit, safePrimaryOutgoing, Material.z);

    // Trace one physically meaningful internal bounce. This makes the rear and
    // side surfaces contribute through the front instead of drawing them as
    // separately blended panes.
    float3 bounceLocalDirection = normalize(reflect(localDirection, localExitNormal));
    float3 bounceLocalOrigin = localExit - localExitNormal * 0.002;
    float bounceDistance = BoxExitDistance(bounceLocalOrigin, bounceLocalDirection);
    float3 bounceLocalExit = bounceLocalOrigin + bounceLocalDirection * bounceDistance;
    float3 bounceLocalNormal = BoxNormal(bounceLocalExit);
    float3 bounceWorldExit = mul(float4(bounceLocalExit, 1), World).xyz;
    float3 bounceWorldNormal = normalize(mul(float4(bounceLocalNormal, 0), World).xyz);
    float3 bounceWorldDirection = normalize(mul(float4(bounceLocalDirection, 0), World).xyz);
    float3 bounceOutgoing = refract(bounceWorldDirection, -bounceWorldNormal, glassIor);
    float bounceTransmission = step(0.00001, dot(bounceOutgoing, bounceOutgoing));
    float3 safeBounceOutgoing = normalize(bounceOutgoing + bounceWorldNormal * (1.0 - bounceTransmission));
    float3 bouncedScene = SampleSceneRay(bounceWorldExit, safeBounceOutgoing, Material.z);
    float3 bounceEnvironment = EnvironmentMap.Sample(EnvironmentSampler,
        reflect(bounceWorldDirection, bounceWorldNormal)).rgb;
    float bounceExitFacing = saturate(dot(bounceWorldDirection, bounceWorldNormal));
    float bounceExitFresnel = FresnelSchlick(bounceExitFacing);
    bouncedScene = lerp(bounceEnvironment, bouncedScene,
        bounceTransmission * (1.0 - bounceExitFresnel));

    float facing = saturate(dot(-incident, entryNormal));
    float fresnel = FresnelSchlick(facing);
    float exitFacing = saturate(dot(glassDirection, exitNormal));
    float exitFresnel = FresnelSchlick(exitFacing);
    float3 absorptionCoefficient = float3(0.030, 0.014, 0.004);
    float3 primaryAbsorption = exp(-absorptionCoefficient * exitDistance);
    float3 bounceAbsorption = exp(-absorptionCoefficient * (exitDistance + bounceDistance));

    float bounceWeight = saturate(exitFresnel + (1.0 - primaryTransmission));
    float3 transmission = lerp(primaryScene * primaryAbsorption,
        bouncedScene * bounceAbsorption, bounceWeight);

    float3 glass = transmission;
    glass = lerp(glass, surfaceReflection * 1.22 + float3(0.025, 0.035, 0.045), fresnel);
    glass += fresnel * float3(0.08, 0.16, 0.28);

    // Broad edge caustics reveal both the entry surface and the refracted rear
    // geometry without turning the object back into a wireframe.
    float entryEdge = SurfaceEdgeFactor(input.LocalPosition);
    float exitEdge = SurfaceEdgeFactor(localExit);
    glass += entryEdge * (0.08 + fresnel * 0.20) * float3(0.34, 0.52, 0.72);
    glass += exitEdge * (0.06 + exitFresnel * 0.18) * float3(0.28, 0.46, 0.68);

    // The selected light is a centred object-space emission volume. The broad
    // component illuminates the glass around it while the compact component
    // reads as the source, even as the cube rotates.
    float3 localCamera = mul(float4(Camera.xyz, 1), InverseWorld).xyz;
    float coreGlow = Material.x * CenterEmission(localCamera, input.LocalPosition);
    glass += coreGlow * float3(0.018, 0.30, 1.18);

    // This pass writes the final scene colour, not an independently blended
    // transparent pane. Material.y fades the complete solid-glass result for
    // menu transitions without exposing separately rendered rear polygons.
    float2 screenUv = input.Position.xy * Viewport.zw;
    float3 background = SceneBackdrop.Sample(SceneSampler, screenUv).rgb;
    return float4(lerp(background, glass, Material.y), 1.0);
}
