using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Финальный лимитер на <see cref="AudioListener"/> (10.1): мягкое колено от <c>threshold</c> до <c>ceiling</c>,
    /// плюс счётчик пика для отладки. Не даёт выходу превысить потолок даже при сложении слоёв.
    /// </summary>
    public sealed class MasterLimiter : MonoBehaviour
    {
        private volatile float _threshold = 0.7f;
        private volatile float _ceiling = 0.95f;
        private float _peak;     // пик последнего блока (аудио-поток)
        private float _maxPeak;  // максимум с момента сброса
        private long _clipped;   // число сэмплов, где ограничение сработало

        public float LastPeak => _peak;
        public float MaxPeak => _maxPeak;
        public long LimitedSamples => _clipped;

        public void Configure(float threshold, float ceiling)
        {
            _ceiling = Mathf.Clamp(ceiling, 0.1f, 1f);
            _threshold = Mathf.Clamp(threshold, 0.1f, _ceiling - 0.01f);
        }

        public void ResetStats() { _maxPeak = 0f; _clipped = 0; }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float th = _threshold, ceil = _ceiling, peak = 0f;
            float knee = ceil - th;
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                float a = x < 0f ? -x : x;
                if (a > th)
                {
                    // th…∞ → th…ceil по мягкой кривой; предел ceil.
                    float y = th + knee * ProcAudio.SoftClip((a - th) / knee * 1.5f) ;
                    data[i] = x < 0f ? -y : y;
                    a = y;
                    _clipped++;
                }
                if (a > peak) peak = a;
            }
            _peak = peak;
            if (peak > _maxPeak) _maxPeak = peak;
        }
    }
}
