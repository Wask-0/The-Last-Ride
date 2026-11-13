using System.Collections.Generic;
using UnityEngine;

namespace LastRide.Fire
{
    /// <summary>
    /// Все подбираемые предметы сцены и id уже подобранных (ТЗ 4.7: «предметы, подобранные после чекпоинта,
    /// возвращаются на места»). Сохранение (11.1) пишет <see cref="CollectedIds"/>, восстановление вызывает <see cref="ApplyCollected"/>.
    /// </summary>
    public static class PickupRegistry
    {
        private static readonly List<FuelPickup> All = new List<FuelPickup>();
        private static readonly HashSet<string> Collected = new HashSet<string>();

        public static IReadOnlyCollection<string> CollectedIds => Collected;

        internal static void Register(FuelPickup p) { if (!All.Contains(p)) All.Add(p); }
        internal static void Unregister(FuelPickup p) => All.Remove(p);

        internal static bool IsCollected(string id) => !string.IsNullOrEmpty(id) && Collected.Contains(id);
        internal static void MarkCollected(string id) { if (!string.IsNullOrEmpty(id)) Collected.Add(id); }

        /// <summary>Заменить набор подобранных id: подобранные скрываются, остальные возвращаются на места.</summary>
        public static void ApplyCollected(IEnumerable<string> ids)
        {
            Collected.Clear();
            if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) Collected.Add(id);
            foreach (var p in All) if (p != null) p.Refresh();
        }

        /// <summary>Новая игра: ничего не подобрано.</summary>
        public static void Reset() => ApplyCollected(null);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() { All.Clear(); Collected.Clear(); }
    }
}
