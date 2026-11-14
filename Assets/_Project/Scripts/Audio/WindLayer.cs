using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Ветер (ТЗ 8.3): розовый шум через два биквада — низкий «гул» и подвижный «свист». Сила ветра поднимает громкость и срез,
    /// порыв — всплеск и сдвиг свиста вверх; внутри фургона звук глушится. Параметры пишет <see cref="SoundscapeDirector"/>.
    /// </summary>
    public sealed class WindLayer : ProceduralLayer
    {
        // Управление (основной поток → аудио-поток).
        private volatile float _wind;    // 0…1+ (сила ветра)
        private volatile float _gust;    // 0…1
        private volatile float _muffle;  // 0 снаружи … 1 в фургоне
        private volatile float _inVanCutoff = 450f;
        private volatile float _floor = 0.12f;
        private volatile float _inVanGain = 0.6f;

        public void Set(float wind01, float gust01, float muffle01, float floor, float inVanGain, float inVanCutoff)
        {
            _wind = wind01; _gust = gust01; _muffle = muffle01;
            _floor = floor; _inVanGain = inVanGain; _inVanCutoff = inVanCutoff;
        }

        // Состояние аудио-потока.
        private ProcAudio.PinkNoise _n1, _n2;
        private ProcAudio.Biquad _low, _howl, _mufflef;
        private float _w, _g, _m;     // сглаженные
        private float _lfoA, _lfoB;   // фазы медленной модуляции
        private int _coefCounter;

        protected override void OnInit()
        {
            _n1 = new ProcAudio.PinkNoise(0x1234567u);
            _n2 = new ProcAudio.PinkNoise(0x9E3779B9u);
            _lfoB = 1.7f;
        }

        protected override void Generate(float[] mono, int frames)
        {
            int sr = SampleRate;
            float tw = _wind, tg = _gust, tm = _muffle, floor = _floor, vanGain = _inVanGain, vanCut = _inVanCutoff;
            float dt = 1f / sr;
            float k = Mathf.Min(1f, 4f * frames * dt);          // ~0.25 с сглаживание параметров на блок
            float kg = Mathf.Min(1f, 12f * frames * dt);        // порыв быстрее

            _w += (tw - _w) * k; _g += (tg - _g) * kg; _m += (tm - _m) * k;

            float wind = Mathf.Clamp(_w, 0f, 1.3f);
            float baseLevel = floor + (1f - floor) * Mathf.Sqrt(Mathf.Clamp01(wind));
            float level = baseLevel * (1f + 0.6f * _g) * Mathf.Lerp(1f, vanGain, _m);

            // Коэффициенты фильтров пересчитываем раз в блок (медленные параметры).
            float lfo = 0.5f + 0.5f * Mathf.Sin(_lfoA * 2f * Mathf.PI);
            float lowHz = Mathf.Lerp(90f, 380f, wind) * Mathf.Lerp(1f, 0.55f, _m);
            float howlHz = Mathf.Lerp(380f, 900f, wind) * (0.8f + 0.5f * lfo) * (1f + 0.9f * _g);
            howlHz *= Mathf.Lerp(1f, 0.4f, _m);
            float howlQ = Mathf.Lerp(1.2f, 3.2f, wind);
            _low.Set(ProcAudio.BiquadType.LowPass, lowHz, 0.7f, sr);
            _howl.Set(ProcAudio.BiquadType.BandPass, howlHz, howlQ, sr);
            _mufflef.Set(ProcAudio.BiquadType.LowPass, Mathf.Lerp(8000f, vanCut, _m), 0.7f, sr);

            float howlAmt = Mathf.Lerp(0.2f, 1.1f, wind) * (0.55f + 0.45f * lfo) * (1f + 0.7f * _g);
            float lowAmt = 1.0f;

            _lfoA += frames * dt * 0.11f; if (_lfoA > 1f) _lfoA -= 1f;
            _lfoB += frames * dt * 0.27f; if (_lfoB > 1f) _lfoB -= 1f;

            for (int i = 0; i < frames; i++)
            {
                float a = _low.Process(_n1.Next()) * lowAmt;
                float b = _howl.Process(_n2.Next()) * howlAmt * 2.2f;
                mono[i] = _mufflef.Process((a * 2.6f + b) * level) * 0.8f;
            }
        }
    }
}
