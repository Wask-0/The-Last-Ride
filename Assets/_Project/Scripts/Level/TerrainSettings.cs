using UnityEngine;

namespace LastRide.Level
{
    /// <summary>
    /// Параметры рельефа, дороги, обочин и стен (7.2). Правятся в инспекторе, после чего
    /// <c>The Last Ride/Level/Build Terrain</c> пересобирает рельеф. Лежит в <c>Data/Resources/TerrainSettings.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Ride/Terrain Settings", fileName = "TerrainSettings")]
    public sealed class TerrainSettings : ScriptableObject
    {
        public const string ResourcePath = "TerrainSettings";

        public static TerrainSettings Load() => Resources.Load<TerrainSettings>(ResourcePath);

        [Header("Дорога и обочина")]
        [Tooltip("Ширина обочины за кромкой дороги, м")] public float shoulderWidth = 1.5f;
        [Tooltip("Насколько рельеф «утоплен» под дорогой и обочиной, м (дорога — отдельный меш с коллайдером)")] public float roadSink = 0.35f;
        [Tooltip("Дорожный меш выше оси пути на столько, м")] public float roadLift = 0.03f;
        [Tooltip("Шаг дорожного меша вдоль оси, м")] public float roadStep = 1f;
        [Tooltip("Длина куска дорожного меша, м")] public float roadChunkLength = 250f;

        [Header("Невидимые стены и сугробы")]
        [Tooltip("Стена начинается на столько дальше внешнего края обочины, м")] public float wallOffset = 0.8f;
        [Tooltip("То же для пешей тропы акта 3, м")] public float trailWallOffset = 3f;
        public float wallThickness = 1f;
        public float wallHeight = 3f;
        [Tooltip("Шаг вдоль оси для стены и сугроба, м")] public float wallStep = 2f;
        [Tooltip("Высота гребня сугроба, м (случайно в пределах 0.5…1)")] public float bermHeight = 1f;
        public float bermWidth = 1.6f;

        [Header("Чанки")]
        public float nearChunkSize = 64f;
        public float nearSpacing = 2f;
        [Tooltip("Ближняя полоса рельефа с коллайдером вокруг оси, м")] public float nearBand = 110f;
        public float farChunkSize = 256f;
        public float farSpacing = 8f;
        [Tooltip("Дальний рельеф без коллайдера вокруг оси, м")] public float farBand = 600f;
        [Tooltip("Дальний рельеф опущен, чтобы не мерцал с ближним, м")] public float farSink = 0.8f;

        [Header("Форма склонов")]
        [Tooltip("Максимальный подъём стороны-горы, м")] public float mountainRise = 80f;
        public float mountainSlopeMin = 0.3f;
        public float mountainSlopeMax = 1f;
        [Tooltip("Глубина обрыва на внешней стороне поворота, м")] public float cliffDepth = 40f;
        public float cliffSlopeMin = 0.25f;
        public float cliffSlopeMax = 1f;
        [Tooltip("Окно усреднения поворота для выбора стороны горы/обрыва, м")] public float biasWindow = 150f;
        [Tooltip("Чувствительность стороны к суммарному повороту (рад⁻¹)")] public float biasGain = 1.2f;

        [Header("Шум")]
        public float noiseAmplitude = 2.5f;
        public float noiseScale = 25f;
        public float ridgeAmplitude = 8f;
        public float ridgeScale = 90f;
        [Tooltip("Расстояние от кромки, на котором шум набирает полную силу, м")] public float noiseFadeDistance = 15f;
        public int seed = 7;

        [Header("Вершина")]
        [Tooltip("Плоская площадка вокруг конца пути (под Маяк), радиус м")] public float summitPlateauRadius = 40f;
        [Tooltip("Радиус, на котором площадка сходит на нет, м")] public float summitBlendRadius = 90f;
    }
}
