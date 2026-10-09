// TRACE/FieldWater — still technical water and puddles: two scrolling normal layers, transparency and a light depth
// tint read from the camera depth, Fresnel environment reflection (reflection probes), a restrained sun highlight.
// No waves, caustics or refraction. A shape mask (UV) gives puddles their outline.
Shader "TRACE/FieldWater"
{
    Properties
    {
        [Normal] _NormalMap ("Normal", 2D) = "bump" {}
        _NormalScale ("Normal Tiling (per metre)", Float) = 0.35
        _NormalStrength ("Normal Strength", Range(0, 1)) = 0.35
        _FlowSpeed ("Flow Speed", Range(0, 2)) = 0.3
        _ShallowColor ("Shallow Colour", Color) = (0.16, 0.2, 0.2, 1)
        _DeepColor ("Deep Colour", Color) = (0.03, 0.06, 0.07, 1)
        _DepthMax ("Tint Depth (m)", Float) = 1.2
        _AlphaMin ("Alpha at the Edge", Range(0, 1)) = 0.35
        _AlphaDepth ("Opaque Depth (m)", Float) = 0.8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.7
        _SpecularStrength ("Sun Highlight", Range(0, 2)) = 0.6
        [NoScaleOffset] _ShapeMap ("Shape Mask (R, UV)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #define _SURFACE_TYPE_TRANSPARENT 1
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_ShapeMap); SAMPLER(sampler_ShapeMap);
            CBUFFER_START(UnityPerMaterial)
                float _NormalScale;
                half _NormalStrength;
                half _FlowSpeed;
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMax;
                half _AlphaMin;
                float _AlphaDepth;
                half _Smoothness;
                half _ReflectionStrength;
                half _SpecularStrength;
            CBUFFER_END

            struct WaterAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct WaterVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            WaterVaryings WaterVertex(WaterAttributes input)
            {
                WaterVaryings output = (WaterVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 WaterFragment(WaterVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 flow = _Time.y * _FlowSpeed * float2(0.03, 0.02);
                float2 uvA = input.positionWS.xz * _NormalScale + flow;
                float2 uvB = input.positionWS.xz * _NormalScale * 0.71 - flow.yx * 1.3;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), _NormalStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), _NormalStrength);
                half3 t = normalize(half3(a.xy + b.xy, a.z * b.z));
                float3 normalWS = normalize(float3(t.x, t.z, t.y));

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(0.0, sceneDepth - input.positionCS.w);

                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = 0.02h + 0.98h * pow(1.0h - saturate(dot(normalWS, viewWS)), 5.0h);
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(thickness / _DepthMax));
                Light main = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 diffuse = body * (SampleSH(normalWS) + main.color * saturate(dot(normalWS, main.direction)) * 0.25h);
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), input.positionWS, 1.0h - _Smoothness, 1.0h, screenUV) * _ReflectionStrength;
                half3 halfDir = normalize(main.direction + viewWS);
                half specular = pow(saturate(dot(normalWS, halfDir)), 64.0h + 448.0h * _Smoothness) * _SpecularStrength;
                half3 color = lerp(diffuse, reflection, fresnel) + main.color * specular * main.shadowAttenuation;

                half alpha = lerp(_AlphaMin, 1.0h, saturate(thickness / _AlphaDepth));
                alpha = max(alpha, fresnel) * SAMPLE_TEXTURE2D(_ShapeMap, sampler_ShapeMap, input.uv).r;
                color = MixFog(color, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
