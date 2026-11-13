using System.Collections;
using System.Text;
using LastRide.Core;
using LastRide.Player;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Автопроверки робота на VanTestTrack: <c>FindAnyObjectByType&lt;RobotTestDriver&gt;().Run(Scenario.X)</c>; результат — <see cref="LastReport"/>.
    /// </summary>
    public sealed class RobotTestDriver : MonoBehaviour
    {
        public enum Scenario { Settle, WalkWarm, WalkCold, DeepSnow, IceStop, Jams, Bob, CameraBlend }

        public RobotController robot;
        public static string LastReport { get; private set; } = "";
        public static bool IsRunning { get; private set; }

        public void Run(Scenario s)
        {
            if (IsRunning) return;
            if (robot == null) robot = FindAnyObjectByType<RobotController>();
            StartCoroutine(RunRoutine(s));
        }

        private IEnumerator RunRoutine(Scenario s)
        {
            IsRunning = true;
            LastReport = s + ": running…";
            var sb = new StringBuilder(s.ToString()).Append(": ");
            var gm = GameManager.Instance;
            if (gm != null) gm.SetPlayerMode(PlayerMode.OnFoot);
            yield return null; // VanBoarding высаживает робота на следующем кадре
            yield return null;
            robot.mouseLookEnabled = false;
            robot.warmth = 100f;

            IEnumerator Walk(float seconds, Vector2 input)
            {
                robot.MoveOverride = input;
                yield return new WaitForSeconds(seconds);
            }

            switch (s)
            {
                case Scenario.Settle:
                {
                    robot.Teleport(new Vector3(0f, 1f, -60f), 0f);
                    yield return Walk(1.5f, Vector2.zero);
                    sb.Append($"y {robot.transform.position.y:0.00}, grounded {robot.IsGrounded}, speed {robot.Speed:0.000}");
                    break;
                }
                case Scenario.WalkWarm:
                case Scenario.WalkCold:
                {
                    robot.warmth = s == Scenario.WalkWarm ? 100f : 0f;
                    robot.Teleport(new Vector3(0f, 0.5f, -60f), 0f);
                    yield return Walk(0.5f, Vector2.zero);
                    Vector3 p0 = robot.transform.position;
                    float vmax = 0f;
                    robot.MoveOverride = Vector2.up;
                    for (float t = 0f; t < 3f; t += Time.deltaTime) { vmax = Mathf.Max(vmax, robot.Speed); yield return null; }
                    float dist = Vector3.Distance(p0, robot.transform.position);
                    sb.Append($"vmax {vmax:0.00} м/с, за 3 с {dist:0.0} м (ожидаемая {robot.WalkSpeed:0.00} м/с)");
                    break;
                }
                case Scenario.DeepSnow:
                {
                    robot.Teleport(new Vector3(-24f, 0.5f, 5f), 0f); // Zone_DeepSnow z 8..32
                    yield return Walk(0.3f, Vector2.zero);
                    robot.MoveOverride = Vector2.up;
                    yield return new WaitForSeconds(1.5f);
                    sb.Append($"surface {robot.Surface}, v {robot.Speed:0.00} м/с (ожидаемая {robot.WalkSpeed * 0.6f:0.00})");
                    break;
                }
                case Scenario.IceStop:
                {
                    robot.Teleport(new Vector3(-24f, 0.5f, -40f), 0f); // Zone_Ice z -42..-18
                    yield return Walk(0.3f, Vector2.zero);
                    robot.MoveOverride = Vector2.up;
                    yield return new WaitForSeconds(1.5f);
                    float vRun = robot.Speed;
                    Vector3 p = robot.transform.position;
                    robot.MoveOverride = Vector2.zero;
                    float tStop = 0f;
                    while (robot.Speed > 0.05f && tStop < 3f) { tStop += Time.deltaTime; yield return null; }
                    sb.Append($"surface {robot.Surface}, v {vRun:0.00} м/с, остановка {tStop:0.00} с, пробег {Vector3.Distance(p, robot.transform.position):0.00} м");
                    break;
                }
                case Scenario.Jams:
                {
                    robot.warmth = 10f;
                    robot.Teleport(new Vector3(0f, 0.5f, -60f), 0f);
                    robot.MoveOverride = Vector2.up;
                    int jams = 0; bool was = false; float total = 0f;
                    for (float t = 0f; t < 12f; t += Time.deltaTime)
                    {
                        if (robot.IsJammed && !was) jams++;
                        if (robot.IsJammed) total += Time.deltaTime;
                        was = robot.IsJammed;
                        yield return null;
                    }
                    sb.Append($"заеданий за 12 с при тепле 10: {jams}, суммарно {total:0.0} с");
                    break;
                }
                case Scenario.CameraBlend:
                {
                    var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
                    if (brain == null) { sb.Append("нет CinemachineBrain"); break; }
                    robot.Teleport(new Vector3(0f, 0.5f, -60f), 0f);
                    yield return new WaitForSeconds(0.5f);

                    IEnumerator Switch(PlayerMode to, string label)
                    {
                        Transform cam = Camera.main.transform;
                        gm.SetPlayerMode(to);
                        Vector3 prevPos = cam.position; Quaternion prevRot = cam.rotation;
                        float t0 = Time.unscaledTime, maxMove = 0f, maxTurn = 0f;
                        yield return null;
                        bool sawBlend = false;
                        while (Time.unscaledTime - t0 < 3f)
                        {
                            if (brain.IsBlending) sawBlend = true;
                            maxMove = Mathf.Max(maxMove, Vector3.Distance(prevPos, cam.position));
                            maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(prevRot, cam.rotation));
                            prevPos = cam.position; prevRot = cam.rotation;
                            if (sawBlend && !brain.IsBlending) break;
                            yield return null;
                        }
                        float dur = Time.unscaledTime - t0;
                        bool van = GameInput.IsMapEnabled(InputNames.MapVan), foot = GameInput.IsMapEnabled(InputNames.MapOnFoot);
                        sb.Append($"{label}: бленд {(sawBlend ? dur.ToString("0.00") + " с" : "НЕ БЫЛО")}, макс. скачок за кадр {maxMove:0.00} м / {maxTurn:0.0}°, карты Van={van} OnFoot={foot}; ");
                    }

                    gm.SetPlayerMode(PlayerMode.InVan);
                    yield return new WaitForSeconds(2f);
                    yield return Switch(PlayerMode.OnFoot, "выход");
                    yield return new WaitForSeconds(0.5f);
                    yield return Switch(PlayerMode.InVan, "посадка");
                    break;
                }
                case Scenario.Bob:
                {
                    robot.Teleport(new Vector3(0f, 0.5f, -60f), 0f);
                    float[] warmths = { 100f, 0f };
                    foreach (float w in warmths)
                    {
                        robot.warmth = w;
                        robot.MoveOverride = Vector2.up;
                        yield return new WaitForSeconds(0.6f);
                        int cross = 0; float prev = robot.cameraPivot.localPosition.x;
                        for (float t = 0f; t < 4f; t += Time.deltaTime)
                        {
                            float x = robot.cameraPivot.localPosition.x;
                            if (prev < 0f && x >= 0f) cross++;
                            prev = x;
                            yield return null;
                        }
                        sb.Append($"тепло {w:0}: {cross / 4f:0.0} шаг/с; ");
                    }
                    break;
                }
            }

            robot.MoveOverride = null;
            robot.mouseLookEnabled = true;
            LastReport = sb.ToString();
            Debug.Log("[RobotTest] " + LastReport);
            IsRunning = false;
        }
    }
}
