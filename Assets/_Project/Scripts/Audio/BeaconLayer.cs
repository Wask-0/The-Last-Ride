using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Гул Великого Маяка (ТЗ 8.3): низкий синус ~55 Гц + обертоны, нарастает к финалу.
    /// <c>level</c> — общая громкость, <c>richness</c> — насколько раскрыты обертоны (в финале гул «расцветает»).
    /// Обертоны слегка расстроены — медленные биения, плюс очень медленное дыхание амплитуды.
    /// </summary>
    public sealed class BeaconLayer : ProceduralLayer
    {
        private volatile float _level;
        private volatile float _rich;

        public void Set(float level01, float richness01) { _level = level01; _rich = richness01; }

        private const int Partials = 6;
        private static readonly float[] Ratio = { 1f, 2f, 3f, 4f, 5f, 6f };
        private static readonly float[] Detune = { 1f, 1.002f, 0.998f, 1.003f, 1.0015f, 0.997f };
        private static readonly float[] Weight = { 1f, 0.5f, 0.32f, 0.22f, 0.14f, 0.09f };
        private readonly float[] _ph = new float[Partials];
        private float _l, _r, _breath;
        private ProcAudio.PinkNoise _noise;
        private ProcAudio.Biquad _rumble;

        protected override void OnInit() { _noise = new ProcAudio.PinkNoise(0xB3AC0Eu); }

        protected override void Generate(float[] mono, int frames)
        {
            int sr = SampleRate;
            float dt = 1f / sr;
            float k = Mathf.Min(1f, 1.5f * frames * dt);
            _l += (_level - _l) * k; _r += (_rich - _r) * k;

            float level = Mathf.Clamp01(_l), rich = Mathf.Clamp01(_r);
            // Обертоны раскрываются квадратично с «богатством»; чем громче, тем богаче.
            float rr = rich * rich;
            _rumble.Set(ProcAudio.BiquadType.LowPass, 90f + 120f * level, 0.8f, sr);

            for (int i = 0; i < frames; i++)
            {
                _breath += dt * 0.17f; if (_breath > 1f) _breath -= 1f;
                float breathe = 0.88f + 0.12f * Mathf.Sin(_breath * 6.2831853f);
                float s = 0f;
                for (int p = 0; p < Partials; p++)
                {
                    _ph[p] += 55f * Ratio[p] * Detune[p] * dt;
                    if (_ph[p] >= 1f) _ph[p] -= 1f;
                    float w = Weight[p] * (p == 0 ? 1f : Mathf.Lerp(0.25f, 1f, rr));
                    s += Mathf.Sin(_ph[p] * 6.2831853f) * w;
                }
                float rumble = _rumble.Process(_noise.Next()) * 1.5f;
                mono[i] = (s * 0.42f + rumble * 0.25f) * level * breathe;
            }
        }
    }
}
