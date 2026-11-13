#ifndef LR_WORLD_FORWARD_PASS_INCLUDED
#define LR_WORLD_FORWARD_PASS_INCLUDED

#include "LR_WorldInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    half4  color      : COLOR;      // цвет палитры (sRGB)
    float2 uv0        : TEXCOORD0;  // x — «снежность», y — свечение
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3  normalWS   : TEXCOORD1;
    half4  color      : TEXCOORD2;  // rgb — альбедо (linear), a — свечение
    half   snowable   : TEXCOORD3;
    half   fogFactor  : TEXCOORD4;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD6;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD7;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings LRWorldVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs nrmInputs = GetVertexNormalInputs(input.normalOS);

    output.positionCS = posInputs.positionCS;
    output.positionWS = posInputs.positionWS;
    output.normalWS = NormalizeNormalPerVertex(nrmInputs.normalWS);
    output.color = half4(LR_VertexColorToLinear(input.color.rgb), input.uv0.y);
    output.snowable = input.uv0.x;

#if defined(_FOG_FRAGMENT)
    output.fogFactor = 0;
#else
    output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
#endif

    float2 noLightmapUV = 0;
    OUTPUT_LIGHTMAP_UV(noLightmapUV, unity_LightmapST, output.staticLightmapUV);
    OUTPUT_SH4(posInputs.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(posInputs.positionWS), output.vertexSH, output.probeOcclusion);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(posInputs);
#endif
    return output;
}

void LRWorldFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // ---- InputData (как в URP SimpleLit) ----
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
    inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(input.positionWS));
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

#if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH, GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
        inputData.viewDirectionWS, input.positionCS.xy, input.probeOcclusion, inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif

    // ---- Снег ----
    half snow = LR_SnowMask(inputData.normalWS, input.snowable);
    half3 albedo = lerp(input.color.rgb * _BaseTint.rgb, _SnowColor.rgb, snow);

    // ---- Мерцание снега: редкие ячейки, зависят от направления на солнце и взгляда ----
    half3 emission = input.color.rgb * _EmissionTint.rgb * (input.color.a * _EmissionIntensity);
    if (_SparkleStrength > 0 && snow > 0.01)
    {
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
        float3 cell = floor(input.positionWS * _SparkleDensity);
        float h = LR_Hash31(cell);
        float h2 = LR_Hash31(cell + 17.3);
        // Направление «зеркальца» в ячейке — чуть отклонено от нормали.
        float3 facet = normalize(inputData.normalWS + (float3(h, h2, frac(h * 7.7)) - 0.5) * 0.9);
        float3 halfDir = SafeNormalize(mainLight.direction + inputData.viewDirectionWS);
        float glint = pow(saturate(dot(facet, halfDir)), 64.0);
        float twinkle = 0.5 + 0.5 * sin(_Time.y * (2.0 + h2 * 3.0) + h * 40.0);
        float dist = distance(GetCameraPositionWS(), input.positionWS);
        float fade = saturate(1.0 - dist / max(_SparkleDistance, 0.01));
        float sparkle = step(_SparkleThreshold, h) * glint * twinkle * fade * snow;
        emission += mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * sparkle * _SparkleStrength);
    }

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.alpha = 1;
    surfaceData.emission = emission;
    surfaceData.occlusion = 1;
    surfaceData.normalTS = half3(0, 0, 1);

    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
    // _FogAmount < 1 — объект «пробивает» туман (силуэт Маяка, видимый издалека в акте 1).
    color.rgb = lerp(color.rgb, MixFog(color.rgb, inputData.fogCoord), _FogAmount);
    color.a = 1;
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
