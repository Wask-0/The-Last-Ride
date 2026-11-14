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
        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.ResetPlayerMode(PlayerMode.OnFoot);
            gm.Tutorial.Begin();
        }
    }
}
