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

            // Запуск Play Mode прямо со сцены Game (в редакторе) — сразу играем.
            State = SceneManager.GetActiveScene().name == SceneNames.MainMenu ? GameState.MainMenu : GameState.Playing;
            ApplyStateSideEffects();
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
