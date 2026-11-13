using System.Collections.Generic;
using LastRide.Core;
using UnityEngine;

namespace LastRide.Fire
{
    /// <summary>
    /// Отрезок пути с необязательными подборами (ТЗ 9). На старте оставляет у дочерних <see cref="FuelPickup"/>
    /// с флагом <c>optional</c> столько предметов, сколько даёт <see cref="DifficultyProfile.ResolvePickupCount"/>
    /// (множитель сложности с гарантированным минимумом); остальные убирает. Выбор детерминирован —
    /// зависит только от id отрезка и id предметов, поэтому одинаков при каждом запуске и после загрузки чекпоинта.
    /// </summary>
    public sealed class PickupSegment : MonoBehaviour
    {
        public string segmentId = "";

        private void Start() => Apply(GameManager.Instance != null ? GameManager.Instance.Difficulty : null);

        /// <summary>Применить профиль сложности (вызывается и при смене сложности).</summary>
        public void Apply(DifficultyProfile difficulty)
        {
            var optional = new List<FuelPickup>();
            foreach (var p in GetComponentsInChildren<FuelPickup>(true)) if (p.optional) optional.Add(p);

            int keep = difficulty != null ? difficulty.ResolvePickupCount(optional.Count) : optional.Count;
            optional.Sort((a, b) => StableHash(segmentId + "|" + a.pickupId + "|" + a.name)
                .CompareTo(StableHash(segmentId + "|" + b.pickupId + "|" + b.name)));

            for (int i = 0; i < optional.Count; i++) optional[i].SetSuppressed(i >= keep);
        }

        /// <summary>FNV-1a: одинаков на любой платформе и при каждом запуске (string.GetHashCode — нет).</summary>
        private static uint StableHash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h;
        }
    }
}
