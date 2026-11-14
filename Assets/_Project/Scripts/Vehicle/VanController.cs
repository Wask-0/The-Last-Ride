using LastRide.Core;
using LastRide.DebugTools;
using LastRide.ProcGen.Models;
using UnityEngine;

namespace LastRide.Vehicle
{
    /// <summary>
    /// Аркадный контроллер фургона (ТЗ 4.5): Rigidbody + подвеска на лучах + силы сцепления колёс.
    /// Без WheelCollider. Заносы на льду и покачивание возникают естественно из сцепления и подвески.
    /// Все числа — в <see cref="GameBalance.VanSettings"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)]
    public sealed class VanController : MonoBehaviour
    {
        private sealed class Wheel
        {
            public Transform visual;
            public Vector3 localCenter;      // центр колеса в покое (локально к фургону)
            public Quaternion baseLocalRotation;
            public bool front, left;
            public bool grounded;
            public RaycastHit hit;
            public float springLength, lastSpringLength;
            public float load;               // сила пружины, Н
            public float spinDeg;
            public SurfaceType surface;
        }

        private Wheel[] _wheels;
        private Rigidbody _rb;
        private GameBalance.VanSettings _cfg;
        private float _steerAngle;
        private float _stuckTimer;
        private PhysicsMaterial _bodyMaterial;

        // ---- Состояние для других систем (звук, камера, частицы, HUD) ----
        /// <summary>Скорость вдоль фургона, м/с (отрицательная — задний ход).</summary>
        public float ForwardSpeed { get; private set; }
        public float Speed => Mathf.Abs(ForwardSpeed);
        /// <summary>Газ/тормоз −1..1 после обработки (0, если ехать нельзя).</summary>
        public float Throttle { get; private set; }
        public float SteerAngle => _steerAngle;
        public int GroundedWheels { get; private set; }
        public bool IsGrounded => GroundedWheels > 0;
        public bool IsHandbrake { get; private set; }
        /// <summary>Средняя нагрузка-сжатие подвески 0..1 (для тряски камеры и звука).</summary>
        public float SuspensionCompression01 { get; private set; }
        /// <summary>Доля колёс на льду / в глубоком снегу (0..1).</summary>
        public float IceFraction { get; private set; }
        public float DeepSnowFraction { get; private set; }
        public Rigidbody Body => _rb;

        /// <summary>Можно ли ехать (выключается при поломке в акте 3).</summary>
        public bool CanDrive { get; set; } = true;

        /// <summary>Принудительное торможение, м/с² (поломка: фургон должен встать). 0 — нет.</summary>
        public float ForcedBrakeDeceleration { get; set; }

        /// <summary>Множитель бокового сцепления шин (1 — норма). Порыв ветра временно снижает, чтобы боковая сила его сносила (WindPusher).</summary>
        public float GripMultiplier { get; set; } = 1f;

        /// <summary>Ввод вместо игрока (тесты, постановочные сцены). null — читать <see cref="GameInput"/>.</summary>
        public Vector2? InputOverride { get; set; }
        public bool? HandbrakeOverride { get; set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cfg = ResolveConfig();

            _rb.mass = _cfg.mass;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0.5f;
            _rb.maxAngularVelocity = 6f;
            _rb.automaticCenterOfMass = false;
            _rb.centerOfMass = _cfg.centerOfMass;

            // Корпус без трения: скользит вдоль сугробов и невидимых стен, а не «прилипает».
            _bodyMaterial = new PhysicsMaterial("VanBody")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.05f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            foreach (var c in GetComponentsInChildren<Collider>()) if (!c.isTrigger) c.sharedMaterial = _bodyMaterial;

            _wheels = new[]
            {
                CreateWheel(Anchors.WheelFrontLeft, true, true),
                CreateWheel(Anchors.WheelFrontRight, true, false),
                CreateWheel(Anchors.WheelRearLeft, false, true),
                CreateWheel(Anchors.WheelRearRight, false, false),
            };
        }

        private static GameBalance.VanSettings ResolveConfig()
        {
            var balance = GameManager.Instance != null ? GameManager.Instance.Balance : null;
            if (balance == null)
            {
                var cfg = GameConfig.Load();
                balance = cfg != null ? cfg.balance : null;
            }
            return balance != null ? balance.van : new GameBalance.VanSettings();
        }

