// The Last Ride — основной шейдер мира (ТЗ 8.1).
// Цвет — из vertex color (палитра, sRGB), UV0.x — «снежность», UV0.y — свечение.
// Снег сам ложится на грани, смотрящие вверх; на снегу — редкие искорки.
// Освещение — штатный URP Blinn-Phong (без спекуляра): тени, Forward+, амбиент, туман.
Shader "The Last Ride/World"
{
    Properties
    {
        [Header(Base)]
        _BaseTint("Base Tint (multiplies vertex color)", Color) = (1, 1, 1, 1)

        [Header(Snow)]
        _SnowColor("Snow Color", Color) = (0.93, 0.95, 0.99, 1)
        _SnowThreshold("Snow Threshold (normal.y)", Range(-1, 1)) = 0.55
        _SnowBlend("Snow Blend", Range(0.001, 1)) = 0.12
        _SnowAmount("Snow Amount", Range(0, 2)) = 1

        [Header(Sparkle)]
        _SparkleStrength("Sparkle Strength", Range(0, 10)) = 2.5
        _SparkleDensity("Sparkle Density (cells per m)", Range(1, 60)) = 22
        _SparkleThreshold("Sparkle Rarity", Range(0.5, 1)) = 0.96
        _SparkleDistance("Sparkle Fade Distance (m)", Range(1, 200)) = 35

        [Header(Emission)]
        _EmissionIntensity("Emission Intensity (HDR)", Range(0, 30)) = 4
        _EmissionTint("Emission Tint (multiplies glow color)", Color) = (1, 1, 1, 1)

        [Header(Fog)]
        _FogAmount("Fog Amount (1 = normal, 0 = ignores fog)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LRWorldVertex
            #pragma fragment LRWorldFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing

            #include "LR_WorldForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "LR_WorldInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "LR_WorldInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "LR_WorldInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
