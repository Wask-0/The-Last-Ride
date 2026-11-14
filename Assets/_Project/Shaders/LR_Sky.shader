// Небо игры: простой градиент зенит → горизонт → земля, сливающийся с туманом (этап 9.2).
// Цвета неба задаёт акт (ActLighting), AtmosphereView плавно меняет их и густоту «мглы» (_Whiteout).
Shader "The Last Ride/Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.35, 0.5, 0.75, 1)
        _Horizon("Horizon", Color) = (1, 0.78, 0.62, 1)
        _Ground("Ground", Color) = (0.6, 0.62, 0.7, 1)
        _FogColor("Fog Color", Color) = (0.75, 0.78, 0.86, 1)
        _Whiteout("Whiteout (0 = clear, 1 = full fog)", Range(0, 1)) = 0
        _HorizonPower("Horizon Power (lower = wider horizon glow)", Range(0.1, 2)) = 0.5
        _HorizonFog("Horizon Fog Band", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith;
                half4 _Horizon;
                half4 _Ground;
                half4 _FogColor;
                half _Whiteout;
                half _HorizonPower;
                half _HorizonFog;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half y = normalize(i.dir).y;
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(y), _HorizonPower));
                half3 col = lerp(sky, _Ground.rgb, saturate(-y * 6.0));
                // Полоса тумана у горизонта: далёкие склоны сливаются с небом.
                half band = saturate(1.0 - abs(y) * 5.0) * _HorizonFog;
                col = lerp(col, _FogColor.rgb, saturate(max(_Whiteout, band)));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
