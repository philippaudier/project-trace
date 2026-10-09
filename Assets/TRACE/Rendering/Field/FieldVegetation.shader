// TRACE/FieldVegetation — grass, plants and leaf cards: alpha clip, double sided, a light wind sway on the top of each
// card (game time, so Tactical Focus slows it), soft normals bent upward and a touch of back-light translucency.
Shader "TRACE/FieldVegetation"
{
    Properties
    {
        _BaseMap ("Albedo (A = opacity)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _WindStrength ("Wind Strength (m)", Range(0, 0.5)) = 0.06
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.3
        _Translucency ("Translucency", Range(0, 1)) = 0.35
        _NormalBend ("Normal Bend Up", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Smoothness;
            half _WindStrength;
            half _WindSpeed;
            half _Translucency;
            half _NormalBend;
        CBUFFER_END

        struct VegAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct VegVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 VegWind(float3 positionWS, float height)
        {
            float phase = _Time.y * _WindSpeed + positionWS.x * 0.37 + positionWS.z * 0.29;
            float sway = (sin(phase) * 0.7 + sin(phase * 2.3 + 1.7) * 0.3) * _WindStrength * height * height;
            return positionWS + float3(sway, 0.0, sway * 0.6);
        }

        VegVaryings VegVertexCommon(VegAttributes input)
        {
            VegVaryings output = (VegVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            float3 positionWS = VegWind(TransformObjectToWorld(input.positionOS.xyz), saturate(input.uv.y));
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        half4 VegAlbedo(float2 uv)
        {
            half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            clip(albedo.a - _Cutoff);
            return albedo;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VegVertex
            #pragma fragment VegFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            VegVaryings VegVertex(VegAttributes input)
            {
                VegVaryings output = VegVertexCommon(input);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 VegFragment(VegVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = VegAlbedo(input.uv);
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                normalWS = normalize(lerp(normalWS, float3(0.0, 1.0, 0.0), _NormalBend));

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1.0h;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0.0h, 0.0h, 1.0h);

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normalWS;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                data.bakedGI = SampleSH(normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                // Light through the leaves when the sun is behind them.
                Light main = GetMainLight(data.shadowCoord);
                half back = saturate(dot(-main.direction, data.viewDirectionWS));
                surface.emission = albedo.rgb * main.color * main.shadowAttenuation * back * back * _Translucency;

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VegShadowVertex
            #pragma fragment VegClipFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            VegVaryings VegShadowVertex(VegAttributes input)
            {
                VegVaryings output = VegVertexCommon(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - output.positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 VegClipFragment(VegVaryings input) : SV_Target { VegAlbedo(input.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VegVertexCommon
            #pragma fragment VegDepthFragment
            #pragma multi_compile_instancing
            half4 VegDepthFragment(VegVaryings input) : SV_Target { VegAlbedo(input.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VegVertexCommon
            #pragma fragment VegNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            half4 VegNormalsFragment(VegVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                VegAlbedo(input.uv);
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
