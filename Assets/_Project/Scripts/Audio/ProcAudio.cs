using System;
using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Процедурный звук (ТЗ 8.3): математика сигналов, фильтры, шум и рендер коротких клипов в <see cref="AudioClip"/>.
    /// Всё, что вызывается из <c>OnAudioFilterRead</c>, — структуры без выделений памяти; <see cref="SampleRate"/> читать только в основном потоке.
    /// </summary>
    public static class ProcAudio
    {
        private static int _sr;
        private static AudioClip _silence;

        /// <summary>Частота дискретизации вывода (основной поток; слои кэшируют её в Awake).</summary>
        public static int SampleRate => _sr > 0 ? _sr : (_sr = AudioSettings.outputSampleRate);

        /// <summary>Зацикленный беззвучный клип: нужен, чтобы AudioSource «играл» и звал <c>OnAudioFilterRead</c>.</summary>
        public static AudioClip SilentLoop
        {
            get
            {
                if (_silence == null)
                {
                    _silence = AudioClip.Create("LR_Silence", 4096, 1, SampleRate, false);
                    _silence.SetData(new float[4096], 0);
                }
                return _silence;
            }
        }

        // ------------------------------------------------------------------ математика

        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);
        public static float LinearToDb(float v) => v <= 0.0001f ? -80f : 20f * Mathf.Log10(v);
        public static float MidiToHz(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        /// <summary>Мягкое ограничение (рациональная аппроксимация tanh): вход ограничен ±3, выход в пределах ±1.</summary>
        public static float SoftClip(float x)
        {
            if (x > 3f) x = 3f; else if (x < -3f) x = -3f;
            return x * (27f + x * x) / (27f + 9f * x * x);
        }

        /// <summary>Лимитер с «коленом»: до <paramref name="threshold"/> сигнал не трогаем, выше — плавно прижимаем к ±1.</summary>
        public static float Limit(float x, float threshold)
        {
            float a = x < 0f ? -x : x;
            if (a <= threshold) return x;
            float knee = 1f - threshold;
            float y = threshold + knee * SoftClip((a - threshold) / Mathf.Max(0.0001f, knee) * 0.5f) * 1.0f;
            return x < 0f ? -y : y;
        }

        // ------------------------------------------------------------------ шум

        /// <summary>Быстрый белый шум xorshift32 в диапазоне −1…1 (на потоках — по экземпляру на слой).</summary>
        public struct Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 2463534242u : seed; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / 8388608f - 1f;
            }
            /// <summary>0…1.</summary>
            public float Next01() => Next() * 0.5f + 0.5f;
        }

        /// <summary>Розовый шум (фильтр Пола Келлета) — мягче белого, основа ветра.</summary>
        public struct PinkNoise
        {
            private Rng _rng;
            private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;
            public PinkNoise(uint seed) { _rng = new Rng(seed); _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = _b6 = 0f; }
            public float Next()
            {
                float w = _rng.Next();
                _b0 = 0.99886f * _b0 + w * 0.0555179f;
                _b1 = 0.99332f * _b1 + w * 0.0750759f;
                _b2 = 0.96900f * _b2 + w * 0.1538520f;
                _b3 = 0.86650f * _b3 + w * 0.3104856f;
                _b4 = 0.55000f * _b4 + w * 0.5329522f;
                _b5 = -0.7616f * _b5 - w * 0.0168980f;
                float p = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362f;
                _b6 = w * 0.115926f;
                return p * 0.11f;
            }
        }

        // ------------------------------------------------------------------ фильтры

        /// <summary>Однополюсный ФНЧ.</summary>
        public struct OnePoleLP
        {
            private float _y, _a;
            public void SetCutoff(float hz, int sr) => _a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(hz, 1f, sr * 0.45f) / sr);
            public float Process(float x) { _y += _a * (x - _y); return _y; }
            public void Reset() => _y = 0f;
        }

        /// <summary>Однополюсный ФВЧ.</summary>
        public struct OnePoleHP
        {
            private float _lp, _a;
            public void SetCutoff(float hz, int sr) => _a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(hz, 1f, sr * 0.45f) / sr);
            public float Process(float x) { _lp += _a * (x - _lp); return x - _lp; }
            public void Reset() => _lp = 0f;
        }

        public enum BiquadType { LowPass, HighPass, BandPass }

        /// <summary>Биквад (RBJ cookbook), direct form I. Пересчёт коэффициентов дёшев — можно менять частоту каждый блок.</summary>
        public struct Biquad
        {
            private float _b0, _b1, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

            public void Set(BiquadType type, float hz, float q, int sr)
            {
                hz = Mathf.Clamp(hz, 10f, sr * 0.45f);
                q = Mathf.Max(0.1f, q);
                float w0 = 2f * Mathf.PI * hz / sr;
                float cos = Mathf.Cos(w0), sin = Mathf.Sin(w0);
                float alpha = sin / (2f * q);
                float a0 = 1f + alpha;
                switch (type)
                {
                    case BiquadType.LowPass:
                        _b0 = (1f - cos) * 0.5f; _b1 = 1f - cos; _b2 = (1f - cos) * 0.5f; break;
                    case BiquadType.HighPass:
                        _b0 = (1f + cos) * 0.5f; _b1 = -(1f + cos); _b2 = (1f + cos) * 0.5f; break;
                    default: // BandPass (постоянная пиковая 0 дБ)
                        _b0 = alpha; _b1 = 0f; _b2 = -alpha; break;
                }
                _a1 = -2f * cos / a0; _a2 = (1f - alpha) / a0;
                _b0 /= a0; _b1 /= a0; _b2 /= a0;
            }

            public float Process(float x)
            {
                float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
                // Защита от денормалей и взрыва при резком изменении частоты.
                if (y > 8f || y < -8f || float.IsNaN(y)) { _x1 = _x2 = _y1 = _y2 = 0f; return 0f; }
                return y;
            }
        }

        /// <summary>Огибающая атака–спад (линейная атака, экспоненциальный спад) для щелчков, треска и т.п.</summary>
        public static float AttackDecay(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return attack <= 0f ? 1f : t / attack;
            return Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));
        }

        // ------------------------------------------------------------------ рендер клипов

        /// <summary>
        /// Рендер короткого моно-клипа (один раз при старте). <paramref name="sample"/>(i, sampleRate) → −1…1.
        /// Края плавно гасятся (<paramref name="fadeMs"/>) — без щелчков; пик нормируется к <paramref name="peak"/>.
        /// </summary>
        public static AudioClip Render(string name, float seconds, Func<int, int, float> sample, float peak = 0.8f, float fadeMs = 4f)
        {
            int sr = SampleRate;
            int n = Mathf.Max(16, Mathf.CeilToInt(seconds * sr));
            var data = new float[n];
            float max = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = sample(i, sr);
                data[i] = v;
                float a = v < 0f ? -v : v;
                if (a > max) max = a;
            }
            float scale = max > 0.0001f ? peak / max : 1f;
            int fade = Mathf.Min(n / 2, Mathf.Max(1, Mathf.RoundToInt(fadeMs * 0.001f * sr)));
            for (int i = 0; i < n; i++)
            {
                float g = scale;
                if (i < fade) g *= (float)i / fade;
                else if (i >= n - fade) g *= (float)(n - 1 - i) / fade;
                data[i] *= g;
            }
            var clip = AudioClip.Create(name, n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
