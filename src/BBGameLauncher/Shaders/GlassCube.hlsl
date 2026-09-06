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

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 incident = normalize(input.WorldPosition - Camera.xyz);
    float3 normal = normalize(input.WorldNormal);

    if (Material.w > 0.5)
    {
        float coreFacing = saturate(dot(normal, -incident));
        float coreLight = 0.42 + 0.58 * pow(coreFacing, 0.55);
        float coreAlpha = Material.y * (0.68 + coreFacing * 0.22);
        // AlphaBlend expects straight (not premultiplied) source colour.  The
        // old premultiplied return was multiplied by alpha a second time in
        // the blend unit, which made the emissive centre almost disappear.
        return float4(float3(0.02, 0.38, 1.15) * coreLight, coreAlpha);
    }

    // Back faces are reached by the volume ray below. Blending them again as
    // independent glass panes is what made the old result read as hollow.
    if (dot(normal, -incident) <= 0) discard;

    float2 screenUv = input.Position.xy * Viewport.zw;
    float3 reflectedDirection = reflect(incident, normal);
    float3 reflection = EnvironmentMap.Sample(EnvironmentSampler, reflectedDirection).rgb;
    float facing = saturate(dot(-incident, normal));
    float fresnel = 0.035 + 0.965 * pow(1.0 - facing, 5.0);
    // Screen-space refraction samples the same D3D scene that is visible behind
    // the cube. This stable surface pass is the baseline for the later volume
    // pass; it never depends on an invalid intermediate back-face texture.
    float2 refractionOffset = normal.xy * (0.010 + (1.0 - facing) * 0.018);
    float3 transmission = SceneBackdrop.Sample(SceneSampler, saturate(screenUv + refractionOffset)).rgb;
    float3 keyLight = float3(0.16, 0.34, 0.58) * (0.38 + 0.62 * saturate(dot(normal, normalize(float3(-0.38, 0.58, -0.72)))));
    float3 glass = lerp(transmission * float3(0.86, 0.94, 1.0), reflection * 1.35, fresnel);
    glass += keyLight * (0.48 + fresnel * 0.75);
    glass += fresnel * float3(0.34, 0.56, 0.82);

    // Selected cubes get a restrained blue transmission boost until the
    // internal emissive-volume draw is added to the rebuilt scene pipeline.
    glass += Material.x * (1.0 - fresnel) * float3(0.008, 0.10, 0.25);

    float opacity = Material.y * lerp(0.43, 0.66, fresnel);
    // Keep the glass colour straight for BlendDescription.AlphaBlend.  This
    // preserves the bright cyan rim instead of attenuating it twice.
    return float4(glass, opacity);
}
