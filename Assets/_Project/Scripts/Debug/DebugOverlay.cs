using System;
using System.Collections.Generic;
using System.Text;
using LastRide.Core;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Отладочные инструменты (ТЗ 10), только в Editor / Development build.
    /// F1 — оверлей, F5 — ускорение времени ×4. F2–F4 добавятся вместе с нужными системами.
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

            if (GameInput.DebugTimeScalePressed && GameManager.Instance != null)
            {
                var gm = GameManager.Instance;
                gm.DebugTimeMultiplier = gm.DebugTimeMultiplier > 1.01f ? 1f : FastTimeMultiplier;
            }
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
                if (gm.Difficulty != null) _sb.Append("Сложность: ").Append(gm.Difficulty.displayName).Append('\n');
            }

            foreach (var line in Lines)
            {
                string v;
                try { v = line.Value(); }
                catch (Exception e) { v = "<" + e.GetType().Name + ">"; }
                _sb.Append(line.Key).Append(": ").Append(v).Append('\n');
            }
            _sb.Append("\nF1 — оверлей   F5 — время ×4");

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
