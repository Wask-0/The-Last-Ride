using System.Collections;
using LastRide.Core;
using UnityEngine;

namespace LastRide.Save
{
    /// <summary>
    /// Смерть и рестарт с чекпоинта (ТЗ 4.7): <c>GameManager.Instance.Death</c>.
    /// • Смерть: <see cref="PlayerDied"/> (замёрз) или <see cref="FireExtinguished"/> (Искра погасла) → состояние Dying,
    ///   фейд в иней (<c>frost.deathFillSeconds</c>) + пауза на полном инее (<c>death.holdFullFrostSeconds</c>) →
    ///   Restoring: <see cref="CheckpointSystem.Restore"/> под закрытым экраном → иней тает (<c>death.restoreHoldSeconds</c>) → Playing.
    /// • <see cref="RestartFromCheckpoint"/> — то же без фейда смерти (пункт паузы «Начать с чекпоинта», 11.4).
    /// Чекпоинт берётся из памяти (<see cref="CheckpointSystem.Last"/>), иначе из файла сохранения; если нет и его — продолжаем как есть.
    /// </summary>
    public sealed class DeathDirector : MonoBehaviour
    {
        private Coroutine _routine;

        /// <summary>Идёт смерть или восстановление.</summary>
        public bool Busy => _routine != null;

        private void OnEnable()
        {
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<FireExtinguished>.Subscribe(OnFireExtinguished);
        }

        private void OnDisable()
        {
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<FireExtinguished>.Unsubscribe(OnFireExtinguished);
        }

        /// <summary>Оборвать смерть/восстановление (выход в меню, новая игра).</summary>
        public void Cancel()
        {
            StopAllCoroutines();
            _routine = null;
        }

        private void OnFireExtinguished(FireExtinguished e) =>
            EventBus<PlayerDied>.Publish(new PlayerDied { Cause = DeathCause.SparkExtinguished });

        private void OnPlayerDied(PlayerDied e) => Die();

        /// <summary>Умереть (фейд в иней) и вернуться на чекпоинт. Игнорируется вне Playing.</summary>
        public bool Die()
        {
            var gm = GameManager.Instance;
            if (gm == null || _routine != null || gm.State != GameState.Playing) return false;
            if (!gm.RequestState(GameState.Dying)) return false;
            _routine = StartCoroutine(Sequence(true));
            return true;
        }

        /// <summary>Вернуться на последний чекпоинт без смерти (пауза → «Начать с чекпоинта»).</summary>
        public bool RestartFromCheckpoint()
        {
            var gm = GameManager.Instance;
            if (gm == null || _routine != null) return false;
            if (!gm.RequestState(GameState.Restoring)) return false;
            _routine = StartCoroutine(Sequence(false));
            return true;
        }

        private IEnumerator Sequence(bool fromDeath)
        {
            var gm = GameManager.Instance;
            var bal = gm.Balance;
            if (fromDeath)
            {
                float fill = bal != null ? bal.frost.deathFillSeconds : 1.5f;
                float hold = bal != null ? bal.death.holdFullFrostSeconds : 0.35f;
                yield return new WaitForSecondsRealtime(fill + hold);
                gm.RequestState(GameState.Restoring);
            }

            var data = gm.Checkpoints.Last;
            if (data == null && SaveSystem.TryLoad(out var fromFile)) data = fromFile;
            if (data != null) gm.Checkpoints.Restore(data);
            else Debug.LogWarning("[Death] Нет чекпоинта для восстановления — продолжаем с текущего состояния.");

            yield return new WaitForSecondsRealtime(bal != null ? bal.death.restoreHoldSeconds : 0.6f);
            gm.RequestState(GameState.Playing);
            _routine = null;
        }
    }
}
