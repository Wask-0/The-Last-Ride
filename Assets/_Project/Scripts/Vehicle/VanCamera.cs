using LastRide.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace LastRide.Vehicle
{
    /// <summary>
    /// Камера от 3-го лица за фургоном (ТЗ 2.1). Висит на объекте с <see cref="CinemachineCamera"/>
    /// без Body/Aim — позицию и поворот задаёт этот скрипт, а Cinemachine выводит кадр и делает
    /// бленды между камерами (выход из фургона — 4.3).
    /// Базовая позиция — из <c>GameBalance → Van Camera</c>: <c>distance</c> позади, <c>height</c> над фургоном,
    /// наклон <c>pitchDown</c>. Мышь вращает камеру вокруг точки над фургоном; без мыши при движении камера
    /// возвращается за спину. Мышь вращает камеру всегда (без зажатия кнопок), колесо меняет дальность. Тряска — от скорости, кочек и порывов (<see cref="AddTrauma"/>).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class VanCamera : MonoBehaviour
    {
        public VanController van;

        /// <summary>Выключить орбиту мышью (автотесты, постановочные сцены).</summary>
        public bool mouseLookEnabled = true;

        /// <summary>Защита от скачков дельты мыши (alt-tab, захват курсора), пикселей за кадр.</summary>
        private const float MaxLookDeltaPerFrame = 250f;

        private static VanCamera _active;
        private CinemachineCamera _cm;
        private GameBalance.VanCameraSettings _fallback;

        private float _baseYaw, _baseYawVel;
        private float _orbitYaw, _orbitPitch;
        private float _idleTime;
        private float _zoomTarget = 1f, _zoom = 1f;
        private Vector3 _pivot, _pivotVel;
        private float _distanceCurrent;
        private float _trauma;
        private float _lastCompression;
        private bool _initialized;

        public static VanCamera Active => _active;

        /// <summary>Текущее отклонение орбиты от «за спиной», °. Для тестов и отладки.</summary>
        public float OrbitYaw => _orbitYaw;
        public float OrbitPitch => _orbitPitch;

        private GameBalance.VanCameraSettings Settings
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.vanCamera;
                var cfg = GameConfig.Load();
                if (cfg != null && cfg.balance != null) return cfg.balance.vanCamera;
                return _fallback ??= new GameBalance.VanCameraSettings();
            }
        }

        /// <summary>Встряхнуть камеру (0..1 складывается, ограничено 1). Порывы ветра, удары.</summary>
        public static void AddTrauma(float amount)
        {
            if (_active != null) _active._trauma = Mathf.Clamp01(_active._trauma + amount);
        }

        private void Awake() => _cm = GetComponent<CinemachineCamera>();
        private void OnEnable() => _active = this;
        private void OnDisable() { if (_active == this) _active = null; }

        /// <summary>Мгновенно поставить камеру за фургоном (после телепорта, загрузки чекпоинта).</summary>
        public void Snap()
        {
            if (van == null) return;
            _baseYaw = FlatYaw(van.transform);
            _baseYawVel = 0f;
            _orbitYaw = _orbitPitch = 0f;
            _pivot = PivotTarget(Settings);
            _pivotVel = Vector3.zero;
            _distanceCurrent = Radius(Settings);
            _initialized = true;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (van == null) return;
            var s = Settings;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            if (!_initialized) Snap();

            // ---- Базовый поворот: мягко за фургоном ----
            float targetYaw = FlatYaw(van.transform);
            _baseYaw = Mathf.SmoothDampAngle(_baseYaw, targetYaw, ref _baseYawVel, s.yawSmoothTime, Mathf.Infinity, dt);

            // ---- Орбита мышью (карта Van активна только в фургоне и не на паузе) ----
            // Камера вращается мышью всегда (без зажатия кнопок). Пока мышь двигается, таймер возврата сброшен —
            // камера остаётся там, куда её повернули, и возвращается за спину после паузы в движении мыши.
            Vector2 look = mouseLookEnabled ? Vector2.ClampMagnitude(GameInput.Look, MaxLookDeltaPerFrame) : Vector2.zero;
            bool orbiting = look.sqrMagnitude > 0.25f;
            if (orbiting)
            {
                float orbitK = s.orbitSensitivity * LastRide.Save.GameSettings.MouseSensitivity;
                _orbitYaw += look.x * orbitK;
                _orbitPitch = Mathf.Clamp(_orbitPitch + (LastRide.Save.GameSettings.InvertY ? look.y : -look.y) * orbitK, s.orbitPitchRange.x, s.orbitPitchRange.y);
                _idleTime = 0f;
            }
            else
            {
                _idleTime += dt;
                // Возврат за спину — только при движении, чтобы можно было спокойно осмотреться на стоянке.
                if (_idleTime > s.recenterDelay && van.Speed > 2f)
                {
                    float step = s.recenterSpeed * dt;
                    _orbitYaw = Mathf.MoveTowardsAngle(_orbitYaw, 0f, step);
                    _orbitPitch = Mathf.MoveTowards(_orbitPitch, 0f, step);
                }
            }
            _orbitYaw = Mathf.Repeat(_orbitYaw + 180f, 360f) - 180f;

            // ---- Колесо мыши: ближе / дальше ----
            var gmz = GameManager.Instance;
            if (mouseLookEnabled && gmz != null && gmz.IsGameplayActive && gmz.PlayerMode == PlayerMode.InVan)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                if (Mathf.Abs(wheel) > 0.01f) _zoomTarget = Mathf.Clamp(_zoomTarget - Mathf.Clamp(wheel, -1f, 1f) * s.zoomStep, s.zoomMin, s.zoomMax);
            }
            _zoomTarget = Mathf.Clamp(_zoomTarget, s.zoomMin, s.zoomMax);
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-s.zoomSmooth * dt));

            // ---- Точка вращения над фургоном (мягкое следование) ----
            _pivot = Vector3.SmoothDamp(_pivot, PivotTarget(s), ref _pivotVel, s.followSmoothTime, Mathf.Infinity, dt);

            float yaw = _baseYaw + _orbitYaw;
            float pitch = s.pitchDown + _orbitPitch;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

            // ---- Препятствия: подтягиваем камеру ближе, если между ней и фургоном что-то есть ----
            float radius = Radius(s) * _zoom;
            float wanted = radius;
            Vector3 back = rot * Vector3.back;
            if (Physics.SphereCast(_pivot, s.collisionRadius, back, out var hit, radius, Layers.GroundMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.8f, hit.distance - 0.05f);
            // Приближаемся быстро, отдаляемся плавно.
            _distanceCurrent = wanted < _distanceCurrent ? wanted : Mathf.MoveTowards(_distanceCurrent, wanted, 6f * dt);

            // ---- Тряска: дорога + кочки + травма ----
            float compression = van.SuspensionCompression01;
            float bump = Mathf.Abs(compression - _lastCompression);
            _lastCompression = compression;
            _trauma = Mathf.Clamp01(_trauma + bump * s.bumpTraumaScale * (van.IsGrounded ? 1f : 0f));
            var gm = GameManager.Instance;
            float maxSpeed = gm != null && gm.Balance != null ? gm.Balance.van.maxSpeed : 14f;
            float speed01 = Mathf.Clamp01(van.Speed / Mathf.Max(0.1f, maxSpeed));
            float shakeAngle = s.rumbleAtMaxSpeed * speed01 + s.maxShakeAngle * _trauma * _trauma;
            // Погода: фоновая тряска в шторме и удар порыва — фазу видно и без HUD (ТЗ 3, акт 2).
            if (gm != null && gm.Weather != null && gm.Balance != null)
            {
                var c = gm.Balance.weather;
                shakeAngle += c.vanCameraStormRumbleDeg * gm.Weather.StormMix + c.vanCameraGustRumbleDeg * gm.Weather.GustStrength01 * gm.Weather.GustPower;
            }
            _trauma = Mathf.Max(0f, _trauma - s.traumaDecay * dt);
            float t = Time.unscaledTime * s.shakeFrequency;
            var shake = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shakeAngle,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shakeAngle,
                (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 1.2f * shakeAngle);

            transform.SetPositionAndRotation(_pivot + back * _distanceCurrent, rot * shake);

            var lens = _cm.Lens;
            lens.FieldOfView = s.fieldOfView;
            _cm.Lens = lens;
        }

        /// <summary>
        /// Точка вращения: на такой высоте над фургоном, чтобы при базовом наклоне
        /// камера оказывалась ровно на distance позади и height выше.
        /// </summary>
        private Vector3 PivotTarget(GameBalance.VanCameraSettings s)
        {
            float pivotHeight = s.height - s.distance * Mathf.Tan(s.pitchDown * Mathf.Deg2Rad);
            // Упреждение по скорости: мягкое следование (SmoothDamp) в установившемся режиме отстаёт на
            // скорость × время сглаживания — компенсируем, чтобы на ходу камера держала заданную дистанцию.
            Vector3 lead = van.Body.linearVelocity * s.followSmoothTime;
            return van.transform.position + lead + Vector3.up * pivotHeight;
        }

        private static float Radius(GameBalance.VanCameraSettings s) =>
            s.distance / Mathf.Max(0.05f, Mathf.Cos(s.pitchDown * Mathf.Deg2Rad));

        private static float FlatYaw(Transform t)
        {
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            return f.sqrMagnitude > 1e-4f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : t.eulerAngles.y;
        }
    }
}
