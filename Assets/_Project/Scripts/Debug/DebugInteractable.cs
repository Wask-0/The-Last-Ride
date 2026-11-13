using LastRide.Player;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>Тестовый интерактивный объект (проверка подсказок и луча): по E исчезает. Настоящий подбор — в 5.1.</summary>
    public sealed class DebugInteractable : MonoBehaviour, IInteractable
    {
        public string prompt = "Подобрать";
        public string PromptText => prompt;
        public bool CanInteract => true;

        public void Interact(Interactor who)
        {
            Debug.Log("[DebugInteractable] взято: " + name);
            Destroy(gameObject);
        }
    }
}
