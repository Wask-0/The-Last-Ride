using System;
using UnityEngine;

namespace LastRide.Core
{
    /// <summary>
    /// Общий баланс игры (ТЗ 4). Значения, зависящие от акта и погоды, — в <see cref="ActSettings"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Ride/Game Balance", fileName = "GameBalance")]
    public sealed class GameBalance : ScriptableObject
    {
        public FireSettings fire = new FireSettings();
        public WarmthSettings warmth = new WarmthSettings();
        public MovementSettings movement = new MovementSettings();
        public VanSettings van = new VanSettings();
        public GustSettings gusts = new GustSettings();
        public PresentationSettings presentation = new PresentationSettings();
        public SubtitleSettings subtitles = new SubtitleSettings();

        [Serializable]
        public sealed class FireSettings
        {
            [Tooltip("Максимальный запас горения печки, с.")]
            [Min(1)] public float stoveMaxBurnSeconds = 240f;
            [Tooltip("Максимальный запас горения лампы, с.")]
            [Min(1)] public float lampMaxBurnSeconds = 240f;
            [Tooltip("Сколько тлеет уголь после того, как запас кончился, с.")]
            [Min(0)] public float embersSeconds = 10f;
            [Tooltip("Запас огня на старте новой игры (Искра только что поставлена в печку), с.")]
            [Min(0)] public float startBurnSeconds = 120f;
            [Tooltip("Защита от тупика: минимальный запас огня после восстановления с чекпоинта, с.")]
            [Min(0)] public float restoreMinBurnSeconds = 90f;
        }

        [Serializable]
        public sealed class WarmthSettings
        {
            [Min(1)] public float max = 100f;
            [Tooltip("За сколько секунд тепло восстанавливается от 0 до max в фургоне при горящей печке.")]
            [Min(0.1f)] public float vanRecoverSeconds = 8f;
            [Tooltip("За сколько секунд робот застывает после того, как тепло дошло до 0.")]
            [Min(0.1f)] public float freezeToStopSeconds = 3f;
            [Tooltip("Доля шкалы, ниже которой HUD мигает.")]
            [Range(0, 1)] public float criticalFraction = 0.2f;
            [Tooltip("Ниже этого значения тепла начинаются «заедания» механизма.")]
            [Min(0)] public float jamThreshold = 30f;
            [Tooltip("Длительность одного заедания, с.")]
            [Min(0)] public float jamDuration = 0.2f;
            [Tooltip("Интервал между заеданиями при тепле на пороге (x) и при 0 (y), с.")]
            public Vector2 jamInterval = new Vector2(4f, 1.2f);
        }

        [Serializable]
        public sealed class MovementSettings
        {
            [Tooltip("Скорость ходьбы при тепле 0, м/с.")]
            [Min(0)] public float walkSpeedCold = 1.4f;
            [Tooltip("Скорость ходьбы при полном тепле, м/с.")]
            [Min(0)] public float walkSpeedWarm = 3.0f;
            [Tooltip("Множитель скорости в глубоком снегу (пешком).")]
            [Range(0, 1)] public float deepSnowSpeedMultiplier = 0.6f;
            [Tooltip("Насколько медленно робот набирает/сбрасывает скорость на льду, с.")]
            [Min(0)] public float iceInertiaSeconds = 0.6f;
            [Tooltip("Чувствительность мыши по умолчанию (градусы на единицу ввода).")]
            [Min(0)] public float mouseSensitivity = 0.12f;
            [Tooltip("Дальность взаимодействия (луч из прицела), м.")]
            [Min(0)] public float interactDistance = 2.5f;
        }

        [Serializable]
        public sealed class VanSettings
        {
            [Tooltip("Максимальная скорость, м/с.")]
            [Min(0)] public float maxSpeed = 14f;
            [Tooltip("Множитель сцепления на льду.")]
            [Range(0, 1)] public float iceGripMultiplier = 0.3f;
            [Tooltip("Множитель максимальной скорости в глубоком снегу.")]
            [Range(0, 1)] public float deepSnowMaxSpeedMultiplier = 0.5f;
            [Tooltip("Дополнительное торможение в глубоком снегу, м/с².")]
            [Min(0)] public float deepSnowDrag = 4f;
            [Tooltip("Боковой импульс от порыва ветра на единицу силы ветра.")]
            [Min(0)] public float gustSideForce = 1.2f;
        }

        [Serializable]
        public sealed class GustSettings
        {
            [Tooltip("Интервал между порывами (мин, макс), с.")]
            public Vector2 interval = new Vector2(8f, 20f);
            [Tooltip("Длительность порыва (мин, макс), с.")]
            public Vector2 duration = new Vector2(1.5f, 3f);
            [Tooltip("Во сколько раз порыв ускоряет потерю тепла.")]
            [Min(1)] public float warmthLossMultiplier = 2f;
        }

        [Serializable]
        public sealed class PresentationSettings
        {
            [Tooltip("Бленд камеры при выходе из фургона / посадке, с.")]
            [Min(0)] public float cameraBlendSeconds = 0.6f;
            [Tooltip("Фейд в иней при смерти, с.")]
            [Min(0)] public float deathFadeSeconds = 1.5f;
            [Tooltip("Финальная засветка экрана, с.")]
            [Min(0)] public float finalWhiteFadeSeconds = 4f;
            [Tooltip("Время полёта подобранного предмета в робота, с.")]
            [Min(0)] public float pickupFlySeconds = 0.35f;
        }

        [Serializable]
        public sealed class SubtitleSettings
        {
            [Tooltip("Авто-тайминг строки без клипа: секунд на символ.")]
            [Min(0)] public float secondsPerChar = 0.07f;
            [Tooltip("Минимальная длительность строки, с.")]
            [Min(0)] public float minLineSeconds = 2f;
            [Tooltip("Насколько приглушаются музыка и ветер во время записи, дБ.")]
            public float duckingDb = -9f;
        }
    }
}
