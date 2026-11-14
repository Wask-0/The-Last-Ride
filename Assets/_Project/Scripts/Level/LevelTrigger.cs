using System.Collections.Generic;
using LastRide.Core;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Level
{
    public enum LevelTriggerKind
    {
        /// <summary>Начало акта: переключает погоду (<c>Weather.SetAct</c>) и шлёт <see cref="StoryTriggerFired"/>.</summary>
        ActStart,
        /// <summary>Чекпоинт: шлёт <see cref="CheckpointReached"/> (сохранение — этап 11).</summary>
        Checkpoint,
        /// <summary>Сюжетная точка (аудиозапись и т.п.): шлёт <see cref="StoryTriggerFired"/> с id.</summary>
        Story,
        /// <summary>Подсказка обучения: шлёт <see cref="TutorialHintRequested"/>.</summary>
        Tutorial,
        /// <summary>Поломка фургона: <see cref="VanBrokeDown"/> + <see cref="StoryTriggerFired"/>.</summary>
        Breakdown,
        /// <summary>Подход к Вершине: <see cref="StoryTriggerFired"/> (музыка/гул Маяка).</summary>
        Summit
    }

    /// <summary>
    /// Триггерная зона на пути (ТЗ 5): BoxCollider-триггер поперёк дороги. Срабатывает, когда в неё входит фургон или робот
    /// (при посадке робот «выключен» — считается фургон). Зоны ставит <c>The Last Ride/Level/Build Triggers</c> по маркерам оси.
    /// Сработавшие одноразовые зоны запоминаются на сессию: <see cref="Fired"/> / <see cref="MarkFired"/> / <see cref="ClearFired"/>
    /// (чекпоинты 11.x сохраняют и восстанавливают этот набор).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LevelTrigger : MonoBehaviour
    {
        public LevelTriggerKind kind;
        public string id;
        [Tooltip("Для ActStart: какой акт начинается")] public ActId act = ActId.Act2;
        [Tooltip("Расстояние вдоль оси пути, м (для отладки и порядка)")] public float distance;
        [Tooltip("Для Tutorial: текст подсказки")] [TextArea] public string hintText;
        public float hintDuration = 6f;
        public bool once = true;

        private static readonly HashSet<string> FiredIds = new HashSet<string>();
        private static readonly List<LevelTrigger> Registry = new List<LevelTrigger>();
        private int _inside;

        public static IReadOnlyCollection<string> Fired => FiredIds;
        public static IReadOnlyList<LevelTrigger> All => Registry;

        public static void ClearFired() => FiredIds.Clear();

        public static void MarkFired(IEnumerable<string> ids)
        {
            if (ids != null) foreach (var i in ids) FiredIds.Add(i);
        }

        public static bool HasFired(string triggerId) => FiredIds.Contains(triggerId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            FiredIds.Clear();
            Registry.Clear();
        }

        private void OnEnable()
        {
            Registry.Add(this);
            Registry.Sort((a, b) => a.distance.CompareTo(b.distance));
        }

        private void OnDisable() => Registry.Remove(this);

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (_inside++ == 0) Fire();
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other) && _inside > 0) _inside--;
        }

        private static bool IsPlayer(Collider c) =>
            c.GetComponentInParent<VanController>() != null || c.GetComponentInParent<RobotController>() != null;

        /// <summary>Сработать (из игры — при входе в зону; из отладки/тестов — вручную). False — зона уже срабатывала.</summary>
        public bool Fire()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsGameplayActive) return false;
            if (once && !FiredIds.Add(id)) return false;

            switch (kind)
            {
                case LevelTriggerKind.ActStart:
                    gm.Acts.StartAct(act);
                    Story();
                    break;
                case LevelTriggerKind.Checkpoint:
                    EventBus<CheckpointReached>.Publish(new CheckpointReached { CheckpointId = id });
                    break;
                case LevelTriggerKind.Tutorial:
                    gm.Tutorial.ShowTriggerHint(id, hintText, hintDuration);
                    break;
                case LevelTriggerKind.Breakdown:
                    EventBus<VanBrokeDown>.Publish(new VanBrokeDown());
                    Story();
                    break;
                default:
                    Story();
                    break;
            }
            return true;
        }

        private void Story() => EventBus<StoryTriggerFired>.Publish(new StoryTriggerFired { TriggerId = id });

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = kind switch
            {
                LevelTriggerKind.Checkpoint => new Color(0.3f, 1f, 0.4f, 0.8f),
                LevelTriggerKind.ActStart => new Color(1f, 0.6f, 0.1f, 0.9f),
                LevelTriggerKind.Breakdown => new Color(1f, 0.2f, 0.2f, 0.9f),
                LevelTriggerKind.Tutorial => new Color(0.4f, 0.8f, 1f, 0.8f),
                LevelTriggerKind.Summit => new Color(1f, 1f, 0.4f, 0.9f),
                _ => new Color(0.8f, 0.5f, 1f, 0.8f)
            };
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 6f, id);
        }
#endif
    }
}
