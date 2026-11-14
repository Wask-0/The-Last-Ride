using LastRide.Core;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Level
{
    /// <summary>
    /// Где игрок на оси пути: расстояние, смещение от оси, акт по положению. Принадлежит <see cref="GameManager"/>,
    /// тикает раз в кадр в состоянии Playing. В акте 3 передаёт погоде прогресс подъёма (<c>Weather.SetProgress01</c>) —
    /// метель стихает к Вершине (ТЗ 3). Без собранного <c>RoadPath</c> ничего не делает.
    /// </summary>
    public sealed class LevelProgress
    {
        private float _hint = -1f;
        private VanController _van;

        public bool Valid { get; private set; }
        /// <summary>Расстояние вдоль оси, м.</summary>
        public float Distance { get; private set; }
        /// <summary>Смещение от оси, м (плюс — справа по ходу).</summary>
        public float Lateral { get; private set; }
        /// <summary>Горизонтальное расстояние до оси, м.</summary>
        public float AxisDistance { get; private set; }
        /// <summary>Акт по положению на оси (может отличаться от текущего акта игры, пока зона-триггер не сработала).</summary>
        public ActId ActByPosition { get; private set; } = ActId.Act1;
        public RoadPath Path { get; private set; }

        public void Tick(GameManager gm)
        {
            Path = RoadPath.Load();
            if (Path == null || !TryGetPlayerPosition(gm, out var pos)) { Valid = false; return; }

            var pr = Path.Project(pos, _hint);
            if (pr.axisDistance > 120f) pr = Path.Project(pos); // далеко от подсказки — искать по всему пути
            _hint = pr.distance;
            Distance = pr.distance;
            Lateral = pr.lateral;
            AxisDistance = pr.axisDistance;
            ActByPosition = Path.ActAt(pr.distance);
            Valid = true;

            if (gm.Weather != null && gm.Weather.Act == ActId.Act3)
                gm.Weather.SetProgress01(Path.TrailProgress01(pr.distance));
        }

        /// <summary>Сбросить подсказку поиска (телепорт, чекпоинт).</summary>
        public void ResetHint() => _hint = -1f;

        private bool TryGetPlayerPosition(GameManager gm, out Vector3 pos)
        {
            var robot = RobotController.Active;
            if (gm.PlayerMode != PlayerMode.InVan && robot != null) { pos = robot.transform.position; return true; }
            if (_van == null) _van = Object.FindAnyObjectByType<VanController>();
            if (_van == null) { pos = default; return false; }
            pos = _van.transform.position;
            return true;
        }

        public override string ToString()
        {
            if (Path == null) return "RoadPath не собран (The Last Ride/Level/Build Road Path)";
            if (!Valid) return "нет фургона/робота";
            var s = Path.Evaluate(Distance);
            return $"{ActByPosition} {Distance:0} из {Path.Length:0} м, от оси {Lateral:+0.0;-0.0} м (ширина {s.width:0.#}), уклон {s.slopeDeg:0.0}°";
        }
    }
}
