using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastRide.Core
{
    /// <summary>
    /// Единственный менеджер игры: машина состояний (ТЗ 1.2) и текущий режим игрока.
    /// Создаётся автоматически до загрузки первой сцены и живёт между сценами.
    /// Остальные системы узнают о переходах через <see cref="GameStateChanged"/> и <see cref="PlayerModeChanged"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.MainMenu;
        public PlayerMode PlayerMode { get; private set; } = PlayerMode.OnFoot;

        /// <summary>Корневой конфиг баланса (из Resources).</summary>
        public GameConfig Config { get; private set; }

        /// <summary>Текущая сложность. До этапа 14 всегда «Лёгкая».</summary>
        public DifficultyProfile Difficulty { get; private set; }

        public GameBalance Balance => Config != null ? Config.balance : null;

        /// <summary>Инвентарь топлива (общий для робота, печки и лампы).</summary>
        public LastRide.Fire.Inventory Inventory { get; } = new LastRide.Fire.Inventory();

        /// <summary>Искра: запас горения печки/лампы, тление, угасание (ТЗ 4.2).</summary>
        public LastRide.Fire.SparkFire Spark { get; private set; }

        /// <summary>Тепло робота: убывает снаружи, растёт у горящей печки, замерзание (ТЗ 4.3).</summary>
        public LastRide.Player.WarmthSystem Warmth { get; private set; }

        /// <summary>Погода: акты, шторм/затишье, порывы (ТЗ 4.6). Пишет время замерзания и множитель порыва в <see cref="Warmth"/>.</summary>
        public LastRide.Weather.WeatherSystem Weather { get; private set; }

        /// <summary>Контроллер актов: текущий акт, переходы, время в актах.</summary>
        public LastRide.Narrative.ActController Acts { get; private set; }

        /// <summary>Пролог и контекстное обучение акта 1.</summary>
        public LastRide.Narrative.TutorialController Tutorial { get; private set; }

        /// <summary>Очередь аудиозаписей создателя и субтитры (ТЗ 7.1).</summary>
        public LastRide.Narrative.AudioLogPlayer AudioLogs { get; private set; }

        /// <summary>Положение игрока на оси пути (расстояние, смещение, акт по положению).</summary>
        public LastRide.Level.LevelProgress Progress { get; } = new LastRide.Level.LevelProgress();

        /// <summary>Множитель скорости времени для отладки (F5). На паузе время всё равно стоит.</summary>
        public float DebugTimeMultiplier
        {
            get => _debugTimeMultiplier;
            set { _debugTimeMultiplier = Mathf.Max(0.01f, value); ApplyTimeScale(); }
        }

        private float _debugTimeMultiplier = 1f;

        // Разрешённые переходы. Всё остальное отклоняется с предупреждением.
        private static readonly Dictionary<GameState, GameState[]> Transitions = new Dictionary<GameState, GameState[]>
        {
            { GameState.MainMenu,  new[] { GameState.Playing } },
            { GameState.Playing,   new[] { GameState.Paused, GameState.Dying, GameState.Restoring, GameState.Ending, GameState.MainMenu } },
            { GameState.Paused,    new[] { GameState.Playing, GameState.Restoring, GameState.MainMenu } },
            { GameState.Dying,     new[] { GameState.Restoring } },
            { GameState.Restoring, new[] { GameState.Playing } },
            { GameState.Ending,    new[] { GameState.Credits } },
            { GameState.Credits,   new[] { GameState.MainMenu } },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameManager]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Config = GameConfig.Load();
            Difficulty = Config != null ? Config.defaultDifficulty : null;
            GameInput.Initialize();
            Spark = new LastRide.Fire.SparkFire(Balance);
            Warmth = new LastRide.Player.WarmthSystem(Balance);
            DebugTools.DebugOverlay.RegisterLine("Тепло", () => Warmth.ToString());
            Weather = new LastRide.Weather.WeatherSystem(Config, Balance);
            DebugTools.DebugOverlay.RegisterLine("Погода", () => Weather.ToString());
            Acts = new LastRide.Narrative.ActController(this);
            DebugTools.DebugOverlay.RegisterLine("Акт", () => Acts.ToString());
            AudioLogs = new LastRide.Narrative.AudioLogPlayer(Config, Balance);
            DebugTools.DebugOverlay.RegisterLine("Записи", () => AudioLogs.ToString());
            Tutorial = new LastRide.Narrative.TutorialController(this);
            DebugTools.DebugOverlay.RegisterLine("Обучение", () => Tutorial.ToString());
            gameObject.AddComponent<LastRide.UI.SubtitleView>();
            gameObject.AddComponent<LastRide.UI.TutorialHintView>();
            gameObject.AddComponent<LastRide.Weather.WindPusher>();
            gameObject.AddComponent<LastRide.Weather.SnowfallView>();
            gameObject.AddComponent<LastRide.Weather.AtmosphereView>();
            gameObject.AddComponent<LastRide.Weather.PostFxView>();
            gameObject.AddComponent<LastRide.Weather.FrostView>();
            gameObject.AddComponent<LastRide.Audio.AudioManager>();
            DebugTools.DebugOverlay.RegisterLine("Путь", () => Progress.ToString());
            DebugTools.DebugOverlay.RegisterLine("Инвентарь", () => Inventory.ToString());
            DebugTools.DebugOverlay.RegisterLine("Огонь", () => Spark.ToString());

            // Запуск Play Mode прямо со сцены Game (в редакторе) — сразу играем.
            State = SceneManager.GetActiveScene().name == SceneNames.MainMenu ? GameState.MainMenu : GameState.Playing;
            ApplyStateSideEffects();
        }

        private void Update()
        {
            // Огонь горит всегда, пока идёт игра — и в фургоне, и пока робот снаружи. На паузе timeScale = 0.
            if (State == GameState.Playing)
            {
                Spark.Tick(Time.deltaTime);
                Weather.Tick(Time.deltaTime, Warmth); // до Warmth.Tick: тот читает свежее время замерзания и множитель порыва
                Progress.Tick(this);
                Acts.Tick(Time.deltaTime);
                AudioLogs.Tick(Time.deltaTime);
                Tutorial.Tick(Time.deltaTime);
                Warmth.Tick(Time.deltaTime, PlayerMode, Spark);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool CanTransition(GameState to)
        {
            return Transitions.TryGetValue(State, out var allowed) && System.Array.IndexOf(allowed, to) >= 0;
        }

        /// <summary>Перейти в состояние. Возвращает false, если переход запрещён.</summary>
        public bool RequestState(GameState to)
        {
            if (to == State) return true;
            if (!CanTransition(to))
            {
                Debug.LogWarning($"[GameManager] Переход {State} → {to} запрещён.");
                return false;
            }

            var prev = State;
            State = to;
            ApplyStateSideEffects();
            EventBus<GameStateChanged>.Publish(new GameStateChanged { Previous = prev, Current = to });
            return true;
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) RequestState(GameState.Paused);
            else if (State == GameState.Paused) RequestState(GameState.Playing);
        }

        public void SetPlayerMode(PlayerMode mode)
        {
            if (mode == PlayerMode) return;
            var prev = PlayerMode;
            PlayerMode = mode;
            GameInput.ApplyContext(State, PlayerMode);
            EventBus<PlayerModeChanged>.Publish(new PlayerModeChanged { Previous = prev, Current = mode });
        }

        public void SetDifficulty(DifficultyProfile profile)
        {
            if (profile != null) Difficulty = profile;
        }

        /// <summary>Сброс режима без события — при загрузке чекпоинта / новой игре.</summary>
        public void ResetPlayerMode(PlayerMode mode)
        {
            PlayerMode = mode;
            GameInput.ApplyContext(State, PlayerMode);
        }

        public bool IsGameplayActive => State == GameState.Playing;

        private void ApplyStateSideEffects()
        {
            ApplyTimeScale();
            GameInput.ApplyContext(State, PlayerMode);
            bool lockCursor = State == GameState.Playing;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = State == GameState.Paused ? 0f : _debugTimeMultiplier;
        }
    }
}
