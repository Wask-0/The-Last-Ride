using System;
using UnityEngine;

namespace LastRide.Level
{
    /// <summary>
    /// Правила расстановки пропсов вдоль пути (7.4): плотности по актам, расстояния от дороги, поляны на стоянках.
    /// Плотность — штук на 10 м пути на одну сторону. Seed фиксирован: результат <c>Build Props</c> детерминирован.
    /// Лежит в <c>Data/Resources/PropsSettings.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Ride/Props Settings", fileName = "PropsSettings")]
    public sealed class PropsSettings : ScriptableObject
    {
        public const string ResourcePath = "PropsSettings";

        [Serializable]
        public struct ActProps
        {
            [Tooltip("Группы ёлок у дороги (в группе 2–5 деревьев)")] public float treeGroups;
            [Tooltip("Мелкие и средние камни у дороги")] public float rocks;
            [Tooltip("Крупные скалы чуть дальше от дороги")] public float bigRocks;
            [Tooltip("Группы ёлок вдали (фон, полоса «далеко»)")] public float farTreeGroups;
            [Tooltip("Камни и скалы вдали")] public float farRocks;
        }

        public int seed = 11;
        [Tooltip("Шаг проверки вдоль оси, м")] public float step = 5f;

        [Header("Плотность по актам: [0] акт 1, [1] акт 2, [2] акт 3")]
        public ActProps[] acts =
        {
            new ActProps { treeGroups = 0.35f, rocks = 0.5f, bigRocks = 0.12f, farTreeGroups = 0.5f, farRocks = 0.2f },
            new ActProps { treeGroups = 0.12f, rocks = 0.8f, bigRocks = 0.3f, farTreeGroups = 0.15f, farRocks = 0.4f },
            new ActProps { treeGroups = 0f, rocks = 0.7f, bigRocks = 0.4f, farTreeGroups = 0f, farRocks = 0.5f }
        };

        [Header("Расстояния от внешнего края стены, м")]
        [Tooltip("Ближняя полоса: от стены на столько вглубь")] public float nearDepth = 28f;
        [Tooltip("Дальняя полоса: от и до, м от стены")] public Vector2 farRange = new Vector2(28f, 95f);
        [Tooltip("Минимальный просвет между пропсом и стеной, м")] public float roadClearance = 1.2f;

        [Header("Ограничения")]
        public float treeMaxSlopeDeg = 32f;
        public float bigRockMaxSlopeDeg = 55f;
        [Tooltip("Поляна: на таком расстоянии вдоль оси от стоянок/старта/вершины нет деревьев и камней, м")] public float clearingAlong = 35f;
        [Tooltip("…и в такой полосе от оси, м")] public float clearingAcross = 28f;
        public string[] clearingMarkers = { "Start", "Stop_A1_1", "Stop_A1_2", "Stop_A2_1", "Stop_A2_2", "Stop_A2_3", "Stop_A2_4", "Summit" };

        [Header("Столбы ЛЭП (акт 1)")]
        public bool poles = true;
        public string polesFromMarker = "Start";
        public float polesFromOffset = 30f;
        public string polesToMarker = "Stop_A1_2";
        public float polesToOffset = 60f;
        public float polesSpacing = 36f;
        [Tooltip("Расстояние от стены, м (минус — слева по ходу)")] public float polesSide = -3.5f;
        [Tooltip("Доля покосившихся столбов")] public float leaningShare = 0.15f;
    }
}
