cbuffer Frame : register(b0)
{
    row_major float4x4 ViewProjection;
    float4 Camera;
};

cbuffer Object : register(b1)
{
    row_major float4x4 World;
    float4 Material; // selected, opacity, cube size, unused
};

TextureCube EnvironmentMap : register(t0);
SamplerState EnvironmentSampler : register(s0);

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
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float4 worldPosition = mul(float4(input.Position, 1), World);
    output.Position = mul(worldPosition, ViewProjection);
    output.WorldPosition = worldPosition.xyz;
    output.WorldNormal = normalize(mul(float4(input.Normal, 0), World).xyz);
    output.LocalPosition = input.Position;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 normal = normalize(input.WorldNormal);
    float3 incident = normalize(input.WorldPosition - Camera.xyz);
    if (dot(normal, incident) > 0) normal = -normal;

    float3 reflectedDirection = reflect(incident, normal);
    float3 refractedDirection = refract(incident, normal, 1.0 / 1.33);
    float3 reflection = EnvironmentMap.Sample(EnvironmentSampler, reflectedDirection).rgb;
    float3 transmission = EnvironmentMap.Sample(EnvironmentSampler, refractedDirection).rgb;
    float facing = saturate(dot(-incident, normal));
    float fresnel = 0.035 + 0.965 * pow(1.0 - facing, 5.0);
    float3 glass = lerp(transmission * float3(0.76, 0.90, 1.0), reflection, fresnel);

    // The selected cube gets a transmitted blue source from within the volume;
    // idle cubes remain neutral and clear rather than receiving this emission.
    float axialCore = pow(saturate(1.0 - length(input.LocalPosition.xy) * 0.72), 3.0);
    float blueCore = Material.x * axialCore * (0.36 + 0.42 * facing);
    glass += blueCore * float3(0.04, 0.48, 1.0);
    glass += fresnel * float3(0.32, 0.48, 0.64);

    float opacity = Material.y * lerp(0.32, 0.58, fresnel);
    return float4(glass * opacity, opacity);
}
