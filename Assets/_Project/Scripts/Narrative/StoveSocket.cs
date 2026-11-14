using LastRide.Core;
using LastRide.Player;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Печка фургона как цель взаимодействия в прологе: пока робот несёт банку, наведение на любую часть фургона
    /// даёт «[E] Поставить Искру в печку». После этого компонент неактивен (дальше — «[E] Сесть» от VanBoarding).
    /// Вешается на корень фургона: луч попадает в его коллайдеры, а <c>GetComponentInParent</c> находит этот компонент.
    /// </summary>
    public sealed class StoveSocket : MonoBehaviour, IInteractable
    {
        public string PromptText => "Поставить Искру в печку";

        public bool CanInteract
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.Tutorial.State == PrologueState.JarTaken;
            }
        }

        public void Interact(Interactor who)
        {
            if (!CanInteract) return;
            GameManager.Instance.Tutorial.PlaceSparkInStove();
        }
    }
}
