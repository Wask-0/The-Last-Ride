using UnityEngine;
using UnityEngine.Audio;

namespace LastRide.Audio
{
    /// <summary>
    /// База непрерывных процедурных слоёв (ветер, огонь, двигатель, гул…) на <c>OnAudioFilterRead</c> (ТЗ 8.3).
    /// Слой — AudioSource с беззвучным зацикленным клипом; сигнал пишется в буфер, затем проходит через
    /// мягкий лимитер слоя, плавное изменение громкости (без щелчков) и уходит в группу микшера.
    /// Управляющие параметры задаются из основного потока (float/volatile), синтез — в аудио-потоке без выделений памяти.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public abstract class ProceduralLayer : MonoBehaviour
    {
        protected int SampleRate { get; private set; }
        protected int Channels { get; private set; }

        private AudioSource _source;
        private volatile float _targetGain;
        private float _gain;                     // текущее значение (аудио-поток)
        private volatile float _limitThreshold = 0.8f;
        private volatile bool _ready;

        /// <summary>Целевая громкость слоя 0…1 (плавно догоняется ~30 мс в аудио-потоке).</summary>
        public float Gain { get => _targetGain; set => _targetGain = Mathf.Clamp01(value); }

        public AudioSource Source => _source;

        // Диагностика (аудио-поток пишет, основной читает; для F1-оверлея и тестов).
        private float _lastSample;
        public float Peak { get; private set; }
        public float Rms { get; private set; }
        /// <summary>Максимальный пик с момента <see cref="ResetStats"/>.</summary>
        public float MaxPeak { get; private set; }
        /// <summary>Максимальный скачок между соседними сэмплами (большие значения = щелчки).</summary>
        public float MaxStep { get; private set; }
        public void ResetStats() { MaxPeak = 0f; MaxStep = 0f; }

        public void Init(AudioMixerGroup group, float limitThreshold)
        {
            SampleRate = ProcAudio.SampleRate;
            _limitThreshold = limitThreshold;
            _source = GetComponent<AudioSource>();
            _source.clip = ProcAudio.SilentLoop;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.outputAudioMixerGroup = group;
            OnInit();
            _ready = true;
            _source.Play();
        }

        protected virtual void OnInit() { }

        /// <summary>Заполнить моно-блок (длина = frames). Вызывается из аудио-потока.</summary>
        protected abstract void Generate(float[] mono, int frames);

        private float[] _mono = new float[4096];

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_ready) return;
            Channels = channels;
            int frames = data.Length / channels;
            if (_mono.Length < frames) _mono = new float[frames];
            Generate(_mono, frames);

            float target = _targetGain, g = _gain, th = _limitThreshold;
            float step = 1f / (0.03f * SampleRate);
            float peak = 0f, sum = 0f, maxStep = 0f, prev = _lastSample;
            for (int i = 0; i < frames; i++)
            {
                if (g < target) { g += step; if (g > target) g = target; }
                else if (g > target) { g -= step; if (g < target) g = target; }
                float s = ProcAudio.Limit(_mono[i] * g, th);
                if (float.IsNaN(s)) s = 0f;
                int k = i * channels;
                for (int c = 0; c < channels; c++) data[k + c] = s;
                float a = s < 0f ? -s : s;
                if (a > peak) peak = a;
                sum += s * s;
                float d = s - prev; if (d < 0f) d = -d;
                if (d > maxStep) maxStep = d;
                prev = s;
            }
            _lastSample = prev;
            _gain = g;
            Peak = peak;
            Rms = Mathf.Sqrt(sum / Mathf.Max(1, frames));
            if (peak > MaxPeak) MaxPeak = peak;
            if (maxStep > MaxStep) MaxStep = maxStep;
        }
    }
}
