using LastRide.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace LastRide.Audio
{
    /// <summary>
    /// Аудиоядро (10.1): владеет микшером <c>GameMixer</c>, AudioListener с <see cref="MasterLimiter"/>,
    /// пулом AudioSource для коротких SFX и фабрикой непрерывных слоёв. Громкости шин: настройки игрока (PlayerPrefs, 0…1)
    /// + базовые дБ из <c>GameBalance.audio</c> + ducking. Создаётся в <c>GameManager.Awake</c>.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const float MinDb = -80f;

        private AudioMixer _mixer;
        private readonly AudioMixerGroup[] _groups = new AudioMixerGroup[AudioBusInfo.Count];
        private readonly float[] _userVolume = { 1f, 1f, 1f, 1f, 1f };      // 0…1 из настроек
        private readonly float[] _duckDb = new float[AudioBusInfo.Count];   // текущий ducking (≤ 0)
        private readonly float[] _duckTarget = new float[AudioBusInfo.Count];
        private AudioSource[] _pool;
        private int _poolNext;
        private GameBalance.AudioSettingsData _cfg;
        private MasterLimiter _limiter;

        public MasterLimiter Limiter => _limiter;
        public AudioMixer Mixer => _mixer;
        public bool HasMixer => _mixer != null;

        private GameBalance.AudioSettingsData Cfg => _cfg ??= (GameManager.Instance != null && GameManager.Instance.Balance != null
            ? GameManager.Instance.Balance.audio : new GameBalance.AudioSettingsData());

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;

            var cfgAsset = GameConfig.Load();
            _mixer = cfgAsset != null ? cfgAsset.mixer : null;
            if (_mixer == null)
                Debug.LogWarning("[AudioManager] Нет GameMixer (GameConfig.mixer) — звук идёт мимо микшера. Запусти «The Last Ride/Setup/Create Audio Mixer».");
            else
                for (int i = 0; i < AudioBusInfo.Count; i++)
                {
                    var found = _mixer.FindMatchingGroups(AudioBusInfo.GroupPaths[i]);
                    _groups[i] = found.Length > 0 ? found[0] : null;
                }

            // Слушатель + лимитер: если на камере уже есть AudioListener, лимитер вешаем на него (OnAudioFilterRead требует компонент на том же объекте).
            EnsureListener();

            // Пул одноразовых источников.
            int n = Mathf.Max(4, Cfg.sfxPoolSize);
            _pool = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("SFX_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.outputAudioMixerGroup = _groups[(int)AudioBus.SFX];
                _pool[i] = s;
            }

            gameObject.AddComponent<SoundscapeDirector>();
            gameObject.AddComponent<SfxDirector>();
            gameObject.AddComponent<MusicDirector>();

            for (int i = 0; i < AudioBusInfo.Count; i++)
                _userVolume[i] = PlayerPrefs.GetFloat(AudioBusInfo.PrefKeys[i], 1f);
            ApplyAllVolumes(true);
        }

        // Микшер в Awake первого кадра может ещё не принимать SetFloat — повторяем после старта.
        private void Start() => ApplyAllVolumes(true);

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void EnsureListener()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            GameObject host;
            if (listener != null) host = listener.gameObject;
            else
            {
                host = new GameObject("AudioListener");
                host.transform.SetParent(transform, false);
                host.AddComponent<AudioListener>();
            }
            _limiter = host.GetComponent<MasterLimiter>();
            if (_limiter == null) _limiter = host.AddComponent<MasterLimiter>();
            _limiter.Configure(Cfg.limiterThreshold, Cfg.limiterCeiling);
        }

        /// <summary>Вызывать, если слушатель пересоздан (смена камеры).</summary>
        public void RefreshListener() => EnsureListener();

        // ------------------------------------------------------------------ громкости

        public AudioMixerGroup Group(AudioBus bus) => _groups[(int)bus];

        public float GetUserVolume(AudioBus bus) => _userVolume[(int)bus];

        /// <summary>Пользовательская громкость шины 0…1 (меню настроек), сохраняется в PlayerPrefs.</summary>
        public void SetUserVolume(AudioBus bus, float v01)
        {
            int i = (int)bus;
            _userVolume[i] = Mathf.Clamp01(v01);
            PlayerPrefs.SetFloat(AudioBusInfo.PrefKeys[i], _userVolume[i]);
            ApplyVolume(i);
        }

        /// <summary>Ducking шины в дБ (≤ 0): плавно применяется в Update. Используется AudioLogPlayer (10.5).</summary>
        public void SetDuck(AudioBus bus, float db) => _duckTarget[(int)bus] = Mathf.Min(0f, db);

        private float BaseDb(int bus)
        {
            var c = Cfg;
            switch ((AudioBus)bus)
            {
                case AudioBus.Master: return c.masterDb;
                case AudioBus.Music: return c.musicDb;
                case AudioBus.SFX: return c.sfxDb;
                case AudioBus.Ambience: return c.ambienceDb;
                default: return c.voiceDb;
            }
        }

        private void ApplyVolume(int i)
        {
            if (_mixer == null) return;
            float user = _userVolume[i];
            float userDb = user <= 0.0001f ? MinDb : ProcAudio.LinearToDb(user);
            float db = userDb <= MinDb ? MinDb : Mathf.Max(MinDb, BaseDb(i) + userDb + _duckDb[i]);
            _mixer.SetFloat(AudioBusInfo.VolumeParams[i], db);
        }

        private void ApplyAllVolumes(bool snapDuck)
        {
            for (int i = 0; i < AudioBusInfo.Count; i++)
            {
                if (snapDuck) _duckDb[i] = _duckTarget[i];
                ApplyVolume(i);
            }
        }

        private void Update()
        {
            // Плавный ducking (разные скорости на вход/выход).
            for (int i = 0; i < AudioBusInfo.Count; i++)
            {
                float t = _duckTarget[i], d = _duckDb[i];
                if (Mathf.Approximately(t, d)) continue;
                float speed = Mathf.Abs(Cfg.duckDb) / (t < d ? Cfg.duckAttack : Cfg.duckRelease);
                _duckDb[i] = Mathf.MoveTowards(d, t, speed * Time.unscaledDeltaTime);
                ApplyVolume(i);
            }
        }

        // ------------------------------------------------------------------ SFX и слои

        /// <summary>Проиграть короткий клип из пула (разовый). Возвращает источник (для настройки питча и т.п.).</summary>
        public AudioSource PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f, AudioBus bus = AudioBus.SFX)
        {
            if (clip == null || _pool == null) return null;
            // Ищем свободный источник; если все заняты — вытесняем самый старый.
            AudioSource src = null;
            for (int k = 0; k < _pool.Length; k++)
            {
                var s = _pool[(_poolNext + k) % _pool.Length];
                if (!s.isPlaying) { src = s; _poolNext = (_poolNext + k + 1) % _pool.Length; break; }
            }
            if (src == null) { src = _pool[_poolNext]; _poolNext = (_poolNext + 1) % _pool.Length; src.Stop(); }
            src.outputAudioMixerGroup = _groups[(int)bus];
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.Play();
            return src;
        }

        /// <summary>Создать непрерывный слой на отдельном объекте под менеджером.</summary>
        public T CreateLayer<T>(string name, AudioBus bus = AudioBus.Ambience) where T : ProceduralLayer
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var layer = go.AddComponent<T>();
            layer.Init(_groups[(int)bus], Cfg.layerThreshold);
            return layer;
        }
    }
}
