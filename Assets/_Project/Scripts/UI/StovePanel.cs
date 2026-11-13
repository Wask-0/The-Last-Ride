using LastRide.Core;
using LastRide.Fire;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Панель печки (ТЗ 4.2, 6): Tab в фургоне открывает/закрывает заслонку (анимация, событие <see cref="StoveFlapChanged"/> для звука),
    /// пока панель открыта — клавиши 1–3 подкидывают щепки / доски / брикеты. Показывает запас огня, окошко с пламенем
    /// (цвет и яркость берёт у <see cref="SparkGlow"/>) и количество топлива. Фургон едет как обычно.
    /// Панель закрывается сама при выходе из фургона, паузе, угасании или переносе Искры в лампу.
    /// UI строится кодом (uGUI + TMP, шрифт по умолчанию — кириллический). Мышь не нужна — курсор в игре захвачен.
    /// </summary>
    public sealed class StovePanel : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        /// <summary>Прогресс заслонки 0 (закрыта) … 1 (открыта).</summary>
        public float OpenProgress => _open;

        private const float PanelWidth = 400f;
        private const float WindowHeight = 110f;
        private const float RowHeight = 54f;

        private static readonly Color TextColor = new Color(1f, 0.93f, 0.8f, 0.96f);
        private static readonly Color DimColor = new Color(1f, 0.93f, 0.8f, 0.4f);
        private static readonly Color WarnColor = new Color(1f, 0.28f, 0.18f, 1f);
        private static readonly Color RowColor = new Color(1f, 0.85f, 0.6f, 0.08f);
        private static readonly Color FlashColor = new Color(1f, 0.8f, 0.4f, 0.5f);
        private static readonly Color DenyColor = new Color(1f, 0.2f, 0.15f, 0.45f);

        private CanvasGroup _group;
        private RectTransform _panel, _flap, _reserveFill;
        private Image _glow;
        private TextMeshProUGUI _reserveLabel, _reserveSeconds;
        private Image _reserveFillImage;
        private readonly Image[] _rowBg = new Image[3];
        private readonly TextMeshProUGUI[] _rowName = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _rowCount = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _rowBonus = new TextMeshProUGUI[3];
        private readonly float[] _flash = new float[3];
        private readonly float[] _deny = new float[3];
        private float _open;
        private GameBalance.PresentationSettings _fallback;

        private GameBalance.PresentationSettings Presentation
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.presentation;
                return _fallback ??= new GameBalance.PresentationSettings();
            }
        }

        // ------------------------------------------------------------------ жизненный цикл

        private void Awake() => BuildUi();

        private void Update()
        {
            var gm = GameManager.Instance;
            bool canUse = gm != null && gm.IsGameplayActive && gm.PlayerMode == PlayerMode.InVan
                          && gm.Spark.IsLit && gm.Spark.Source == FireSourceKind.Stove;

            if (!canUse)
            {
                if (IsOpen) SetOpen(false);
            }
            else
            {
                if (GameInput.StovePressed) SetOpen(!IsOpen);
                if (IsOpen)
                {
                    int slot = GameInput.FuelSlotPressed;
                    if (slot > 0) TryThrow(slot);
                }
            }

            AnimateAndRefresh(gm);
        }

        /// <summary>Открыть/закрыть заслонку.</summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            EventBus<StoveFlapChanged>.Publish(new StoveFlapChanged { Open = open });
        }

        /// <summary>Подкинуть топливо слота 1–3. Нет в инвентаре — красная вспышка строки.</summary>
        public bool TryThrow(int slot)
        {
            var gm = GameManager.Instance;
            var cfg = GameConfig.Load();
            if (gm == null || cfg == null || slot < 1 || slot > 3 || slot > cfg.fuelTypes.Length) return false;
            bool ok = gm.Spark.ThrowFuel(cfg.fuelTypes[slot - 1]);
            if (ok) _flash[slot - 1] = 1f; else _deny[slot - 1] = 1f;
            return ok;
        }

        // ------------------------------------------------------------------ анимация и обновление

        private void AnimateAndRefresh(GameManager gm)
        {
            float dt = Time.unscaledDeltaTime;
            float flapSeconds = Mathf.Max(0.05f, Presentation.stoveFlapSeconds);
            _open = Mathf.MoveTowards(_open, IsOpen ? 1f : 0f, dt / flapSeconds);
            float eased = _open * _open * (3f - 2f * _open); // smoothstep

            _group.alpha = Mathf.Clamp01(_open * 2f);
            _group.blocksRaycasts = false;
            _panel.anchoredPosition = new Vector2(-48f + (1f - eased) * 70f, 0f);
            // Заслонка поднимается над окошком (окошко обрезает её RectMask2D).
            _flap.anchoredPosition = new Vector2(0f, eased * (WindowHeight + 4f));

            if (_open <= 0.001f || gm == null) return;
            var spark = gm.Spark;
            var cfg = GameConfig.Load();

            // Окошко с пламенем: цвет и яркость — у визуала печки.
            var glow = SparkGlow.For(FireSourceKind.Stove);
            float b = glow != null ? glow.Brightness : spark.Fraction;
            Color c = glow != null ? glow.CurrentColor : Color.white;
            _glow.color = new Color(c.r * Mathf.Lerp(0.15f, 1f, b), c.g * Mathf.Lerp(0.15f, 1f, b), c.b * Mathf.Lerp(0.15f, 1f, b), 1f);

            // Запас огня.
            bool embers = spark.State == SparkState.Embers;
            float fraction = embers ? spark.EmbersFraction : spark.Fraction;
            _reserveFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            float blink = embers ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
            _reserveFillImage.color = embers ? Color.Lerp(new Color(0.4f, 0.05f, 0.03f), WarnColor, blink) : Color.Lerp(new Color(0.9f, 0.55f, 0.2f), new Color(1f, 0.8f, 0.45f), spark.Fraction);
            _reserveLabel.text = embers ? "Тлеет!" : "Запас огня";
            _reserveLabel.color = embers ? Color.Lerp(DimColor, WarnColor, blink) : TextColor;
            _reserveSeconds.text = embers ? $"{spark.EmbersLeft:0} с" : $"{spark.Reserve:0} с";

            // Строки топлива.
            for (int i = 0; i < 3; i++)
            {
                var fuel = cfg != null && i < cfg.fuelTypes.Length ? cfg.fuelTypes[i] : null;
                int count = fuel != null ? gm.Inventory.Count(fuel) : 0;
                _rowName[i].text = fuel != null ? $"[{i + 1}]  {fuel.displayName}" : "—";
                _rowCount[i].text = "×" + count;
                _rowBonus[i].text = fuel != null ? $"+{fuel.burnSeconds:0} с" : "";
                bool have = count > 0;
                _rowName[i].color = have ? TextColor : DimColor;
                _rowCount[i].color = have ? TextColor : DimColor;
                _rowBonus[i].color = have ? new Color(1f, 0.75f, 0.4f, 0.9f) : DimColor;

                _flash[i] = Mathf.MoveTowards(_flash[i], 0f, dt * 4f);
                _deny[i] = Mathf.MoveTowards(_deny[i], 0f, dt * 4f);
                Color bg = RowColor;
                bg = Color.Lerp(bg, FlashColor, _flash[i]);
                bg = Color.Lerp(bg, DenyColor, _deny[i]);
                _rowBg[i].color = bg;
                // Короткое «подёргивание» строки при отказе.
                _rowBg[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(_deny[i] * 30f) * 6f * _deny[i], RowY(i));
            }
        }

        // ------------------------------------------------------------------ построение UI

        private static float RowY(int i) => -262f - i * (RowHeight + 6f);

        private void BuildUi()
        {
            var canvasGo = new GameObject("StovePanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _panel = NewRect("Panel", canvasGo.transform);
            _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 0.5f);
            _panel.pivot = new Vector2(1f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, 470f);
            _panel.anchoredPosition = new Vector2(-48f, 0f);
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.09f, 0.8f);
            bg.raycastTarget = false;

            AddText("Title", _panel, "Печка", 30, TextAlignmentOptions.Center, TextColor, new Vector2(0f, -16f), new Vector2(PanelWidth, 40f));

            // Окошко: тёмная рамка, внутри пламя, сверху заслонка. RectMask2D обрезает заслонку, когда она поднимается.
            var window = NewRect("Window", _panel);
            SetTop(window, new Vector2(0f, -66f), new Vector2(360f, WindowHeight));
            window.gameObject.AddComponent<RectMask2D>();
            var frame = window.gameObject.AddComponent<Image>();
            frame.color = new Color(0.03f, 0.02f, 0.02f, 1f);
            frame.raycastTarget = false;

            var glowRt = NewRect("Glow", window);
            Stretch(glowRt, 6f);
            _glow = glowRt.gameObject.AddComponent<Image>();
            _glow.color = new Color(0.2f, 0.1f, 0.05f, 1f);
            _glow.raycastTarget = false;

            _flap = NewRect("Flap", window);
            Stretch(_flap, 0f);
            var flapImg = _flap.gameObject.AddComponent<Image>();
            flapImg.color = new Color(0.17f, 0.17f, 0.2f, 1f);
            flapImg.raycastTarget = false;
            // Ручка и «рёбра» заслонки.
            for (int i = 0; i < 4; i++)
            {
                var rib = NewRect("Rib" + i, _flap);
                rib.anchorMin = new Vector2(0f, 0f); rib.anchorMax = new Vector2(1f, 0f);
                rib.pivot = new Vector2(0.5f, 0f);
                rib.sizeDelta = new Vector2(-24f, 5f);
                rib.anchoredPosition = new Vector2(0f, 14f + i * 24f);
                var ribImg = rib.gameObject.AddComponent<Image>();
                ribImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
                ribImg.raycastTarget = false;
            }

            // Запас огня.
            // Содержимое занимает x ∈ [-180, 180] от центра панели; позиция блока — по его центру.
            _reserveLabel = AddText("ReserveLabel", _panel, "Запас огня", 24, TextAlignmentOptions.Left, TextColor, new Vector2(-80f, -190f), new Vector2(200f, 32f));
            _reserveSeconds = AddText("ReserveSeconds", _panel, "0 с", 24, TextAlignmentOptions.Right, TextColor, new Vector2(100f, -190f), new Vector2(160f, 32f));
            var barBg = NewRect("ReserveBar", _panel);
            SetTop(barBg, new Vector2(0f, -226f), new Vector2(360f, 10f));
            var barBgImg = barBg.gameObject.AddComponent<Image>();
            barBgImg.color = new Color(1f, 1f, 1f, 0.12f);
            barBgImg.raycastTarget = false;
            _reserveFill = NewRect("Fill", barBg);
            _reserveFill.anchorMin = Vector2.zero; _reserveFill.anchorMax = Vector2.one;
            _reserveFill.offsetMin = _reserveFill.offsetMax = Vector2.zero;
            _reserveFillImage = _reserveFill.gameObject.AddComponent<Image>();
            _reserveFillImage.raycastTarget = false;

            // Три строки топлива.
            for (int i = 0; i < 3; i++)
            {
                var row = NewRect("Row" + (i + 1), _panel);
                SetTop(row, new Vector2(0f, RowY(i)), new Vector2(360f, RowHeight));
                _rowBg[i] = row.gameObject.AddComponent<Image>();
                _rowBg[i].color = RowColor;
                _rowBg[i].raycastTarget = false;
                // Ширина строки 360: имя [-170,-20], бонус [-20,70], счётчик [70,170].
                _rowName[i] = AddText("Name", row, "", 26, TextAlignmentOptions.Left, TextColor, new Vector2(-95f, 0f), new Vector2(150f, RowHeight), true);
                _rowBonus[i] = AddText("Bonus", row, "", 22, TextAlignmentOptions.Right, TextColor, new Vector2(25f, 0f), new Vector2(90f, RowHeight), true);
                _rowCount[i] = AddText("Count", row, "", 28, TextAlignmentOptions.Right, TextColor, new Vector2(120f, 0f), new Vector2(100f, RowHeight), true);
            }

            AddText("Footer", _panel, "Tab — закрыть", 20, TextAlignmentOptions.Center, DimColor, new Vector2(0f, -442f), new Vector2(PanelWidth, 28f));
        }

        // ---- помощники построения ----

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Закрепить по верху родителя по центру: позиция — от верхнего края.</summary>
        private static void SetTop(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Текст. <paramref name="middle"/> — позиция от центра родителя (для строк), иначе от верха родителя (x — от центра).</summary>
        private static TextMeshProUGUI AddText(string name, RectTransform parent, string text, float size, TextAlignmentOptions align,
                                                Color color, Vector2 pos, Vector2 box, bool middle = false)
        {
            var rt = NewRect(name, parent);
            if (middle)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
            }
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
