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
        public VanCameraSettings vanCamera = new VanCameraSettings();
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
            [Tooltip("Максимальная скорость фургона, при которой можно выйти (E), м/с.")]
            [Min(0)] public float maxExitVanSpeed = 1.5f;
            [Tooltip("Пауза после выхода/посадки, прежде чем E сработает снова, с.")]
            [Min(0)] public float boardingCooldown = 0.5f;
        }

        [Serializable]
        public sealed class VanSettings
        {
            [Header("Скорость и тяга")]
            [Tooltip("Максимальная скорость, м/с.")]
            [Min(0)] public float maxSpeed = 14f;
            [Tooltip("Максимальная скорость задним ходом, м/с.")]
            [Min(0)] public float reverseMaxSpeed = 4f;
            [Tooltip("Ускорение на малой скорости, м/с² (падает к максимальной скорости).")]
            [Min(0)] public float driveAcceleration = 3.5f;
            [Tooltip("Ускорение задним ходом, м/с².")]
            [Min(0)] public float reverseAcceleration = 2f;
            [Tooltip("Торможение (S при движении вперёд), м/с².")]
            [Min(0)] public float brakeDeceleration = 6f;
            [Tooltip("Торможение ручником, м/с².")]
            [Min(0)] public float handbrakeDeceleration = 4f;
            [Tooltip("Замедление накатом (качение + двигатель), м/с².")]
            [Min(0)] public float coastDeceleration = 0.6f;
            [Tooltip("Сопротивление воздуха: a = k · v².")]
            [Min(0)] public float airDrag = 0.002f;

            [Header("Сцепление (коэффициенты трения шин о снег)")]
            [Tooltip("Продольное сцепление при разгоне. Меньше tan 20° (0.36), но больше tan 14° (0.25): 20° — только с разгона. " +
                     "0.30 даёт на 20° дефицит ~0.7 м/с² — с места/короткого разгона склон не взять, с 10+ м/с — взять.")]
            [Range(0, 2)] public float driveTraction = 0.30f;
            [Tooltip("Продольное сцепление при торможении.")]
            [Range(0, 2)] public float brakeTraction = 0.6f;
            [Tooltip("Боковое сцепление (держит в повороте).")]
            [Range(0, 2)] public float lateralGrip = 0.9f;
            [Tooltip("Насколько быстро колесо гасит боковое скольжение (0..1 за шаг физики).")]
            [Range(0, 1)] public float gripResponse = 0.6f;
            [Tooltip("Множитель бокового сцепления задних колёс на ручнике.")]
            [Range(0, 1)] public float handbrakeRearGrip = 0.35f;
            [Tooltip("Множитель сцепления на льду.")]
            [Range(0, 1)] public float iceGripMultiplier = 0.3f;
            [Tooltip("На льду задние колёса держат ещё слабее передних (× этот множитель) — корму уводит, фургон «заносит», а не просто плывёт прямо.")]
            [Range(0, 1)] public float iceRearGripBias = 0.6f;

            [Header("Глубокий снег")]
            [Tooltip("Множитель максимальной скорости в глубоком снегу.")]
            [Range(0, 1)] public float deepSnowMaxSpeedMultiplier = 0.5f;
            [Tooltip("Дополнительное торможение в глубоком снегу, м/с².")]
            [Min(0)] public float deepSnowDrag = 4f;

            [Header("Руль")]
            [Tooltip("Максимальный угол передних колёс на малой скорости, °.")]
            [Range(0, 45)] public float steerAngleLowSpeed = 30f;
            [Tooltip("Максимальный угол на максимальной скорости, °.")]
            [Range(0, 45)] public float steerAngleHighSpeed = 10f;
            [Tooltip("Скорость поворота руля к нажатию, °/с.")]
            [Min(0)] public float steerSpeed = 80f;
            [Tooltip("Скорость возврата руля в центр, °/с.")]
            [Min(0)] public float steerReturnSpeed = 140f;

            [Header("Подвеска")]
            [Tooltip("Длина пружины в покое, м.")]
            [Min(0.05f)] public float suspensionRest = 0.35f;
            [Tooltip("Дополнительный ход колеса вниз от положения покоя, м. Больше — колёса дольше держатся за землю на кочках.")]
            [Min(0)] public float suspensionTravel = 0.35f;
            [Tooltip("Жёсткость пружины одного колеса, Н/м.")]
            [Min(0)] public float springStiffness = 26000f;
            [Tooltip("Демпфер одного колеса, Н·с/м.")]
            [Min(0)] public float springDamper = 2800f;
            [Tooltip("Стабилизатор поперечной устойчивости, Н/м разницы сжатия.")]
            [Min(0)] public float antiRoll = 9000f;

            [Header("Корпус")]
            [Min(1)] public float mass = 1200f;
            [Tooltip("Центр масс относительно корня фургона (низко — не валится в поворотах).")]
            public Vector3 centerOfMass = new Vector3(0f, 0.6f, 0.15f);
            [Tooltip("Если фургон перевёрнут/завис дольше — плавно ставится на колёса, с.")]
            [Min(0.5f)] public float uprightAfterSeconds = 3f;

            [Header("Ветер")]
            [Tooltip("Боковой импульс от порыва ветра на единицу силы ветра.")]
            [Min(0)] public float gustSideForce = 1.2f;
        }

        /// <summary>
        /// Камера от 3-го лица за фургоном. Читается каждый кадр — можно подстраивать в инспекторе прямо в Play Mode.
        /// </summary>
        [Serializable]
        public sealed class VanCameraSettings
        {
            [Tooltip("Расстояние позади фургона (по горизонтали), м.")]
            [Min(0)] public float distance = 5f;
            [Tooltip("Высота камеры над корнем фургона (над землёй под ним), м.")]
            [Min(0)] public float height = 4.3f;
            [Tooltip("Наклон камеры вниз от горизонта, градусы.")]
            [Range(-10, 80)] public float pitchDown = 20f;
            [Tooltip("Плавность следования: время догоняния, с (меньше — жёстче).")]
            [Min(0)] public float followSmoothTime = 0.25f;
            [Tooltip("Плавность поворота вслед за фургоном, с.")]
            [Min(0)] public float yawSmoothTime = 0.35f;
            [Tooltip("Поле зрения по вертикали, градусы.")]
            [Range(20, 100)] public float fieldOfView = 60f;

            [Header("Орбита мышью")]
            [Tooltip("Чувствительность орбиты, градусов на пиксель движения мыши.")]
            [Min(0)] public float orbitSensitivity = 0.12f;
            [Tooltip("Насколько можно опустить/поднять камеру мышью относительно базового наклона (мин, макс), °.")]
            public Vector2 orbitPitchRange = new Vector2(-15f, 35f);
            [Tooltip("Через сколько секунд без мыши камера начинает возвращаться за спину фургона (при движении).")]
            [Min(0)] public float recenterDelay = 1.5f;
            [Tooltip("Скорость возврата за спину, °/с.")]
            [Min(0)] public float recenterSpeed = 90f;
            [Tooltip("Радиус камеры для отталкивания от препятствий (сугробы, стены, скалы), м.")]
            [Min(0)] public float collisionRadius = 0.3f;

            [Header("Тряска")]
            [Tooltip("Лёгкая «дорожная» тряска на максимальной скорости, °.")]
            [Min(0)] public float rumbleAtMaxSpeed = 0.12f;
            [Tooltip("Сколько «травмы» (0..1) добавляет резкое сжатие подвески (на кочках).")]
            [Min(0)] public float bumpTraumaScale = 4f;
            [Tooltip("Максимальный угол тряски при травме 1, °.")]
            [Min(0)] public float maxShakeAngle = 2.5f;
            [Tooltip("Скорость затухания травмы, 1/с.")]
            [Min(0)] public float traumaDecay = 1.8f;
            [Tooltip("Частота тряски, Гц.")]
            [Min(0)] public float shakeFrequency = 14f;
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
