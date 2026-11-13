using System.Collections.Generic;
using UnityEngine;

namespace LastRide.UI
{
    /// <summary>
    /// Текущая подсказка взаимодействия внизу по центру (ТЗ 2.2, 6.1): «[E] Подобрать доски».
    /// Источники (робот, фургон, постановки) ставят текст через <see cref="Show"/> со своим приоритетом;
    /// <see cref="Current"/> — самый приоритетный непросроченный. Рисует <see cref="InteractionPromptView"/>.
    /// </summary>
    public static class InteractionPrompt
    {
        private struct Entry { public string text; public int priority; public float expiresAt; }

        private static readonly Dictionary<object, Entry> Entries = new Dictionary<object, Entry>();

        /// <summary>Подсказка вида «[E] Действие».</summary>
        public static string Key(string key, string action) => "[" + key + "] " + action;

        /// <summary>Показать/обновить подсказку источника. <paramref name="duration"/> &gt; 0 — исчезнет сама через столько секунд.</summary>
        public static void Show(object owner, string text, int priority = 0, float duration = 0f)
        {
            if (owner == null || string.IsNullOrEmpty(text)) { Hide(owner); return; }
            Entries[owner] = new Entry
            {
                text = text,
                priority = priority,
                expiresAt = duration > 0f ? Time.unscaledTime + duration : float.PositiveInfinity
            };
        }

        public static void Hide(object owner)
        {
            if (owner != null) Entries.Remove(owner);
        }

        /// <summary>Самая приоритетная подсказка или null.</summary>
        public static string Current
        {
            get
            {
                string best = null;
                int bestPriority = int.MinValue;
                float now = Time.unscaledTime;
                List<object> expired = null;
                foreach (var kv in Entries)
                {
                    if (kv.Value.expiresAt < now) { (expired ??= new List<object>()).Add(kv.Key); continue; }
                    if (kv.Value.priority > bestPriority) { bestPriority = kv.Value.priority; best = kv.Value.text; }
                }
                if (expired != null) foreach (var k in expired) Entries.Remove(k);
                return best;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Entries.Clear();
    }
}
