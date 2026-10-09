// Tactical Focus enemy outline, drawn by TacticalOutlineFeature as an override material on renderers carrying one of
// the Tactical rendering layers. Inverted hull: back faces pushed out along the normal in clip space, so the width
// stays in pixels whatever the distance. ZTest LEqual against the camera depth: occluded enemies get no outline
// (Tactical Focus is not a wallhack). Per-tier look comes from the material; the fade, the clock, the scan reveal and
// the distance falloff come from globals set by TacticalReadability.
Shader "TRACE/TacticalOutline"
{
    Properties
    {
        _OutlineColor ("Colour", Color) = (0.62, 0.8, 0.88, 0.5)
        _AccentColor ("Accent colour", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Width (pixels)", Float) = 1.3
        _PulseSpeed ("Pulse speed (Hz)", Float) = 0
        _PulseAmount ("Pulse brightness amount", Range(0, 1)) = 0
        _AccentAmount ("Accent amount at pulse peak", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "TacticalOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half4 _AccentColor;
                float _OutlineWidth;
                float _PulseSpeed;
                half _PulseAmount;
                half _AccentAmount;
            CBUFFER_END

            float _TacticalOutlineFade;
            float _TacticalOutlineTime;
            float4 _TacticalOutlineReveal;   // xyz origin, w radius
            float4 _TacticalOutlineRange;    // x full until, y gone at

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float len = max(length(normalCS), 1e-5);
                positionCS.xy += normalCS / len * _OutlineWidth * 2.0 / _ScreenParams.xy * positionCS.w;
                output.positionCS = positionCS;
                output.positionWS = positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float pulse = _PulseSpeed > 0 ? 0.5 + 0.5 * sin(_TacticalOutlineTime * _PulseSpeed * 6.2831853) : 1.0;
                half4 colour = lerp(_OutlineColor, _AccentColor, _AccentAmount * pulse);
                colour.a = _OutlineColor.a * (1.0 - _PulseAmount * (1.0 - pulse));
                float distanceToCamera = distance(input.positionWS, _WorldSpaceCameraPos.xyz);
                colour.a *= 1.0 - smoothstep(_TacticalOutlineRange.x, _TacticalOutlineRange.y, distanceToCamera);
                // Revealed just behind the scan front, without ever waiting long for it.
                colour.a *= saturate((_TacticalOutlineReveal.w - distance(input.positionWS.xz, _TacticalOutlineReveal.xz)) / 0.75);
                colour.a *= _TacticalOutlineFade;
                return colour;
            }
            ENDHLSL
        }
    }
}
