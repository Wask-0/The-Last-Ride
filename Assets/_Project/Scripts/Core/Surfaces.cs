using System;
using UnityEngine;

namespace LastRide.Core
{
    /// <summary>Тип поверхности под колесом / ногами (ТЗ 4.5, 4.6).</summary>
    public enum SurfaceType
    {
        Snow,
        Ice,
        DeepSnow
    }

    /// <summary>
    /// Запрос поверхности в точке мира. По умолчанию — обычный снег;
    /// система зон (подзадача 3.3) подменяет <see cref="Resolver"/>.
    /// </summary>
    public static class Surfaces
    {
        public static Func<Vector3, SurfaceType> Resolver;

        public static SurfaceType At(Vector3 worldPosition)
        {
            return Resolver != null ? Resolver(worldPosition) : SurfaceType.Snow;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Resolver = null;
    }

    /// <summary>Слои физики проекта (ProjectSettings/TagManager).</summary>
    public static class Layers
    {
        public const string Vehicle = "Vehicle";
        public const string Player = "Player";
        public const string Interactable = "Interactable";

        /// <summary>Маска «земли» для лучей подвески и шагов: всё, кроме фургона, игрока и Ignore Raycast.</summary>
        public static int GroundMask => ~(LayerMask.GetMask(Vehicle, Player, Interactable, "Ignore Raycast"));
    }
}
