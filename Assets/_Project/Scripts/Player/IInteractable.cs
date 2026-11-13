namespace LastRide.Player
{
    /// <summary>
    /// Объект, с которым робот взаимодействует по E, наведя прицел (подбор, лампа, лифт Маяка…).
    /// Компонент вешается на объект с коллайдером (триггер подходит) или на его родителя.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Действие для подсказки: «Подобрать доски» (без «[E]» — клавишу добавляет Interactor).</summary>
        string PromptText { get; }

        /// <summary>Можно ли взаимодействовать прямо сейчас (иначе подсказка не показывается).</summary>
        bool CanInteract { get; }

        void Interact(Interactor who);
    }
}
