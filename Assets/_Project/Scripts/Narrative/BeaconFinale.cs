using System.Collections;
using LastRide.Core;
using LastRide.Player;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Финал у Великого Маяка (ТЗ 3, этап 12.1): вставка Искры в лифт-гнездо и подъём кабинки.
    /// Стоит на корне Маяка в сцене Game (ставит <c>FinaleBuilder</c>). По E у гнезда (<see cref="BeaconSocket"/>):
    /// 1) управление отнимается, робот поворачивается к гнезду; 2) лампа опускается и убирается, Искра «переезжает» в лампу
    /// в кабинке (<see cref="cageLamp"/>); 3) публикуется <see cref="SparkDelivered"/> (гул, кода); 4) кабинка едет вверх за
    /// <c>finale.liftSeconds</c>, камера следит за ней; 5) <see cref="FinaleLiftArrived"/>. На время финала тепло и Искра не падают.
    /// Огни Маяка, A3_FINAL и белый фейд — 12.2/12.3 (слушают <see cref="LiftProgress01"/> и <see cref="FinaleLiftArrived"/>).
    /// </summary>
    public sealed class BeaconFinale : MonoBehaviour
    {
        public Transform cage;
        public Transform liftBottom;
        public Transform liftTop;
        public Transform socket;
        [Tooltip("Лампа в кабинке (выключена, пока Искры в ней нет).")]
        public GameObject cageLamp;

        private GameBalance.FinaleSettings _fallback;

        public const string FinalLogId = "A3_FINAL";

        public bool Started { get; private set; }

        /// <summary>Искра в кабинке, гул и кода запущены.</summary>
        public bool Delivered { get; private set; }

        public bool Arrived { get; private set; }

        /// <summary>Реплика A3_FINAL отзвучала и выдержана пауза: <see cref="FinaleCompleted"/> опубликован.</summary>
        public bool Completed { get; private set; }

        /// <summary>Ход кабинки 0..1.</summary>
        public float LiftProgress01 { get; private set; }

        private GameBalance.FinaleSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.finale;
                return _fallback ??= new GameBalance.FinaleSettings();
            }
        }

        public string PromptText => Cfg.socketPrompt;

        /// <summary>Искру можно вставить: идёт игра, робот пешком с горящей лампой, финал ещё не начат.</summary>
        public bool CanInsert
        {
            get
            {
                var gm = GameManager.Instance;
                var robot = RobotController.Active;
                return !Started && gm != null && gm.IsGameplayActive && robot != null && !robot.Seated
                       && gm.PlayerMode == PlayerMode.OnFootWithLamp && gm.Spark.IsLit && gm.Spark.Source == FireSourceKind.Lamp;
            }
        }

        private void Awake()
        {
            if (cageLamp != null) cageLamp.SetActive(false);
        }

        /// <summary>Запустить финал (E у гнезда; из отладки — вручную).</summary>
        public bool Begin()
        {
            if (Started) return false;
            Started = true;
            StartCoroutine(Run());
            return true;
        }

        private IEnumerator Run()
        {
            var gm = GameManager.Instance;
            var robot = RobotController.Active;
            var c = Cfg;

            gm.Warmth.Invulnerable = true;
            gm.Spark.Invulnerable = true;
            robot.MoveOverride = Vector2.zero;
            robot.mouseLookEnabled = false;
            gm.Tutorial.Hide();

            // 1. Поворот к гнезду.
            yield return LookAt(robot, socket.position + Vector3.up * 0.3f, c.lookSeconds);

            // 2. Лампа опускается к гнезду, Искра переезжает в кабинку.
            var held = robot.GetComponent<HeldLamp>();
            for (float e = 0f; e < c.lowerSeconds; e += Time.deltaTime)
            {
                if (held != null) held.Raise01 = 1f - e / c.lowerSeconds;
                yield return null;
            }
            if (held != null) held.SetHeld(false);
            if (cageLamp != null) cageLamp.SetActive(true);
            yield return new WaitForSeconds(c.preLiftSeconds);

            // 3. Гул и кода начинаются, кабинка едет.
            Delivered = true;
            EventBus<SparkDelivered>.Publish(new SparkDelivered());
            for (float e = 0f; e < c.liftSeconds; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / c.liftSeconds);
                LiftProgress01 = k;
                cage.position = Vector3.Lerp(liftBottom.position, liftTop.position, k);
                robot.MoveOverride = e < c.retreatSeconds ? new Vector2(0f, -1f) : Vector2.zero;
                Follow(robot, cage.position, c.cameraFollow);
                yield return null;
            }
            LiftProgress01 = 1f;
            cage.position = liftTop.position;
            Arrived = true;
            EventBus<FinaleLiftArrived>.Publish(new FinaleLiftArrived());

            // 4. Реплика создателя (шёпот) — когда фонарь уже разгорается; после неё — конец финала.
            yield return new WaitForSeconds(c.finalLogDelaySeconds);
            bool logDone = false;
            System.Action<AudioLogFinished> onFinished = e => { if (e.Log != null && e.Log.id == FinalLogId) logDone = true; };
            EventBus<AudioLogFinished>.Subscribe(onFinished);
            if (!gm.AudioLogs.Play(FinalLogId)) logDone = true; // уже звучала/нет записи — не ждём
            float waited = 0f;
            while (!logDone && waited < 60f) { waited += Time.deltaTime; yield return null; }
            EventBus<AudioLogFinished>.Unsubscribe(onFinished);
            yield return new WaitForSeconds(c.afterLogSeconds);
            Completed = true;
            EventBus<FinaleCompleted>.Publish(new FinaleCompleted());
        }

        private static void AimAngles(RobotController robot, Vector3 worldPoint, out float yaw, out float pitch)
        {
            Transform eye = robot.cameraPivot != null ? robot.cameraPivot : robot.transform;
            Vector3 d = worldPoint - eye.position;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float flat = new Vector2(d.x, d.z).magnitude;
            pitch = -Mathf.Atan2(d.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg;
        }

        private static IEnumerator LookAt(RobotController robot, Vector3 worldPoint, float seconds)
        {
            AimAngles(robot, worldPoint, out float ty, out float tp);
            float y0 = robot.Yaw, p0 = robot.Pitch;
            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / seconds);
                robot.SetLook(Mathf.LerpAngle(y0, ty, k), Mathf.Lerp(p0, tp, k));
                yield return null;
            }
            robot.SetLook(ty, tp);
        }

        private static void Follow(RobotController robot, Vector3 worldPoint, float speed)
        {
            AimAngles(robot, worldPoint, out float ty, out float tp);
            float a = 1f - Mathf.Exp(-speed * Time.deltaTime);
            robot.SetLook(Mathf.LerpAngle(robot.Yaw, ty, a), Mathf.Lerp(robot.Pitch, tp, a));
        }
    }
}
