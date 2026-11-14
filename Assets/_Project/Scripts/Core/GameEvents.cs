using UnityEngine;

namespace LastRide.Core
{
    // Базовый набор событий. События, которым нужны типы конкретных систем
    // (FuelType, AudioLogData и т.п.), добавляются вместе с этими системами.
    // Частые значения (запас огня, тепло каждый кадр) по шине не гоняем —
    // их читают напрямую у владельца; по шине идут только переходы.

    // ---- Игра ----
    public struct GameStateChanged : IGameEvent
    {
        public GameState Previous;
        public GameState Current;
    }

    public struct PlayerModeChanged : IGameEvent
    {
        public PlayerMode Previous;
        public PlayerMode Current;
    }

    public struct ActChanged : IGameEvent
    {
        public ActId Previous;
        public ActId Current;
    }

    // ---- Огонь ----
    /// <summary>Запас горения кончился — начались 10 с тления.</summary>
    public struct FireEmbersStarted : IGameEvent { public FireSourceKind Source; }

    /// <summary>Огонь снова разгорелся (подкинули топливо во время тления или после старта).</summary>
    public struct FireRekindled : IGameEvent { public FireSourceKind Source; }

    /// <summary>Искра погасла окончательно.</summary>
    public struct FireExtinguished : IGameEvent { public FireSourceKind Source; }

    /// <summary>Искра перенесена в другой источник (печка → лампа, акт 3).</summary>
    public struct SparkMoved : IGameEvent
    {
        public FireSourceKind From;
        public FireSourceKind To;
    }

    /// <summary>Заслонка печки открылась / закрылась (звук 10.3, подсказки обучения).</summary>
    public struct StoveFlapChanged : IGameEvent { public bool Open; }

    /// <summary>Подкинули топливо в огонь (Added — сколько секунд реально добавилось, остальное сгорело). Для A1_02, звука, обучения.</summary>
    public struct FuelThrown : IGameEvent
    {
        public LastRide.Fire.FuelType Fuel;
        public FireSourceKind Source;
        public float AddedSeconds;
    }

    // ---- Инвентарь ----
    /// <summary>Изменился счётчик топлива (Delta &gt; 0 — подобрали, &lt; 0 — потратили, 0 — сброс).</summary>
    public struct InventoryChanged : IGameEvent
    {
        public LastRide.Fire.FuelType Fuel;
        public int Count;
        public int Delta;
    }

    // ---- Тепло ----
    /// <summary>Тепло пересекло критический порог (вниз — Entered=true, вверх — false).</summary>
    public struct WarmthCritical : IGameEvent { public bool Entered; }

    /// <summary>Заводной механизм «заел» (тепло ниже порога): на Duration секунд робот встал. Для скрипа (10.3).</summary>
    public struct MechanismJammed : IGameEvent { public float Duration; }

    /// <summary>Шаг робота пешком (для звука шагов, 10.3). Strength 0…1 — по скорости.</summary>
    public struct RobotStep : IGameEvent
    {
        public SurfaceType Surface;
        public float Strength;
    }

    /// <summary>Тепло достигло 0 — робот начинает застывать.</summary>
    public struct PlayerFreezing : IGameEvent { }

    // ---- Погода ----
    public struct WeatherPhaseChanged : IGameEvent
    {
        public WeatherPhase Previous;
        public WeatherPhase Current;
    }

    public struct WindGustStarted : IGameEvent
    {
        /// <summary>Направление и сила порыва в мировых координатах.</summary>
        public Vector3 Force;
        public float Duration;
    }

    public struct WindGustEnded : IGameEvent { }

    // ---- Смерть / чекпоинты ----
    public struct PlayerDied : IGameEvent { public DeathCause Cause; }

    public struct CheckpointReached : IGameEvent { public string CheckpointId; }

    /// <summary>Состояние восстановлено с чекпоинта — системы должны перечитать свои данные.</summary>
    public struct CheckpointRestored : IGameEvent { public string CheckpointId; }

    // ---- Сюжет ----
    /// <summary>Сработал сюжетный триггер на уровне (id задаётся в разметке уровня).</summary>
    public struct StoryTriggerFired : IGameEvent { public string TriggerId; }

    public struct VanBrokeDown : IGameEvent { }

    public struct SparkDelivered : IGameEvent { }

    // ---- Аудиозаписи ----
    /// <summary>Запись началась (Log.clip может быть пустым — тогда радиопомехи и авто-тайминг).</summary>
    public struct AudioLogStarted : IGameEvent { public LastRide.Narrative.AudioLogData Log; }

    /// <summary>Сменилась строка субтитров. Пустой Text — строки нет (между строками/после записи).</summary>
    public struct AudioLogLineChanged : IGameEvent
    {
        public LastRide.Narrative.AudioLogData Log;
        public int LineIndex;
        public string Text;
        public float Duration;
    }

    /// <summary>Запись закончилась (для ducking и щелчка плёнки).</summary>
    public struct AudioLogFinished : IGameEvent { public LastRide.Narrative.AudioLogData Log; }

    // ---- UI ----
    /// <summary>Показать / скрыть подсказку взаимодействия. Пустой Text — скрыть.</summary>
    public struct InteractionPromptChanged : IGameEvent { public string Text; }

    /// <summary>Контекстная подсказка обучения.</summary>
    public struct TutorialHintRequested : IGameEvent
    {
        public string Text;
        public float Duration;
    }
}
