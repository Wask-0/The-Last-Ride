using LastRide.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Меню паузы (ТЗ 6.2): «Продолжить», «Начать с чекпоинта», «Настройки», «В главное меню».
    /// Появляется в состоянии Paused (в Playing в Paused переводит GameManager по Esc), Esc в паузе — «Продолжить».
    /// Живёт на GameManager, интерфейс собирается кодом при первом показе.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        private const float ButtonWidth = 560f;
        private const float ButtonHeight = 76f;

        private GameObject _root;
        private Button _resume, _restart, _settings, _toMenu;
        private int _shownFrame = -1;

        private void OnEnable() => EventBus<GameStateChanged>.Subscribe(OnState);
        private void OnDisable() => EventBus<GameStateChanged>.Unsubscribe(OnState);

        private void OnState(GameStateChanged e)
        {
            if (e.Current == GameState.Paused) Show();
            else Hide();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (_root == null || !_root.activeSelf || gm == null) return;
            if (gm.State != GameState.Paused) { Hide(); return; }
            // Тот же Esc, что открыл паузу, закрывать её не должен.
            if (SettingsView.IsOpen || Time.frameCount == SettingsView.ClosedFrame) return;
            if (Time.frameCount != _shownFrame && GameInput.PausePressed) gm.TogglePause();
        }

        private void Show()
        {
            if (_root == null) Build();
            _root.SetActive(true);
            _shownFrame = Time.frameCount;
            _settings.interactable = UiKit.OpenSettings != null;
            UiKit.Select(_resume.gameObject);
        }

        private void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        private void Build()
        {
            var canvas = UiKit.CreateCanvas(transform, "PauseCanvas", 120);
            _root = canvas.gameObject;

            var dim = UiKit.Panel(canvas.transform, "Dim", new Color(0.02f, 0.03f, 0.06f, 0.62f));
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            var title = UiKit.Label(drt, "Пауза", 84f, UiKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 270f);
            trt.sizeDelta = new Vector2(900f, 110f);

            var list = UiKit.Rect(drt, "Buttons");
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 0.5f);
            list.pivot = new Vector2(0.5f, 0.5f);
            list.anchoredPosition = new Vector2(0f, -10f);
            list.sizeDelta = new Vector2(ButtonWidth, 400f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var gm = GameManager.Instance;
            float font = gm != null && gm.Balance != null ? gm.Balance.menu.buttonFontSize : 36f;
            _resume = UiKit.MakeButton(list, "Продолжить", ButtonWidth, ButtonHeight, OnResume, font);
            _restart = UiKit.MakeButton(list, "Начать с чекпоинта", ButtonWidth, ButtonHeight, OnRestart, font);
            _settings = UiKit.MakeButton(list, "Настройки", ButtonWidth, ButtonHeight, () => UiKit.OpenSettings?.Invoke(), font);
            _toMenu = UiKit.MakeButton(list, "В главное меню", ButtonWidth, ButtonHeight, OnToMenu, font);
        }

        private static void OnResume() => GameManager.Instance?.TogglePause();

        private static void OnRestart()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Death != null) gm.Death.RestartFromCheckpoint();
        }

        private static void OnToMenu()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Flow != null) gm.Flow.ToMainMenu();
        }
    }
}
