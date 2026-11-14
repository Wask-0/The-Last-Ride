using System.Collections;
using System.Text;
using LastRide.Core;
using LastRide.Level;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Автопилот для замера пейсинга (этап 13, только Editor/Development): едет фургоном или идёт роботом по оси пути и пишет
    /// время по отрезкам. Это ВЕРХНЯЯ граница скорости (без остановок за топливом, без раздумий) — живое прохождение дольше.
    /// Запуск: <c>FindAnyObjectByType&lt;PacingBot&gt;().DriveVan(from, to)</c> / <c>WalkRobot(from, to)</c>; результат — <see cref="Report"/>.
    /// </summary>
    public sealed class PacingBot : MonoBehaviour
    {
        public string Report { get; private set; } = "";
        public bool Running { get; private set; }
        public bool Failed { get; private set; }

        [Tooltip("Расстояние между строками отчёта, м.")]
        public float reportEvery = 100f;

        private readonly StringBuilder _log = new StringBuilder();

        public void DriveVan(float from, float to, float timeScale = 4f)
        {
            if (Running) return;
            StartCoroutine(Drive(from, to, timeScale));
        }

        public void WalkRobot(float from, float to, float timeScale = 4f, bool throwFuel = true, bool rest = false)
        {
            if (Running) return;
            StartCoroutine(Walk(from, to, timeScale, throwFuel, rest));
        }

        private IEnumerator Drive(float from, float to, float timeScale)
        {
            Begin("DRIVE", timeScale);
            var gm = GameManager.Instance;
            var van = FindAnyObjectByType<VanController>();
            var path = RoadPath.Load();
            gm.Spark.Invulnerable = true;
            gm.Warmth.Invulnerable = true;
            gm.SetPlayerMode(PlayerMode.InVan);
            var s0 = path.Evaluate(from);
            van.ResetTo(s0.position + Vector3.up * 0.6f, Quaternion.LookRotation(s0.flatTangent, Vector3.up));
            gm.Progress.ResetHint();

            float hint = from, nextReport = from + reportEvery, t0 = Time.time, stuckFor = 0f, lastD = from;
            while (true)
            {
                var pr = path.Project(van.transform.position, hint);
                hint = pr.distance;
                if (hint >= to) break;

                float v = van.Speed;
                float look = 10f + v * 0.9f;
                var target = path.Evaluate(Mathf.Min(path.Length, hint + look)).position;
                Vector3 local = van.transform.InverseTransformPoint(target);
                float steer = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg / 22f, -1f, 1f);

                // Скорость по кривизне впереди.
                float vt = 14f;
                var t = path.Evaluate(hint).flatTangent;
                for (float s = 15f; s <= 60f; s += 15f)
                {
                    float ang = Vector3.Angle(t, path.Evaluate(Mathf.Min(path.Length, hint + s)).flatTangent) * Mathf.Deg2Rad;
                    float kappa = ang / s;
                    if (kappa > 1e-4f) vt = Mathf.Min(vt, Mathf.Sqrt(6f / kappa));
                }
                vt = Mathf.Max(vt, 4f);
                float throttle = Mathf.Clamp((vt - v) * 0.6f, -1f, 1f);
                van.InputOverride = new Vector2(steer, throttle);
                van.HandbrakeOverride = false;

                // Застряли?
                stuckFor = (v < 0.4f && Mathf.Abs(hint - lastD) < 0.05f) ? stuckFor + Time.deltaTime : 0f;
                lastD = hint;
                if (stuckFor > 8f) { Fail($"застрял на {hint:0} м"); break; }

                if (hint >= nextReport) { Row(hint, t0, $"v {v:0.0}"); nextReport += reportEvery; }
                yield return null;
            }
            van.InputOverride = null;
            van.HandbrakeOverride = null;
            Finish(t0, "DRIVE");
        }

        private IEnumerator Walk(float from, float to, float timeScale, bool throwFuel, bool rest)
        {
            Begin("WALK", timeScale);
            var gm = GameManager.Instance;
            var robot = RobotController.Active;
            var path = RoadPath.Load();
            gm.Progress.ResetHint();
            var s0 = path.Evaluate(from);
            robot.mouseLookEnabled = false;
            robot.Teleport(s0.position + Vector3.up * 0.8f, Mathf.Atan2(s0.flatTangent.x, s0.flatTangent.z) * Mathf.Rad2Deg);

            float hint = from, nextReport = from + reportEvery, t0 = Time.time, rested = 0f;
            bool resting = false;
            while (true)
            {
                if (gm.State != GameState.Playing) { Fail($"состояние {gm.State} на {hint:0} м (смерть?)"); break; }
                var pr = path.Project(robot.transform.position, hint, 40f);
                hint = pr.distance;
                if (hint >= to) break;

                var target = path.Evaluate(Mathf.Min(path.Length, hint + 7f)).position;
                Vector3 d = target - robot.transform.position;
                robot.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                // «Живой» игрок: когда тепло на исходе — останавливается, пока лампа не согреет.
                if (rest)
                {
                    if (!resting && gm.Warmth.Fraction < 0.2f) resting = true;
                    else if (resting && gm.Warmth.Fraction > 0.9f) resting = false;
                    if (resting) rested += Time.deltaTime;
                }
                robot.MoveOverride = resting ? Vector2.zero : new Vector2(0f, 1f);

                if (throwFuel && gm.Spark.IsLit && gm.Spark.Reserve < 60f)
                    foreach (var f in gm.Config.fuelTypes)
                        if (f != null && gm.Inventory.Count(f) > 0) { gm.Spark.ThrowFuel(f); break; }

                if (hint >= nextReport)
                {
                    Row(hint, t0, $"тепло {gm.Warmth.Value:0}, v {robot.Speed:0.0}, огонь {gm.Spark.Reserve:0}");
                    nextReport += reportEvery;
                }
                yield return null;
            }
            robot.MoveOverride = null;
            robot.mouseLookEnabled = true;
            _log.AppendLine($"отдыхал у лампы: {rested:0} с");
            Finish(t0, "WALK");
        }


        /// <summary>
        /// Проверка чекпоинтов (13.2): по цепочке F2 → на каждом чекпоинте портит состояние (топливо, огонь, тепло, позицию),
        /// «убивает» игрока и сверяет, что восстановление вернуло снимок чекпоинта (инвентарь, Искра ≥ защиты от тупика, тепло, акт,
        /// режим, позиции, поломка фургона). Отчёт — <see cref="Report"/>.
        /// </summary>
        public void CheckpointSweep(float timeScale = 4f)
        {
            if (Running) return;
            StartCoroutine(Sweep(timeScale));
        }

        private IEnumerator Sweep(float timeScale)
        {
            Begin("SWEEP", timeScale);
            var gm = GameManager.Instance;
            float t0 = Time.time;
            if (gm.Tutorial.State == LastRide.Narrative.PrologueState.FindJar) gm.Tutorial.TakeJar();
            if (gm.Tutorial.State == LastRide.Narrative.PrologueState.JarTaken) gm.Tutorial.PlaceSparkInStove();
            gm.Warmth.Invulnerable = true;
            gm.Spark.Invulnerable = true;
            var fuel = gm.Config.fuelTypes[0];
            var breakdown = FindAnyObjectByType<LastRide.Narrative.BreakdownDirector>();
            int passed = 0, failed = 0, idle = 0;
            string lastId = gm.Checkpoints.LastId;

            for (int step = 0; step < 14 && idle < 3; step++)
            {
                DebugOverlay.TeleportToNextCheckpoint(gm);
                // Сцена поломки идёт сама — ждём её конца.
                float wait = 0f;
                yield return new WaitForSeconds(0.6f);
                while (breakdown != null && (breakdown.Running || (gm.PlayerMode == PlayerMode.InVan && !breakdown.Done && wait < 5f)) && wait < 120f)
                { wait += Time.unscaledDeltaTime; yield return null; }
                yield return new WaitForSeconds(0.8f);

                if (gm.Checkpoints.LastId == lastId) { idle++; _log.AppendLine($"  (шаг {step}: новый чекпоинт не взят, breakdownDone={breakdown != null && breakdown.Done})"); continue; }
                idle = 0;
                lastId = gm.Checkpoints.LastId;
                var snap = gm.Checkpoints.Last;

                // Портим состояние.
                gm.Inventory.Add(fuel, 5);
                gm.Spark.PlaceSpark(gm.Spark.Source, 3f);
                gm.Warmth.SetValue(4f);
                var robot = RobotController.Active;
                var van = FindAnyObjectByType<VanController>();
                if (gm.PlayerMode == PlayerMode.InVan) van.ResetTo(van.transform.position + new Vector3(0f, 0f, -30f), van.transform.rotation);
                else robot.Teleport(robot.transform.position + new Vector3(40f, 0f, 0f), 90f);
                gm.Death.Die();

                float rw = 0f;
                while ((gm.State != GameState.Playing || gm.Death.Busy) && rw < 20f) { rw += Time.unscaledDeltaTime; yield return null; }
                yield return new WaitForSeconds(0.3f);

                // Сверяем.
                var errs = new StringBuilder();
                int invNow = gm.Inventory.Total, invSnap = 0; foreach (var st in snap.inventory) invSnap += st.count;
                if (invNow != invSnap) errs.Append($" инвентарь {invNow}≠{invSnap};");
                if (gm.Spark.Source != snap.spark.source && snap.spark.state != LastRide.Fire.SparkState.Unlit) errs.Append(" источник огня;");
                if (snap.spark.state != LastRide.Fire.SparkState.Unlit && gm.Spark.Reserve < gm.Balance.fire.restoreMinBurnSeconds - 5f) errs.Append($" запас {gm.Spark.Reserve:0}<{gm.Balance.fire.restoreMinBurnSeconds:0};");
                if (gm.Warmth.Value < 95f) errs.Append($" тепло {gm.Warmth.Value:0};");
                if (gm.Acts.Current != snap.act) errs.Append($" акт {gm.Acts.Current}≠{snap.act};");
                if (gm.PlayerMode != snap.playerMode) errs.Append($" режим {gm.PlayerMode}≠{snap.playerMode};");
                if (snap.playerMode == PlayerMode.InVan)
                { float dv = Vector3.Distance(FindAnyObjectByType<VanController>().transform.position, snap.van.position); if (dv > 3f) errs.Append($" фургон смещён на {dv:0} м;"); }
                else
                { float dr = Vector3.Distance(RobotController.Active.transform.position, snap.robot.position); if (dr > 3f) errs.Append($" робот смещён на {dr:0} м;"); }
                if (breakdown != null && breakdown.Done != snap.breakdownDone) errs.Append(" флаг поломки;");
                var vanNow = FindAnyObjectByType<VanController>();
                if (snap.breakdownDone && vanNow.CanDrive) errs.Append(" фургон на ходу после поломки;");
                if (!snap.breakdownDone && !vanNow.CanDrive) errs.Append(" фургон не едет;");
                if (gm.State != GameState.Playing) errs.Append($" состояние {gm.State};");

                bool ok = errs.Length == 0;
                if (ok) passed++; else failed++;
                _log.AppendLine($"{(ok ? "ОК " : "СБОЙ")} {lastId} ({snap.act}, {snap.playerMode}, запас {snap.spark.reserve:0}→{gm.Spark.Reserve:0}){errs}");
                Report = _log.ToString();
            }
            Failed = failed > 0;
            _log.AppendLine($"SWEEP: ок {passed}, сбоев {failed}, {(Time.time - t0):0} с игрового времени");
            Report = _log.ToString();
            Running = false;
            gm.DebugTimeMultiplier = 1f;
        }

        // ------------------------------------------------------------------ отчёт

        private void Begin(string what, float timeScale)
        {
            Running = true;
            Failed = false;
            _log.Clear();
            _log.AppendLine($"{what}: ×{timeScale:0}");
            GameManager.Instance.DebugTimeMultiplier = timeScale;
            Report = _log.ToString();
        }

        private void Row(float d, float t0, string extra)
        {
            _log.AppendLine($"{d,6:0} м  t={(Time.time - t0):0} с  {extra}");
            Report = _log.ToString();
        }

        private void Fail(string why)
        {
            Failed = true;
            _log.AppendLine("СБОЙ: " + why);
        }

        private void Finish(float t0, string what)
        {
            _log.AppendLine($"{what}: итого {(Time.time - t0):0} с игрового времени{(Failed ? " (прервано)" : "")}");
            Report = _log.ToString();
            Running = false;
            GameManager.Instance.DebugTimeMultiplier = 1f;
        }
    }
}
