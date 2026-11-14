using System.Collections;
using LastRide.Core;
using LastRide.Level;
using LastRide.Player;
using LastRide.ProcGen.Models;
using LastRide.UI;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Поломка фургона и постановочная сцена с лампой (ТЗ 3, акт 3), подзадача 8.5. Стоит в сцене Game (ставит PlayerRigBuilder).
    /// По <see cref="VanBrokeDown"/> (триггер на подъёме), если игрок в фургоне:
    /// 1) двигатель глохнет (<c>CanDrive = false</c>), фургон за <c>breakdown.stopSeconds</c> встаёт до завала, толчок камеры, запись A3_01;
    /// 2) после записи робот сам выходит (управление отнято), смотрит на печку, поднимает лампу в правую руку;
    ///    на середине подъёма Искра переходит из печки в лампу (<see cref="LastRide.Fire.SparkFire.MoveToLamp"/>, запас как есть);
    /// 3) акт 3 начинается мгновенно (<see cref="ActController.StartAct"/>), режим — <c>OnFootWithLamp</c>, взгляд вдоль пути,
    ///    управление возвращается; сесть в фургон больше нельзя. Инвентарь остаётся у игрока.
    /// На время сцены тепло не падает (робот стоит на улице в погоде акта 2). Если триггер сработал, когда игрок пешком,
    /// сцена ждёт, пока он сядет в фургон. Лязг двигателя — звук (этап 10) подпишется на <see cref="VanBrokeDown"/>.
    /// </summary>
    public sealed class BreakdownDirector : MonoBehaviour
    {
        public const string LogId = "A3_01";

        public VanController van;
        public RobotController robot;
        public VanBoarding boarding;

        private bool _pending, _running;
        private GameBalance.BreakdownSettings _fallback;

        /// <summary>Поломка и сцена уже произошли.</summary>
        public bool Done { get; private set; }
        public bool Running => _running;

        private GameBalance.BreakdownSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.breakdown;
                return _fallback ??= new GameBalance.BreakdownSettings();
            }
        }

        private void Start()
        {
            if (van == null) van = FindAnyObjectByType<VanController>();
            if (robot == null) robot = FindAnyObjectByType<RobotController>();
            if (boarding == null) boarding = FindAnyObjectByType<VanBoarding>();
        }

        private void OnEnable() => EventBus<VanBrokeDown>.Subscribe(OnBrokeDown);
        private void OnDisable() => EventBus<VanBrokeDown>.Unsubscribe(OnBrokeDown);

        private void OnBrokeDown(VanBrokeDown e)
        {
            if (Done || _running) return;
            _pending = true;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (!_pending || _running || Done || gm == null || !gm.IsGameplayActive) return;
            // Сцена начинается, когда игрок в фургоне (если прошёл зону пешком — ждёт посадки).
            if (gm.PlayerMode == PlayerMode.InVan && van != null && robot != null)
            {
                _pending = false;
                StartCoroutine(Run(gm));
            }
        }

        /// <summary>Запустить сцену вручную (отладка/тесты), минуя триггер.</summary>
        public void Trigger() { if (!Done && !_running) _pending = true; }

        private IEnumerator Run(GameManager gm)
        {
            _running = true;
            var c = Cfg;

            // ---- 1. Двигатель глохнет, фургон встаёт ----
            van.CanDrive = false;
            float v = van.Speed;
            van.ForcedBrakeDeceleration = Mathf.Max(v / c.stopSeconds, v * v / (2f * c.maxStopDistance));
            if (boarding != null) boarding.boardingLocked = true;
            VanCamera.AddTrauma(c.cameraTrauma);

            yield return new WaitForSeconds(c.logDelaySeconds);
            bool logDone = false;
            System.Action<AudioLogFinished> onFinished = e => { if (e.Log != null && e.Log.id == LogId) logDone = true; };
            EventBus<AudioLogFinished>.Subscribe(onFinished);
            gm.AudioLogs.Play(LogId);

            float waited = c.logDelaySeconds;
            while ((!logDone || van.Speed > 0.3f) && waited < c.maxWaitSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            EventBus<AudioLogFinished>.Unsubscribe(onFinished);
            van.ForcedBrakeDeceleration = 0f; // стоит: удержание на месте даёт «парковка» контроллера
            yield return new WaitForSeconds(c.sceneDelaySeconds);

            // ---- 2. Сцена: робот выходит, берёт лампу ----
            bool prevInvulnerable = gm.Warmth.Invulnerable;
            gm.Warmth.Invulnerable = true;
            robot.MoveOverride = Vector2.zero;
            robot.mouseLookEnabled = false;

            gm.SetPlayerMode(PlayerMode.OnFoot);
            float t = 0f;
            while (robot.Seated && t < 3f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(c.exitSeconds);

            var held = robot.GetComponent<HeldLamp>();
            if (held != null) held.SetHeld(true, 0f);
            else Debug.LogWarning("[BreakdownDirector] У робота нет HeldLamp — пересобери геймплейные префабы (Setup/Build Gameplay Prefabs).");

            Vector3 stovePoint = StovePoint();
            yield return LookAt(stovePoint, c.lookAtVanSeconds, c.stepBackSeconds);

            bool moved = false;
            for (float e = 0f; e < c.raiseSeconds; e += Time.deltaTime)
            {
                float k = e / c.raiseSeconds;
                if (held != null) held.Raise01 = k;
                if (!moved && k >= 0.5f) { moved = true; MoveSpark(gm); }
                yield return null;
            }
            if (held != null) held.Raise01 = 1f;
            if (!moved) MoveSpark(gm);
            yield return new WaitForSeconds(c.holdSeconds);

            // ---- 3. Акт 3: идём вверх с лампой ----
            gm.Acts.StartAct(ActId.Act3, true);
            gm.SetPlayerMode(PlayerMode.OnFootWithLamp);
            yield return LookAt(PathAhead(gm), c.lookAlongPathSeconds);

            robot.MoveOverride = null;
            robot.mouseLookEnabled = true;
            gm.Warmth.Invulnerable = prevInvulnerable;
            gm.Tutorial.Hint("lamp_taken", c.hintText);
            gm.Tutorial.LampTaken();

            Done = true;
            _running = false;
        }

        private static void MoveSpark(GameManager gm)
        {
            if (!gm.Spark.MoveToLamp()) Debug.LogWarning("[BreakdownDirector] Искра не перенесена в лампу: " + gm.Spark);
        }

        private Vector3 StovePoint()
        {
            // Окно с печкой — у фургона; смотрим туда, чуть выше середины.
            foreach (var t in van.GetComponentsInChildren<Transform>(true))
                if (t.name == Anchors.VanWindow) return t.position;
            return van.transform.position + Vector3.up * 1.3f;
        }

        private Vector3 PathAhead(GameManager gm)
        {
            var path = RoadPath.Load();
            Vector3 from = robot.transform.position;
            if (path == null) return from + robot.transform.forward * 20f;
            gm.Progress.Tick(gm); // свежее положение на оси
            var s = path.Evaluate(Mathf.Min(gm.Progress.Distance + 30f, path.Length));
            return s.position + Vector3.up * 1.2f;
        }

        /// <param name="stepBackSeconds">Столько секунд робот идёт назад (от цели взгляда), пока поворачивается.</param>
        private IEnumerator LookAt(Vector3 worldPoint, float seconds, float stepBackSeconds = 0f)
        {
            Transform eye = robot.cameraPivot != null ? robot.cameraPivot : robot.transform;
            Vector3 d = worldPoint - eye.position;
            float targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float flat = new Vector2(d.x, d.z).magnitude;
            float targetPitch = -Mathf.Atan2(d.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg;
            float yaw0 = robot.Yaw, pitch0 = robot.Pitch;
            if (stepBackSeconds > 0f) robot.MoveOverride = new Vector2(0f, -1f);
            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / seconds);
                robot.SetLook(Mathf.LerpAngle(yaw0, targetYaw, k), Mathf.Lerp(pitch0, targetPitch, k));
                if (e >= stepBackSeconds) robot.MoveOverride = Vector2.zero;
                yield return null;
            }
            robot.MoveOverride = Vector2.zero;
            robot.SetLook(targetYaw, targetPitch);
        }
    }
}
