// Иней на экране (этап 9.4): полноэкранный проход, процедурный узор.
// Кристаллы (ridged-шум) нарастают от краёв к центру + молочная дымка и лёгкое искажение картинки подо льдом.
// Силу задаёт FrostView через глобальные _LR_Frost (0..1) и _LR_Fill (0..1, заполнение экрана при замерзании/смерти).
Shader "The Last Ride/Frost"
{
    Properties
    {
        _IceColor("Ice Color", Color) = (0.86, 0.94, 1, 1)
        _Distortion("Distortion", Range(0, 0.05)) = 0.018
        _Haze("Haze", Range(0, 1)) = 0.55
        _CrystalScale("Crystal Scale", Range(2, 40)) = 22
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Frost"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _LR_Frost;
            float _LR_Fill;
            half4 _IceColor;
            half _Distortion;
            half _Haze;
            half _CrystalScale;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash21(i), b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { v += a * vnoise(p); p = p * 2.03 + 11.7; a *= 0.5; }
                return v;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float frost = saturate(_LR_Frost);
                float fill = saturate(_LR_Fill);
                float total = saturate(frost + fill);
                if (total <= 0.001)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);

                // Расстояние до края кадра 0 (край) … 1 (центр), с эллиптическим скруглением углов.
                float2 q = abs(uv - 0.5) * 2.0;
                float edge = 1.0 - pow(pow(q.x, 3.0) + pow(q.y, 3.0), 1.0 / 3.0);
                edge = saturate(edge);

                // Кристаллы: рёбра ячеек Вороного (F2 − F1) по слегка искажённым координатам — грани и «трещины» льда.
                float2 cp = p * _CrystalScale;
                cp += (float2(fbm(cp * 0.35), fbm(cp * 0.35 + 5.2)) - 0.5) * 1.4;
                float2 ip = floor(cp), fp = frac(cp);
                float f1 = 8.0, f2 = 8.0, cellId = 0.0;
                [unroll] for (int j = -1; j <= 1; j++)
                [unroll] for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 o = float2(hash21(ip + g), hash21(ip + g + 31.7));
                    float d = length(g + o - fp);
                    if (d < f1) { f2 = f1; f1 = d; cellId = hash21(ip + g + 7.1); }
                    else if (d < f2) { f2 = d; }
                }
                float seam = 1.0 - smoothstep(0.0, 0.08, f2 - f1);   // тонкие светлые линии
                float grain = fbm(p * 38.0);                          // мелкая изморозь

                // Покрытие: чем сильнее иней, тем дальше от края он заходит; граница рваная.
                float ragged = fbm(p * 7.0 + 3.1) - 0.5;
                float reach = pow(frost, 2.0) * 0.8 + fill * 1.3;
                float cover = smoothstep(edge - 0.08, edge + 0.12, reach + ragged * 0.35 * (1.0 - fill));
                cover = saturate(cover + fill * fill);

                // Искажение картинки подо льдом: каждая ячейка преломляет чуть по-своему.
                float2 warp = (float2(hash21(floor(cp) + 2.3), hash21(floor(cp) + 9.9)) - 0.5) * _Distortion * cover * (1.0 + fill);
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + warp).rgb;

                // Дымка (молочная) + грани + изморозь.
                half haze = saturate(_Haze * cover * (0.55 + 0.45 * grain) * (0.7 + 0.3 * cellId) + fill * 0.4);
                half3 col = lerp(scene, _IceColor.rgb, haze);
                half lines = saturate(seam * cover);
                col = lerp(col, half3(1, 1, 1), lines * 0.6);
                col = lerp(col, half3(1, 1, 1), saturate(grain * grain * cover * 0.6));

                // Полное заполнение: почти сплошная ледяная корка с видимыми гранями.
                half3 crust = lerp(_IceColor.rgb, half3(1, 1, 1), saturate(seam * 0.7 + grain * 0.3));
                col = lerp(col, crust, saturate(fill * fill * 0.92));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
