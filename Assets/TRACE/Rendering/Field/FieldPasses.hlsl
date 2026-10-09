// Forward, shadow, depth and depth-normal passes shared by the Field surface shaders. FIELD_SAMPLE(p, n) selects the
// mapping (box or triplanar) before inclusion.
#ifndef TRACE_FIELD_PASSES_INCLUDED
#define TRACE_FIELD_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct FieldAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FieldVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    half fogFactor : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

FieldVaryings FieldVertex(FieldAttributes input)
{
    FieldVaryings output = (FieldVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    return output;
}

half4 FieldFragment(FieldVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 geometric = normalize(input.normalWS);
    FieldSample s = FIELD_SAMPLE(input.positionWS, geometric);
    SurfaceData surface; float3 normalWS;
    FieldCompose(input.positionWS, geometric, s, surface, normalWS);

    InputData data = (InputData)0;
    data.positionWS = input.positionWS;
    data.positionCS = input.positionCS;
    data.normalWS = normalWS;
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    data.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    data.vertexLighting = half3(0.0h, 0.0h, 0.0h);
    data.bakedGI = SampleSH(normalWS);
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    data.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

    half4 color = UniversalFragmentPBR(data, surface);
    color.rgb = MixFog(color.rgb, data.fogCoord);
    color.a = 1.0h;
    return color;
}

float3 _LightDirection;
float3 _LightPosition;

float4 FieldShadowPosition(FieldAttributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirection = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirection = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return positionCS;
}

FieldVaryings FieldShadowVertex(FieldAttributes input)
{
    FieldVaryings output = (FieldVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    output.positionCS = FieldShadowPosition(input);
    return output;
}

half4 FieldDepthFragment(FieldVaryings input) : SV_Target
{
    return 0;
}

half4 FieldDepthNormalsFragment(FieldVaryings input) : SV_Target
{
    float3 normalWS = normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
        float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
        return half4(PackFloat2To888(remapped), 0.0);
    #else
        return half4(NormalizeNormalPerPixel(normalWS), 0.0);
    #endif
}

#endif