        private Wheel CreateWheel(string name, bool front, bool left)
        {
            var t = transform.Find(name);
            if (t == null) Debug.LogError("[VanController] Не найдено колесо " + name);
            return new Wheel
            {
                visual = t,
                localCenter = t != null ? t.localPosition : Vector3.zero,
                baseLocalRotation = t != null ? t.localRotation : Quaternion.identity,
                front = front,
                left = left,
                springLength = _cfg.suspensionRest,
                lastSpringLength = _cfg.suspensionRest
            };
        }

        private void OnEnable()
        {
            DebugOverlay.RegisterLine("Фургон", () =>
                $"{Speed * 3.6f:0} км/ч  колёс на земле {GroundedWheels}/4  руль {_steerAngle:0}°" +
                (IceFraction > 0 ? "  ЛЁД" : "") + (DeepSnowFraction > 0 ? "  ГЛУБ.СНЕГ" : "") + (CanDrive ? "" : "  НЕ ЕДЕТ"));
        }

        private void OnDisable()
        {
            DebugOverlay.UnregisterLine("Фургон");
        }

        private void OnDestroy()
        {
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
        }

        // ------------------------------------------------------------------

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 drive = InputOverride ?? GameInput.Drive;
            bool handbrake = HandbrakeOverride ?? GameInput.Handbrake;
            if (!CanDrive)
            {
                drive = Vector2.zero;
                handbrake = false;
            }

            Throttle = Mathf.Clamp(drive.y, -1f, 1f);
            IsHandbrake = handbrake;
            Vector3 up = transform.up;
            ForwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);

