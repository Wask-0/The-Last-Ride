using LastRide.Core;
using LastRide.Fire;
using LastRide.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Минимальный HUD (ТЗ 6.1): в левом верхнем углу, полупрозрачный, без рамок.
    /// Значок пламени + тонкая шкала — запас Искры (печка или лампа); значок шестерёнки + шкала — тепло робота;
    /// маленький флажок-индикатор ветра (виден в акте 2, берёт <c>GameManager.Weather</c>: обвис — затишье, рвётся — шторм/порыв). Шкалы мигают красным ниже
    /// <c>presentation.hudCriticalFraction</c>; при тлеющей Искре пламя мигает чаще. Значки рисуются кодом (процедурные спрайты).
    /// Подсказки взаимодействия и панель печки — отдельные канвасы (<see cref="InteractionPromptView"/>, <see cref="StovePanel"/>).
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private const float BarWidth = 190f;
        private const float BarHeight = 7f;
        private const int FlagStrips = 6;

        private static readonly Color Warm = new Color(1f, 0.78f, 0.42f, 1f);
        private static readonly Color Cold = new Color(0.62f, 0.85f, 1f, 1f);
        private static readonly Color Alarm = new Color(1f, 0.25f, 0.18f, 1f);

        private CanvasGroup _group;
        private Image _flameIcon, _flameFill, _gearIcon, _gearFill, _windPole;
        private RectTransform _windRoot;
        private readonly RectTransform[] _strips = new RectTransform[FlagStrips];
        private Image[] _stripImages;
        private float _flameShown, _gearShown, _alpha;
        private GameBalance.PresentationSettings _fallback;

        private GameBalance.PresentationSettings P
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.presentation;
                return _fallback ??= new GameBalance.PresentationSettings();
            }
        }

        private void Awake() => BuildUi();

        private void Update()
        {
            var gm = GameManager.Instance;
            var p = P;
            bool visible = gm != null && gm.IsGameplayActive;
            _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            _group.alpha = _alpha * p.hudAlpha;
            if (_alpha <= 0.001f || gm == null) return;

            float dt = Time.unscaledDeltaTime;
            float step = p.hudBarSpeed * dt;

            // --- Искра ---
            var spark = gm.Spark;
            bool lit = spark.IsLit;
            bool embers = spark.State == SparkState.Embers;
            float flameTarget = lit ? spark.Fraction : 0f;
            _flameShown = Mathf.MoveTowards(_flameShown, flameTarget, step);
            bool flameCritical = spark.State != SparkState.Unlit && flameTarget < p.hudCriticalFraction;
            float flameBlink = flameCritical ? Blink(p.hudBlinkHz * (embers ? 2f : 1f)) : 1f;
            Color flameColor = flameCritical ? Color.Lerp(Alarm * 0.5f, Alarm, flameBlink) : Color.Lerp(Warm * 0.85f, Warm, flameTarget);
            _flameFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_flameShown), 1f);
            _flameFill.color = flameColor;
            _flameIcon.color = spark.State == SparkState.Unlit ? new Color(1f, 1f, 1f, 0.25f) : flameColor;
            SetAlpha(_flameIcon, flameCritical ? 0.4f + 0.6f * flameBlink : 1f, spark.State == SparkState.Unlit ? 0.25f : 1f);

            // --- Тепло ---
            float warmth = gm.Warmth.Fraction;
            _gearShown = Mathf.MoveTowards(_gearShown, warmth, step);
            bool gearCritical = warmth < p.hudCriticalFraction;
            float gearBlink = gearCritical ? Blink(p.hudBlinkHz) : 1f;
            Color gearColor = gearCritical ? Color.Lerp(Alarm * 0.5f, Alarm, gearBlink) : Color.Lerp(Cold * 0.8f, Cold, warmth);
            _gearFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_gearShown), 1f);
            _gearFill.color = gearColor;
            _gearIcon.color = gearColor;
            SetAlpha(_gearIcon, gearCritical ? 0.4f + 0.6f * gearBlink : 1f, 1f);
            _gearIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 20f * warmth);

            // --- Ветер ---
            bool showWind = gm.Weather != null && gm.Weather.WindIndicatorVisible;
            _windRoot.gameObject.SetActive(showWind);
            if (showWind) AnimateFlag(gm.Weather.WindIndicator01);
        }

        private static float Blink(float hz) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f);

        private static void SetAlpha(Image img, float k, float baseAlpha)
        {
            var c = img.color;
            c.a = baseAlpha * k;
            img.color = c;
        }

        /// <summary>Флажок: в затишье обвисает, в шторм вытягивается по ветру и бьётся.</summary>
        private void AnimateFlag(float wind)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < FlagStrips; i++)
            {
                float k = (i + 1f) / FlagStrips;
                float droop = (1f - wind) * k * 12f;
                float flutter = Mathf.Sin(t * (4f + wind * 14f) - i * 1.1f) * wind * (2f + 5f * k);
                _strips[i].anchoredPosition = new Vector2(i * 4.5f, -droop + flutter);
                _strips[i].sizeDelta = new Vector2(4.5f, Mathf.Lerp(12f, 7f, k * 0.6f));
            }
            var pole = _windPole.color;
            pole.a = 1f;
        }

        // ------------------------------------------------------------------ построение

        private void BuildUi()
        {
            var canvasGo = new GameObject("HudCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var root = NewRect("Root", canvasGo.transform);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(40f, -36f);
            root.sizeDelta = new Vector2(260f, 120f);

            var flameSprite = MakeSprite(FlameShape);
            var gearSprite = MakeSprite(GearShape);

            _flameIcon = BuildRow(root, "Flame", 0f, flameSprite, out _flameFill);
            _gearIcon = BuildRow(root, "Gear", -40f, gearSprite, out _gearFill);

            // Индикатор ветра: шест + полоски-«ткань».
            _windRoot = NewRect("Wind", root);
            _windRoot.anchorMin = _windRoot.anchorMax = new Vector2(0f, 1f);
            _windRoot.pivot = new Vector2(0f, 1f);
            _windRoot.anchoredPosition = new Vector2(4f, -86f);
            _windRoot.sizeDelta = new Vector2(60f, 28f);
            var pole = NewRect("Pole", _windRoot);
            pole.anchorMin = pole.anchorMax = new Vector2(0f, 1f);
            pole.pivot = new Vector2(0.5f, 1f);
            pole.anchoredPosition = Vector2.zero;
            pole.sizeDelta = new Vector2(3f, 28f);
            _windPole = pole.gameObject.AddComponent<Image>();
            _windPole.color = new Color(1f, 1f, 1f, 0.8f);
            _windPole.raycastTarget = false;
            var cloth = NewRect("Cloth", _windRoot);
            cloth.anchorMin = cloth.anchorMax = new Vector2(0f, 1f);
            cloth.pivot = new Vector2(0f, 1f);
            cloth.anchoredPosition = new Vector2(3f, 0f);
            _stripImages = new Image[FlagStrips];
            for (int i = 0; i < FlagStrips; i++)
            {
                var s = NewRect("Strip" + i, cloth);
                s.anchorMin = s.anchorMax = new Vector2(0f, 1f);
                s.pivot = new Vector2(0f, 1f);
                _strips[i] = s;
                _stripImages[i] = s.gameObject.AddComponent<Image>();
                _stripImages[i].color = Color.Lerp(Warm, Color.white, 0.4f);
                _stripImages[i].raycastTarget = false;
            }
            _windRoot.gameObject.SetActive(false);
        }

        private static Image BuildRow(RectTransform root, string name, float y, Sprite icon, out Image fill)
        {
            var row = NewRect(name, root);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(260f, 32f);

            var iconRt = NewRect("Icon", row);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(16f, 0f);
            iconRt.sizeDelta = new Vector2(30f, 30f);
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.raycastTarget = false;
            AddDarkOutline(iconImg.gameObject);

            var bar = NewRect("Bar", row);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = new Vector2(44f, 0f);
            bar.sizeDelta = new Vector2(BarWidth, BarHeight);
            var bg = bar.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.16f);
            bg.raycastTarget = false;
            AddDarkOutline(bar.gameObject);

            var f = NewRect("Fill", bar);
            f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one;
            f.offsetMin = f.offsetMax = Vector2.zero;
            fill = f.gameObject.AddComponent<Image>();
            fill.raycastTarget = false;
            return iconImg;
        }

        /// <summary>Мягкая тёмная обводка — чтобы HUD читался и на светлом снегу, и в тёмной пурге.</summary>
        private static void AddDarkOutline(GameObject go)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.04f, 0.05f, 0.09f, 0.7f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            o.useGraphicAlpha = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // ------------------------------------------------------------------ процедурные значки

        /// <summary>Капля пламени: u,v ∈ [0,1], v вверх. Возвращает покрытие 0..1.</summary>
        private static float FlameShape(float u, float v)
        {
            float x = Mathf.Abs(u - 0.5f);
            float y = (v - 0.06f) / 0.9f;
            if (y < 0f || y > 1f) return 0f;
            float half = 0.36f * Mathf.Pow(1f - y, 0.7f) * Mathf.Clamp01(Mathf.Sqrt(y) * 2.2f);
            // Вырез-язычок внутри.
            float inner = 0.1f * Mathf.Pow(1f - Mathf.Clamp01((y - 0.12f) / 0.4f), 0.8f);
            bool hole = y > 0.12f && y < 0.52f && x < inner * 0.6f;
            return x < half && !hole ? 1f : 0f;
        }

        /// <summary>Шестерёнка: круг с зубцами и отверстием.</summary>
        private static float GearShape(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Atan2(dy, dx);
            float tooth = Mathf.Cos(a * 8f) > 0.1f ? 0.1f : 0f;
            float outer = 0.33f + tooth;
            return r < outer && r > 0.13f ? 1f : 0f;
        }

        private static Sprite MakeSprite(System.Func<float, float, float> shape)
        {
            const int size = 64, ss = 3; // суперсэмплинг для сглаживания краёв
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                    sum += shape((x + (sx + 0.5f) / ss) / size, (y + (sy + 0.5f) / ss) / size);
                byte a = (byte)Mathf.RoundToInt(255f * sum / (ss * ss));
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
