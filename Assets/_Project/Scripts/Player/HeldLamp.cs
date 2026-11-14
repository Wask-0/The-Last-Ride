using LastRide.Core;
using UnityEngine;

namespace LastRide.Player
{
    /// <summary>
    /// Лампа в руке робота от 1-го лица (ТЗ 3, акт 3): процедурная «анимация» без клипов.
    /// <see cref="Raise01"/> 0 — рука опущена, лампа ниже кадра; 1 — лампа на ручке в правой руке перед собой.
    /// Правая рука поворачивается к кисти, лампа висит на ручке; при ходьбе лёгкое покачивание и раскачивание лампы.
    /// Свет и искры лампы ведёт <c>SparkGlow</c> (вид Lamp) на самой лампе. Положения — <c>GameBalance.lampHand</c>.
    /// Вешается на корень робота (ставит билдер геймплейных префабов); пока <see cref="Held"/> = false — скрыта и не трогает руку.
    /// </summary>
    public sealed class HeldLamp : MonoBehaviour
    {
        [Tooltip("Лампа (вариант префаба Lamp) под точкой глаз.")]
        public Transform lamp;
        [Tooltip("Правая рука от 1-го лица (FPArm_R).")]
        public Transform rightArm;

        private Vector3 _armRestPos;
        private Quaternion _armRestRot;
        private bool _restCaptured;
        private float _raise01;
        private float _sway;
        private float _swayAmount;
        private RobotController _robot;
        private GameBalance.LampHandSettings _fallback;

        public bool Held { get; private set; }

        /// <summary>Подъём лампы 0..1.</summary>
        public float Raise01
        {
            get => _raise01;
            set => _raise01 = Mathf.Clamp01(value);
        }

        private GameBalance.LampHandSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.lampHand;
                return _fallback ??= new GameBalance.LampHandSettings();
            }
        }

        private void Awake()
        {
            _robot = GetComponent<RobotController>();
            CaptureRest();
            if (lamp != null) lamp.gameObject.SetActive(false);
        }

        private void CaptureRest()
        {
            if (_restCaptured || rightArm == null) return;
            _armRestPos = rightArm.localPosition;
            _armRestRot = rightArm.localRotation;
            _restCaptured = true;
        }

        /// <summary>Взять лампу в руку (начинает опущенной) или убрать.</summary>
        public void SetHeld(bool held, float raise01 = 0f)
        {
            CaptureRest();
            Held = held;
            _raise01 = Mathf.Clamp01(raise01);
            if (lamp != null) lamp.gameObject.SetActive(held);
            if (!held && rightArm != null)
            {
                rightArm.localPosition = _armRestPos;
                rightArm.localRotation = _armRestRot;
            }
            if (held) Pose();
        }

        private void LateUpdate()
        {
            if (!Held) return;
            if (_robot != null)
            {
                float moving = _robot.IsGrounded ? Mathf.Clamp01(_robot.Speed / 1.5f) : 0f;
                _swayAmount = Mathf.MoveTowards(_swayAmount, moving, 6f * Time.deltaTime);
                _sway += Cfg.swayStepsPerSecond * Mathf.PI * 2f * Time.deltaTime * _swayAmount;
            }
            Pose();
        }

        private void Pose()
        {
            if (lamp == null || rightArm == null) return;
            var c = Cfg;
            float k = Mathf.SmoothStep(0f, 1f, _raise01);
            Vector3 hand = Vector3.Lerp(c.hiddenHand, c.carryHand, k);
            hand += new Vector3(Mathf.Cos(_sway) * c.swaySide, Mathf.Sin(_sway * 2f) * c.swayVertical, 0f) * (_swayAmount * k);

            // Рука от локтя направлена на кисть; кисть ровно в целевой точке. При подъёме локоть уходит вправо от кисти.
            Vector3 elbow = Vector3.Lerp(_armRestPos, c.carryElbow, k);
            Vector3 dir = hand - elbow;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            rightArm.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            rightArm.localPosition = hand - dir * c.armLength;

            // Лампа висит на ручке под кистью и чуть раскачивается.
            lamp.localPosition = hand + Vector3.down * c.lampBelowHand;
            float swing = Mathf.Sin(_sway) * c.swingDegrees * _swayAmount * k;
            // Порыв ветра наклоняет лампу по ветру (боковая составляющая относительно робота).
            float tilt = 0f;
            var gm = GameManager.Instance;
            var glow = LastRide.Fire.SparkGlow.For(FireSourceKind.Lamp);
            if (gm != null && gm.Weather != null && glow != null && glow.GustBend > 0.001f)
                tilt = -Vector3.Dot(gm.Weather.GustDirection, transform.right) * glow.GustBend * c.gustLampTiltDegrees * k;
            lamp.localRotation = Quaternion.Euler(0f, 0f, swing + tilt);
        }
    }
}
