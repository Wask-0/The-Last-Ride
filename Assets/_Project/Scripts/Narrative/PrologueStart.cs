using LastRide.Core;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Старт новой игры в сцене Game (ставит Билдер уровня): робот на своих ногах (1-е лицо), Искры в печке ещё нет —
    /// запускается пролог (<see cref="TutorialController.Begin"/>). Загрузку с чекпоинта сделает этап 11 (там пролог пропускается).
    /// </summary>
    public sealed class PrologueStart : MonoBehaviour
    {
        private System.Collections.IEnumerator Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) yield break;

            // «Продолжить»: вместо пролога применяем чекпоинт — через кадр, когда остальные объекты сцены уже прошли Start.
            var pending = gm.Flow != null ? gm.Flow.TakePending() : null;
            if (pending != null)
            {
                yield return null;
                gm.Checkpoints.Restore(pending);
                yield break;
            }

            gm.ResetPlayerMode(PlayerMode.OnFoot);
            gm.Tutorial.Begin();
            // Точка возврата, если умереть раньше первого чекпоинта: сам старт (в файл не пишется).
            gm.Checkpoints.SetLast(gm.Checkpoints.Capture("NEW_GAME"));
        }
    }
}
