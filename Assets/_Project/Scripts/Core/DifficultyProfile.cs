using UnityEngine;

namespace LastRide.Core
{
    /// <summary>
    /// Сложность (ТЗ 9): отличается только количеством необязательного топлива на локациях.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Ride/Difficulty Profile", fileName = "Difficulty")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        public string id = "easy";
        public string displayName = "Лёгкая";
        [TextArea] public string description = "Акцент на истории.";

        [Tooltip("Множитель количества необязательных подборов.")]
        [Range(0, 1)] public float optionalPickupMultiplier = 1f;

        [Tooltip("Гарантированный минимум необязательных подборов на каждом отрезке пути.")]
        [Min(0)] public int guaranteedPickupsPerSegment = 1;

        /// <summary>Сколько из <paramref name="authored"/> необязательных подборов отрезка оставить.</summary>
        public int ResolvePickupCount(int authored)
        {
            if (authored <= 0) return 0;
            int scaled = Mathf.RoundToInt(authored * optionalPickupMultiplier);
            return Mathf.Clamp(Mathf.Max(scaled, guaranteedPickupsPerSegment), 0, authored);
        }
    }
}
