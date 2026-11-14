using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Огонь (ТЗ 8.3): низкий «рокот» (ФНЧ-шум с дрожанием) + случайные щелчки-треск (поток Пуассона). Интенсивность —
    /// от запаса Искры; тление — тихий неровный шорох; <c>proximity</c> — слышимость (печка издали, лампа в руке).
    /// </summary>
    public sealed class FireLayer : ProceduralLayer
    {
        private volatile float _intensity;  // 0…1
        private volatile float _embers;     // 0…1 (тление: доля оставшегося)
        private volatile float _proximity;  // 0…1
        private volatile float _crackleMax = 14f;

        public void Set(float intensity01, float embers01, float proximity01, float crackleRateMax)
        {
            _intensity = intensity01; _embers = embers01; _proximity = proximity01; _crackleMax = crackleRateMax;
        }

        private ProcAudio.Rng _rng;
        private ProcAudio.PinkNoise _rumbleNoise;
        private ProcAudio.Biquad _rumble, _crackBp, _near;
        private float _i, _e, _p;
        private float _flickerA, _flickerB;
        private float _clickEnv, _clickDecay;
        private float _clickHz = 2500f;
        private int _coef;

        protected override void OnInit()
        {
            _rng = new ProcAudio.Rng(0xBEEF1234u);
            _rumbleNoise = new ProcAudio.PinkNoise(0xA5A5A5A5u);
            _clickDecay = 0.995f;
        }

        protected override void Generate(float[] mono, int frames)
        {
            int sr = SampleRate;
            float dt = 1f / sr;
            float k = Mathf.Min(1f, 6f * frames * dt);
            _i += (_intensity - _i) * k; _e += (_embers - _e) * k; _p += (_proximity - _p) * k;

            float intensity = Mathf.Clamp01(_i);
            float prox = Mathf.Clamp01(_p);
            // Тление: слабый, но не нулевой уровень, неровный.
            float level = intensity * prox;

            float rumbleHz = Mathf.Lerp(70f, 170f, intensity);
            _rumble.Set(ProcAudio.BiquadType.LowPass, rumbleHz, 0.8f, sr);
            _crackBp.Set(ProcAudio.BiquadType.BandPass, _clickHz, 1.4f, sr);
            // Издалека — глуше.
            _near.Set(ProcAudio.BiquadType.LowPass, Mathf.Lerp(700f, 9000f, prox * prox), 0.7f, sr);

            float rate = _crackleMax * Mathf.Lerp(0.15f, 1f, intensity);
            if (_e > 0f && intensity < 0.05f) rate = 2f; // тление: редкие щелчки
            float pClick = rate * dt;

            for (int i = 0; i < frames; i++)
            {
                // Дрожание рокота (два медленных случайных шума).
                _flickerA += (_rng.Next() - _flickerA) * 0.0004f;
                _flickerB += (_rng.Next() - _flickerB) * 0.003f;
                float flick = 0.7f + 0.9f * _flickerA + 0.3f * _flickerB;
                float rum = _rumble.Process(_rumbleNoise.Next()) * flick * 3.2f;

                if (_rng.Next01() < pClick)
                {
                    _clickEnv = 0.3f + 0.7f * _rng.Next01();
                    _clickDecay = Mathf.Lerp(0.992f, 0.9985f, _rng.Next01()); // 6–40 мс
                    _clickHz = Mathf.Lerp(1200f, 4200f, _rng.Next01());
                    if (_rng.Next01() < 0.1f) _crackBp.Set(ProcAudio.BiquadType.BandPass, _clickHz, 1.4f, sr);
                }
                float click = _crackBp.Process(_rng.Next()) * _clickEnv * 1.9f;
                _clickEnv *= _clickDecay;

                float emberHiss = 0f;
                if (_e > 0.01f) emberHiss = _rng.Next() * 0.04f * _e * (0.5f + 0.5f * _flickerB);

                mono[i] = _near.Process((rum * (0.5f + 0.5f * intensity) + click * (0.35f + 0.65f * intensity) + emberHiss)) * Mathf.Max(level, 0.02f * prox * _e);
            }
        }
    }
}
