using System;
using System.Collections.Generic;
using System.Text;
using LastRide.Core;
using LastRide.Level;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Отладочные инструменты (ТЗ 10), только в Editor / Development build.
    /// F1 — оверлей, F2 — телепорт к следующему чекпоинту (или поломке фургона), F3 — +10 каждого топлива,
    /// F4 — бессмертие (тепло и Искра не падают), F5 — ускорение времени ×4.
    /// Системы добавляют свои строки в оверлей через <see cref="RegisterLine"/>.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float FastTimeMultiplier = 4f;

        private static readonly List<KeyValuePair<string, Func<string>>> Lines = new List<KeyValuePair<string, Func<string>>>();

        public static bool OverlayVisible { get; private set; }

        private readonly StringBuilder _sb = new StringBuilder(512);
        private float _fpsSmoothed;
        private GUIStyle _style;
        private Texture2D _bg;

        /// <summary>Добавить строку в оверлей (повторная регистрация с тем же label заменяет её).</summary>
        public static void RegisterLine(string label, Func<string> value)
        {
            UnregisterLine(label);
            Lines.Add(new KeyValuePair<string, Func<string>>(label, value));
        }

        public static void UnregisterLine(string label)
        {
            Lines.RemoveAll(l => l.Key == label);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Lines.Clear();
            OverlayVisible = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject("[DebugOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugOverlay>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float fps = 1f / dt;
                _fpsSmoothed = _fpsSmoothed <= 0f ? fps : Mathf.Lerp(_fpsSmoothed, fps, 1f - Mathf.Exp(-dt * 4f));
            }

            if (GameInput.DebugOverlayPressed) OverlayVisible = !OverlayVisible;

            var game = GameManager.Instance;
            if (game != null && game.IsGameplayActive)
            {
                if (GameInput.DebugNextCheckpointPressed) TeleportToNextCheckpoint(game);
                if (GameInput.DebugAddFuelPressed) AddFuel(game);
                if (GameInput.DebugGodModePressed) ToggleGodMode(game);
            }

            if (GameInput.DebugTimeScalePressed && GameManager.Instance != null)
            {
                var gm = GameManager.Instance;
                gm.DebugTimeMultiplier = gm.DebugTimeMultiplier > 1.01f ? 1f : FastTimeMultiplier;
            }
        }

        // ------------------------------------------------------------------ F2–F4

        private static bool GodMode(GameManager gm) => gm.Warmth.Invulnerable && gm.Spark.Invulnerable;

        private static void ToggleGodMode(GameManager gm)
        {
            bool on = !GodMode(gm);
            gm.Warmth.Invulnerable = on;
            gm.Spark.Invulnerable = on;
            Debug.Log("[Debug] Бессмертие: " + (on ? "вкл" : "выкл"));
        }

        private static void AddFuel(GameManager gm)
        {
            if (gm.Config == null) return;
            foreach (var f in gm.Config.fuelTypes) if (f != null) gm.Inventory.Add(f, 10);
        }

        /// <summary>
        /// F2: к ближайшему впереди чекпоинту (или зоне поломки фургона — без неё акт 3 не начать). По дороге срабатывают
        /// пропущенные начала актов, чтобы погода и акт соответствовали месту; затем срабатывает сама зона (сохранение чекпоинта).
        /// </summary>
        public static void TeleportToNextCheckpoint(GameManager gm)
        {
            var path = RoadPath.Load();
            if (path == null) return;
            float here = gm.Progress.Distance;
            LevelTrigger target = null;
            foreach (var t in LevelTrigger.All)
            {
                if (t == null || (t.kind != LevelTriggerKind.Checkpoint && t.kind != LevelTriggerKind.Breakdown)) continue;
                if (t.distance > here + 3f) { target = t; break; } // All отсортирован по расстоянию
            }
            if (target == null) { Debug.Log("[Debug] F2: впереди больше нет чекпоинтов."); return; }

            // Акт 3 начинается сценой поломки фургона — перепрыгнуть её нельзя.
            var breakdown = UnityEngine.Object.FindAnyObjectByType<LastRide.Narrative.BreakdownDirector>();
            if (breakdown != null && breakdown.Running) { Debug.Log("[Debug] F2: идёт сцена поломки."); return; }
            if (breakdown != null && !breakdown.Done && target.kind != LevelTriggerKind.Breakdown)
            {
                foreach (var t in LevelTrigger.All)
                    if (t != null && t.kind == LevelTriggerKind.Breakdown && t.distance < target.distance) { target = t; break; }
            }

            foreach (var t in LevelTrigger.All)
                if (t != null && t.kind == LevelTriggerKind.ActStart && t.distance <= target.distance && !LevelTrigger.HasFired(t.id)) t.Fire();

            var s = path.Evaluate(target.distance);
            Vector3 right = Vector3.Cross(Vector3.up, s.flatTangent).normalized;
            var van = UnityEngine.Object.FindAnyObjectByType<VanController>();
            var robot = RobotController.Active;
            bool onFoot = gm.PlayerMode != PlayerMode.InVan;

            if (van != null && !(gm.PlayerMode == PlayerMode.OnFootWithLamp))
                van.ResetTo(s.position + Vector3.up * 0.6f, Quaternion.LookRotation(s.flatTangent, Vector3.up));
            if (robot != null && onFoot)
            {
                float yaw = Mathf.Atan2(s.flatTangent.x, s.flatTangent.z) * Mathf.Rad2Deg;
                robot.Teleport(s.position + right * 2.4f + Vector3.up * 0.8f, yaw);
            }
            if (VanCamera.Active != null) VanCamera.Active.Snap();
            gm.Progress.ResetHint();
            gm.Progress.Tick(gm);

            if (target.kind == LevelTriggerKind.Breakdown && breakdown != null && !breakdown.Done)
            {
                gm.SetPlayerMode(PlayerMode.InVan); // сцена начинается, когда игрок в фургоне
                if (!target.Fire()) breakdown.Trigger(); // зона уже срабатывала — запустить сцену вручную
            }
            else target.Fire();
            Debug.Log($"[Debug] F2 → {target.id} ({target.distance:0} м)");
        }

        private void OnGUI()
        {
            if (!OverlayVisible) return;
            EnsureStyle();

            _sb.Clear();
            var gm = GameManager.Instance;
            _sb.Append("FPS: ").Append(_fpsSmoothed.ToString("0")).Append("  (").Append((1000f / Mathf.Max(1f, _fpsSmoothed)).ToString("0.0")).Append(" мс)\n");
            if (gm != null)
            {
                _sb.Append("Состояние: ").Append(gm.State).Append('\n');
                _sb.Append("Режим: ").Append(gm.PlayerMode).Append('\n');
                _sb.Append("Время: ×").Append(gm.DebugTimeMultiplier.ToString("0.#")).Append('\n');
                if (GodMode(gm)) _sb.Append("БЕССМЕРТИЕ: вкл\n");
                if (gm.Difficulty != null) _sb.Append("Сложность: ").Append(gm.Difficulty.displayName).Append('\n');
            }

            foreach (var line in Lines)
            {
                string v;
                try { v = line.Value(); }
                catch (Exception e) { v = "<" + e.GetType().Name + ">"; }
                _sb.Append(line.Key).Append(": ").Append(v).Append('\n');
            }
            _sb.Append("\nF1 — оверлей   F2 — след. чекпоинт   F3 — +10 топлива   F4 — бессмертие   F5 — время ×4");

            var content = new GUIContent(_sb.ToString());
            var size = _style.CalcSize(content);
            GUI.Box(new Rect(10, 10, size.x + 4, size.y + 4), content, _style);
        }

        private void EnsureStyle()
        {
            if (_style != null) return;
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _bg.Apply();
            _style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(8, 8, 6, 6),
                richText = false
            };
            _style.normal.background = _bg;
            _style.normal.textColor = Color.white;
        }

        private void OnDestroy()
        {
            if (_bg != null) Destroy(_bg);
        }
    }
}
