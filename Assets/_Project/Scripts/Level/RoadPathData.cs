using System;
using UnityEngine;

namespace LastRide.Level
{
    /// <summary>Именованная точка на оси пути (граница акта, стоянка, шпилька, …). Расстояние — по длине дуги, м.</summary>
    [Serializable]
    public struct RoadMarker
    {
        public string id;
        public float distance;
    }

    /// <summary>
    /// Запечённая ось пути уровня (ТЗ 5): дорога актов 1–2 и пешая тропа акта 3 — одна непрерывная линия.
    /// Точки идут с шагом <see cref="step"/> метров по длине дуги; <see cref="widths"/> — ширина полотна в каждой точке.
    /// Собирается меню <c>The Last Ride/Level/Build Road Path</c> (<c>Editor/RoadPathBuilder</c>), читается через <see cref="RoadPath"/>.
    /// </summary>
    public sealed class RoadPathData : ScriptableObject
    {
        [Tooltip("Шаг между точками по длине дуги, м.")]
        public float step = 1f;
        public Vector3[] points = new Vector3[0];
        public float[] widths = new float[0];
        public RoadMarker[] markers = new RoadMarker[0];
    }
}
