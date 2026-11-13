using UnityEngine;
using UnityEngine.InputSystem;

namespace LastRide.Core
{
    /// <summary>Имена карт и действий в LastRideInput.inputactions.</summary>
    public static class InputNames
    {
        public const string MapGlobal = "Global";
        public const string MapVan = "Van";
        public const string MapOnFoot = "OnFoot";
        public const string MapUI = "UI";
        public const string MapDebug = "Debug";

        public const string Pause = "Pause";
        public const string Look = "Look";
        public const string Drive = "Drive";
        public const string Handbrake = "Handbrake";
        public const string ExitVan = "ExitVan";
        public const string Stove = "Stove";
        public const string Move = "Move";
        public const string Interact = "Interact";
        public const string Lamp = "Lamp";
        public const string Fuel1 = "Fuel1";
        public const string Fuel2 = "Fuel2";
        public const string Fuel3 = "Fuel3";

        public const string DebugOverlay = "Overlay";
        public const string DebugNextCheckpoint = "NextCheckpoint";
        public const string DebugAddFuel = "AddFuel";
        public const string DebugGodMode = "GodMode";
        public const string DebugTimeScale = "TimeScale";
    }

    /// <summary>
    /// Обёртка над project-wide Input Actions. Сама включает нужные карты по состоянию игры
    /// и режиму игрока (вызывает GameManager). Геймплейные значения при выключенной карте = 0 / false.
    /// </summary>
    public static class GameInput
    {
        public static InputActionAsset Asset { get; private set; }

        private static InputActionMap _global, _van, _foot, _ui, _debug;
        private static InputAction _pause;
        private static InputAction _vanDrive, _vanLook, _handbrake, _exitVan, _stove, _vanFuel1, _vanFuel2, _vanFuel3;
        private static InputAction _move, _footLook, _interact, _lamp, _footFuel1, _footFuel2, _footFuel3;
        private static InputAction _dbgOverlay, _dbgCheckpoint, _dbgFuel, _dbgGod, _dbgTime;

        public static bool IsReady => Asset != null;

        public static void Initialize()
        {
            Asset = InputSystem.actions;
            if (Asset == null)
            {
                Debug.LogError("[GameInput] Project-wide Input Actions не назначены. Запусти «The Last Ride/Setup/Create Input Actions».");
                return;
            }

            _global = Asset.FindActionMap(InputNames.MapGlobal, true);
            _van = Asset.FindActionMap(InputNames.MapVan, true);
            _foot = Asset.FindActionMap(InputNames.MapOnFoot, true);
            _ui = Asset.FindActionMap(InputNames.MapUI, true);
            _debug = Asset.FindActionMap(InputNames.MapDebug, true);

            _pause = _global.FindAction(InputNames.Pause, true);

            _vanDrive = _van.FindAction(InputNames.Drive, true);
            _vanLook = _van.FindAction(InputNames.Look, true);
            _handbrake = _van.FindAction(InputNames.Handbrake, true);
            _exitVan = _van.FindAction(InputNames.ExitVan, true);
            _stove = _van.FindAction(InputNames.Stove, true);
            _vanFuel1 = _van.FindAction(InputNames.Fuel1, true);
            _vanFuel2 = _van.FindAction(InputNames.Fuel2, true);
            _vanFuel3 = _van.FindAction(InputNames.Fuel3, true);

            _move = _foot.FindAction(InputNames.Move, true);
            _footLook = _foot.FindAction(InputNames.Look, true);
            _interact = _foot.FindAction(InputNames.Interact, true);
            _lamp = _foot.FindAction(InputNames.Lamp, true);
            _footFuel1 = _foot.FindAction(InputNames.Fuel1, true);
            _footFuel2 = _foot.FindAction(InputNames.Fuel2, true);
            _footFuel3 = _foot.FindAction(InputNames.Fuel3, true);

            _dbgOverlay = _debug.FindAction(InputNames.DebugOverlay, true);
            _dbgCheckpoint = _debug.FindAction(InputNames.DebugNextCheckpoint, true);
            _dbgFuel = _debug.FindAction(InputNames.DebugAddFuel, true);
            _dbgGod = _debug.FindAction(InputNames.DebugGodMode, true);
            _dbgTime = _debug.FindAction(InputNames.DebugTimeScale, true);

            Asset.Disable();
        }

        /// <summary>Включить карты под текущий контекст.</summary>
        public static void ApplyContext(GameState state, PlayerMode mode)
        {
            if (!IsReady) return;

            bool playing = state == GameState.Playing;
            bool inVan = mode == PlayerMode.InVan;

            Set(_global, state == GameState.Playing || state == GameState.Paused);
            Set(_van, playing && inVan);
            Set(_foot, playing && !inVan);
            Set(_ui, state == GameState.MainMenu || state == GameState.Paused || state == GameState.Credits);
            Set(_debug, playing && Debug.isDebugBuild);
        }

        public static bool IsMapEnabled(string mapName)
        {
            var map = Asset != null ? Asset.FindActionMap(mapName) : null;
            return map != null && map.enabled;
        }

        private static void Set(InputActionMap map, bool on)
        {
            if (on && !map.enabled) map.Enable();
            else if (!on && map.enabled) map.Disable();
        }

        // ---- Общие ----
        public static bool PausePressed => Pressed(_pause);

        /// <summary>Сырая дельта мыши (пиксели за кадр) из активной геймплейной карты.</summary>
        public static Vector2 Look => _van != null && _van.enabled ? _vanLook.ReadValue<Vector2>()
            : _foot != null && _foot.enabled ? _footLook.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>Нажатая клавиша топлива 1–3 в активной карте, 0 — ничего.</summary>
        public static int FuelSlotPressed
        {
            get
            {
                if (_van != null && _van.enabled) return Slot(_vanFuel1, _vanFuel2, _vanFuel3);
                if (_foot != null && _foot.enabled) return Slot(_footFuel1, _footFuel2, _footFuel3);
                return 0;
            }
        }

        // ---- Фургон ----
        /// <summary>x — руль (A/D), y — газ/тормоз-назад (W/S).</summary>
        public static Vector2 Drive => Read(_vanDrive);
        public static bool Handbrake => _handbrake != null && _handbrake.enabled && _handbrake.IsPressed();
        public static bool ExitVanPressed => Pressed(_exitVan);
        public static bool StovePressed => Pressed(_stove);

        // ---- Пешком ----
        public static Vector2 Move => Read(_move);
        public static bool InteractPressed => Pressed(_interact);
        public static bool LampPressed => Pressed(_lamp);

        // ---- Отладка ----
        public static bool DebugOverlayPressed => Pressed(_dbgOverlay);
        public static bool DebugNextCheckpointPressed => Pressed(_dbgCheckpoint);
        public static bool DebugAddFuelPressed => Pressed(_dbgFuel);
        public static bool DebugGodModePressed => Pressed(_dbgGod);
        public static bool DebugTimeScalePressed => Pressed(_dbgTime);

        private static bool Pressed(InputAction a) => a != null && a.enabled && a.WasPressedThisFrame();
        private static Vector2 Read(InputAction a) => a != null && a.enabled ? a.ReadValue<Vector2>() : Vector2.zero;

        private static int Slot(InputAction a, InputAction b, InputAction c)
        {
            if (a.WasPressedThisFrame()) return 1;
            if (b.WasPressedThisFrame()) return 2;
            if (c.WasPressedThisFrame()) return 3;
            return 0;
        }
    }
}
