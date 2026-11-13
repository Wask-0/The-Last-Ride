using System.Collections;
using System.Text;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Автоматические заезды для проверки и тюнинга фургона на тестовой площадке.
    /// Запуск: <c>FindAnyObjectByType&lt;VanTestDriver&gt;().Run(Scenario.X)</c>; результат — в <see cref="LastReport"/> и в консоли.
    /// Позиции рассчитаны на сцену VanTestTrack.
    /// </summary>
    public sealed class VanTestDriver : MonoBehaviour
    {
        public enum Scenario { Settle, Accelerate, Brake, Turn, Handbrake, Bumps, HillGentle, HillMax14Standing, HillSteepStanding, HillSteepRunUp, SlipSnow, SlipIce, DeepSnow, CameraFollow, CameraShake, CameraOrbit }

        public VanController van;
        public static string LastReport { get; private set; } = "";
        public static bool IsRunning { get; private set; }

        public void Run(Scenario s)
        {
            if (IsRunning) return;
            if (van == null) van = FindAnyObjectByType<VanController>();
            StartCoroutine(RunRoutine(s));
        }

        /// <summary>Все сценарии подряд (время ×4 — шаг физики тот же, замеры корректны). Итог — в <see cref="AllReport"/>.</summary>
        public void RunAll()
        {
            if (IsRunning) return;
            if (van == null) van = FindAnyObjectByType<VanController>();
            StartCoroutine(RunAllRoutine());
        }

        public static string AllReport { get; private set; } = "";
        public static bool AllDone { get; private set; }

        private IEnumerator RunAllRoutine()
        {
            AllDone = false;
            var gm = LastRide.Core.GameManager.Instance;
            if (gm != null) gm.DebugTimeMultiplier = 4f;
            var all = new StringBuilder();
            foreach (Scenario s in System.Enum.GetValues(typeof(Scenario)))
            {
                // Камерные сценарии считают в реальном времени — их запускаем отдельно на Time.timeScale = 1.
                if (s == Scenario.CameraFollow || s == Scenario.CameraShake || s == Scenario.CameraOrbit) continue;
                yield return RunRoutine(s);
                all.AppendLine(LastReport);
            }
            if (gm != null) gm.DebugTimeMultiplier = 1f;
            AllReport = all.ToString();
            AllDone = true;
        }

        private IEnumerator RunRoutine(Scenario s)
        {
            IsRunning = true;
            LastReport = s + ": running…";
            var sb = new StringBuilder(s.ToString()).Append(": ");
            var wait = new WaitForFixedUpdate();
            float minUp = 1f;

            void Place(Vector3 pos, float yaw)
            {
                van.ResetTo(pos, Quaternion.Euler(0f, yaw, 0f));
                if (VanCamera.Active != null) VanCamera.Active.Snap();
            }

            IEnumerator Drive(float seconds, float throttle, float steer, bool handbrake = false)
            {
                van.InputOverride = new Vector2(steer, throttle);
                van.HandbrakeOverride = handbrake;
                float t = 0f;
                while (t < seconds)
                {
                    minUp = Mathf.Min(minUp, van.transform.up.y);
                    t += Time.fixedDeltaTime;
                    yield return wait;
                }
            }

            switch (s)
            {
                case Scenario.Settle:
                {
                    Place(new Vector3(0f, 0.5f, -80f), 0f);
                    yield return Drive(1.5f, 0f, 0f);
                    Vector3 p0 = van.transform.position;
                    float maxV = 0f;
                    van.InputOverride = Vector2.zero;
                    for (float t = 0f; t < 2f; t += Time.fixedDeltaTime)
                    {
                        maxV = Mathf.Max(maxV, van.Body.linearVelocity.magnitude);
                        yield return wait;
                    }
                    sb.Append($"drift {(van.transform.position - p0).magnitude * 100f:0.0} cm in 2 s, max |v| {maxV:0.000} m/s, height {van.transform.position.y:0.00}, wheels {van.GroundedWheels}/4");
                    break;
                }
                case Scenario.Accelerate:
                {
                    Place(new Vector3(0f, 0.5f, -88f), 0f);
                    yield return Drive(1f, 0f, 0f);
                    float t5 = -1, t10 = -1, t13 = -1, vmax = 0f, t = 0f;
                    van.InputOverride = new Vector2(0f, 1f);
                    while (t < 14f)
                    {
                        float v = van.ForwardSpeed;
                        vmax = Mathf.Max(vmax, v);
                        if (t5 < 0 && v >= 5f) t5 = t;
                        if (t10 < 0 && v >= 10f) t10 = t;
                        if (t13 < 0 && v >= 13f) t13 = t;
                        t += Time.fixedDeltaTime;
                        yield return wait;
                    }
                    sb.Append($"0→5 {t5:0.0}s, 0→10 {t10:0.0}s, 0→13 {t13:0.0}s, max {vmax:0.00} m/s");
                    break;
                }
                case Scenario.Brake:
                {
                    Place(new Vector3(0f, 0.5f, -88f), 0f);
                    yield return Drive(10f, 1f, 0f);
                    float v0 = van.ForwardSpeed;
                    Vector3 p0 = van.transform.position;
                    float t = 0f;
                    van.InputOverride = new Vector2(0f, -1f);
                    while (van.ForwardSpeed > 0.3f && t < 10f) { t += Time.fixedDeltaTime; yield return wait; }
                    sb.Append($"from {v0:0.0} m/s: {(van.transform.position - p0).magnitude:0.0} m in {t:0.0} s");
                    break;
                }
                case Scenario.Turn:
                {
                    Place(new Vector3(-60f, 0.5f, -70f), 0f);
                    yield return Drive(4f, 0.6f, 0f);
                    yield return Drive(2f, 0.4f, 1f);
                    float yawRate = 0f, v = 0f; int n = 0;
                    van.InputOverride = new Vector2(1f, 0.4f);
                    for (float t = 0f; t < 3f; t += Time.fixedDeltaTime)
                    {
                        yawRate += Mathf.Abs(van.Body.angularVelocity.y);
                        v += van.Speed;
                        n++;
                        yield return wait;
                    }
                    yawRate /= n; v /= n;
                    sb.Append($"speed {v:0.0} m/s, yaw {yawRate * Mathf.Rad2Deg:0} °/s, radius {v / Mathf.Max(0.01f, yawRate):0.0} m, min up {minUp:0.00}");
                    break;
                }
                case Scenario.Handbrake:
                {
                    Place(new Vector3(60f, 0.5f, -90f), 0f);
                    yield return Drive(8f, 1f, 0f);
                    float v0 = van.Speed;
                    yield return Drive(1.6f, 0f, 1f, true);
                    yield return Drive(2f, 0f, 0f);
                    sb.Append($"from {v0:0.0} m/s: yaw turned {Mathf.DeltaAngle(0f, van.transform.eulerAngles.y):0}°, min up {minUp:0.00} ({(minUp > 0.5f ? "не перевернулся" : "ПЕРЕВОРОТ")})");
                    break;
                }
                case Scenario.Bumps:
                {
                    Place(new Vector3(0f, 0.5f, 15f), 0f);
                    yield return Drive(0.5f, 0f, 0f);
                    float maxPitch = 0f, maxY = 0f; int airborne = 0;
                    van.InputOverride = new Vector2(0f, 1f);
                    for (float t = 0f; t < 7f; t += Time.fixedDeltaTime)
                    {
                        float pitch = Mathf.Abs(Mathf.DeltaAngle(0f, van.transform.eulerAngles.x));
                        maxPitch = Mathf.Max(maxPitch, pitch);
                        maxY = Mathf.Max(maxY, van.Body.linearVelocity.y);
                        if (van.GroundedWheels == 0) airborne++;
                        yield return wait;
                    }
                    sb.Append($"max pitch {maxPitch:0.0}°, max vertical speed {maxY:0.00} m/s, airborne steps {airborne}, end speed {van.Speed:0.0}");
                    break;
                }
                case Scenario.CameraFollow:
                {
                    // Камера держит заданную дистанцию на полном ходу (камера считает в реальном времени —
                    // замер корректен только при Time.timeScale = 1).
                    var cam = VanCamera.Active;
                    if (cam == null) { sb.Append("нет VanCamera"); break; }
                    cam.mouseLookEnabled = false; // реальная мышь не должна влиять на замер
                    Place(new Vector3(0f, 0.5f, -88f), 0f);
                    van.InputOverride = new Vector2(0f, 1f);
                    float dMin = 99f, dMax = 0f, height = 0f, pitch = 0f;
                    for (float t = 0f; t < 9f; t += Time.fixedDeltaTime)
                    {
                        if (van.Speed > 13f)
                        {
                            Vector3 d = van.transform.position - cam.transform.position;
                            float h = new Vector2(d.x, d.z).magnitude;
                            dMin = Mathf.Min(dMin, h);
                            dMax = Mathf.Max(dMax, h);
                            height = cam.transform.position.y - van.transform.position.y;
                            pitch = cam.transform.eulerAngles.x;
                        }
                        yield return wait;
                    }
                    cam.mouseLookEnabled = true;
                    sb.Append($"at >13 m/s horizontal distance {dMin:0.00}..{dMax:0.00} m, height {height:0.00} m, pitch {pitch:0.0}°");
                    break;
                }
                case Scenario.CameraOrbit:
                {
                    // Орбита только при зажатой ЛКМ. События мыши подаём сами каждый кадр.
                    var cam = VanCamera.Active;
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    if (cam == null || mouse == null) { sb.Append("нет камеры или мыши"); break; }
                    cam.mouseLookEnabled = true;
                    Place(new Vector3(0f, 0.5f, -88f), 0f);
                    van.InputOverride = Vector2.zero;
                    yield return Drive(1.5f, 0f, 0f);

                    void Inject(bool lmb, Vector2 delta)
                    {
                        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                            new UnityEngine.InputSystem.LowLevel.MouseState { buttons = (ushort)(lmb ? 1 : 0), delta = delta });
                    }

                    // 1) Двигаем мышь БЕЗ кнопки: 20 кадров по 20 px вправо.
                    float y0 = cam.OrbitYaw;
                    for (int i = 0; i < 20; i++) { Inject(false, new Vector2(20f, 0f)); yield return null; }
                    float noButton = cam.OrbitYaw - y0;

                    // 2) Та же мышь, но ЛКМ зажата.
                    y0 = cam.OrbitYaw;
                    for (int i = 0; i < 20; i++) { Inject(true, new Vector2(20f, 0f)); yield return null; }
                    float withButton = cam.OrbitYaw - y0;

                    // 3) Кнопка отпущена и стоим на месте: камера остаётся там, куда её повернули.
                    Inject(false, Vector2.zero);
                    float held = cam.OrbitYaw;
                    yield return Drive(3f, 0f, 0f);
                    float stillAfterRelease = cam.OrbitYaw;

                    // 4) Поехали: через ~1.5 с камера возвращается за спину.
                    van.InputOverride = new Vector2(0f, 1f);
                    yield return Drive(4f, 1f, 0f);
                    float recentred = cam.OrbitYaw;

                    sb.Append($"без ЛКМ {noButton:0.0}° (ждём 0), с ЛКМ {withButton:0.0}° (ждём {20 * 20 * LastRide.Core.GameConfig.Load().balance.vanCamera.orbitSensitivity:0.0}), " +
                              $"после отпускания на стоянке {held:0.0}→{stillAfterRelease:0.0}° (ждём без изменений), после 4 с езды {recentred:0.0}° (ждём 0)");
                    break;
                }
                case Scenario.CameraShake:
                {
                    var cam = VanCamera.Active;
                    if (cam == null) { sb.Append("нет VanCamera"); break; }
                    cam.mouseLookEnabled = false;
                    Place(new Vector3(0f, 0.5f, -88f), 0f);
                    van.InputOverride = Vector2.zero;
                    yield return Drive(2f, 0f, 0f);
                    // Без тряски стоящий фургон даёт неподвижную камеру.
                    Quaternion baseline = cam.transform.rotation;
                    float quiet = 0f;
                    for (float t = 0f; t < 0.5f; t += Time.fixedDeltaTime) { quiet = Mathf.Max(quiet, Quaternion.Angle(baseline, cam.transform.rotation)); yield return wait; }
                    VanCamera.AddTrauma(1f);
                    float shaken = 0f;
                    for (float t = 0f; t < 0.6f; t += Time.fixedDeltaTime) { shaken = Mathf.Max(shaken, Quaternion.Angle(baseline, cam.transform.rotation)); yield return wait; }
                    yield return Drive(3f, 0f, 0f);
                    float after = Quaternion.Angle(baseline, cam.transform.rotation);
                    cam.mouseLookEnabled = true;
                    sb.Append($"standing still: {quiet:0.00}°, after AddTrauma(1): up to {shaken:0.00}°, 3 s later: {after:0.00}°");
                    break;
                }
                case Scenario.SlipSnow:
                case Scenario.SlipIce:
                {
                    // Одинаковый манёвр: разгон до ~10 м/с, затем полный руль на 2.5 с (газ 0.3).
                    bool ice = s == Scenario.SlipIce;
                    // Старты — внутри кольца ёлок (радиус 85–105 м от центра).
                    Place(ice ? new Vector3(-24f, 0.5f, -75f) : new Vector3(-60f, 0.5f, -50f), 0f);
                    yield return Drive(1f, 0f, 0f);
                    van.InputOverride = new Vector2(0f, 1f);
                    for (float t = 0f; t < 12f; t += Time.fixedDeltaTime)
                    {
                        bool ready = van.Speed >= 10f && (!ice || van.IceFraction > 0.99f);
                        if (ready) break;
                        yield return wait;
                    }
                    float v0 = van.Speed, yaw0 = van.transform.eulerAngles.y, maxSlip = 0f, iceShare = 0f;
                    int n = 0;
                    van.InputOverride = new Vector2(1f, 0.3f);
                    for (float t = 0f; t < 2.5f; t += Time.fixedDeltaTime)
                    {
                        Vector3 vel = Vector3.ProjectOnPlane(van.Body.linearVelocity, Vector3.up);
                        Vector3 fwd = Vector3.ProjectOnPlane(van.transform.forward, Vector3.up);
                        if (vel.magnitude > 2f) maxSlip = Mathf.Max(maxSlip, Vector3.Angle(vel, fwd));
                        iceShare += van.IceFraction;
                        n++;
                        yield return wait;
                    }
                    sb.Append($"entry {v0:0.0} m/s, yaw +{Mathf.DeltaAngle(yaw0, van.transform.eulerAngles.y):0}°, max slip angle {maxSlip:0}°, on ice {iceShare / n * 100f:0}%, end speed {van.Speed:0.0}");
                    break;
                }
                case Scenario.DeepSnow:
                {
                    Place(new Vector3(-24f, 0.5f, -14f), 0f);
                    yield return Drive(0.5f, 0f, 0f);
                    van.InputOverride = new Vector2(0f, 1f);
                    float entry = -1f, maxIn = 0f, inTime = 0f;
                    for (float t = 0f; t < 20f; t += Time.fixedDeltaTime)
                    {
                        bool inDeep = van.DeepSnowFraction > 0.99f;
                        if (inDeep)
                        {
                            if (entry < 0f) entry = van.Speed;
                            inTime += Time.fixedDeltaTime;
                            if (inTime > 1.5f) maxIn = Mathf.Max(maxIn, van.Speed); // после «вязкой» фазы входа
                        }
                        if (van.transform.position.z > 31f) break;
                        yield return wait;
                    }
                    var vc = LastRide.Core.GameConfig.Load().balance.van;
                    sb.Append($"entry {entry:0.0} m/s, max speed inside after 1.5 s {maxIn:0.0} m/s (limit {vc.maxSpeed * vc.deepSnowMaxSpeedMultiplier:0.0}), time inside {inTime:0.0}s");
                    break;
                }
                case Scenario.HillGentle:
                case Scenario.HillMax14Standing:
                case Scenario.HillSteepStanding:
                case Scenario.HillSteepRunUp:
                {
                    bool steep = s == Scenario.HillSteepStanding || s == Scenario.HillSteepRunUp;
                    bool max14 = s == Scenario.HillMax14Standing;
                    float x = steep ? 62f : (max14 ? 84f : 40f);
                    float startZ = s == Scenario.HillSteepRunUp ? -50f : (steep || max14 ? 19f : 15f);
                    Place(new Vector3(x, 0.5f, startZ), 0f);
                    yield return Drive(1f, 0f, 0f);
                    float maxH = 0f;
                    van.InputOverride = new Vector2(0f, 1f);
                    for (float t = 0f; t < 14f; t += Time.fixedDeltaTime)
                    {
                        maxH = Mathf.Max(maxH, van.transform.position.y);
                        if (van.transform.position.z > 43f) break;
                        yield return wait;
                    }
                    float top = steep ? 6f : (max14 ? 16f * Mathf.Tan(14f * Mathf.Deg2Rad) : 3.5f);
                    sb.Append($"max height {maxH:0.0} / {top} m → {(maxH > top - 0.6f ? "ВЗЯЛ подъём" : "не взял")}");
                    break;
                }
            }

            van.InputOverride = null;
            van.HandbrakeOverride = null;
            LastReport = sb.ToString();
            Debug.Log("[VanTest] " + LastReport);
            IsRunning = false;
        }
    }
}
