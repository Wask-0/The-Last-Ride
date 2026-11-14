using LastRide.Fire;
using LastRide.Narrative;
using UnityEngine;

namespace LastRide.Core
{
    /// <summary>
    /// Корневой конфиг: ссылки на все ScriptableObject-ы баланса.
    /// Лежит в <c>Data/Resources/GameConfig.asset</c> и грузится через <see cref="Load"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Ride/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        public GameBalance balance;
        [Tooltip("Акты по порядку: 1, 2, 3.")]
        public ActSettings[] acts = new ActSettings[0];
        [Tooltip("Типы топлива по порядку клавиш 1–3.")]
        public FuelType[] fuelTypes = new FuelType[0];
        public DifficultyProfile[] difficulties = new DifficultyProfile[0];
        public DifficultyProfile defaultDifficulty;
        public AudioLogData[] audioLogs = new AudioLogData[0];
        [Tooltip("Микшер игры (Master/Music/SFX/Ambience/Voice). Создаётся «The Last Ride/Setup/Create Audio Mixer».")]
        public UnityEngine.Audio.AudioMixer mixer;

        private static GameConfig _cached;

        public static GameConfig Load()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<GameConfig>(ResourcePath);
                if (_cached == null) Debug.LogError("[GameConfig] Не найден Resources/" + ResourcePath + ". Запусти «The Last Ride/Setup/Create Config Assets».");
            }
            return _cached;
        }

        public ActSettings GetAct(ActId id)
        {
            foreach (var a in acts) if (a != null && a.act == id) return a;
            return null;
        }

        public FuelType GetFuel(string id)
        {
            foreach (var f in fuelTypes) if (f != null && f.id == id) return f;
            return null;
        }

        public DifficultyProfile GetDifficulty(string id)
        {
            foreach (var d in difficulties) if (d != null && d.id == id) return d;
            return defaultDifficulty;
        }

        public AudioLogData GetAudioLog(string id)
        {
            foreach (var l in audioLogs) if (l != null && l.id == id) return l;
            return null;
        }
    }
}
