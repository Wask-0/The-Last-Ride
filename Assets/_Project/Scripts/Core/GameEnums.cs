namespace LastRide.Core
{
    /// <summary>Состояния игры (ТЗ 1.2).</summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        Dying,
        Restoring,
        Ending,
        Credits
    }

    /// <summary>Режим управления игрока (ТЗ 1.2).</summary>
    public enum PlayerMode
    {
        InVan,
        OnFoot,
        OnFootWithLamp
    }

    /// <summary>Акты истории.</summary>
    public enum ActId
    {
        Act1 = 1,
        Act2 = 2,
        Act3 = 3
    }

    /// <summary>Фаза погоды. None — вне акта 2 (цикла нет).</summary>
    public enum WeatherPhase
    {
        None,
        Calm,
        Storm
    }

    /// <summary>Где сейчас находится Искра.</summary>
    public enum FireSourceKind
    {
        Stove,
        Lamp
    }

    /// <summary>Причина смерти — для разной подачи (иней / угасание Искры).</summary>
    public enum DeathCause
    {
        Frozen,
        SparkExtinguished
    }
}
