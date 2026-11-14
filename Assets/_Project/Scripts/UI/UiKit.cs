using LastRide.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Общие строительные блоки меню (главное меню, пауза, настройки): единый стиль, канвас, кнопка, надпись,
    /// EventSystem с картой <c>UI</c> из project-wide Input Actions. Весь интерфейс собирается кодом (как и остальной UI игры).
    /// </summary>
    public static class UiKit
    {
        /// <summary>Открыть окно настроек (из главного меню и паузы). Подставляет 11.5; null — кнопки «Настройки» отключены.</summary>
        public static System.Action OpenSettings { get; set; }

        public static readonly Color Accent = new Color(1f, 0.62f, 0.25f, 1f);
        public static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.11f, 0.84f);
        public static readonly Color TextColor = new Color(0.96f, 0.98f, 1f, 1f);
        public static readonly Color DimColor = new Color(0.62f, 0.68f, 0.78f, 1f);
        public static readonly Color ButtonColor = new Color(0.06f, 0.09f, 0.14f, 0.82f);

        /// <summary>
        /// EventSystem с <see cref="InputSystemUIInputModule"/>, привязанным к карте UI проекта (Point/Click/Navigate/Submit/Cancel…).
        /// Карту включает <c>GameInput.ApplyContext</c> (главное меню, пауза, титры). Один на игру, живёт под <paramref name="parent"/>.
        /// </summary>
        public static void EnsureEventSystem(Transform parent)
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                if (parent != null) go.transform.SetParent(parent, false);
                es = go.AddComponent<EventSystem>();
            }
            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();

            var asset = GameInput.Asset;
            var ui = asset != null ? asset.FindActionMap(InputNames.MapUI) : null;
            if (ui == null) return;
            module.actionsAsset = asset;
            module.point = Ref(ui, "Point");
            module.leftClick = Ref(ui, "Click");
            module.rightClick = Ref(ui, "RightClick");
            module.middleClick = Ref(ui, "MiddleClick");
            module.scrollWheel = Ref(ui, "ScrollWheel");
            module.move = Ref(ui, "Navigate");
            module.submit = Ref(ui, "Submit");
            module.cancel = Ref(ui, "Cancel");
        }

        private static InputActionReference Ref(InputActionMap map, string action)
        {
            var a = map.FindAction(action);
            return a != null ? InputActionReference.Create(a) : null;
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(parent, "Label");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.outlineWidth = 0.16f;
            t.outlineColor = new Color32(14, 18, 28, 255);
            return t;
        }

        /// <summary>Кнопка меню: подложка, надпись по центру; подсветка при наведении/выборе клавиатурой.</summary>
        public static Button MakeButton(Transform parent, string text, float width, float height, UnityAction onClick, float fontSize = 34f)
        {
            var img = Panel(parent, "Button_" + text, ButtonColor);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.9f, 1.25f, 0.8f, 1f);
            colors.selectedColor = new Color(1.9f, 1.25f, 0.8f, 1f);
            colors.pressedColor = new Color(2.2f, 1.4f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.55f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = Label(rt, text, fontSize, TextColor, TextAlignmentOptions.Left);
            label.gameObject.name = "Text";
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(34f, 0f);
            lrt.offsetMax = new Vector2(-16f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return btn;
        }

        /// <summary>Ползунок в стиле меню (подложка, заливка акцентом, ручка).</summary>
        public static Slider MakeSlider(Transform parent, float min, float max, float value, UnityAction<float> onChange, float width = 420f, float height = 44f)
        {
            var rt = Rect(parent, "Slider");
            rt.sizeDelta = new Vector2(width, height);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            var bg = Panel(rt, "Background", ButtonColor);
            bg.rectTransform.anchorMin = new Vector2(0f, 0.32f);
            bg.rectTransform.anchorMax = new Vector2(1f, 0.68f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var fillArea = Rect(rt, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.32f);
            fillArea.anchorMax = new Vector2(1f, 0.68f);
            fillArea.offsetMin = new Vector2(0f, 0f);
            fillArea.offsetMax = new Vector2(-12f, 0f);
            var fill = Panel(fillArea, "Fill", Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect(rt, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(6f, 0f);
            handleArea.offsetMax = new Vector2(-6f, 0f);
            var handle = Panel(handleArea, "Handle", TextColor);
            handle.rectTransform.sizeDelta = new Vector2(18f, 0f);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            var colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.78f, 0.5f, 1f);
            colors.selectedColor = new Color(1f, 0.78f, 0.5f, 1f);
            colors.pressedColor = new Color(1f, 0.7f, 0.35f, 1f);
            slider.colors = colors;
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        /// <summary>Выбор из списка «◄ значение ►»: <paramref name="onStep"/> получает -1/+1; текст обновляется через возвращаемый label.</summary>
        public static CycleSelector MakeCycle(Transform parent, UnityAction<int> onStep, out TextMeshProUGUI valueLabel, float width = 420f, float height = 54f)
        {
            var img = Panel(parent, "Cycle", ButtonColor);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            var sel = rt.gameObject.AddComponent<CycleSelector>();
            sel.targetGraphic = img;
            var colors = sel.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.9f, 1.25f, 0.8f, 1f);
            colors.selectedColor = new Color(1.9f, 1.25f, 0.8f, 1f);
            colors.pressedColor = new Color(2.2f, 1.4f, 0.8f, 1f);
            colors.fadeDuration = 0.08f;
            sel.colors = colors;
            sel.Step = d => onStep?.Invoke(d);

            valueLabel = Label(rt, "", 30f, TextColor, TextAlignmentOptions.Center);
            var vrt = valueLabel.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(46f, 0f);
            vrt.offsetMax = new Vector2(-46f, 0f);

            var left = Label(rt, "◄", 28f, DimColor, TextAlignmentOptions.MidlineLeft);
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = Vector2.one;
            left.rectTransform.offsetMin = new Vector2(14f, 0f);
            left.rectTransform.offsetMax = Vector2.zero;
            var right = Label(rt, "►", 28f, DimColor, TextAlignmentOptions.MidlineRight);
            right.rectTransform.anchorMin = Vector2.zero;
            right.rectTransform.anchorMax = Vector2.one;
            right.rectTransform.offsetMin = Vector2.zero;
            right.rectTransform.offsetMax = new Vector2(-14f, 0f);
            return sel;
        }

        /// <summary>Выбрать кнопку для управления с клавиатуры (на следующем кадре, чтобы не потерялось при включении).</summary>
        public static void Select(GameObject go)
        {
            if (EventSystem.current != null && go != null) EventSystem.current.SetSelectedGameObject(go);
        }
    }
}
