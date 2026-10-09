// TRACE/FieldSurface — concrete, metal and painted surfaces: world-space box projection. Static wetness (global and rising
// from the ground level), AO / smoothness / metal mask (R / G / B), macro variation. URP Forward, Forward+ and SSAO.
Shader "TRACE/FieldSurface"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _NormalMap ("Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        [NoScaleOffset] _MaskMap ("Mask (R AO, G Smoothness, B Metallic)", 2D) = "white" {}
        _Tiling ("Tiling (repeats per metre)", Float) = 0.5
        _OcclusionStrength ("Occlusion", Range(0, 1)) = 1
        _Metallic ("Metallic Scale", Range(0, 1)) = 1
        _SmoothnessMin ("Smoothness Min", Range(0, 1)) = 0.1
        _SmoothnessMax ("Smoothness Max", Range(0, 1)) = 0.3
        [NoScaleOffset] _MacroMap ("Macro Noise", 2D) = "gray" {}
        _MacroScale ("Macro Scale", Float) = 0.04
        _MacroStrength ("Macro Strength", Range(0, 1)) = 0.12
        _Wetness ("Wetness", Range(0, 1)) = 0
        _WetDarken ("Wet Darkening (albedo multiplier)", Range(0.5, 1)) = 0.85
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.62
        _WetNormalFlatten ("Wet Normal Flatten", Range(0, 1)) = 0.4
        _WetPatchiness ("Wet Patchiness", Range(0, 1)) = 0.5
        _GroundWetHeight ("Ground Damp Height (m, 0 = off)", Float) = 0
        _GroundWetLevel ("Ground Level (world Y)", Float) = 0
        _GroundWetAmount ("Ground Damp Amount", Range(0, 1)) = 0.7
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _LayerMap ("Layer Albedo (projected from above)", 2D) = "white" {}
        [NoScaleOffset] _LayerMaskMap ("Layer Mask", 2D) = "white" {}
        _LayerTint ("Layer Tint", Color) = (1, 1, 1, 1)
        _LayerTiling ("Layer Tiling (repeats per metre)", Float) = 0.3
        _LayerAmount ("Layer Amount (0 = off)", Range(0, 1)) = 0
        _LayerContrast ("Layer Edge Contrast", Range(1, 16)) = 5
        _LayerUp ("Layer Only On Top Faces", Range(0, 1)) = 0
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 16)) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "FieldCommon.hlsl"
        #define FIELD_SAMPLE FieldSampleBox
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldVertex
            #pragma fragment FieldFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "FieldPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldShadowVertex
            #pragma fragment FieldDepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "FieldPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldVertex
            #pragma fragment FieldDepthFragment
            #pragma multi_compile_instancing
            #include "FieldPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldVertex
            #pragma fragment FieldDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "FieldPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
