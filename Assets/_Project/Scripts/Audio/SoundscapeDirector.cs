using LastRide.Core;
using LastRide.Fire;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Связывает состояние игры с непрерывными слоями звука (10.2): ветер, огонь, механизм робота, двигатель.
    /// Создаётся <see cref="AudioManager"/>; читает <c>GameManager</c> каждый кадр и пишет параметры в слои.
    /// Все числа — <c>GameBalance → Audio</c>.
    /// </summary>
    public sealed class SoundscapeDirector : MonoBehaviour
    {
        public WindLayer Wind { get; private set; }
        public FireLayer Fire { get; private set; }
        public MechanismLayer Mechanism { get; private set; }
        public EngineLayer Engine { get; private set; }

        public BeaconLayer Beacon { get; private set; }
        /// <summary>Финал: 0 — не начат, 0…1 — гул набирает силу после доставки Искры.</summary>
        public float Finale { get; private set; }
        private bool _finaleOn;

        private void OnEnable()
        {
            EventBus<SparkDelivered>.Subscribe(OnDelivered);
            EventBus<CheckpointRestored>.Subscribe(OnRestored);
        }

        private void OnDisable()
        {
            EventBus<SparkDelivered>.Unsubscribe(OnDelivered);
            EventBus<CheckpointRestored>.Unsubscribe(OnRestored);
        }

        private void OnDelivered(SparkDelivered e) => _finaleOn = true;
        private void OnRestored(CheckpointRestored e) { _finaleOn = false; Finale = 0f; }

        private VanController _van;
        private float _nextVanSearch;
        private float _master = 1f;       // общая «живость» слоёв по состоянию игры (сглаженная)
        private float _inVan;             // 0 снаружи … 1 в фургоне (сглаженная)

        private void Start()
        {
            var am = AudioManager.Instance;
            if (am == null) { enabled = false; return; }
            Wind = am.CreateLayer<WindLayer>("Layer_Wind", AudioBus.Ambience);
            Fire = am.CreateLayer<FireLayer>("Layer_Fire", AudioBus.Ambience);
            Engine = am.CreateLayer<EngineLayer>("Layer_Engine", AudioBus.Ambience);
            Mechanism = am.CreateLayer<MechanismLayer>("Layer_Mechanism", AudioBus.SFX);
            Beacon = am.CreateLayer<BeaconLayer>("Layer_Beacon", AudioBus.Ambience);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Balance == null || Wind == null) return;
            var c = gm.Balance.audio;

            // Общая громкость по состоянию: пауза — приглушено, меню/титры — тишина.
            float targetMaster;
            switch (gm.State)
            {
                case GameState.Playing:
                case GameState.Dying:
                case GameState.Restoring:
                case GameState.Ending: targetMaster = 1f; break;
                case GameState.Paused: targetMaster = 0.25f; break;
                default: targetMaster = 0f; break;
            }
            float dt = Time.unscaledDeltaTime;
            _master = Mathf.MoveTowards(_master, targetMaster, dt * 2.5f);

            bool inVanMode = gm.PlayerMode == PlayerMode.InVan;
            _inVan = Mathf.MoveTowards(_inVan, inVanMode ? 1f : 0f, dt * 3f);

            if (_van == null && Time.unscaledTime >= _nextVanSearch)
            {
                _van = FindAnyObjectByType<VanController>();
                _nextVanSearch = Time.unscaledTime + 1f;
            }

            UpdateWind(gm, c);
            UpdateFire(gm, c);
            UpdateMechanism(gm, c);
            UpdateEngine(gm, c, inVanMode);
            UpdateBeacon(gm, c, dt);
        }

        private void UpdateBeacon(GameManager gm, GameBalance.AudioSettingsData c, float dt)
        {
            if (gm.State == GameState.MainMenu) { _finaleOn = false; Finale = 0f; }
            if (_finaleOn) Finale = Mathf.MoveTowards(Finale, 1f, dt / Mathf.Max(1f, c.beaconFinaleRise));

            float level = 0f;
            if (gm.Weather != null && gm.Weather.Act == ActId.Act3)
            {
                float p = Mathf.Clamp01(gm.Weather.Progress01);
                float t = Mathf.Clamp01((p - c.beaconStartProgress) / Mathf.Max(0.01f, 1f - c.beaconStartProgress));
                level = c.beaconSummitLevel * t * t * (3f - 2f * t);
            }
            // Финал: гул растёт от уровня Вершины до полного и раскрывает обертоны.
            level = Mathf.Lerp(level, 1f, Finale);
            float rich = Mathf.Clamp01(Mathf.Max(level * 0.5f, Finale));
            Beacon.Set(level, rich);
            Beacon.Gain = c.beaconGain * _master;
        }

        private void UpdateWind(GameManager gm, GameBalance.AudioSettingsData c)
        {
            var w = gm.Weather;
            float wind01 = w != null ? Mathf.Clamp(w.Current.windStrength / Mathf.Max(1f, c.windFullAt), 0f, 1.3f) : 0f;
            float gust = w != null ? w.GustStrength01 : 0f;
            Wind.Set(wind01, gust, _inVan, c.windFloor, c.windInVanGain, c.windInVanCutoff);
            Wind.Gain = c.windGain * _master;
        }

        private void UpdateFire(GameManager gm, GameBalance.AudioSettingsData c)
        {
            var s = gm.Spark;
            float intensity = 0f, embers = 0f, prox = 0f;
            if (s != null && s.IsLit)
            {
                if (s.State == SparkState.Embers) { intensity = 0.08f; embers = s.EmbersFraction; }
                else intensity = 0.2f + 0.8f * Mathf.Sqrt(s.Fraction);

                if (s.Source == FireSourceKind.Lamp) prox = c.lampHandGain;
                else if (gm.PlayerMode == PlayerMode.InVan) prox = 1f;
                else
                {
                    var robot = RobotController.Active;
                    if (_van != null && robot != null)
                    {
                        float d = Vector3.Distance(_van.transform.position, robot.transform.position);
                        float t = Mathf.Clamp01(1f - d / Mathf.Max(1f, c.stoveHearRange));
                        prox = Mathf.Max(0.04f, t * t);
                    }
                    else prox = 0.3f;
                }
            }
            Fire.Set(intensity, embers, prox, c.crackleRateMax);
            Fire.Gain = c.fireGain * _master;
        }

        private void UpdateMechanism(GameManager gm, GameBalance.AudioSettingsData c)
        {
            var wm = gm.Warmth;
            if (wm == null) { Mechanism.Gain = 0f; return; }
            var robot = RobotController.Active;
            bool jammed = robot != null && robot.IsJammed;
            float alive = 1f - Mathf.Clamp01(wm.FreezeProgress);
            Mechanism.Set(wm.TickRate, wm.TickPitch, wm.Creak, jammed, alive, c.creakGain);
            Mechanism.Gain = c.mechanismGain * _master * Mathf.Lerp(1f, c.mechanismInVanGain, _inVan);
        }

        private void UpdateEngine(GameManager gm, GameBalance.AudioSettingsData c, bool inVanMode)
        {
            if (_van == null) { Engine.Gain = 0f; return; }
            bool running = inVanMode && _van.CanDrive && gm.State != GameState.MainMenu;
            float maxSpeed = Mathf.Max(1f, gm.Balance.van.maxSpeed);
            Engine.Set(running, Mathf.Clamp01(_van.Speed / maxSpeed), Mathf.Clamp01(Mathf.Abs(_van.Throttle)),
                       _van.IceFraction, _van.DeepSnowFraction, c.engineIdleHz, c.engineMaxHz, c.engineRevUp, c.engineRevDown, c.wheelNoiseGain);
            // Снаружи слышно издали (на холостых — стоит заглушенный), поэтому слой не режем — он сам замолкает без оборотов.
            Engine.Gain = c.engineGain * _master;
        }
    }
}
