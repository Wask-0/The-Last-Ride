using UnityEngine;

namespace LastRide.Fire
{
    /// <summary>Тип топлива (ТЗ 4.1).</summary>
    [CreateAssetMenu(menuName = "The Last Ride/Fuel Type", fileName = "Fuel")]
    public sealed class FuelType : ScriptableObject
    {
        [Tooltip("Стабильный id для сохранений.")]
        public string id = "kindling";
        [Tooltip("Название для HUD и панели печки.")]
        public string displayName = "Щепки";
        [Tooltip("Для подсказки «[E] Подобрать …».")]
        public string promptName = "щепки";
        [Tooltip("Время горения, с.")]
        [Min(0)] public float burnSeconds = 20f;
        [Tooltip("Клавиша на панели печки / в лампе (1, 2, 3).")]
        [Range(1, 9)] public int hotkey = 1;
        [Tooltip("Цвет иконки / тёплого контура предмета.")]
        public Color tint = new Color(1f, 0.7f, 0.35f);
    }
}
