using LastRide.Core;
using LastRide.Fire;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Выбор топлива для лампы (ТЗ 2.2, акт 3): R пешком с лампой открывает/закрывает панель, пока она открыта —
    /// клавиши 1–3 подкидывают щепки / доски / брикеты в лампу (<see cref="SparkFire.ThrowFuel"/>, запас лампы как у печки).
    /// Панель закрывается сама, если лампа не в руке, Искра не в лампе, пауза и т.п. Мышь не нужна.
    /// UI строится кодом (uGUI + TMP), компактно справа по центру.
    /// </summary>
    public sealed class LampPanel : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        private const float PanelWidth = 360f;
        private const float RowHeight = 52f;

        private static readonly Color TextColor = new Color(1f, 0.93f, 0.8f, 0.96f);
        private static readonly Color DimColor = new Color(1f, 0.93f, 0.8f, 0.4f);
        private static readonly Color WarnColor = new Color(1f, 0.28f, 0.18f, 1f);
        private static readonly Color RowColor = new Color(1f, 0.85f, 0.6f, 0.08f);
        private static readonly Color FlashColor = new Color(1f, 0.8f, 0.4f, 0.5f);
        private static readonly Color DenyColor = new Color(1f, 0.2f, 0.15f, 0.45f);

        private CanvasGroup _group;
        private RectTransform _panel, _fill;
        private Image _fillImage;
        private TextMeshProUGUI _reserveLabel, _reserveSeconds;
        private readonly Image[] _rowBg = new Image[3];
        private readonly TextMeshProUGUI[] _rowName = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _rowCount = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _rowBonus = new TextMeshProUGUI[3];
        private readonly float[] _flash = new float[3];
        private readonly float[] _deny = new float[3];
        private float _open;
        private GameBalance.LampHandSettings _fallback;

        private GameBalance.LampHandSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.lampHand;
                return _fallback ??= new GameBalance.LampHandSettings();
            }
        }

        private void Awake() => BuildUi();

        private void Update()
        {
            var gm = GameManager.Instance;
            bool canUse = gm != null && gm.IsGameplayActive && gm.PlayerMode == PlayerMode.OnFootWithLamp
                          && gm.Spark.IsLit && gm.Spark.Source == FireSourceKind.Lamp;

            if (!canUse)
            {
                IsOpen = false;
            }
            else
            {
                if (GameInput.LampPressed)
                {
                    IsOpen = !IsOpen;
                    if (IsOpen) gm.Tutorial.LampPanelOpened();
                }
                if (IsOpen)
                {
                    int slot = GameInput.FuelSlotPressed;
                    if (slot > 0) TryThrow(slot);
                }
            }
            Refresh(gm);
        }

        /// <summary>Подкинуть топливо слота 1–3 в лампу. Нет в инвентаре — красная вспышка строки.</summary>
        public bool TryThrow(int slot)
        {
            var gm = GameManager.Instance;
            var cfg = GameConfig.Load();
            if (gm == null || cfg == null || slot < 1 || slot > 3 || slot > cfg.fuelTypes.Length) return false;
            bool ok = gm.Spark.ThrowFuel(cfg.fuelTypes[slot - 1]);
            if (ok) _flash[slot - 1] = 1f; else _deny[slot - 1] = 1f;
            return ok;
        }

        private void Refresh(GameManager gm)
        {
            float dt = Time.unscaledDeltaTime;
            _open = Mathf.MoveTowards(_open, IsOpen ? 1f : 0f, dt / Mathf.Max(0.05f, Cfg.panelFadeSeconds));
            _group.alpha = _open;
            _panel.anchoredPosition = new Vector2(-48f + (1f - _open) * 40f, 0f);
            if (_open <= 0.001f || gm == null) return;

            var spark = gm.Spark;
            var cfg = GameConfig.Load();
            bool embers = spark.State == SparkState.Embers;
            float fraction = embers ? spark.EmbersFraction : spark.Fraction;
            _fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            float blink = embers ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
            _fillImage.color = embers ? Color.Lerp(new Color(0.4f, 0.05f, 0.03f), WarnColor, blink)
                : Color.Lerp(new Color(0.9f, 0.55f, 0.2f), new Color(1f, 0.8f, 0.45f), spark.Fraction);
            _reserveLabel.text = embers ? "Тлеет!" : "Запас лампы";
            _reserveLabel.color = embers ? Color.Lerp(DimColor, WarnColor, blink) : TextColor;
            _reserveSeconds.text = embers ? $"{spark.EmbersLeft:0} с" : $"{spark.Reserve:0} с";

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
                _rowBg[i].color = Color.Lerp(Color.Lerp(RowColor, FlashColor, _flash[i]), DenyColor, _deny[i]);
                _rowBg[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(_deny[i] * 30f) * 6f * _deny[i], RowY(i));
            }
        }

        // ------------------------------------------------------------------ построение UI

        private static float RowY(int i) => -128f - i * (RowHeight + 6f);

        private void BuildUi()
        {
            var canvasGo = new GameObject("LampPanelCanvas");
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
            _panel.sizeDelta = new Vector2(PanelWidth, 340f);
            _panel.anchoredPosition = new Vector2(-48f, 0f);
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.09f, 0.8f);
            bg.raycastTarget = false;

            AddText("Title", _panel, "Лампа", 30, TextAlignmentOptions.Center, TextColor, new Vector2(0f, -14f), new Vector2(PanelWidth, 40f));
            _reserveLabel = AddText("ReserveLabel", _panel, "Запас лампы", 24, TextAlignmentOptions.Left, TextColor, new Vector2(-70f, -58f), new Vector2(200f, 32f));
            _reserveSeconds = AddText("ReserveSeconds", _panel, "0 с", 24, TextAlignmentOptions.Right, TextColor, new Vector2(100f, -58f), new Vector2(140f, 32f));

            var barBg = NewRect("ReserveBar", _panel);
            SetTop(barBg, new Vector2(0f, -94f), new Vector2(320f, 10f));
            var barBgImg = barBg.gameObject.AddComponent<Image>();
            barBgImg.color = new Color(1f, 1f, 1f, 0.12f);
            barBgImg.raycastTarget = false;
            _fill = NewRect("Fill", barBg);
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = Vector2.one;
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _fillImage = _fill.gameObject.AddComponent<Image>();
            _fillImage.raycastTarget = false;

            for (int i = 0; i < 3; i++)
            {
                var row = NewRect("Row" + (i + 1), _panel);
                SetTop(row, new Vector2(0f, RowY(i)), new Vector2(320f, RowHeight));
                _rowBg[i] = row.gameObject.AddComponent<Image>();
                _rowBg[i].color = RowColor;
                _rowBg[i].raycastTarget = false;
                _rowName[i] = AddText("Name", row, "", 26, TextAlignmentOptions.Left, TextColor, new Vector2(-75f, 0f), new Vector2(150f, RowHeight), true);
                _rowBonus[i] = AddText("Bonus", row, "", 22, TextAlignmentOptions.Right, TextColor, new Vector2(30f, 0f), new Vector2(90f, RowHeight), true);
                _rowCount[i] = AddText("Count", row, "", 28, TextAlignmentOptions.Right, TextColor, new Vector2(110f, 0f), new Vector2(90f, RowHeight), true);
            }

            AddText("Footer", _panel, "R — закрыть", 20, TextAlignmentOptions.Center, DimColor, new Vector2(0f, -312f), new Vector2(PanelWidth, 28f));
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void SetTop(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI AddText(string name, RectTransform parent, string text, float size, TextAlignmentOptions align,
                                                Color color, Vector2 pos, Vector2 box, bool middle = false)
        {
            var rt = NewRect(name, parent);
            if (middle) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); }
            else { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f); }
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
