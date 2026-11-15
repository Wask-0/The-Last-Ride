using System.Collections;
using LastRide.Core;
using LastRide.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastRide.UI
{
    /// <summary>
    /// Главное меню (ТЗ 6.2): «Продолжить» (если есть сохранение), «Новая игра» (с подтверждением при существующем сохранении),
    /// «Настройки», «Выход». Стоит в сцене MainMenu, интерфейс собирается кодом. Фон — диорама со слабо облетающей камерой
    /// (<see cref="MenuCameraFlyover"/>) и тлеющим огоньком.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        private const float ButtonWidth = 520f;
        private const float ButtonHeight = 76f;

        public const string DifficultyPrefKey = "opt_difficulty";

        private GameObject _mainGroup, _confirmGroup, _difficultyGroup;
        private Button _continue, _newGame, _settings, _quit, _confirmNo, _difficultyBack;
        private readonly System.Collections.Generic.List<Button> _difficultyButtons = new System.Collections.Generic.List<Button>();
        private TextMeshProUGUI _continueNote;

        private IEnumerator Start()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.MainMenu) gm.RequestState(GameState.MainMenu);
            if (gm != null && gm.Fader != null) gm.Fader.Set(1f);

            Build();
            Refresh();

            yield return null;
            // Видеозаставка при запуске игры (один раз, до появления меню); без файла Intro.mp4 пропускается сама.
            if (gm != null && !IntroSplash.Played && (gm.Flow == null || !gm.Flow.Busy)) yield return IntroSplash.Play(gm);
            if (gm != null && gm.Fader != null && gm.Flow != null && !gm.Flow.Busy)
            {
                float seconds = gm.Balance != null ? gm.Balance.menu.fadeInSeconds : 1.3f;
                yield return gm.Fader.FadeTo(0f, seconds);
            }
            SelectDefault();
        }

        private void Update()
        {
            // Esc в диалогах = «Нет» / «Назад». В самом меню Esc ничего не делает.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (_difficultyGroup != null && _difficultyGroup.activeSelf) CloseDifficulty();
            else if (_confirmGroup != null && _confirmGroup.activeSelf) CloseConfirm();
        }

        private void Build()
        {
            var canvas = UiKit.CreateCanvas(transform, "MainMenuCanvas", 100);
            float font = GameManager.Instance != null && GameManager.Instance.Balance != null ? GameManager.Instance.Balance.menu.buttonFontSize : 36f;

            // Затенение слева — чтобы меню читалось на светлом снегу.
            var shade = UiKit.Panel(canvas.transform, "Shade", new Color(0.03f, 0.05f, 0.09f, 0.62f));
            shade.raycastTarget = false;
            var srt = shade.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0.5f, 1f); // название целиком на полупрозрачной панели
            srt.offsetMin = srt.offsetMax = Vector2.zero;

            _mainGroup = UiKit.Rect(canvas.transform, "Main").gameObject;
            var mrt = (RectTransform)_mainGroup.transform;
            mrt.anchorMin = new Vector2(0f, 0f);
            mrt.anchorMax = new Vector2(0.5f, 1f);
            mrt.offsetMin = new Vector2(110f, 0f);
            mrt.offsetMax = Vector2.zero;

            var title = UiKit.Label(mrt, "THE LAST RIDE", 86f, UiKit.TextColor, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var trt = title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(0f, -150f);
            trt.sizeDelta = new Vector2(900f, 120f);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var sub = UiKit.Label(mrt, "Довези Искру до Вершины", 40f, new Color(1f, 0.86f, 0.62f, 1f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            sub.outlineWidth = 0.3f; // контур потемнее — читается и на светлом снегу
            var subRt = sub.rectTransform;
            subRt.anchorMin = subRt.anchorMax = new Vector2(0f, 1f);
            subRt.pivot = new Vector2(0f, 1f);
            subRt.anchoredPosition = new Vector2(4f, -270f);
            subRt.sizeDelta = new Vector2(800f, 50f);

            var list = UiKit.Rect(mrt, "Buttons");
            list.anchorMin = list.anchorMax = new Vector2(0f, 0.5f);
            list.pivot = new Vector2(0f, 0.5f);
            list.anchoredPosition = new Vector2(0f, -90f);
            list.sizeDelta = new Vector2(ButtonWidth, 420f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _continue = UiKit.MakeButton(list, "Продолжить", ButtonWidth, ButtonHeight, OnContinue, font);
            _newGame = UiKit.MakeButton(list, "Новая игра", ButtonWidth, ButtonHeight, OnNewGame, font);
            _settings = UiKit.MakeButton(list, "Настройки", ButtonWidth, ButtonHeight, OnSettings, font);
            _quit = UiKit.MakeButton(list, "Выход", ButtonWidth, ButtonHeight, OnQuit, font);

            _continueNote = UiKit.Label(_continue.transform, "", 24f, UiKit.DimColor, TextAlignmentOptions.MidlineRight);
            var nrt = _continueNote.rectTransform;
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(20f, 0f);
            nrt.offsetMax = new Vector2(-26f, 0f);

            BuildConfirm(canvas.transform, font);
            BuildDifficulty(canvas.transform, font);
        }

        private void BuildDifficulty(Transform canvas, float font)
        {
            var dim = UiKit.Panel(canvas, "DifficultyPicker", new Color(0f, 0f, 0f, 0.7f));
            _difficultyGroup = dim.gameObject;
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            var box = UiKit.Panel(drt, "Box", PopupColor());
            var brt = box.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(900f, 620f);

            var title = UiKit.Label(brt, "Сложность", 54f, UiKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -24f);
            trt.sizeDelta = new Vector2(0f, 80f);

            var list = UiKit.Rect(brt, "Options");
            list.anchorMin = new Vector2(0.5f, 0f);
            list.anchorMax = new Vector2(0.5f, 1f);
            list.offsetMin = new Vector2(-390f, 30f);
            list.offsetMax = new Vector2(390f, -110f);
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 16f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;

            var gm = GameManager.Instance;
            var diffs = gm != null && gm.Config != null ? gm.Config.difficulties : new DifficultyProfile[0];
            foreach (var d in diffs)
            {
                if (d == null) continue;
                var captured = d;
                var btn = UiKit.MakeButton(list, d.displayName, 780f, 130f, () => StartWith(captured), font);
                var main = btn.GetComponentInChildren<TextMeshProUGUI>();
                main.alignment = TextAlignmentOptions.TopLeft;
                main.rectTransform.offsetMin = new Vector2(34f, 0f);
                main.rectTransform.offsetMax = new Vector2(-16f, -14f);
                var desc = UiKit.Label(btn.transform, d.description, 26f, UiKit.DimColor, TextAlignmentOptions.BottomLeft);
                desc.rectTransform.anchorMin = Vector2.zero;
                desc.rectTransform.anchorMax = Vector2.one;
                desc.rectTransform.offsetMin = new Vector2(34f, 12f);
                desc.rectTransform.offsetMax = new Vector2(-24f, 0f);
                btn.gameObject.name = "Difficulty_" + d.id;
                _difficultyButtons.Add(btn);
            }
            _difficultyBack = UiKit.MakeButton(list, "Назад", 780f, ButtonHeight, CloseDifficulty, font - 4f);
            _difficultyGroup.SetActive(false);
        }

        /// <summary>Фон всплывающих окон меню: цвет панели с непрозрачностью из <c>GameBalance.menu.popupBackgroundAlpha</c>.</summary>
        private static Color PopupColor()
        {
            var gm = GameManager.Instance;
            float a = gm != null && gm.Balance != null ? gm.Balance.menu.popupBackgroundAlpha : 0.97f;
            var c = UiKit.PanelColor;
            c.a = a;
            return c;
        }

        private void BuildConfirm(Transform canvas, float font)
        {
            var dim = UiKit.Panel(canvas, "Confirm", new Color(0f, 0f, 0f, 0.62f));
            _confirmGroup = dim.gameObject;
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            var box = UiKit.Panel(drt, "Box", PopupColor());
            var brt = box.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(860f, 380f);

            var text = UiKit.Label(brt, "Начать заново?\nТекущее сохранение будет удалено.", 38f, UiKit.TextColor, TextAlignmentOptions.Center);
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.45f);
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(30f, 0f);
            trt.offsetMax = new Vector2(-30f, -20f);

            var row = UiKit.Rect(brt, "Row");
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0.45f);
            row.offsetMin = row.offsetMax = Vector2.zero;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var yes = UiKit.MakeButton(row, "Да, заново", 340f, ButtonHeight, ConfirmNewGame, font - 4f);
            _confirmNo = UiKit.MakeButton(row, "Нет", 340f, ButtonHeight, CloseConfirm, font - 4f);
            _confirmGroup.SetActive(false);
        }

        private void Refresh()
        {
            bool canContinue = SaveSystem.TryLoad(out var data) && !data.completed;
            _continue.gameObject.SetActive(canContinue);
            string diffName = string.Empty;
            if (canContinue && GameManager.Instance != null && GameManager.Instance.Config != null)
            {
                var d = GameManager.Instance.Config.GetDifficulty(data.difficultyId);
                if (d != null && GameManager.Instance.Config.difficulties.Length > 1) diffName = " · " + d.displayName;
            }
            _continueNote.text = canContinue ? $"Акт {(int)data.act}{diffName}" : string.Empty;
            _settings.interactable = UiKit.OpenSettings != null;
        }

        private void SelectDefault()
        {
            UiKit.Select(_continue.gameObject.activeSelf ? _continue.gameObject : _newGame.gameObject);
        }

        // ------------------------------------------------------------------ кнопки

        private void OnContinue()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Flow == null) return;
            if (!gm.Flow.ContinueGame()) Refresh(); // сохранение пропало/испортилось — скрыть кнопку
        }

        private void OnNewGame()
        {
            if (SaveSystem.HasContinue) OpenConfirm();
            else OpenDifficulty();
        }

        // «Да, заново» в диалоге: дальше — выбор сложности.
        private void ConfirmNewGame()
        {
            _confirmGroup.SetActive(false);
            OpenDifficulty();
        }

        private void OpenDifficulty()
        {
            var gm = GameManager.Instance;
            // Одна сложность в конфиге — выбирать нечего.
            if (gm != null && gm.Config != null && gm.Config.difficulties.Length <= 1) { StartWith(gm.Config.defaultDifficulty); return; }
            _difficultyGroup.SetActive(true);
            string last = PlayerPrefs.GetString(DifficultyPrefKey, "");
            Button pick = _difficultyButtons.Count > 0 ? _difficultyButtons[0] : _difficultyBack;
            foreach (var b in _difficultyButtons) if (b.name == "Difficulty_" + last) pick = b;
            UiKit.Select(pick.gameObject);
        }

        private void CloseDifficulty()
        {
            _difficultyGroup.SetActive(false);
            UiKit.Select(_newGame.gameObject);
        }

        private void StartWith(DifficultyProfile difficulty)
        {
            var gm = GameManager.Instance;
            if (difficulty != null) { PlayerPrefs.SetString(DifficultyPrefKey, difficulty.id); PlayerPrefs.Save(); }
            if (gm != null && gm.Flow != null) gm.Flow.StartNewGame(difficulty);
        }

        private void OpenConfirm()
        {
            _confirmGroup.SetActive(true);
            UiKit.Select(_confirmNo.gameObject);
        }

        private void CloseConfirm()
        {
            _confirmGroup.SetActive(false);
            UiKit.Select(_newGame.gameObject);
        }

        private void OnSettings() => UiKit.OpenSettings?.Invoke();

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
