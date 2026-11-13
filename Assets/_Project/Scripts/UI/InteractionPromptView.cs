using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Рисует <see cref="InteractionPrompt.Current"/> внизу по центру: плавное появление/исчезновение, без рамок.
    /// Сам строит свой Canvas (uGUI + TMP). Шрифт — по умолчанию из TMP Settings (кириллица — «Setup/Create UI Font»).
    /// Позже HUD (5.5) может взять этот компонент себе.
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        public float fadeSpeed = 8f;
        public float bottomOffset = 150f;
        public float fontSize = 34f;
        public Color color = new Color(1f, 0.93f, 0.8f, 0.95f);

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private string _shown = "";

        private void Awake()
        {
            var canvasGo = new GameObject("PromptCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var textGo = new GameObject("PromptText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = fontSize;
            _label.color = color;
            _label.raycastTarget = false;
            // Тёмная обводка: читается и на белом снегу, и на фоне фургона.
            _label.outlineWidth = 0.22f;
            _label.outlineColor = new Color32(28, 24, 38, 255);
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = _label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottomOffset);
            rt.sizeDelta = new Vector2(1200f, 60f);
        }

        private void Update()
        {
            string current = InteractionPrompt.Current;
            if (!string.IsNullOrEmpty(current) && current != _shown)
            {
                _shown = current;
                _label.text = current;
            }
            float target = string.IsNullOrEmpty(current) ? 0f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
