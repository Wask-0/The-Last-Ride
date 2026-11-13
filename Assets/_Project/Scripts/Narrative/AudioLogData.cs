using System;
using LastRide.Core;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>Аудиозапись создателя с субтитрами (ТЗ 7.1).</summary>
    [CreateAssetMenu(menuName = "The Last Ride/Audio Log", fileName = "AudioLog")]
    public sealed class AudioLogData : ScriptableObject
    {
        [Serializable]
        public sealed class SubtitleLine
        {
            [TextArea(1, 3)] public string text;
            [Tooltip("Длительность строки, с. 0 — авто (по числу символов). Используется, когда есть клип.")]
            [Min(0)] public float duration;
        }

        public string id = "A1_01";
        public ActId act = ActId.Act1;
        [Tooltip("Обязательная реплика (★ в ТЗ).")]
        public bool required;
        [Tooltip("Голос создателя. Пусто — играют радиопомехи, субтитры по авто-таймингу.")]
        public AudioClip clip;
        public SubtitleLine[] lines = Array.Empty<SubtitleLine>();

        /// <summary>
        /// Длительности строк. Без клипа — авто-тайминг. С клипом — ручные значения
        /// (0 → авто), затем всё масштабируется под длину клипа.
        /// </summary>
        public float[] ResolveLineDurations(float secondsPerChar, float minLineSeconds)
        {
            var result = new float[lines.Length];
            float total = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                float auto = AutoDuration(lines[i].text, secondsPerChar, minLineSeconds);
                result[i] = clip != null && lines[i].duration > 0f ? lines[i].duration : auto;
                total += result[i];
            }

            if (clip != null && total > 0f)
            {
                float k = clip.length / total;
                for (int i = 0; i < result.Length; i++) result[i] *= k;
            }
            return result;
        }

        public static float AutoDuration(string text, float secondsPerChar, float minLineSeconds)
        {
            int n = string.IsNullOrEmpty(text) ? 0 : text.Length;
            return Mathf.Max(minLineSeconds, n * secondsPerChar);
        }
    }
}
