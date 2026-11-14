using System.Collections.Generic;
using LastRide.Audio;
using LastRide.Core;
using LastRide.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Окно настроек (ТЗ 6.2): громкость (общая/музыка/эффекты/голос), качество графики (пресеты URP из QualitySettings),
    /// разрешение и полный экран, чувствительность мыши, инверсия Y, субтитры. Значения применяются сразу и пишутся в PlayerPrefs.
    /// Открывается из главного меню и паузы через <see cref="UiKit.OpenSettings"/>; закрывается кнопкой «Назад» или Esc.
    /// Живёт на GameManager, интерфейс собирается кодом при первом открытии.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        private const float RowHeight = 62f;
        private const float LabelWidth = 470f;
        private const float ControlWidth = 430f;

        private GameObject _root;
        private Button _back;
        private readonly List<Selectable> _disabledBehind = new List<Selectable>();
        private int _openedFrame = -1;

        // Контролы, значения которых надо обновить при открытии.
        private readonly Slider[] _volumeSliders = new Slider[4];
        private readonly TextMeshProUGUI[] _volumeTexts = new TextMeshProUGUI[4];
        private static readonly AudioBus[] VolumeBuses = { AudioBus.Master, AudioBus.Music, AudioBus.SFX, AudioBus.Voice };
        private static readonly string[] VolumeNames = { "Общая громкость", "Музыка", "Эффекты", "Голос" };
        private Slider _mouse;
        private TextMeshProUGUI _mouseText;
        private TextMeshProUGUI _quality, _resolution, _fullscreen, _invert, _subtitles;
        private List<Vector2Int> _resolutions = new List<Vector2Int>();

        public static bool IsOpen { get; private set; }

        /// <summary>Кадр, в который окно закрылось (меню под ним не должно отреагировать на тот же Esc).</summary>
        public static int ClosedFrame { get; private set; } = -1;

        private void Awake()
        {
            UiKit.OpenSettings = Open;
            GameSettings.ApplyOnStartup();
        }

        private void OnDestroy()
        {
            if (UiKit.OpenSettings == (System.Action)Open) UiKit.OpenSettings = null;
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (Time.frameCount != _openedFrame && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            if (_root == null) Build();
            Refresh();
            _root.SetActive(true);
            IsOpen = true;
            _openedFrame = Time.frameCount;

            // Пока окно открыто, элементы меню под ним недоступны (мышь блокирует подложка, клавиатуру — это).
            _disabledBehind.Clear();
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.interactable || s.transform.IsChildOf(_root.transform)) continue;
                s.interactable = false;
                _disabledBehind.Add(s);
            }
            UiKit.Select(_volumeSliders[0].gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            GameSettings.Save();
            AudioManagerPrefsSave();
            _root.SetActive(false);
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            foreach (var s in _disabledBehind) if (s != null) s.interactable = true;
            _disabledBehind.Clear();
        }

        private static void AudioManagerPrefsSave() => PlayerPrefs.Save();

        // ------------------------------------------------------------------ интерфейс

        private void Build()
        {
            var canvas = UiKit.CreateCanvas(transform, "SettingsCanvas", 150);
            _root = canvas.gameObject;

            var dim = UiKit.Panel(canvas.transform, "Dim", new Color(0.02f, 0.03f, 0.06f, 0.92f));
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            var title = UiKit.Label(drt, "Настройки", 68f, UiKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -36f);
            trt.sizeDelta = new Vector2(900f, 90f);

            var list = UiKit.Rect(drt, "Rows");
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = new Vector2(0f, -140f);
            list.sizeDelta = new Vector2(LabelWidth + ControlWidth + 90f, 800f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < VolumeBuses.Length; i++)
            {
                int idx = i;
                var row = Row(list, VolumeNames[i]);
                _volumeSliders[i] = UiKit.MakeSlider(row, 0f, 1f, 1f, v => OnVolume(idx, v), ControlWidth - 96f, RowHeight - 8f);
                _volumeTexts[i] = UiKit.Label(row, "100%", 28f, UiKit.DimColor, TextAlignmentOptions.MidlineRight);
                SizeLabel(_volumeTexts[i], 96f);
            }

            var q = Row(list, "Качество графики");
            UiKit.MakeCycle(q, StepQuality, out _quality, ControlWidth, RowHeight - 8f);
            var r = Row(list, "Разрешение");
            UiKit.MakeCycle(r, StepResolution, out _resolution, ControlWidth, RowHeight - 8f);
            var f = Row(list, "Полный экран");
            UiKit.MakeCycle(f, _ => { GameSettings.Fullscreen = !GameSettings.Fullscreen; Refresh(); }, out _fullscreen, ControlWidth, RowHeight - 8f);

            var m = Row(list, "Чувствительность мыши");
            _mouse = UiKit.MakeSlider(m, GameSettings.MouseMin, GameSettings.MouseMax, 1f, OnMouse, ControlWidth - 96f, RowHeight - 8f);
            _mouseText = UiKit.Label(m, "×1.0", 28f, UiKit.DimColor, TextAlignmentOptions.MidlineRight);
            SizeLabel(_mouseText, 96f);

            var inv = Row(list, "Инверсия оси Y");
            UiKit.MakeCycle(inv, _ => { GameSettings.InvertY = !GameSettings.InvertY; Refresh(); }, out _invert, ControlWidth, RowHeight - 8f);
            var sub = Row(list, "Субтитры");
            UiKit.MakeCycle(sub, _ => { SubtitleView.Enabled = !SubtitleView.Enabled; Refresh(); }, out _subtitles, ControlWidth, RowHeight - 8f);

            var gm = GameManager.Instance;
            float font = gm != null && gm.Balance != null ? gm.Balance.menu.buttonFontSize : 36f;
            var backRow = UiKit.Rect(list, "BackRow");
            backRow.sizeDelta = new Vector2(LabelWidth + ControlWidth, 90f);
            var le = backRow.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = LabelWidth + ControlWidth;
            le.preferredHeight = 90f;
            var hl = backRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            _back = UiKit.MakeButton(backRow, "Назад", 360f, 70f, Close, font);

            _root.SetActive(false);
        }

        private static Transform Row(Transform parent, string label)
        {
            var row = UiKit.Rect(parent, "Row_" + label);
            row.sizeDelta = new Vector2(LabelWidth + ControlWidth, RowHeight);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = LabelWidth + ControlWidth;
            le.preferredHeight = RowHeight;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.spacing = 0f;

            var text = UiKit.Label(row, label, 32f, UiKit.TextColor, TextAlignmentOptions.MidlineLeft);
            SizeLabel(text, LabelWidth);
            return row;
        }

        private static void SizeLabel(TextMeshProUGUI t, float width)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(width, RowHeight);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = RowHeight;
        }

        // ------------------------------------------------------------------ значения

        private void Refresh()
        {
            var am = AudioManager.Instance;
            for (int i = 0; i < VolumeBuses.Length; i++)
            {
                float v = am != null ? am.GetUserVolume(VolumeBuses[i]) : PlayerPrefs.GetFloat(AudioBusInfo.PrefKeys[(int)VolumeBuses[i]], 1f);
                _volumeSliders[i].SetValueWithoutNotify(v);
                _volumeTexts[i].text = Mathf.RoundToInt(v * 100f) + "%";
            }

            var names = GameSettings.QualityNames;
            _quality.text = names.Length > 0 ? names[Mathf.Clamp(GameSettings.QualityLevel, 0, names.Length - 1)] : "—";

            _resolutions = GameSettings.AvailableResolutions();
            var res = GameSettings.Resolution;
            _resolution.text = res.x + " × " + res.y;
            _fullscreen.text = GameSettings.Fullscreen ? "Вкл" : "Выкл";

            float ms = GameSettings.MouseSensitivity;
            _mouse.SetValueWithoutNotify(ms);
            _mouseText.text = "×" + ms.ToString("0.0");
            _invert.text = GameSettings.InvertY ? "Вкл" : "Выкл";
            _subtitles.text = SubtitleView.Enabled ? "Вкл" : "Выкл";
        }

        private void OnVolume(int index, float v)
        {
            var am = AudioManager.Instance;
            if (am != null) am.SetUserVolume(VolumeBuses[index], v);
            else PlayerPrefs.SetFloat(AudioBusInfo.PrefKeys[(int)VolumeBuses[index]], v);
            _volumeTexts[index].text = Mathf.RoundToInt(v * 100f) + "%";
        }

        private void OnMouse(float v)
        {
            v = Mathf.Round(v * 10f) / 10f;
            GameSettings.MouseSensitivity = v;
            _mouseText.text = "×" + v.ToString("0.0");
        }

        private void StepQuality(int d)
        {
            int n = GameSettings.QualityNames.Length;
            if (n == 0) return;
            GameSettings.QualityLevel = (GameSettings.QualityLevel + d + n) % n;
            Refresh();
        }

        private void StepResolution(int d)
        {
            if (_resolutions.Count == 0) return;
            int i = Mathf.Max(0, _resolutions.IndexOf(GameSettings.Resolution));
            GameSettings.Resolution = _resolutions[(i + d + _resolutions.Count) % _resolutions.Count];
            Refresh();
        }
    }
}
