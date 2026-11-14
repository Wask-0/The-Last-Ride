using LastRide.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Контекстные подсказки обучения (ТЗ 3, акт 1): короткая строка вверху по центру, плавно появляется и гаснет сама.
    /// Слушает <see cref="TutorialHintRequested"/>; пустой текст — спрятать. Подсказки взаимодействия («[E] …»)
    /// и субтитры внизу — отдельные канвасы. Создаётся GameManager-ом. Параметры — <c>GameBalance.tutorial</c>.
    /// </summary>
    public sealed class TutorialHintView : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(0.96f, 0.98f, 1f, 1f);

        private static readonly Color BackgroundColor = new Color(0.05f, 0.07f, 0.11f, 0.78f);
        private const float PaddingX = 28f;
        private const float PaddingY = 10f;

        private CanvasGroup _group;
        private Image _background;
        private TextMeshProUGUI _label;
        private float _hideAt = -1f;
        private bool _show;
        private GameBalance.TutorialSettings _fallback;

        private GameBalance.TutorialSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.tutorial;
                return _fallback ??= new GameBalance.TutorialSettings();
            }
        }

        private void Awake()
        {
            var canvasGo = new GameObject("TutorialHintCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Тёмная полупрозрачная подложка: подсказка читается и на светлом небе, и на снегу.
            var bgGo = new GameObject("HintBackground");
            bgGo.transform.SetParent(canvasGo.transform, false);
            _background = bgGo.AddComponent<Image>();
            _background.color = BackgroundColor;
            _background.raycastTarget = false;
            var bgRt = _background.rectTransform;
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 1f);
            bgRt.pivot = new Vector2(0.5f, 1f);

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Top;
            _label.fontSize = Cfg.hintFontSize;
            _label.color = TextColor;
            _label.raycastTarget = false;
            _label.outlineWidth = 0.22f;
            _label.outlineColor = new Color32(24, 28, 40, 255);
            _label.textWrappingMode = TextWrappingModes.Normal;
            var rt = _label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -Cfg.hintTopOffset);
            rt.sizeDelta = new Vector2(1300f, 100f);
            _background.rectTransform.anchoredPosition = rt.anchoredPosition + new Vector2(0f, PaddingY);
        }

        private void FitBackground()
        {
            _label.ForceMeshUpdate();
            var size = new Vector2(Mathf.Min(_label.preferredWidth, 1300f) + PaddingX * 2f, _label.preferredHeight + PaddingY * 2f);
            _background.rectTransform.sizeDelta = size;
        }

        private void OnEnable() => EventBus<TutorialHintRequested>.Subscribe(OnHint);
        private void OnDisable() => EventBus<TutorialHintRequested>.Unsubscribe(OnHint);

        private void OnHint(TutorialHintRequested e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                _show = false;
                return;
            }
            _label.text = e.Text;
            FitBackground();
            _show = true;
            _hideAt = e.Duration > 0f ? Time.unscaledTime + e.Duration : -1f;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (_show && _hideAt >= 0f && Time.unscaledTime >= _hideAt) _show = false;
            bool visible = _show && gm != null && gm.IsGameplayActive;
            _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, Cfg.hintFadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
