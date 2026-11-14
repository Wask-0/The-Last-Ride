using LastRide.Core;
using LastRide.Fire;
using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Разовые звуки (10.3): слушает события игры и играет клипы из <see cref="SfxBank"/> через пул <see cref="AudioManager"/>.
    /// Сюда же — радиоэффекты аудиозаписей (щелчок плёнки, помехи, помехи-подложка на записи без голоса) и проигрывание
    /// записанного голоса создателя (<c>AudioLogData.clip</c>) на шине Voice. Все громкости — <c>GameBalance → Audio</c>.
    /// </summary>
    public sealed class SfxDirector : MonoBehaviour
    {
        private SfxBank _bank;
        private AudioManager _am;
        private AudioSource _voice, _staticLoop;
        private float _alarmTimer;
        private float _readyAt;      // не играть звуки перехода до этого времени (инициализация сцены)

        private GameBalance.AudioSettingsData Cfg =>
            GameManager.Instance != null && GameManager.Instance.Balance != null ? GameManager.Instance.Balance.audio : _fallback ??= new GameBalance.AudioSettingsData();
        private GameBalance.AudioSettingsData _fallback;

        private void Awake()
        {
            _am = AudioManager.Instance;
            _bank = SfxBank.Build();
            _readyAt = Time.unscaledTime + 1.0f;

            _voice = NewSource("Voice_Log", AudioBus.Voice, false);
            _staticLoop = NewSource("Voice_Static", AudioBus.Voice, true);
            _staticLoop.clip = _bank.StaticLoop;
        }

        private AudioSource NewSource(string n, AudioBus bus, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f;
            s.outputAudioMixerGroup = _am != null ? _am.Group(bus) : null;
            return s;
        }

        private void OnEnable()
        {
            EventBus<InventoryChanged>.Subscribe(OnInventory);
            EventBus<StoveFlapChanged>.Subscribe(OnFlap);
            EventBus<FuelThrown>.Subscribe(OnThrown);
            EventBus<FireRekindled>.Subscribe(OnRekindle);
            EventBus<FireExtinguished>.Subscribe(OnExtinguish);
            EventBus<SparkMoved>.Subscribe(OnSparkMoved);
            EventBus<PlayerModeChanged>.Subscribe(OnMode);
            EventBus<VanBrokeDown>.Subscribe(OnBreakdown);
            EventBus<RobotStep>.Subscribe(OnStep);
            EventBus<WarmthCritical>.Subscribe(OnWarmthCritical);
            EventBus<PlayerFreezing>.Subscribe(OnFreezing);
            EventBus<WindGustStarted>.Subscribe(OnGust);
            EventBus<AudioLogStarted>.Subscribe(OnLogStarted);
            EventBus<AudioLogFinished>.Subscribe(OnLogFinished);
            EventBus<CheckpointRestored>.Subscribe(OnRestored);
        }

        private void OnDisable()
        {
            EventBus<InventoryChanged>.Unsubscribe(OnInventory);
            EventBus<StoveFlapChanged>.Unsubscribe(OnFlap);
            EventBus<FuelThrown>.Unsubscribe(OnThrown);
            EventBus<FireRekindled>.Unsubscribe(OnRekindle);
            EventBus<FireExtinguished>.Unsubscribe(OnExtinguish);
            EventBus<SparkMoved>.Unsubscribe(OnSparkMoved);
            EventBus<PlayerModeChanged>.Unsubscribe(OnMode);
            EventBus<VanBrokeDown>.Unsubscribe(OnBreakdown);
            EventBus<RobotStep>.Unsubscribe(OnStep);
            EventBus<WarmthCritical>.Unsubscribe(OnWarmthCritical);
            EventBus<PlayerFreezing>.Unsubscribe(OnFreezing);
            EventBus<WindGustStarted>.Unsubscribe(OnGust);
            EventBus<AudioLogStarted>.Unsubscribe(OnLogStarted);
            EventBus<AudioLogFinished>.Unsubscribe(OnLogFinished);
            EventBus<CheckpointRestored>.Unsubscribe(OnRestored);
        }

        private bool Ready => Time.unscaledTime >= _readyAt && GameManager.Instance != null && GameManager.Instance.State != GameState.MainMenu;

        private float Jitter() => 1f + Random.Range(-Cfg.pitchJitter, Cfg.pitchJitter);

        private void Play(AudioClip clip, float vol, float pitch = 1f, AudioBus bus = AudioBus.SFX)
        {
            if (_am != null) _am.PlaySfx(clip, vol, pitch, bus);
        }

        // ------------------------------------------------------------------ события

        private void OnInventory(InventoryChanged e)
        {
            if (e.Delta > 0 && Ready) Play(_bank.Pickup, Cfg.interactGain, Jitter());
        }

        private void OnFlap(StoveFlapChanged e) => Play(e.Open ? _bank.FlapOpen : _bank.FlapClose, Cfg.interactGain);

        private void OnThrown(FuelThrown e)
        {
            // Тяжёлое топливо — ниже тоном.
            float heavy = Mathf.Clamp01(e.AddedSeconds / 120f);
            Play(_bank.Throw, Cfg.interactGain, Mathf.Lerp(1.12f, 0.85f, heavy) * Jitter());
        }

        private void OnRekindle(FireRekindled e) => Play(_bank.Rekindle, Cfg.interactGain);

        private void OnExtinguish(FireExtinguished e) { _alarmTimer = 0f; Play(_bank.Extinguish, Cfg.interactGain); }

        private void OnSparkMoved(SparkMoved e) { Play(_bank.Clink, Cfg.interactGain); Play(_bank.Rekindle, Cfg.interactGain * 0.7f, 1.1f); }

        private void OnMode(PlayerModeChanged e)
        {
            if (!Ready) return;
            // Хлопок двери — при выходе из фургона и посадке (не в переходе лампа ↔ пешком).
            if (e.Previous == PlayerMode.InVan || e.Current == PlayerMode.InVan)
                Play(_bank.DoorSlam, Cfg.vanGain, Jitter());
        }

        private void OnBreakdown(VanBrokeDown e) => Play(_bank.Clang, Cfg.vanGain);

        private void OnStep(RobotStep e)
        {
            AudioClip[] set = e.Surface == SurfaceType.DeepSnow ? _bank.StepDeep : e.Surface == SurfaceType.Ice ? _bank.StepIce : _bank.StepSnow;
            var clip = set[Random.Range(0, set.Length)];
            float vol = Cfg.footstepGain * Mathf.Lerp(0.55f, 1f, e.Strength);
            Play(clip, vol, Jitter() * (e.Surface == SurfaceType.DeepSnow ? 0.9f : 1f));
        }

        private void OnWarmthCritical(WarmthCritical e)
        {
            // Согрелись: робот «подзаводит» механизм ключом.
            if (!e.Entered && Ready) Play(_bank.WindKey, Cfg.interactGain);
        }

        private void OnFreezing(PlayerFreezing e) => Play(_bank.WindDown, Cfg.interactGain);

        private void OnGust(WindGustStarted e)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PlayerMode != PlayerMode.InVan) return; // в фургоне — удар порыва по корпусу
            float k = Mathf.Clamp01(e.Force.magnitude / Mathf.Max(1f, gm.Balance.audio.windFullAt * 1.2f));
            Play(_bank.GustThump, Cfg.vanGain * Mathf.Lerp(0.4f, 1f, k), Jitter());
        }

        // Ducking на время записи: музыка и ambience (ветер/огонь/двигатель/гул) приглушаются, но не полностью.
        private void SetLogDuck(bool on)
        {
            if (_am == null) return;
            _am.SetDuck(AudioBus.Music, on ? Cfg.duckDb : 0f);
            _am.SetDuck(AudioBus.Ambience, on ? Cfg.duckAmbienceDb : 0f);
        }

        private void OnLogStarted(AudioLogStarted e)
        {
            SetLogDuck(true);
            Play(_bank.TapeClick, Cfg.radioGain, 1f, AudioBus.Voice);
            Play(_bank.StaticBurst, Cfg.radioGain, 1f, AudioBus.Voice);
            if (e.Log != null && e.Log.clip != null)
            {
                _voice.clip = e.Log.clip;
                _voice.volume = Cfg.voiceClipGain;
                _voice.Play();
                _staticLoop.volume = Cfg.radioStaticGain * 0.4f; // лёгкий фон под голосом
            }
            else _staticLoop.volume = Cfg.radioStaticGain;
            _staticLoop.Play();
        }

        private void OnLogFinished(AudioLogFinished e)
        {
            SetLogDuck(false);
            _voice.Stop();
            _staticLoop.Stop();
            Play(_bank.StaticBurst, Cfg.radioGain * 0.8f, 1f, AudioBus.Voice);
            Play(_bank.TapeClick, Cfg.radioGain, 0.9f, AudioBus.Voice);
        }

        private void OnRestored(CheckpointRestored e)
        {
            _alarmTimer = 0f;
            _voice.Stop();
            _staticLoop.Stop();
            SetLogDuck(false);
        }

        // ------------------------------------------------------------------ периодические

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Spark == null || gm.Balance == null) return;

            // Сигнал тления: чаще по мере угасания.
            if (gm.State == GameState.Playing && gm.Spark.State == SparkState.Embers)
            {
                _alarmTimer -= Time.unscaledDeltaTime;
                if (_alarmTimer <= 0f)
                {
                    var iv = Cfg.embersAlarmInterval;
                    _alarmTimer = Mathf.Lerp(iv.y, iv.x, gm.Spark.EmbersFraction);
                    Play(_bank.EmbersAlarm, Cfg.interactGain);
                }
            }
            else _alarmTimer = 0f;
        }
    }
}
