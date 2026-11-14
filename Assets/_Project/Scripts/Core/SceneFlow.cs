using System.Collections;
using LastRide.Save;
using LastRide.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastRide.Core
{
    /// <summary>
    /// Переходы между сценами MainMenu ↔ Game (ТЗ 1.2, 6.2, 6.3): <c>GameManager.Instance.Flow</c>.
    /// «Новая игра» чистит сохранение и сбрасывает системы; «Продолжить» читает последний чекпоинт и после загрузки сцены
    /// <see cref="TakePending"/> отдаёт его <c>PrologueStart</c>, который применяет его вместо пролога.
    /// Загрузка идёт за затемнением (<see cref="ScreenFader"/>).
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        private SaveData _pending;

        public bool Busy { get; private set; }

        private GameBalance.MenuSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.Balance != null ? gm.Balance.menu : new GameBalance.MenuSettings();
            }
        }

        /// <summary>Чекпоинт, который нужно применить после загрузки сцены Game (один раз).</summary>
        public SaveData TakePending()
        {
            var d = _pending;
            _pending = null;
            return d;
        }

        /// <summary>Новая игра: старое сохранение удаляется (подтверждение — в меню).</summary>
        public bool StartNewGame()
        {
            if (Busy) return false;
            StartCoroutine(LoadGame(null));
            return true;
        }

        /// <summary>Продолжить с последнего чекпоинта. False — читаемого сохранения нет.</summary>
        public bool ContinueGame()
        {
            if (Busy || !SaveSystem.TryLoad(out var data) || data.completed) return false;
            StartCoroutine(LoadGame(data));
            return true;
        }

        /// <summary>В главное меню (из паузы, после титров).</summary>
        public bool ToMainMenu()
        {
            if (Busy) return false;
            StartCoroutine(LoadMenu());
            return true;
        }

        private IEnumerator LoadGame(SaveData data)
        {
            Busy = true;
            var gm = GameManager.Instance;
            var fader = gm.Fader;
            if (fader != null) yield return fader.FadeTo(1f, Cfg.fadeOutSeconds);

            if (data == null) SaveSystem.Delete();
            gm.ResetForNewGame();
            if (data != null)
            {
                var diff = gm.Config != null ? gm.Config.GetDifficulty(data.difficultyId) : null;
                gm.SetDifficulty(diff);
            }
            _pending = data;

            var op = SceneManager.LoadSceneAsync(SceneNames.Game);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) yield return null;
            gm.RequestState(GameState.Playing);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            if (fader != null) yield return fader.FadeTo(0f, Cfg.fadeInSeconds);
            Busy = false;
        }

        private IEnumerator LoadMenu()
        {
            Busy = true;
            var gm = GameManager.Instance;
            var fader = gm.Fader;
            if (fader != null) yield return fader.FadeTo(1f, Cfg.fadeOutSeconds);

            gm.ResetForNewGame();
            var op = SceneManager.LoadSceneAsync(SceneNames.MainMenu);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) yield return null;
            gm.RequestState(GameState.MainMenu);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            if (fader != null) yield return fader.FadeTo(0f, Cfg.fadeInSeconds);
            Busy = false;
        }
    }
}
