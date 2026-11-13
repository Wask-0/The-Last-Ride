#ifndef LR_WORLD_INPUT_INCLUDED
#define LR_WORLD_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Один CBUFFER для всех проходов — требование SRP Batcher.
CBUFFER_START(UnityPerMaterial)
    half4 _BaseTint;
    half4 _SnowColor;
    half  _SnowThreshold;
    half  _SnowBlend;
    half  _SnowAmount;
    half  _SparkleStrength;
    half  _SparkleDensity;
    half  _SparkleThreshold;
    half  _SparkleDistance;
    half  _EmissionIntensity;
    half  _FogAmount;
CBUFFER_END

// Глобальные параметры, которые задаёт игра (Shader.SetGlobalFloat).
// По умолчанию 0 — шейдер выглядит «как в материале».
// _LR_SnowBoost  > 0 — снега больше (порог по нормали ниже), < 0 — меньше.
float _LR_SnowBoost;

// Цвета вершин запекаются в sRGB (как в палитре) — в Linear-проекте переводим.
half3 LR_VertexColorToLinear(half3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return FastSRGBToLinear(c);
#endif
}

// Доля снега: по мировой нормали × «снежность» вершины (UV0.x).
half LR_SnowMask(half3 normalWS, half snowable)
{
    half threshold = _SnowThreshold - _LR_SnowBoost;
    half m = smoothstep(threshold - _SnowBlend, threshold + _SnowBlend, normalWS.y);
    return saturate(m * snowable * _SnowAmount);
}

// Дешёвый хеш 3D → [0,1).
float LR_Hash31(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}

#endif
