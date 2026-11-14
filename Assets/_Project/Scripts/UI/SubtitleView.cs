using LastRide.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Субтитры аудиозаписей (ТЗ 6.1, 7.1): внизу по центру, строка за строкой по событиям <see cref="AudioLogLineChanged"/>.
    /// «Радио-эффект» — в начале каждой строки (<c>subtitles.glitchSeconds</c>) буквы подменяются помехами и текст дрожит,
    /// затем помехи рассеиваются, остаётся лёгкое мерцание. Пауза игры не мешает: эффект идёт по unscaled-времени.
    /// Включение — <see cref="Enabled"/> (PlayerPrefs, по умолчанию вкл; переключатель в настройках — этап 11).
    /// Создаётся GameManager-ом и живёт между сценами. Параметры — <c>GameBalance.subtitles</c>.
    /// </summary>
    public sealed class SubtitleView : MonoBehaviour
    {
        public const string PrefKey = "opt_subtitles";
        private const string NoiseGlyphs = "#%&*+=/<>?|~";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) != 0;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); }
        }

        private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.86f, 1f);
        private static readonly Color OutlineColor = new Color32(22, 26, 30, 255);

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private RectTransform _rt;
        private Vector2 _basePos;
        private string _target = string.Empty;
        private float _lineAge;
        private bool _wantVisible;
        private GameBalance.SubtitleSettings _fallback;
        private System.Text.StringBuilder _sb = new System.Text.StringBuilder(128);

        private GameBalance.SubtitleSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.subtitles;
                return _fallback ??= new GameBalance.SubtitleSettings();
            }
        }

        private void Awake()
        {
            var canvasGo = new GameObject("SubtitleCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var textGo = new GameObject("SubtitleText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Bottom;
            _label.fontSize = Cfg.fontSize;
            _label.color = TextColor;
            _label.raycastTarget = false;
            _label.outlineWidth = 0.25f;
            _label.outlineColor = OutlineColor;
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.richText = false;
            _rt = _label.rectTransform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0f);
            _rt.pivot = new Vector2(0.5f, 0f);
            _basePos = new Vector2(0f, Cfg.bottomOffset);
            _rt.anchoredPosition = _basePos;
            _rt.sizeDelta = new Vector2(1400f, 130f);
        }

        private void OnEnable()
        {
            EventBus<AudioLogLineChanged>.Subscribe(OnLine);
            EventBus<AudioLogFinished>.Subscribe(OnFinished);
        }

        private void OnDisable()
        {
            EventBus<AudioLogLineChanged>.Unsubscribe(OnLine);
            EventBus<AudioLogFinished>.Unsubscribe(OnFinished);
        }

        private void OnLine(AudioLogLineChanged e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                _wantVisible = false;
                return;
            }
            _target = e.Text;
            _lineAge = 0f;
            _wantVisible = true;
        }

        private void OnFinished(AudioLogFinished e) => _wantVisible = false;

        private void Update()
        {
            var cfg = Cfg;
            float dt = Time.unscaledDeltaTime;
            bool show = _wantVisible && Enabled;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, cfg.fadeSpeed * dt);
            if (_group.alpha <= 0.001f) return;

            _lineAge += dt;
            float glitch = cfg.glitchSeconds > 0f ? Mathf.Clamp01(1f - _lineAge / cfg.glitchSeconds) : 0f;
            // Квадрат: помехи быстро редеют, хвост остаётся только лёгким.
            float p = glitch * glitch;

            _label.text = p > 0.01f ? Scramble(_target, p) : _target;

            Vector2 jitter = p > 0.01f ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (cfg.glitchJitterPixels * p) : Vector2.zero;
            _rt.anchoredPosition = _basePos + jitter;

            float flicker = 1f - cfg.flickerAmount * Mathf.PerlinNoise(Time.unscaledTime * 18f, 0.37f);
            float noiseDip = 1f - 0.5f * p * Random.value;
            _label.alpha = flicker * noiseDip;
        }

        private string Scramble(string text, float chance)
        {
            _sb.Clear();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != ' ' && Random.value < chance) _sb.Append(NoiseGlyphs[Random.Range(0, NoiseGlyphs.Length)]);
                else _sb.Append(c);
            }
            return _sb.ToString();
        }
    }
}
