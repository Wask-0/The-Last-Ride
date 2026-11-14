using LastRide.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace LastRide.Player
{
    /// <summary>
    /// Робот пешком, вид от 1-го лица (ТЗ 2.1, 4.4, 4.6). CharacterController + мышиный обзор.
    /// Скорость = lerp(cold, warm, тепло/100); глубокий снег ×0.6; лёд — инерция; ниже порога тепла —
    /// периодические «заедания» (рывки 0.2 с); покачивание камеры замедляется на холоде.
    /// Камера — CinemachineCamera на <see cref="cameraPivot"/>; приоритет высокий, только когда режим игрока — пешком.
    /// Тепло ведёт <see cref="WarmthSystem"/> (<see cref="warmth"/> — его значение).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class RobotController : MonoBehaviour
    {
        [Tooltip("Точка глаз: сюда ставится камера. Поворачивается по тангажу.")]
        public Transform cameraPivot;
        public CinemachineCamera cinemachineCamera;

        /// <summary>
        /// Текущее тепло 0..max — это <c>GameManager.Warmth.Value</c> (система тепла, 6.1); запись — для тестов и отладки.
        /// Без GameManager (сцена без него) работает локальное значение.
        /// </summary>
        public float warmth
        {
            get { var gm = GameManager.Instance; return gm != null && gm.Warmth != null ? gm.Warmth.Value : _localWarmth; }
            set { var gm = GameManager.Instance; if (gm != null && gm.Warmth != null) gm.Warmth.SetValue(value); else _localWarmth = value; }
        }

        private float _localWarmth = 100f;

        [Header("Обзор")]
        public Vector2 pitchLimits = new Vector2(-80f, 80f);
        public bool invertY;
        [Tooltip("Защита от скачков дельты мыши, пикселей за кадр.")]
        public float maxLookDeltaPerFrame = 250f;
        public bool mouseLookEnabled = true;

        [Header("Движение")]
        public float gravity = 20f;
        public float groundSnap = 0.3f;

        [Header("Покачивание камеры")]
        [Tooltip("Шагов в секунду при полном тепле (при холоде — реже).")]
        public float bobStepsPerSecondWarm = 1.9f;
        public float bobStepsPerSecondCold = 0.9f;
        public float bobVertical = 0.035f;
        public float bobSide = 0.02f;
        public float bobRollDegrees = 0.5f;

        public static RobotController Active { get; private set; }

        /// <summary>Текущая горизонтальная скорость, м/с.</summary>
        public float Speed => new Vector2(_velocity.x, _velocity.z).magnitude;
        public Vector3 Velocity => _velocity;
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool IsJammed => _jamTimeLeft > 0f;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public SurfaceType Surface { get; private set; }
        public CharacterController Body => _cc;

        /// <summary>Скорость ходьбы при текущем тепле, до учёта поверхности.</summary>
        public float WalkSpeed => Mathf.Lerp(Move.walkSpeedCold, Move.walkSpeedWarm, Mathf.Clamp01(warmth / WarmMax));

        /// <summary>Внешний снос, м/с (порыв ветра, <c>WindPusher</c>): добавляется к движению, не влияет на <see cref="Speed"/>.</summary>
        public Vector3 ExternalVelocity { get; set; }

        /// <summary>Вход в игру: на следующем кадре использовать эти значения вместо ввода (автотесты).</summary>
        public Vector2? MoveOverride { get; set; }

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _yaw, _pitch;
        private float _verticalSpeed;
        private float _bobPhase;
        private float _bobAmount;
        private float _jamTimeLeft, _nextJamIn;
        private Vector3 _pivotRest;
        private GameBalance.MovementSettings _fallbackMove;
        private GameBalance.WarmthSettings _fallbackWarmth;

        private GameBalance.MovementSettings Move
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.movement;
                return _fallbackMove ??= new GameBalance.MovementSettings();
            }
        }

        private GameBalance.WarmthSettings WarmthCfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.warmth;
                return _fallbackWarmth ??= new GameBalance.WarmthSettings();
            }
        }

        private float WarmMax => WarmthCfg.max;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cinemachineCamera == null && cameraPivot != null) cinemachineCamera = cameraPivot.GetComponentInChildren<CinemachineCamera>();
            if (cameraPivot != null) _pivotRest = cameraPivot.localPosition;
            _yaw = transform.eulerAngles.y;
            _nextJamIn = NextJamInterval();
        }

        private void OnEnable()
        {
            Active = this;
            EventBus<PlayerModeChanged>.Subscribe(OnModeChanged);
            ApplyCameraPriority();
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            EventBus<PlayerModeChanged>.Unsubscribe(OnModeChanged);
        }

        private void Start()
        {
            DebugTools.DebugOverlay.RegisterLine("Робот", () =>
                Active == null ? "—" : $"v {Active.Speed:0.00} м/с (ходьба {Active.WalkSpeed:0.00}), {Active.Surface}, тепло {Active.warmth:0}{(Active.IsJammed ? ", ЗАЕДАНИЕ" : "")}");
        }

        private void OnModeChanged(PlayerModeChanged e) => ApplyCameraPriority();

        private bool IsActiveMode
        {
            get
            {
                var gm = GameManager.Instance;
                return gm == null || gm.PlayerMode != PlayerMode.InVan;
            }
        }

        private void ApplyCameraPriority()
        {
            if (cinemachineCamera == null) return;
            cinemachineCamera.Priority = new PrioritySettings { Enabled = true, Value = IsActiveMode ? 20 : 0 };
        }

        /// <summary>Мгновенно поставить робота (старт, посадка/выход, чекпоинт).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
            _yaw = yaw;
            _pitch = 0f;
            _velocity = Vector3.zero;
            _verticalSpeed = 0f;
            ApplyRotation();
        }

        /// <summary>Робот сидит в фургоне: капсула выключена, обзор и ходьба не работают.</summary>
        public bool Seated { get; private set; }

        /// <summary>Посадить в фургон (привязать к <paramref name="seat"/>) или высадить (seat = null — Teleport вызывает вызывающий).</summary>
        public void SetSeated(bool seated, Transform seat)
        {
            Seated = seated;
            if (seated)
            {
                _cc.enabled = false;
                transform.SetParent(seat, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                _velocity = Vector3.zero;
                _verticalSpeed = 0f;
                MoveOverride = null;
            }
            else
            {
                transform.SetParent(null, true);
            }
        }

        /// <summary>Повернуть взгляд (постановки, тесты).</summary>
        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
            ApplyRotation();
        }

        private void Update()
        {
            if (Seated) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool active = IsActiveMode && (GameManager.Instance == null || GameManager.Instance.IsGameplayActive);

            Look(active);
            Walk(active, dt);
            Bob(dt);
        }

        private void Look(bool active)
        {
            if (!active || !mouseLookEnabled) { ApplyRotation(); return; }
            Vector2 look = Vector2.ClampMagnitude(GameInput.Look, maxLookDeltaPerFrame) * Move.mouseSensitivity;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch + (invertY ? look.y : -look.y), pitchLimits.x, pitchLimits.y);
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f) * Quaternion.Euler(0f, 0f, _bobRoll);
        }

        private float _bobRoll;
        private float _jamKick; // 1 в момент заедания, затухает: камера «вздрагивает»

        /// <summary>Сколько заеданий было с начала игры (телеметрия для тестов).</summary>
        public int JamCount { get; private set; }

        [Header("Заедание механизма")]
        [Tooltip("Просадка камеры при заедании, м.")]
        public float jamKickDrop = 0.05f;
        [Tooltip("Наклон камеры вниз при заедании, градусов.")]
        public float jamKickPitch = 1.6f;
        [Tooltip("Скорость затухания толчка, 1/с.")]
        public float jamKickDecay = 7f;

        private void Walk(bool active, float dt)
        {
            var move = Move;
            Surface = Surfaces.At(transform.position);

            Vector2 input = active ? (MoveOverride ?? GameInput.Move) : Vector2.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();

            // «Заедания» заводного механизма на холоде: на 0.2 с механизм встаёт.
            UpdateJam(dt);
            float speed = WalkSpeed;
            if (Surface == SurfaceType.DeepSnow) speed *= move.deepSnowSpeedMultiplier;
            if (IsJammed) speed = 0f;
            // Замерзание (тепло 0): за freezeToStopSeconds механизм встаёт совсем.
            var gmw = GameManager.Instance;
            if (gmw != null && gmw.Warmth != null) speed *= 1f - gmw.Warmth.FreezeProgress;

            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

            // Гладкий разгон/остановка; на льду — большая инерция (ТЗ 4.6).
            float inertia = Surface == SurfaceType.Ice ? Mathf.Max(0.01f, move.iceInertiaSeconds) : 0.08f;
            if (IsJammed) inertia = 0.05f;
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, wish, move.walkSpeedWarm / inertia * dt);

            if (_cc.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
            else _verticalSpeed -= gravity * dt;

            _velocity = new Vector3(horizontal.x, _verticalSpeed, horizontal.z);
            _cc.Move((_velocity + ExternalVelocity) * dt);
            _velocity.y = 0f; // в Speed учитывается только горизонталь
        }

        private void UpdateJam(float dt)
        {
            var w = WarmthCfg;
            if (_jamTimeLeft > 0f)
            {
                _jamTimeLeft -= dt;
                return;
            }
            if (warmth >= w.jamThreshold) { _nextJamIn = NextJamInterval(); return; }
            _nextJamIn -= dt;
            if (_nextJamIn <= 0f)
            {
                _jamTimeLeft = w.jamDuration;
                _nextJamIn = NextJamInterval();
                _jamKick = 1f;
                JamCount++;
                EventBus<MechanismJammed>.Publish(new MechanismJammed { Duration = w.jamDuration });
            }
        }

        private float NextJamInterval()
        {
            var w = WarmthCfg;
            float k = Mathf.Clamp01(warmth / Mathf.Max(1f, w.jamThreshold));
            float baseInterval = Mathf.Lerp(w.jamInterval.y, w.jamInterval.x, k);
            return baseInterval * Random.Range(0.7f, 1.3f);
        }

        private void Bob(float dt)
        {
            if (cameraPivot == null) return;
            float warm01 = Mathf.Clamp01(warmth / WarmMax);
            float stepsPerSecond = Mathf.Lerp(bobStepsPerSecondCold, bobStepsPerSecondWarm, warm01);
            float moving = _cc.isGrounded ? Mathf.Clamp01(Speed / Mathf.Max(0.1f, Move.walkSpeedCold)) : 0f;
            _bobAmount = Mathf.MoveTowards(_bobAmount, moving, 6f * dt);
            _bobPhase += stepsPerSecond * Mathf.PI * 2f * dt * (_bobAmount > 0.01f ? 1f : 0f);

            float vert = Mathf.Sin(_bobPhase * 2f) * bobVertical;
            float side = Mathf.Cos(_bobPhase) * bobSide;
            _jamKick = Mathf.MoveTowards(_jamKick, 0f, jamKickDecay * dt);

            // Погода: дрожь камеры в шторме и наклон в сторону порыва (диегетический сигнал фазы).
            float windPitch = 0f, windRoll = 0f;
            var gmw = GameManager.Instance;
            if (gmw != null && gmw.Weather != null && gmw.Balance != null)
            {
                var c = gmw.Balance.weather;
                float tt = Time.unscaledTime * 17f;
                float storm = gmw.Weather.StormMix;
                windPitch = (Mathf.PerlinNoise(tt, 1.7f) - 0.5f) * 2f * c.robotStormTremorDeg * storm;
                windRoll = (Mathf.PerlinNoise(2.9f, tt) - 0.5f) * 2f * c.robotStormTremorDeg * storm;
                float gustSide = Vector3.Dot(gmw.Weather.GustDirection, transform.right);
                windRoll -= gustSide * c.robotGustLeanDeg * gmw.Weather.GustStrength01 * gmw.Weather.GustPower;
            }
            cameraPivot.localPosition = _pivotRest + new Vector3(side, vert, 0f) * _bobAmount + Vector3.down * (jamKickDrop * _jamKick);
            _bobRoll = Mathf.Cos(_bobPhase) * bobRollDegrees * _bobAmount;
            cameraPivot.localRotation = Quaternion.Euler(_pitch + jamKickPitch * _jamKick + windPitch, 0f, _bobRoll + windRoll);
        }
    }
}
