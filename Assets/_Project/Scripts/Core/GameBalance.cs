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
        public SparkVisualSettings sparkVisual = new SparkVisualSettings();
        public VanSettings van = new VanSettings();
        public GustSettings gusts = new GustSettings();
        public WeatherSettings weather = new WeatherSettings();
        public VanCameraSettings vanCamera = new VanCameraSettings();
        public PresentationSettings presentation = new PresentationSettings();
        public SubtitleSettings subtitles = new SubtitleSettings();
        public TutorialSettings tutorial = new TutorialSettings();
        public BreakdownSettings breakdown = new BreakdownSettings();
        public LampHandSettings lampHand = new LampHandSettings();
        public SnowfallSettings snowfall = new SnowfallSettings();
        public AtmosphereSettings atmosphere = new AtmosphereSettings();
        public PostFxSettings postFx = new PostFxSettings();
        public FrostSettings frost = new FrostSettings();

        /// <summary>Иней на экране (этап 9.4).</summary>
        [Serializable]
        public sealed class FrostSettings
        {
            [Tooltip("Сила инея от холода при полном холоде (0..1).")]
            [Range(0, 1)] public float coldStrength = 0.9f;
            [Tooltip("Множитель базового инея погоды (WeatherPreset.frost).")]
            [Range(0, 1)] public float weatherStrength = 1f;
            [Tooltip("Скорость нарастания инея, 1/с.")]
            [Min(0.01f)] public float growSpeed = 0.5f;
            [Tooltip("Скорость таяния инея, 1/с (когда согрелись).")]
            [Min(0.01f)] public float meltSpeed = 0.35f;
            [Tooltip("За сколько секунд экран полностью покрывается при смерти.")]
            [Min(0.05f)] public float deathFillSeconds = 1.5f;
            [Tooltip("За сколько секунд заполнение тает после восстановления.")]
            [Min(0.05f)] public float meltSeconds = 1.2f;
        }

        /// <summary>Пост-обработка (этап 9.3): общие параметры и реакция на холод. Значения по актам — в <see cref="ActLighting"/>.</summary>
        [Serializable]
        public sealed class PostFxSettings
        {
            [Header("Bloom")]
            [Tooltip("Порог яркости Bloom (HDR).")]
            [Min(0)] public float bloomThreshold = 1f;
            [Range(0, 1)] public float bloomScatter = 0.6f;
            [Header("Холод")]
            [Tooltip("Доля холода (1 − тепло), с которой начинается эффект.")]
            [Range(0, 1)] public float coldStart = 0.25f;
            [Tooltip("Добавка к виньетке при полном холоде.")]
            [Range(0, 1)] public float coldVignette = 0.35f;
            [Tooltip("Обесцвечивание при полном холоде (Color Adjustments → Saturation).")]
            [Range(0, 100)] public float coldDesaturation = 45f;
            [Tooltip("Размытие при холоде (Depth of Field Gaussian): с какой доли холода включается.")]
            [Range(0, 1)] public float blurStart = 0.5f;
            [Tooltip("Дистанция начала размытия, м: при начале эффекта → при полном холоде.")]
            public Vector2 blurStartDistance = new Vector2(60f, 1.5f);
            [Tooltip("Дистанция конца размытия, м: при начале эффекта → при полном холоде.")]
            public Vector2 blurEndDistance = new Vector2(120f, 8f);
            [Tooltip("Максимальный радиус размытия при полном холоде.")]
            [Range(0.5f, 3)] public float blurMaxRadius = 1.6f;
            [Header("Сглаживание")]
            [Tooltip("Скорость реакции на холод, 1/с.")]
            [Min(0.1f)] public float coldSmoothing = 3f;
        }

        /// <summary>Туман, небо и свет по актам (этап 9.2). Сами значения актов — в <see cref="ActSettings"/>.</summary>
        [Serializable]
        public sealed class AtmosphereSettings
        {
            [Tooltip("Насколько шторм (фаза акта 2) приглушает солнце (доля интенсивности при штормовом миксе 1).")]
            [Range(0, 1)] public float stormSunDim = 0.45f;
            [Tooltip("Насколько шторм приглушает рассеянный свет неба.")]
            [Range(0, 1)] public float stormAmbientDim = 0.25f;
            [Tooltip("Материал неба (шейдер The Last Ride/Sky). Создаёт «Setup/Create Materials».")]
            public Material skyMaterial;
            [Tooltip("Плотность тумана, при которой небо полностью сливается с туманом (белая мгла).")]
            [Min(0.001f)] public float whiteoutFogDensity = 0.03f;
            [Tooltip("Скорость сглаживания света к значениям акта: множитель к 1/transitionSeconds акта.")]
            [Min(0.1f)] public float lightingSmoothing = 3f;
        }

        /// <summary>Частицы метели вокруг камеры (этап 9.1): дальний слой мелкого снега и ближний слой крупных хлопьев.</summary>
        [Serializable]
        public sealed class SnowfallSettings
        {
            [Tooltip("Материал снежинки (URP Particles/Unlit, прозрачный, мягкий круг). Создаёт «Setup/Create Materials».")]
            public Material flakeMaterial;
            [Header("Дальний слой (мелкий снег)")]
            [Tooltip("Частиц в секунду при плотности снега 1.")]
            [Min(0)] public float farRate = 5000f;
            [Tooltip("Размеры области вокруг камеры, м (X, Y, Z).")]
            public Vector3 farVolume = new Vector3(32f, 18f, 32f);
            [Tooltip("Время жизни частицы, с.")]
            [Min(0.5f)] public float farLifetime = 6f;
            [Tooltip("Размер частицы (мин, макс), м.")]
            public Vector2 farSize = new Vector2(0.05f, 0.12f);
            [Range(0, 1)] public float farAlpha = 0.85f;
            [Header("Ближний слой (крупные хлопья у камеры)")]
            [Min(0)] public float nearRate = 220f;
            public Vector3 nearVolume = new Vector3(9f, 6f, 9f);
            [Min(0.2f)] public float nearLifetime = 2.2f;
            public Vector2 nearSize = new Vector2(0.12f, 0.32f);
            [Range(0, 1)] public float nearAlpha = 0.5f;
            [Tooltip("Доля ближнего слоя в шторме акта 2 (в акте 3 — 1).")]
            [Range(0, 1)] public float nearStormShare = 0.3f;
            [Tooltip("Как быстро меняется доля ближнего слоя, 1/с.")]
            [Min(0.01f)] public float nearBlendSpeed = 0.2f;
            [Header("Движение")]
            [Tooltip("Какая доля скорости ветра сносит снег по горизонтали.")]
            [Range(0, 2)] public float windCarry = 0.8f;
            [Tooltip("Минимальная скорость падения, м/с (даже в штиль).")]
            [Min(0)] public float minFallSpeed = 0.8f;
            [Tooltip("Во сколько раз порыв усиливает снос снега при силе порыва 1.")]
            [Min(0)] public float gustBoost = 1.2f;
            [Tooltip("Завихрения: боковая турбулентность, м/с.")]
            [Min(0)] public float turbulence = 0.6f;
            [Tooltip("Насколько вперёд по ходу камеры смещается область появления (в секундах скорости камеры).")]
            [Min(0)] public float leadSeconds = 1.2f;
        }

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
            [Tooltip("Время от полного тепла до 0 снаружи по умолчанию (акт 1), с. Погода/акт переопределяют (ТЗ 4.3).")]
            [Min(0.1f)] public float defaultFreezeSeconds = 90f;
            [Tooltip("Акт 3: время замерзания с погасшей лампой, с (по умолчанию; акт может переопределить).")]
            [Min(0.1f)] public float lampOutFreezeSeconds = 20f;
            [Tooltip("За сколько секунд робот застывает после того, как тепло дошло до 0.")]
            [Min(0.1f)] public float freezeToStopSeconds = 3f;
            [Tooltip("Доля шкалы, ниже которой HUD мигает.")]
            [Range(0, 1)] public float criticalFraction = 0.2f;
            [Tooltip("Заводной механизм: тиков в секунду при полном тепле (для звука 10.3; темп падает с холодом).")]
            [Min(0.1f)] public float tickRateWarm = 2.5f;
            [Tooltip("Тиков в секунду при тепле 0.")]
            [Min(0.1f)] public float tickRateCold = 0.8f;
            [Tooltip("Множитель тона тиканья при тепле 0 (при полном тепле — 1).")]
            [Range(0.1f, 1)] public float tickPitchCold = 0.6f;
            [Tooltip("Ниже этого значения тепла начинаются «заедания» механизма.")]
            [Min(0)] public float jamThreshold = 30f;
            [Tooltip("Длительность одного заедания, с.")]
            [Min(0)] public float jamDuration = 0.2f;
            [Tooltip("Интервал между заеданиями при тепле на пороге (x) и при 0 (y), с.")]
            public Vector2 jamInterval = new Vector2(4f, 1.2f);
        }

        /// <summary>Внешний вид Искры (ТЗ 4.2, 8.2): яркость — функция запаса, мерцание, тление. Читается каждый кадр.</summary>
        [Serializable]
        public sealed class SparkVisualSettings
        {
            [Header("Горение")]
            [Tooltip("Яркость (0..1) при почти нулевом запасе. При полном запасе — 1.")]
            [Range(0, 1)] public float minBurnBrightness = 0.3f;
            [Tooltip("Степень кривой яркости от запаса: < 1 — быстро набирает яркость при малых запасах.")]
            [Min(0.1f)] public float brightnessExponent = 0.5f;
            [Tooltip("Цвет пламени.")]
            public Color flameColor = new Color(1f, 0.62f, 0.28f);
            [Tooltip("Частота мерцания света (шум), Гц.")]
            [Min(0)] public float flickerHz = 9f;
            [Tooltip("Глубина мерцания света (доля яркости).")]
            [Range(0, 1)] public float flickerAmount = 0.18f;
            [Tooltip("Искр в секунду при полном запасе (с ростом яркости растёт квадратично).")]
            [Min(0)] public float maxSparksPerSecond = 16f;

            [Header("Тление")]
            [Tooltip("Яркость тлеющего угля (0..1).")]
            [Range(0, 1)] public float embersBrightness = 0.16f;
            [Tooltip("Цвет тлеющего угля — тускло-красный, тревожный.")]
            public Color embersColor = new Color(0.9f, 0.1f, 0.04f);
            [Tooltip("Частота тревожного пульса в тлении, Гц.")]
            [Min(0)] public float embersPulseHz = 2.2f;
            [Tooltip("Глубина пульса в тлении.")]
            [Range(0, 1)] public float embersPulseDepth = 0.55f;
            [Tooltip("Искр в секунду в тлении.")]
            [Min(0)] public float embersSparksPerSecond = 1.5f;

            [Header("Переходы")]
            [Tooltip("Скорость смены цвета пламя ↔ уголь, 1/с.")]
            [Min(0.1f)] public float colorBlendSpeed = 4f;
            [Tooltip("Скорость изменения яркости, 1/с (вспышка при подкидывании).")]
            [Min(0.1f)] public float brightnessSpeed = 5f;
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
            [Tooltip("Сила толчка фургона на пике порыва, Н (масса фургона 1200 кг).")]
            [Min(0)] public float vanForce = 3500f;
            [Tooltip("Насколько порыв снижает боковое сцепление шин на пике (0.7 — до 30% от обычного): иначе сцепление гасит любую силу ветра.")]
            [Range(0, 0.95f)] public float vanGripLoss = 0.7f;
            [Tooltip("Снос робота на пике порыва, м/с.")]
            [Min(0)] public float robotPush = 2.5f;
            [Tooltip("Ветер, при котором порыв даёт полную силу, м/с (слабее ветер — слабее порыв, но не меньше 0.4×, не больше 1.5×).")]
            [Min(1)] public float referenceWind = 12f;
            [Tooltip("Нарастание порыва до пика, с.")]
            [Min(0.05f)] public float attackSeconds = 0.35f;
            [Tooltip("Спад порыва, с.")]
            [Min(0.05f)] public float releaseSeconds = 0.6f;
            [Tooltip("Разброс направления порыва относительно ветра, градусов (±).")]
            [Range(0, 90)] public float directionJitterDeg = 15f;
        }

        [Serializable]
        public sealed class WeatherSettings
        {
            [Tooltip("Акт 2 начинается со шторма (ТЗ: «начинается сильная пурга»); иначе — с затишья.")]
            public bool stormCycleStartsWithStorm = true;
            [Tooltip("Плавный бленд параметров при смене фазы шторм ↔ затишье, с.")]
            [Min(0.1f)] public float phaseBlendSeconds = 4f;
            [Header("Диегетические сигналы (камера, раскачивание) — игрок читает фазу без HUD")]
            [Tooltip("Фоновая тряска камеры фургона в шторме (при «штормистости» 1), градусов.")]
            [Min(0)] public float vanCameraStormRumbleDeg = 0.35f;
            [Tooltip("Тряска камеры фургона на пике порыва, градусов.")]
            [Min(0)] public float vanCameraGustRumbleDeg = 1.2f;
            [Tooltip("Дрожь камеры робота (пешком) в шторме, градусов.")]
            [Min(0)] public float robotStormTremorDeg = 0.4f;
            [Tooltip("Наклон камеры робота в сторону толчка на пике порыва, градусов.")]
            [Min(0)] public float robotGustLeanDeg = 3f;
            [Tooltip("Раскачивание фургона ветром: момент крена в шторме, Н·м (0 — выключить).")]
            [Min(0)] public float vanSwayTorque = 1500f;
            [Tooltip("Дополнительный момент крена на пике порыва, Н·м.")]
            [Min(0)] public float vanGustRollTorque = 2500f;
            [Tooltip("Частота раскачивания, Гц.")]
            [Min(0.05f)] public float vanSwayHz = 0.5f;
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
            [Tooltip("Время открытия/закрытия заслонки печки (панель печки), с.")]
            [Min(0.05f)] public float stoveFlapSeconds = 0.35f;
            [Tooltip("HUD: ниже этой доли шкалы (0..1) она мигает красным.")]
            [Range(0, 1)] public float hudCriticalFraction = 0.2f;
            [Tooltip("HUD: частота мигания критической шкалы, Гц.")]
            [Min(0.1f)] public float hudBlinkHz = 2.5f;
            [Tooltip("HUD: обычная непрозрачность (0..1) — полупрозрачный, без рамок.")]
            [Range(0, 1)] public float hudAlpha = 0.85f;
            [Tooltip("HUD: скорость плавного движения шкал (доля в секунду).")]
            [Min(0.1f)] public float hudBarSpeed = 2f;
        }

        /// <summary>Поломка фургона и постановочная сцена с лампой (ТЗ 3, акт 3).</summary>
        [Serializable]
        public sealed class BreakdownSettings
        {
            [Header("Остановка фургона")]
            [Tooltip("За сколько секунд фургон встаёт после поломки.")]
            [Min(0.5f)] public float stopSeconds = 3.5f;
            [Tooltip("Фургон обязан встать не дальше этого расстояния (м) — до завала. Если на скорости не успевает, тормозит сильнее.")]
            [Min(3)] public float maxStopDistance = 20f;
            [Tooltip("Толчок камеры при лязге (0..1).")]
            [Range(0, 1)] public float cameraTrauma = 0.8f;
            [Header("Сцена")]
            [Tooltip("Через сколько после лязга начинается запись A3_01, с.")]
            [Min(0)] public float logDelaySeconds = 1.2f;
            [Tooltip("Пауза после конца A3_01 до начала сцены с лампой, с.")]
            [Min(0)] public float sceneDelaySeconds = 1.5f;
            [Tooltip("Страховка: сцена начнётся не позже этого времени после лязга, с (даже если запись не закончилась).")]
            [Min(5)] public float maxWaitSeconds = 30f;
            [Tooltip("Сколько ждать, пока робот выйдет из фургона и камера перелетит, с.")]
            [Min(0.2f)] public float exitSeconds = 1.0f;
            [Tooltip("Поворот взгляда на печку фургона, с.")]
            [Min(0.1f)] public float lookAtVanSeconds = 1.0f;
            [Tooltip("Сколько секунд робот отходит от фургона назад, пока смотрит на печку (чтобы окно было видно не в упор).")]
            [Min(0)] public float stepBackSeconds = 0.6f;
            [Tooltip("Подъём лампы в руку, с. На середине Искра переходит из печки в лампу.")]
            [Min(0.2f)] public float raiseSeconds = 1.8f;
            [Tooltip("Пауза с поднятой лампой, с.")]
            [Min(0)] public float holdSeconds = 0.8f;
            [Tooltip("Поворот взгляда вдоль пути вверх по склону, с.")]
            [Min(0.1f)] public float lookAlongPathSeconds = 1.2f;
            [Tooltip("Подсказка после сцены.")]
            public string hintText = "Искра в лампе. Идите вверх по склону";
        }

        /// <summary>Лампа в руке от первого лица: положения кисти в системе камеры робота и покачивание.</summary>
        [Serializable]
        public sealed class LampHandSettings
        {
            [Tooltip("Где кисть держит лампу, м (x вправо, y вверх, z вперёд от точки глаз).")]
            public Vector3 carryHand = new Vector3(0.13f, 0.07f, 0.6f);
            [Tooltip("Где локоть при удержании лампы: правее кисти, чтобы рука шла сбоку и не закрывала лампу.")]
            public Vector3 carryElbow = new Vector3(0.45f, -0.25f, 0.1f);
            [Tooltip("Где кисть до подъёма (лампа ниже кадра).")]
            public Vector3 hiddenHand = new Vector3(0.34f, -0.85f, 0.3f);
            [Tooltip("Лампа висит на ручке: насколько ниже кисти её низ, м.")]
            [Min(0)] public float lampBelowHand = 0.34f;
            [Tooltip("Длина руки от локтя до кисти, м.")]
            [Min(0.1f)] public float armLength = 0.42f;
            [Tooltip("Покачивание при ходьбе: вертикаль и в сторону, м.")]
            [Min(0)] public float swayVertical = 0.02f;
            [Min(0)] public float swaySide = 0.012f;
            [Tooltip("Раскачивание лампы на ручке при ходьбе, градусов.")]
            [Min(0)] public float swingDegrees = 5f;
            [Tooltip("Шагов в секунду для покачивания.")]
            [Min(0.1f)] public float swayStepsPerSecond = 1.6f;
            [Header("Пламя на порывах ветра")]
            [Tooltip("Насколько порыв приглушает свет лампы (доля яркости при силе порыва 1).")]
            [Range(0, 1)] public float gustDimAmount = 0.55f;
            [Tooltip("Во сколько раз быстрее мерцает свет лампы в порыве.")]
            [Min(1)] public float gustFlickerMultiplier = 2.5f;
            [Tooltip("Дополнительная глубина мерцания в порыве.")]
            [Range(0, 1)] public float gustFlickerExtra = 0.3f;
            [Tooltip("Лампу наклоняет по ветру, градусов при силе порыва 1.")]
            [Min(0)] public float gustLampTiltDegrees = 9f;
            [Tooltip("Сколько секунд занимает открытие панели топлива лампы (плавное появление).")]
            [Min(0.05f)] public float panelFadeSeconds = 0.2f;
            [Tooltip("Через сколько секунд после взятия лампы напомнить про R, с.")]
            [Min(0)] public float rHintDelaySeconds = 9f;
            [Tooltip("Текст подсказки про R.")]
            public string rHintText = "R — подкинуть топливо в лампу";
        }

        [Serializable]
        public sealed class TutorialSettings
        {
            [Tooltip("Щепки, которые выдаются, когда Искра поставлена в печку (чтобы было что подкинуть в обучении).")]
            [Min(0)] public int startKindling = 3;
            [Tooltip("Через сколько секунд после старта показать первую подсказку (управление).")]
            [Min(0)] public float firstHintDelaySeconds = 1.5f;
            [Tooltip("Сколько показывается обычная подсказка, с.")]
            [Min(1)] public float hintSeconds = 6f;
            [Tooltip("После постановки Искры в печку: через сколько напомнить про посадку в фургон, с.")]
            [Min(0)] public float sitHintDelaySeconds = 7f;
            [Tooltip("После первой посадки: через сколько показать подсказку про управление фургоном, с.")]
            [Min(0)] public float drivingHintDelaySeconds = 1.5f;
            [Tooltip("Если банку не взяли за столько секунд после первой подсказки — напомнить, где она, с.")]
            [Min(1)] public float findJarHintDelaySeconds = 9f;
            [Header("Вид подсказок")]
            [Min(8)] public float hintFontSize = 30f;
            [Tooltip("Отступ подсказки обучения от верхнего края (px эталона 1920×1080).")]
            [Min(0)] public float hintTopOffset = 150f;
            [Min(0.1f)] public float hintFadeSpeed = 4f;
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
            [Tooltip("Пауза между записями в очереди, с.")]
            [Min(0)] public float queueGapSeconds = 0.8f;
            [Tooltip("Сколько держится последняя строка после конца записи, с (до начала следующей записи не ждём).")]
            [Min(0)] public float lastLineHoldSeconds = 0.5f;

            [Header("Вид субтитров (радио-эффект)")]
            [Tooltip("Размер шрифта субтитров.")]
            [Min(8)] public float fontSize = 36f;
            [Tooltip("Отступ субтитров от нижнего края экрана (в пикселях эталона 1920×1080). Подсказки взаимодействия выше.")]
            [Min(0)] public float bottomOffset = 70f;
            [Tooltip("Сколько длится «шум» в начале каждой строки, с: буквы подменяются помехами и дрожат.")]
            [Min(0)] public float glitchSeconds = 0.4f;
            [Tooltip("Амплитуда дрожания в начале строки, px.")]
            [Min(0)] public float glitchJitterPixels = 7f;
            [Tooltip("Скорость появления/исчезновения субтитров, 1/с.")]
            [Min(0.1f)] public float fadeSpeed = 6f;
            [Tooltip("Слабое постоянное мерцание прозрачности «как в рации» (0 — нет).")]
            [Range(0, 0.5f)] public float flickerAmount = 0.08f;
        }
    }
}
