using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Двигатель и колёса фургона (ТЗ 4.5, 8.3): низкий гул (основной тон + гармоники, тон от скорости) и шум колёс по снегу
    /// (громкость от скорости, лёд — шипение, глубокий снег — глуше). При поломке (<c>running = false</c>) обороты падают — двигатель глохнет.
    /// </summary>
    public sealed class EngineLayer : ProceduralLayer
    {
        private volatile float _speed01;
        private volatile float _throttle;
        private volatile bool _running;
        private volatile float _ice;
        private volatile float _deep;
        private volatile float _idleHz = 34f, _maxHz = 88f, _up = 45f, _down = 14f, _wheelGain = 0.5f;

        public void Set(bool running, float speed01, float throttle01, float ice01, float deep01,
                        float idleHz, float maxHz, float revUp, float revDown, float wheelGain)
        {
            _running = running; _speed01 = speed01; _throttle = throttle01; _ice = ice01; _deep = deep01;
            _idleHz = idleHz; _maxHz = maxHz; _up = revUp; _down = revDown; _wheelGain = wheelGain;
        }

        private ProcAudio.Rng _rng;
        private ProcAudio.PinkNoise _wheel;
        private ProcAudio.Biquad _body, _wheelLp, _iceHp;
        private float _hz;          // текущая частота основного тона
        private float _phase;
        private float _wobble;
        private float _spd, _thr, _ice1, _deep1;

        protected override void OnInit()
        {
            _rng = new ProcAudio.Rng(0xE9915u);
            _wheel = new ProcAudio.PinkNoise(0x77AA55u);
        }

        protected override void Generate(float[] mono, int frames)
        {
            int sr = SampleRate;
            float dt = 1f / sr;
            float block = frames * dt;
            float k = Mathf.Min(1f, 6f * block);
            _spd += (_speed01 - _spd) * k; _thr += (_throttle - _thr) * k;
            _ice1 += (_ice - _ice1) * k; _deep1 += (_deep - _deep1) * k;

            float idle = _idleHz, max = _maxHz;
            float target = _running ? Mathf.Lerp(idle, max, Mathf.Clamp01(_spd + 0.25f * _thr)) : 0f;
            if (_hz < target) _hz = Mathf.Min(target, _hz + _up * block);
            else _hz = Mathf.Max(target, _hz - (_running ? _up : _down) * block);

            // Амплитуда зависит от оборотов: ниже холостых (глохнет) — затухает.
            float rpmAmp = Mathf.Clamp01(_hz / Mathf.Max(1f, idle));
            rpmAmp *= rpmAmp;
            float load = 0.7f + 0.3f * _thr;

            _body.Set(ProcAudio.BiquadType.LowPass, 160f + 260f * _spd + 120f * _thr, 0.9f, sr);
            _wheelLp.Set(ProcAudio.BiquadType.LowPass, Mathf.Lerp(900f, 350f, _deep1) + 500f * _spd, 0.7f, sr);
            _iceHp.Set(ProcAudio.BiquadType.HighPass, 2500f, 0.8f, sr);

            float wheelAmp = _wheelGain * Mathf.Pow(_spd, 1.2f) * (1f - 0.35f * _deep1);
            float iceAmp = _ice1 * _spd * 0.35f * _wheelGain;

            for (int i = 0; i < frames; i++)
            {
                _wobble += (_rng.Next() - _wobble) * 0.0006f;
                float hz = _hz * (1f + 0.012f * _wobble);
                _phase += hz * dt; if (_phase >= 1f) _phase -= 1f;
                // Тон с гармониками (грубый «моторный» пилообразный профиль) → ФНЧ.
                float p = _phase;
                float saw = p * 2f - 1f;
                float tri = (p < 0.5f ? p * 4f - 1f : 3f - p * 4f);
                float osc = saw * 0.55f + tri * 0.45f + Mathf.Sin(p * 4f * Mathf.PI) * 0.3f; // + 2-я гармоника
                float engine = _body.Process(osc) * 0.9f * rpmAmp * load;

                float n = _wheel.Next();
                float wheel = _wheelLp.Process(n) * 3f * wheelAmp + _iceHp.Process(_rng.Next()) * iceAmp;

                mono[i] = engine + wheel;
            }
        }
    }
}
