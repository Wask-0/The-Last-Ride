using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Заводной механизм робота (ТЗ 4.4, 8.3): тик-так (темп и тон от тепла), скрип при холоде, скрежет при заедании.
    /// Это главный диегетический индикатор холода: с падением тепла тики реже, ниже и тише, нарастает скрип.
    /// </summary>
    public sealed class MechanismLayer : ProceduralLayer
    {
        private volatile float _rate = 2.5f;    // тиков в секунду
        private volatile float _pitch = 1f;     // 0.6…1
        private volatile float _creak;          // 0…1
        private volatile float _jam;            // 0/1 (заедание)
        private volatile float _alive = 1f;     // 0…1 (затухание при замерзании/паузе)
        private volatile float _creakGain = 0.5f;

        public void Set(float ticksPerSecond, float pitch, float creak01, bool jammed, float alive01, float creakGain)
        {
            _rate = ticksPerSecond; _pitch = pitch; _creak = creak01; _jam = jammed ? 1f : 0f; _alive = alive01; _creakGain = creakGain;
        }

        private ProcAudio.Rng _rng;
        private ProcAudio.Biquad _clickBp, _grind;
        private float _phase;            // фаза тиков 0…1
        private bool _tock;              // чередование тик/так
        private float _env;              // огибающая клика
        private float _blipPhase, _blipHz, _blipEnv;
        private float _creakPhase, _creakLfo, _creakGate;
        private float _a = 1f, _j, _c;   // сглаженные

        protected override void OnInit()
        {
            _rng = new ProcAudio.Rng(0xC10C4u);
        }

        protected override void Generate(float[] mono, int frames)
        {
            int sr = SampleRate;
            float dt = 1f / sr;
            float k = Mathf.Min(1f, 8f * frames * dt);
            _a += (_alive - _a) * k; _j += (_jam - _j) * Mathf.Min(1f, 30f * frames * dt); _c += (_creak - _c) * k;

            float rate = Mathf.Max(0.05f, _rate);
            float pitch = Mathf.Clamp(_pitch, 0.3f, 1.2f);
            float tickGate = 1f - _j; // при заедании тики пропадают
            _clickBp.Set(ProcAudio.BiquadType.BandPass, 2400f * pitch, 2.2f, sr);
            _grind.Set(ProcAudio.BiquadType.BandPass, 420f + 300f * _c, 1.2f, sr);

            float creakAmp = _c * _creakGain;
            float cg = _creakGain;

            for (int i = 0; i < frames; i++)
            {
                _phase += rate * dt;
                if (_phase >= 1f)
                {
                    _phase -= 1f;
                    _tock = !_tock;
                    _env = 1f;
                    _blipHz = (_tock ? 620f : 940f) * pitch;
                    _blipEnv = 1f;
                    _blipPhase = 0f;
                }
                // Щелчок (шум через полосу) + короткий «тон» корпуса.
                float click = _clickBp.Process(_rng.Next()) * _env * 3f;
                _env *= 0.9965f - 0.0015f * (1f - pitch); // ≈ 6–12 мс
                _blipPhase += _blipHz * dt; if (_blipPhase > 1f) _blipPhase -= 1f;
                float blip = Mathf.Sin(_blipPhase * 2f * Mathf.PI) * _blipEnv * 0.45f;
                _blipEnv *= 0.9988f;

                float tick = (click + blip) * tickGate;

                // Скрип: синус с вибрато/дрожанием и случайным «прерыванием».
                float scr = 0f;
                if (creakAmp > 0.005f)
                {
                    _creakLfo += (_rng.Next() - _creakLfo) * 0.0008f;
                    _creakGate += ((_creakLfo > 0.1f ? 1f : 0f) - _creakGate) * 0.0015f;
                    float hz = (680f + 380f * _creakLfo) * (0.7f + 0.3f * pitch);
                    _creakPhase += hz * dt; if (_creakPhase > 1f) _creakPhase -= 1f;
                    float saw = _creakPhase * 2f - 1f;
                    scr = (saw * 0.5f + Mathf.Sin(_creakPhase * 2f * Mathf.PI) * 0.5f) * creakAmp * _creakGate * 0.35f;
                }

                float grind = _j > 0.01f ? _grind.Process(_rng.Next()) * _j * 1.4f * (0.6f + 0.4f * Mathf.Sin(i * 0.004f)) : 0f;

                mono[i] = (tick + scr + grind) * _a;
            }
        }
    }
}
