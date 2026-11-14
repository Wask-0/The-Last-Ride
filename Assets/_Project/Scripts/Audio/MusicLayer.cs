using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Генеративная музыка (ТЗ 8.3, 10.4): синтез и секвенсер целиком в аудио-потоке.
    /// Прогрессия Am–F–C–G на мягких пэдах (расстроенные треугольные волны, два банка голосов с кроссфейдом),
    /// редкая мелодия (пентатоника, звонкие ноты), разреженные диссонансные тона, низкий дрон и светлая кода.
    /// Настроение задают плавные параметры из <see cref="MusicDirector"/>; ноты и их порядок случайны.
    /// </summary>
    public sealed class MusicLayer : ProceduralLayer
    {
        // ---- управление (основной поток → аудио-поток)
        private volatile float _pad;          // громкость пэдов 0…1
        private volatile float _melody;       // частота/громкость мелодии 0…1
        private volatile float _dissonance;   // плотность диссонансов 0…1
        private volatile float _bright;       // яркость (срез ФНЧ) 0…1
        private volatile float _drone;        // низкий дрон 0…1
        private volatile float _chordSeconds = 9f;
        private volatile bool _codaRequest;
        private volatile bool _codaReset;
        private volatile float _codaLevel = 0.6f;

        public void Set(float pad, float melody, float dissonance, float brightness01, float drone, float chordSeconds)
        {
            _pad = pad; _melody = melody; _dissonance = dissonance; _bright = brightness01; _drone = drone; _chordSeconds = chordSeconds;
        }

        /// <summary>Запустить светлую коду (арпеджио Cmaj и яркие пэды); держится, пока не вызван <see cref="ResetCoda"/>.</summary>
        public void TriggerCoda(float level) { _codaLevel = level; _codaRequest = true; }
        public void ResetCoda() { _codaReset = true; }

        /// <summary>Текущий аккорд (0…3) и признак коды — для диагностики.</summary>
        public int ChordIndex => _chordIdx;
        public bool CodaActive => _coda;
        public int ActiveVoices { get; private set; }

        // ---- голоса
        private const int PadA = 0, PadB = 4, Mel = 8, Dis = 10, CodaV = 12, Drone = 18, VoiceCount = 19;
        private struct Voice
        {
            public float hz, amp, tgt, atk, rel, hold, p1, p2;
            public int kind; // 0 пэд, 1 мелодия, 2 диссонанс, 3 кода, 4 дрон
        }
        private readonly Voice[] _v = new Voice[VoiceCount];

        // Аккорды Am F C G: бас + 3 верхних голоса (MIDI).
        private static readonly int[][] Chords =
        {
            new[] { 45, 57, 60, 64 },
            new[] { 41, 57, 60, 65 },
            new[] { 48, 55, 60, 64 },
            new[] { 43, 55, 59, 62 }
        };
        private static readonly int[] ChordRootPc = { 9, 5, 0, 7 };
        private static readonly int[] MelodyScale = { 67, 69, 72, 74, 76, 79, 81, 84 };       // пентатоника C/Am
        private static readonly int[] DissIntervals = { 1, 6, 8, 13, 11 };                     // м2, тритон, м6, м9, б7

        private ProcAudio.Rng _rng;
        private ProcAudio.Biquad _lp;
        private float _pa, _me, _di, _br, _dr;       // сглаженные параметры
        private float _chordTimer, _melTimer, _disTimer;
        private int _chordIdx = -1;
        private bool _bankB;
        private int _melIdx = 3;
        private bool _coda;
        private float _codaT;
        private int _codaStep;
        private float _codaLevelNow = 0.6f;

        // Время вступления голосов коды (с от старта коды) и ноты MIDI.
        private static readonly float[] CodaTimes = { 0f, 3f, 6f, 9.5f, 13f, 17f };
        private static readonly int[] CodaNotes = { 48, 55, 60, 64, 67, 74 };

        protected override void OnInit()
        {
            _rng = new ProcAudio.Rng(0x5EED1234u);
            for (int i = 0; i < VoiceCount; i++)
            {
                _v[i].kind = i >= Drone ? 4 : i >= CodaV ? 3 : i >= Dis ? 2 : i >= Mel ? 1 : 0;
                _v[i].hz = 110f; _v[i].atk = 3f; _v[i].rel = 3f;
            }
            _chordTimer = 0f;       // первый аккорд сразу
            _melTimer = 5f; _disTimer = 6f;
        }

        private static float Tri(float p) => 4f * Mathf.Abs(p - 0.5f) - 1f;

        private void NoteOn(int idx, float midi, float amp, float attack, float hold, float release)
        {
            ref var v = ref _v[idx];
            v.hz = ProcAudio.MidiToHz(midi);
            v.tgt = amp; v.atk = Mathf.Max(0.01f, attack); v.rel = Mathf.Max(0.05f, release); v.hold = hold;
        }

        private void NoteOff(int idx) { _v[idx].tgt = 0f; _v[idx].hold = 0f; }

        protected override void Generate(float[] mono, int frames)
        {
            int sr = SampleRate;
            float dt = 1f / sr;
            float block = frames * dt;
            float k = Mathf.Min(1f, 2.5f * block);
            _pa += (_pad - _pa) * k; _me += (_melody - _me) * k; _di += (_dissonance - _di) * k;
            _br += (_bright - _br) * k; _dr += (_drone - _dr) * k;

            if (_codaReset) { _codaReset = false; _coda = false; _codaT = 0f; _codaStep = 0; for (int i = CodaV; i < Drone; i++) NoteOff(i); }
            if (_codaRequest) { _codaRequest = false; if (!_coda) { _coda = true; _codaT = 0f; _codaStep = 0; _codaLevelNow = _codaLevel; _chordTimer = 0f; } }

            // ---- секвенсер (блочный)
            _chordTimer -= block;
            if (_chordTimer <= 0f)
            {
                _chordTimer = Mathf.Max(3f, _chordSeconds);
                _chordIdx = (_chordIdx + 1) & 3;
                int old = _bankB ? PadB : PadA;
                _bankB = !_bankB;
                int nu = _bankB ? PadB : PadA;
                var ch = Chords[_chordIdx];
                float atk = _chordTimer * 0.45f, rel = _chordTimer * 0.6f;
                for (int i = 0; i < 4; i++)
                {
                    v_Release(old + i, rel);
                    // Бас громче в низу, верхние тише; лёгкий разброс громкости.
                    float a = (i == 0 ? 0.30f : 0.2f) * (0.85f + 0.3f * _rng.Next01());
                    NoteOn(nu + i, ch[i], a, atk, 1e9f, rel);
                }
            }

            // Мелодия: случайные ноты пентатоники, интервалы зависят от _me.
            _melTimer -= block;
            if (_melTimer <= 0f)
            {
                float rate = Mathf.Clamp01(_me);
                _melTimer = Mathf.Lerp(16f, 3.5f, rate) * (0.6f + 0.8f * _rng.Next01());
                if (rate > 0.03f && _rng.Next01() < Mathf.Lerp(0.3f, 0.9f, rate))
                {
                    _melIdx = Mathf.Clamp(_melIdx + Mathf.RoundToInt(_rng.Next() * 2.2f), 0, MelodyScale.Length - 1);
                    int idx = _v[Mel].amp <= _v[Mel + 1].amp ? Mel : Mel + 1; // берём более тихий голос — без скачка высоты
                    NoteOn(idx, MelodyScale[_melIdx], 0.34f * Mathf.Lerp(0.5f, 1f, rate), 0.04f, 0.5f + 1.5f * _rng.Next01(), 3.2f);
                }
            }

            // Диссонансы: тихие долгие тоны от тоники текущего аккорда.
            _disTimer -= block;
            if (_disTimer <= 0f)
            {
                float d = Mathf.Clamp01(_di);
                _disTimer = Mathf.Lerp(16f, 3.5f, d) * (0.6f + 0.8f * _rng.Next01());
                if (d > 0.03f)
                {
                    int pc = ChordRootPc[Mathf.Max(0, _chordIdx)];
                    int iv = DissIntervals[(int)(_rng.Next01() * DissIntervals.Length) % DissIntervals.Length];
                    int octave = 60 + 12 * ((int)(_rng.Next01() * 2f));
                    int idx = _v[Dis].amp <= _v[Dis + 1].amp ? Dis : Dis + 1;
                    NoteOn(idx, octave + pc + iv, 0.22f * Mathf.Lerp(0.5f, 1f, d), 2.5f, 3f + 5f * _rng.Next01(), 4f);
                }
            }

            // Кода: вступление голосов арпеджио, затем держатся.
            if (_coda)
            {
                _codaT += block;
                while (_codaStep < CodaNotes.Length && _codaT >= CodaTimes[_codaStep])
                {
                    NoteOn(CodaV + _codaStep, CodaNotes[_codaStep], 0.2f * _codaLevelNow * (_codaStep >= 4 ? 0.8f : 1f), 3.5f, 1e9f, 6f);
                    _codaStep++;
                }
            }

            // Дрон держится на уровне _dr.
            _v[Drone].tgt = 0.35f * _dr;
            _v[Drone].hz = ProcAudio.MidiToHz(33f); // A1
            _v[Drone].atk = 4f; _v[Drone].rel = 4f; _v[Drone].hold = 1e9f;

            // Истёкшие ноты отпускаем.
            for (int i = Mel; i < Drone; i++)
            {
                if (_v[i].hold < 1e8f && _v[i].tgt > 0f)
                {
                    _v[i].hold -= block;
                    if (_v[i].hold <= 0f) _v[i].tgt = 0f;
                }
            }

            // ---- рендер
            float cutoff = Mathf.Lerp(380f, 4200f, Mathf.Clamp01(_br));
            _lp.Set(ProcAudio.BiquadType.LowPass, cutoff, 0.6f, sr);
            float padGain = _coda ? Mathf.Max(_pa, 0f) : _pa;
            int active = 0;
            for (int i = 0; i < frames; i++) mono[i] = 0f;
            for (int vi = 0; vi < VoiceCount; vi++)
            {
                ref var v = ref _v[vi];
                if (v.amp < 1e-5f && v.tgt <= 0f) continue;
                active++;
                float upStep = dt / v.atk, downStep = dt / v.rel;
                float group = vi < Mel ? padGain : 1f; // уровень пэдов регулируется общим параметром
                float hz1 = v.hz, hz2 = v.hz * (v.kind == 4 ? 1.006f : 1.0035f);
                float inc1 = hz1 * dt, inc2 = hz2 * dt;
                float amp = v.amp, tgt = v.tgt, p1 = v.p1, p2 = v.p2;
                int kind = v.kind;
                for (int i = 0; i < frames; i++)
                {
                    if (amp < tgt) { amp += upStep; if (amp > tgt) amp = tgt; }
                    else if (amp > tgt) { amp -= downStep; if (amp < tgt) amp = tgt; }
                    p1 += inc1; if (p1 >= 1f) p1 -= 1f;
                    p2 += inc2; if (p2 >= 1f) p2 -= 1f;
                    float s;
                    switch (kind)
                    {
                        case 0: s = Tri(p1) * 0.55f + Tri(p2) * 0.45f; break;
                        case 1: s = Mathf.Sin(p1 * 6.2831853f) * 0.75f + Mathf.Sin(p1 * 12.566371f) * 0.14f + Tri(p2) * 0.1f; break;
                        case 2: s = Mathf.Sin(p1 * 6.2831853f) * 0.7f + Tri(p2) * 0.3f; break;
                        case 3: s = Mathf.Sin(p1 * 6.2831853f) * 0.65f + Mathf.Sin(p1 * 12.566371f) * 0.22f + Mathf.Sin(p1 * 18.849556f) * 0.1f + Tri(p2) * 0.15f; break;
                        default: s = Mathf.Sin(p1 * 6.2831853f) * 0.7f + Mathf.Sin(p2 * 6.2831853f) * 0.5f; break;
                    }
                    mono[i] += s * amp * group;
                }
                v.amp = amp; v.p1 = p1; v.p2 = p2;
            }
            ActiveVoices = active;
            for (int i = 0; i < frames; i++) mono[i] = _lp.Process(mono[i]);
        }

        private void v_Release(int idx, float release)
        {
            _v[idx].tgt = 0f; _v[idx].rel = Mathf.Max(0.05f, release);
        }
    }
}
