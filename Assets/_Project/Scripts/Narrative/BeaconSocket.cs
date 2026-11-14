using LastRide.Player;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Гнездо лифта Маяка как цель взаимодействия: «[E] Вставить Искру» (ТЗ 3, финал). Висит на триггер-коллайдере у гнезда;
    /// само действие — в <see cref="BeaconFinale"/> на корне Маяка.
    /// </summary>
    public sealed class BeaconSocket : MonoBehaviour, IInteractable
    {
        private BeaconFinale _finale;

        private BeaconFinale Finale => _finale != null ? _finale : (_finale = GetComponentInParent<BeaconFinale>());

        public string PromptText => Finale != null ? Finale.PromptText : "Вставить Искру";

        public bool CanInteract => Finale != null && Finale.CanInsert;

        public void Interact(Interactor who)
        {
            if (CanInteract) Finale.Begin();
        }
    }
}