            UpdateSteering(drive.x, dt);
            UpdateSuspension(up, dt);
            ApplyAntiRoll(up, _wheels[0], _wheels[1]);
            ApplyAntiRoll(up, _wheels[2], _wheels[3]);
            ApplyTireForces(dt);
            UpdateStuck(up, dt);
        }

        private void UpdateSteering(float steerInput, float dt)
        {
            float speedT = Mathf.Clamp01(Speed / Mathf.Max(0.1f, _cfg.maxSpeed));
            float maxSteer = Mathf.Lerp(_cfg.steerAngleLowSpeed, _cfg.steerAngleHighSpeed, speedT);
            float target = Mathf.Clamp(steerInput, -1f, 1f) * maxSteer;
            bool turningFurther = Mathf.Abs(target) > Mathf.Abs(_steerAngle) && Mathf.Sign(target) == Mathf.Sign(_steerAngle == 0f ? target : _steerAngle);
            float rate = turningFurther ? _cfg.steerSpeed : _cfg.steerReturnSpeed;
            _steerAngle = Mathf.MoveTowards(_steerAngle, target, rate * dt);
        }

        private void UpdateSuspension(Vector3 up, float dt)
        {
            float rest = _cfg.suspensionRest;
            float maxLen = rest + _cfg.suspensionTravel;
            int grounded = 0;
            float compressionSum = 0f;
            int mask = Layers.GroundMask;
            // Статическое проседание под весом: пружина имеет «натуральную» длину на eq длиннее, поэтому
            // в покое колесо стоит ровно на длине rest — колёса модели касаются земли, кузов не «сидит» ниже модели.
            float staticSag = _cfg.springStiffness > 0f ? _rb.mass * Physics.gravity.magnitude / (4f * _cfg.springStiffness) : 0f;

            foreach (var w in _wheels)
            {
                Vector3 origin = transform.TransformPoint(w.localCenter) + up * rest;
                w.lastSpringLength = w.springLength;
                if (Physics.Raycast(origin, -up, out w.hit, maxLen + VanModel.WheelRadius, mask, QueryTriggerInteraction.Ignore))
                {
                    w.grounded = true;
                    w.springLength = Mathf.Clamp(w.hit.distance - VanModel.WheelRadius, 0f, maxLen);
                    float springVel = (w.lastSpringLength - w.springLength) / dt; // > 0 — сжимается
                    float force = _cfg.springStiffness * (rest + staticSag - w.springLength) + _cfg.springDamper * springVel;
                    w.load = Mathf.Max(0f, force);
                    _rb.AddForceAtPosition(up * w.load, origin);
                    w.surface = Surfaces.At(w.hit.point);
                    grounded++;
                }
                else
                {
                    w.grounded = false;
                    w.springLength = maxLen;
                    w.load = 0f;
                }
                compressionSum += Mathf.Clamp01((maxLen - w.springLength) / maxLen);
            }
            GroundedWheels = grounded;
            SuspensionCompression01 = compressionSum / _wheels.Length;
        }

        private void ApplyAntiRoll(Vector3 up, Wheel l, Wheel r)
        {
            float maxLen = _cfg.suspensionRest + _cfg.suspensionTravel;
            float cl = (maxLen - l.springLength) / maxLen;
            float cr = (maxLen - r.springLength) / maxLen;
            float f = (cl - cr) * _cfg.antiRoll;
            if (l.grounded) _rb.AddForceAtPosition(up * f, l.hit.point);
            if (r.grounded) _rb.AddForceAtPosition(-up * f, r.hit.point);
        }

        private void ApplyTireForces(float dt)
        {
            if (GroundedWheels == 0)
            {
                IceFraction = DeepSnowFraction = 0f;
                return;
            }

            // Доли поверхностей под колёсами.
            int ice = 0, deep = 0;
            foreach (var w in _wheels)
            {
                if (!w.grounded) continue;
                if (w.surface == SurfaceType.Ice) ice++;
                else if (w.surface == SurfaceType.DeepSnow) deep++;
            }
            IceFraction = ice / (float)GroundedWheels;
            DeepSnowFraction = deep / (float)GroundedWheels;

            float v = ForwardSpeed;
            float sign = Mathf.Abs(v) > 0.01f ? Mathf.Sign(v) : 0f;
            float maxSpeed = _cfg.maxSpeed * Mathf.Lerp(1f, _cfg.deepSnowMaxSpeedMultiplier, DeepSnowFraction);

            // Тяга двигателя (ограничивается driveTraction) и сопротивления (ограничиваются brakeTraction).
            float engineAccel = 0f, resistAccel = 0f;
            if (Throttle > 0.05f)
            {
                if (v < -0.5f) resistAccel += _cfg.brakeDeceleration * Throttle;   // ехали назад — тормозим
                else
                {
                    // Кривая тяги падает к максимальной скорости; сопротивление воздуха двигатель компенсирует,
                    // поэтому предел скорости задаёт именно maxSpeed.
                    float r = Mathf.Clamp01(v / maxSpeed);
                    engineAccel = Throttle * (_cfg.driveAcceleration * (1f - r * r * r) + (r < 1f ? _cfg.airDrag * v * v : 0f));
                }
            }
            else if (Throttle < -0.05f)
            {
                if (v > 0.5f) resistAccel += _cfg.brakeDeceleration * -Throttle;    // тормоз
                else
                {
                    float rv = Mathf.Max(0f, -v) / Mathf.Max(0.1f, _cfg.reverseMaxSpeed);
                    engineAccel = -_cfg.reverseAcceleration * -Throttle * Mathf.Clamp01(1f - rv * rv);
                }
            }
            else resistAccel += _cfg.coastDeceleration;

            if (IsHandbrake) resistAccel += _cfg.handbrakeDeceleration;
            resistAccel += ForcedBrakeDeceleration;
            resistAccel += _cfg.airDrag * v * v;
            if (DeepSnowFraction > 0f)
            {
                // Глубокий снег: сильно тормозит, если быстрее предела (влетели с разгона) или катимся без газа;
                // под газом в пределах лимита — только лёгкое вязкое сопротивление, чтобы можно было выехать.
                bool overCap = Mathf.Abs(v) > maxSpeed + 0.3f;
                bool coasting = Mathf.Abs(Throttle) < 0.05f;
                float k = overCap ? 1f : (coasting ? 0.5f : 0.12f);
                resistAccel += _cfg.deepSnowDrag * k * DeepSnowFraction;
            }
            // Сопротивление не должно разворачивать фургон назад за один шаг.
            resistAccel = Mathf.Min(resistAccel, Mathf.Abs(v) / dt);
            bool parking = Mathf.Abs(Throttle) < 0.05f && Mathf.Abs(v) < 0.5f;

            float massPerWheel = _rb.mass / GroundedWheels;
            foreach (var w in _wheels)
            {
                if (!w.grounded) continue;
                Vector3 n = w.hit.normal;
                Quaternion steer = w.front ? Quaternion.AngleAxis(_steerAngle, transform.up) : Quaternion.identity;
                Vector3 fwd = Vector3.ProjectOnPlane(steer * transform.forward, n).normalized;
                Vector3 right = Vector3.Cross(n, fwd);
                Vector3 pv = _rb.GetPointVelocity(w.hit.point);
                float latVel = Vector3.Dot(pv, right);
                float lonVel = Vector3.Dot(pv, fwd);

                bool onIce = w.surface == SurfaceType.Ice;
                float surfaceMul = onIce ? _cfg.iceGripMultiplier : 1f;
                float latMu = _cfg.lateralGrip * surfaceMul * GripMultiplier
                              * (IsHandbrake && !w.front ? _cfg.handbrakeRearGrip : 1f)
                              * (onIce && !w.front ? _cfg.iceRearGripBias : 1f);

                // Боковая сила: гасим скольжение, но не больше, чем держит шина.
                float latForce = -latVel * massPerWheel / dt * _cfg.gripResponse;
                latForce = Mathf.Clamp(latForce, -latMu * w.load, latMu * w.load);

                // Продольная сила.
                float lonForce;
                if (parking)
                {
                    // Стоим без газа — держим на месте (в т.ч. на уклоне до ~30°, если позволяет сцепление).
                    lonForce = -lonVel * massPerWheel / dt * _cfg.gripResponse;
                    float lim = _cfg.brakeTraction * surfaceMul * w.load;
                    lonForce = Mathf.Clamp(lonForce, -lim, lim);
                }
                else
                {
                    float driveLim = _cfg.driveTraction * surfaceMul * w.load;
                    float brakeLim = _cfg.brakeTraction * surfaceMul * w.load;
                    lonForce = Mathf.Clamp(engineAccel * massPerWheel, -driveLim, driveLim)
                             - sign * Mathf.Min(resistAccel * massPerWheel, brakeLim);
                }

                // «Круг трения»: суммарная сила шины ограничена.
                float maxTotal = Mathf.Max(latMu, _cfg.brakeTraction * surfaceMul) * w.load;
                Vector2 total = new Vector2(latForce, lonForce);
                if (total.magnitude > maxTotal && maxTotal > 0f) total = total.normalized * maxTotal;

                // Прикладываем на высоте центра масс — меньше лишнего крена, аркаднее.
                Vector3 comWorld = _rb.worldCenterOfMass;
                Vector3 at = w.hit.point + n * Vector3.Dot(comWorld - w.hit.point, n);
                _rb.AddForceAtPosition(right * total.x + fwd * total.y, at);
            }
        }

        private void UpdateStuck(Vector3 up, float dt)
        {
            bool flipped = Vector3.Dot(up, Vector3.up) < 0.35f;
            bool hanging = GroundedWheels == 0 && _rb.linearVelocity.sqrMagnitude < 0.25f;
            _stuckTimer = flipped || hanging ? _stuckTimer + dt : 0f;
            if (_stuckTimer > _cfg.uprightAfterSeconds)
            {
                _stuckTimer = 0f;
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                ResetTo(transform.position + Vector3.up * 1.5f, Quaternion.LookRotation(fwd.normalized, Vector3.up));
            }
        }

        // ------------------------------------------------------------------

        private void Update()
        {
            if (_wheels == null) return;
            float rest = _cfg.suspensionRest;
            float dt = Time.deltaTime;
            foreach (var w in _wheels)
            {
                if (w.visual == null) continue;
                w.visual.localPosition = w.localCenter + Vector3.up * (rest - w.springLength);
                w.spinDeg = Mathf.Repeat(w.spinDeg + ForwardSpeed / VanModel.WheelRadius * Mathf.Rad2Deg * dt, 360f);
                float spin = w.left ? -w.spinDeg : w.spinDeg;
                var steer = Quaternion.Euler(0f, w.front ? _steerAngle : 0f, 0f);
                w.visual.localRotation = steer * w.baseLocalRotation * Quaternion.Euler(spin, 0f, 0f);
            }
        }

        // ---- API для других систем ----

        /// <summary>Мгновенный толчок (порыв ветра, удар).</summary>
        public void AddImpulse(Vector3 worldImpulse) => _rb.AddForce(worldImpulse, ForceMode.Impulse);

        /// <summary>Постоянная сила на этот шаг физики (вызывать из FixedUpdate).</summary>
        public void AddForce(Vector3 worldForce) => _rb.AddForce(worldForce, ForceMode.Force);

        /// <summary>Переставить фургон (чекпоинт, защита от переворота).</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = position;
            _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _steerAngle = 0f;
            foreach (var w in _wheels)
            {
                w.springLength = w.lastSpringLength = _cfg.suspensionRest;
            }
        }
    }
}
