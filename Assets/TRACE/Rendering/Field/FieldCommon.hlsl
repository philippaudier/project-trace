// Shared by TRACE/FieldSurface (box projection) and TRACE/FieldRock (triplanar): world-space mapping so primitives of any
// size keep a constant texel density, an AO / smoothness / metal mask, a macro variation and a common static wetness.
#ifndef TRACE_FIELD_COMMON_INCLUDED
#define TRACE_FIELD_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_MaskMap);   SAMPLER(sampler_MaskMap);
TEXTURE2D(_MacroMap);  SAMPLER(sampler_MacroMap);
TEXTURE2D(_LayerMap);  SAMPLER(sampler_LayerMap);
TEXTURE2D(_LayerMaskMap);

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    float _Tiling;
    half _NormalStrength;
    half _OcclusionStrength;
    half _Metallic;
    half _SmoothnessMin;
    half _SmoothnessMax;
    float _MacroScale;
    half _MacroStrength;
    half _Wetness;
    half _WetDarken;
    half _WetSmoothness;
    half _WetNormalFlatten;
    half _WetPatchiness;
    float _GroundWetHeight;
    float _GroundWetLevel;
    half _GroundWetAmount;
    half4 _EmissionColor;
    float _TriplanarSharpness;
    half4 _LayerTint;
    float _LayerTiling;
    half _LayerAmount;
    half _LayerContrast;
    half _LayerUp;
CBUFFER_END

struct FieldSample
{
    half3 albedo;
    float3 normalWS;
    half ao;
    half smoothness;
    half metallic;
};

// Whiteout reorientation of a tangent-space normal onto one projection axis (0 = X, 1 = Y, 2 = Z).
float3 FieldReorient(half3 t, float3 n, int axis)
{
    if (axis == 0) { float3 tn = float3(t.xy + n.zy, abs(t.z) * n.x); return tn.zyx; }
    if (axis == 1) { float3 tn = float3(t.xy + n.xz, abs(t.z) * n.y); return tn.xzy; }
    float3 tz = float3(t.xy + n.xy, abs(t.z) * n.z); return tz.xyz;
}

float2 FieldAxisUV(float3 p, int axis)
{
    return axis == 0 ? p.zy : axis == 1 ? p.xz : p.xy;
}

void FieldSampleAxis(float3 p, float3 n, int axis, out half4 base, out float3 normalWS, out half4 mask)
{
    float2 uv = FieldAxisUV(p, axis) * _Tiling;
    base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half3 t = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalStrength);
    normalWS = FieldReorient(t, n, axis);
    mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
}

FieldSample FieldPack(half4 base, float3 normalWS, half4 mask)
{
    FieldSample s;
    s.albedo = base.rgb;
    s.normalWS = normalize(normalWS);
    s.ao = mask.r;
    s.smoothness = mask.g;
    s.metallic = mask.b;
    return s;
}

// Box projection: one sample set on the dominant axis; crisp on architecture.
FieldSample FieldSampleBox(float3 p, float3 n)
{
    float3 an = abs(n);
    int axis = an.x >= an.y && an.x >= an.z ? 0 : an.y >= an.z ? 1 : 2;
    half4 base; float3 nWS; half4 mask;
    FieldSampleAxis(p, n, axis, base, nWS, mask);
    return FieldPack(base, nWS, mask);
}

// Triplanar: three sample sets blended by a sharpened normal; no stretching on irregular rock.
FieldSample FieldSampleTriplanar(float3 p, float3 n)
{
    float3 w = pow(abs(n), _TriplanarSharpness);
    w /= max(w.x + w.y + w.z, 1e-4);
    half4 bx, by, bz; float3 nx, ny, nz; half4 mx, my, mz;
    FieldSampleAxis(p, n, 0, bx, nx, mx);
    FieldSampleAxis(p, n, 1, by, ny, my);
    FieldSampleAxis(p, n, 2, bz, nz, mz);
    return FieldPack(bx * w.x + by * w.y + bz * w.z, nx * w.x + ny * w.y + nz * w.z, mx * w.x + my * w.y + mz * w.z);
}

half FieldMacro(float3 p)
{
    float2 uv = float2(p.x + p.y * 0.5, p.z - p.y * 0.5) * _MacroScale;
    return SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, uv).r;
}

// Second surface (gravel or mud on the ground, moss on top of rocks), projected from above and laid in patches by a
// second reading of the macro noise; _LayerUp keeps it on upward faces. Off when _LayerAmount is 0.
void FieldLayer(float3 positionWS, float3 geometricNormalWS, inout half3 albedo, inout half smoothness, inout half ao)
{
    if (_LayerAmount <= 0.0h) return;
    float2 uv = positionWS.xz * _LayerTiling;
    half4 base = SAMPLE_TEXTURE2D(_LayerMap, sampler_LayerMap, uv);
    half4 mask = SAMPLE_TEXTURE2D(_LayerMaskMap, sampler_LayerMap, uv);
    half patch = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, positionWS.xz * _MacroScale * 2.3 + 0.37).r;
    half up = lerp(1.0h, saturate((geometricNormalWS.y - 0.45h) * 3.0h), _LayerUp);
    half w = saturate((patch - (1.0h - _LayerAmount)) * _LayerContrast) * up;
    albedo = lerp(albedo, base.rgb * _LayerTint.rgb, w);
    smoothness = lerp(smoothness, lerp(_SmoothnessMin, _SmoothnessMax, mask.g), w);
    ao = lerp(ao, mask.r, w);
}

// Albedo variation, smoothness range, then wetness: global amount plus a damp band rising from the ground level,
// broken up by the macro noise. Wet = darker, smoother, slightly flatter normal.
void FieldCompose(float3 positionWS, float3 geometricNormalWS, FieldSample s, out SurfaceData surface, out float3 normalWS)
{
    half macro = FieldMacro(positionWS);
    half3 albedo = s.albedo * _BaseColor.rgb * lerp(1.0h - _MacroStrength, 1.0h + _MacroStrength, macro);
    half smoothness = lerp(_SmoothnessMin, _SmoothnessMax, s.smoothness);
    half ao = s.ao;
    FieldLayer(positionWS, geometricNormalWS, albedo, smoothness, ao);
    half ground = _GroundWetHeight > 0.0 ? (1.0h - saturate((positionWS.y - _GroundWetLevel) / _GroundWetHeight)) * _GroundWetAmount : 0.0h;
    half wet = saturate((_Wetness + ground) * lerp(1.0h, saturate(macro * 1.6h), _WetPatchiness));
    albedo *= lerp(1.0h, _WetDarken, wet);
    smoothness = lerp(smoothness, max(smoothness, _WetSmoothness), wet);
    normalWS = normalize(lerp(s.normalWS, geometricNormalWS, wet * _WetNormalFlatten));

    surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.metallic = s.metallic * _Metallic;
    surface.specular = half3(0.0h, 0.0h, 0.0h);
    surface.smoothness = smoothness;
    surface.occlusion = lerp(1.0h, ao, _OcclusionStrength);
    surface.emission = _EmissionColor.rgb;
    surface.alpha = 1.0h;
    surface.normalTS = half3(0.0h, 0.0h, 1.0h);
}

#endif
