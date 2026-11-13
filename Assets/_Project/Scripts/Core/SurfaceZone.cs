using System.Collections.Generic;
using UnityEngine;

namespace LastRide.Core
{
    /// <summary>
    /// Зона особой поверхности (лёд, глубокий снег) — ТЗ 4.5, 4.6.
    /// Форма — BoxCollider-триггер на этом же объекте (может быть повёрнут).
    /// Общая для фургона и для робота: оба спрашивают <see cref="Surfaces.At"/>.
    /// При пересечении зон побеждает больший <see cref="priority"/>.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SurfaceZone : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Ice;
        [Tooltip("При пересечении зон выигрывает больший приоритет.")]
        public int priority;

        private static readonly List<SurfaceZone> Active = new List<SurfaceZone>();
        private BoxCollider _box;

        public static IReadOnlyList<SurfaceZone> All => Active;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            Surfaces.Resolver = Resolve;
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        /// <summary>Точка внутри коробки зоны (с учётом поворота и масштаба объекта).</summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            Vector3 local = transform.InverseTransformPoint(worldPoint) - _box.center;
            Vector3 h = _box.size * 0.5f;
            return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
        }

        private static SurfaceType Resolve(Vector3 worldPoint)
        {
            SurfaceType result = SurfaceType.Snow;
            int best = int.MinValue;
            for (int i = 0; i < Active.Count; i++)
            {
                var z = Active[i];
                if (z.priority <= best || !z.Contains(worldPoint)) continue;
                best = z.priority;
                result = z.type;
            }
            return result;
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = type == SurfaceType.Ice ? new Color(0.4f, 0.8f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = type == SurfaceType.Ice ? new Color(0.4f, 0.8f, 1f, 0.9f) : new Color(0.9f, 0.9f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
