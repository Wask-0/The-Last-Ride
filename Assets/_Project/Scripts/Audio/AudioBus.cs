namespace LastRide.Audio
{
    /// <summary>Шины звука игры = группы <c>GameMixer</c> (ТЗ 8.3). Ambience — дочерняя группа SFX (ветер, огонь, двигатель…): её приглушает ducking.</summary>
    public enum AudioBus
    {
        Master = 0,
        Music = 1,
        SFX = 2,
        Ambience = 3,
        Voice = 4
    }

    public static class AudioBusInfo
    {
        public const int Count = 5;

        /// <summary>Выведенные параметры громкости микшера (дБ), в порядке <see cref="AudioBus"/>.</summary>
        public static readonly string[] VolumeParams = { "MasterVol", "MusicVol", "SFXVol", "AmbienceVol", "VoiceVol" };

        /// <summary>Пути групп для <c>AudioMixer.FindMatchingGroups</c>.</summary>
        public static readonly string[] GroupPaths = { "Master", "Master/Music", "Master/SFX", "Master/SFX/Ambience", "Master/Voice" };

        /// <summary>Ключи PlayerPrefs пользовательской громкости (настройки 11.x).</summary>
        public static readonly string[] PrefKeys = { "vol_master", "vol_music", "vol_sfx", "vol_ambience", "vol_voice" };
    }
}
