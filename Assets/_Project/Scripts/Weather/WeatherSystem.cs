using System;
using LastRide.Core;
using LastRide.Player;
using UnityEngine;

namespace LastRide.Weather
{
    /// <summary>
    /// Погода игры (ТЗ 4.6): одна на игру (<c>GameManager.Instance.Weather</c>), тикается из GameManager только в Playing.
    /// • Акт (<see cref="SetAct"/>): параметры <see cref="Current"/> плавно (smoothstep) переходят к пресетам акта за <c>transitionSeconds</c>.
    /// • Акт 2 (<c>stormCycle</c>): цикл «Шторм ↔ Затишье» со случайной длительностью фаз; на стыке фаз параметры блендятся
    ///   <c>weather.phaseBlendSeconds</c>. Первая фаза — шторм (<c>weather.stormCycleStartsWithStorm</c>).
    /// • Акт 3 (<c>blendToSummit</c>): погода стихает calm → summit по прогрессу пути <see cref="SetProgress01"/>.
    /// • Порывы (<c>act.gusts</c>): раз в <c>gusts.interval</c> с, длительность <c>gusts.duration</c>; огибающая нарастание/спад.
    ///   Толкают только на время порыва (<see cref="WindPusher"/>); постоянный ветер никого не сносит.
    /// • Каждый кадр пишет в <see cref="WarmthSystem"/>: время замерзания погоды/лампы и множитель потери тепла на порыве.
    /// Визуал (частицы, туман, иней, свет) и звук ветра читают значения отсюда (этапы 9–10).
    /// </summary>
    public sealed class WeatherSystem
    {
        private readonly GameConfig _config;
        private readonly GameBalance _balance;
        private GameBalance.WeatherSettings _fallbackWeather;
        private GameBalance.GustSettings _fallbackGusts;
        private System.Random _rng = new System.Random();

        private ActSettings _act;
        private readonly WeatherPreset _from = new WeatherPreset();
        private readonly WeatherPreset _target = new WeatherPreset();
        private float _transT = 1f;
        private float _phaseMix; // 0 — затишье, 1 — шторм (сглаженно)
        private float _gustElapsed, _gustDuration, _nextGustIn;

        public WeatherSystem(GameConfig config, GameBalance balance)
        {
            _config = config;
            _balance = balance;
            SetAct(ActId.Act1, true);
        }

        private GameBalance.WeatherSettings Cfg => _balance != null ? _balance.weather : (_fallbackWeather ??= new GameBalance.WeatherSettings());
        private GameBalance.GustSettings GustCfg => _balance != null ? _balance.gusts : (_fallbackGusts ??= new GameBalance.GustSettings());

        // ------------------------------------------------------------------ состояние

        public ActId Act { get; private set; } = ActId.Act1;

        /// <summary>Текущие (интерполированные) параметры погоды.</summary>
        public WeatherPreset Current { get; } = new WeatherPreset();

        /// <summary>Фаза акта 2; вне акта 2 — <see cref="WeatherPhase.None"/>.</summary>
        public WeatherPhase Phase { get; private set; } = WeatherPhase.None;

        /// <summary>Сколько осталось до смены фазы, с.</summary>
        public float PhaseTimeLeft { get; private set; }

        /// <summary>Прогресс пути акта 3, 0..1 (calm → summit).</summary>
        public float Progress01 { get; private set; }

        /// <summary>Сглаженная «штормистость» 0 (затишье) … 1 (шторм) — для индикатора ветра, звука, частиц.</summary>
        public float StormMix => _phaseMix;

        public bool GustActive { get; private set; }

        /// <summary>Огибающая порыва 0..1 (0 — порыва нет).</summary>
        public float GustStrength01 { get; private set; }

        /// <summary>Горизонтальное направление ветра (единичный вектор; 0° = +Z мира).</summary>
        public Vector3 WindDirection => Dir(Current.windDirectionDeg);

        /// <summary>Направление текущего порыва (с разбросом).</summary>
        public Vector3 GustDirection { get; private set; } = Vector3.forward;

        /// <summary>Множитель силы порыва от текущего ветра (0.4..1.5): в затишье порывы слабее.</summary>
        public float GustPower => Mathf.Clamp(Current.windStrength / Mathf.Max(1f, GustCfg.referenceWind), 0.4f, 1.5f);

        /// <summary>Сколько осталось до следующего порыва, с (если порывы включены).</summary>
        public float NextGustIn => _nextGustIn;

        /// <summary>Показывать флажок ветра в HUD (акт 2, ТЗ 6.1).</summary>
        public bool WindIndicatorVisible => _act != null && _act.stormCycle;

        /// <summary>Положение флажка 0 (обвис — затишье) … 1 (рвётся — шторм); порыв подбрасывает флажок.</summary>
        public float WindIndicator01 => Mathf.Clamp01(Mathf.Max(_phaseMix, GustStrength01 * 0.7f));

        public ActSettings ActSettings => _act;

        // ------------------------------------------------------------------ управление

        /// <summary>Детерминированный генератор (для тестов).</summary>
        public void Reseed(int seed) => _rng = new System.Random(seed);

        /// <summary>
        /// Перейти к погоде акта: плавно за <c>transitionSeconds</c> (или мгновенно — старт игры, чекпоинт).
        /// Публикует <see cref="ActChanged"/> (если акт сменился) и <see cref="WeatherPhaseChanged"/> (если сменилась фаза).
        /// </summary>
        public void SetAct(ActId id, bool instant = false)
        {
            var next = _config != null ? _config.GetAct(id) : null;
            var prevAct = Act;
            var prevPhase = Phase;
            bool actChanged = _act != null && prevAct != id;

            if (GustActive) EndGust();

            Copy(Current, _from);
            _act = next;
            Act = id;
            Progress01 = 0f;

            if (_act != null && _act.stormCycle)
            {
                Phase = Cfg.stormCycleStartsWithStorm ? WeatherPhase.Storm : WeatherPhase.Calm;
                _phaseMix = Phase == WeatherPhase.Storm ? 1f : 0f;
                PhaseTimeLeft = PhaseDuration(Phase);
            }
            else
            {
                Phase = WeatherPhase.None;
                _phaseMix = 0f;
                PhaseTimeLeft = 0f;
            }

            _nextGustIn = _act != null && _act.gusts ? Range(GustCfg.interval) : 0f;

            ComputeTarget();
            if (instant || _act == null)
            {
                _transT = 1f;
                Copy(_target, Current);
            }
            else
            {
                _transT = 0f;
            }

            if (actChanged) EventBus<ActChanged>.Publish(new ActChanged { Previous = prevAct, Current = id });
            if (Phase != prevPhase) EventBus<WeatherPhaseChanged>.Publish(new WeatherPhaseChanged { Previous = prevPhase, Current = Phase });
        }

        /// <summary>Прогресс пути для акта 3 (0 — у подножия, 1 — Вершина).</summary>
        public void SetProgress01(float p) => Progress01 = Mathf.Clamp01(p);

        // ------------------------------------------------------------------ шаг

        /// <summary>Шаг симуляции. Вызывает GameManager каждый кадр в состоянии Playing.</summary>
        public void Tick(float dt, WarmthSystem warmth)
        {
            if (dt <= 0f || _act == null) return;

            // Цикл шторм ↔ затишье.
            if (_act.stormCycle)
            {
                PhaseTimeLeft -= dt;
                if (PhaseTimeLeft <= 0f)
                {
                    var prev = Phase;
                    Phase = Phase == WeatherPhase.Storm ? WeatherPhase.Calm : WeatherPhase.Storm;
                    PhaseTimeLeft = PhaseDuration(Phase);
                    EventBus<WeatherPhaseChanged>.Publish(new WeatherPhaseChanged { Previous = prev, Current = Phase });
                }
                _phaseMix = Mathf.MoveTowards(_phaseMix, Phase == WeatherPhase.Storm ? 1f : 0f, dt / Mathf.Max(0.1f, Cfg.phaseBlendSeconds));
            }

            // Переход между актами и обновление текущих параметров.
            if (_transT < 1f) _transT = Mathf.Min(1f, _transT + dt / Mathf.Max(0.01f, _act.transitionSeconds));
            ComputeTarget();
            float k = _transT * _transT * (3f - 2f * _transT);
            Lerp(_from, _target, k, Current);

            TickGusts(dt);

            // Связь с теплом.
            if (warmth != null)
            {
                warmth.OutsideFreezeSeconds = Current.freezeSeconds;
                warmth.LampOutFreezeSeconds = _act.freezeSecondsLampOut;
                warmth.ExposureMultiplier = GustActive ? GustCfg.warmthLossMultiplier : 1f;
            }
        }

        private void TickGusts(float dt)
        {
            if (!_act.gusts) { if (GustActive) EndGust(); return; }
            var g = GustCfg;

            if (GustActive)
            {
                _gustElapsed += dt;
                float attack = Mathf.Max(0.05f, g.attackSeconds), release = Mathf.Max(0.05f, g.releaseSeconds);
                float env = Mathf.Min(_gustElapsed / attack, (_gustDuration - _gustElapsed) / release);
                GustStrength01 = Smooth(Mathf.Clamp01(env));
                if (_gustElapsed >= _gustDuration) EndGust();
            }
            else
            {
                _nextGustIn -= dt;
                if (_nextGustIn <= 0f) StartGust();
            }
        }

        private void StartGust()
        {
            var g = GustCfg;
            GustActive = true;
            _gustElapsed = 0f;
            _gustDuration = Range(g.duration);
            float jitter = ((float)_rng.NextDouble() * 2f - 1f) * g.directionJitterDeg;
            GustDirection = Dir(Current.windDirectionDeg + jitter);
            GustStrength01 = 0f;
            EventBus<WindGustStarted>.Publish(new WindGustStarted { Force = GustDirection * (Current.windStrength * GustPower), Duration = _gustDuration });
        }

        private void EndGust()
        {
            GustActive = false;
            GustStrength01 = 0f;
            _nextGustIn = _act != null && _act.gusts ? Range(GustCfg.interval) : 0f;
            EventBus<WindGustEnded>.Publish(new WindGustEnded());
        }

        // ------------------------------------------------------------------ помощники

        private void ComputeTarget()
        {
            if (_act == null) return;
            if (_act.stormCycle) Lerp(_act.calm, _act.storm, _phaseMix, _target);
            else if (_act.blendToSummit) Lerp(_act.calm, _act.summit, Progress01, _target);
            else Copy(_act.calm, _target);
        }

        private float PhaseDuration(WeatherPhase p) => Range(p == WeatherPhase.Storm ? _act.stormSeconds : _act.calmSeconds);

        private float Range(Vector2 r) => r.x + (float)_rng.NextDouble() * Mathf.Max(0f, r.y - r.x);

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static Vector3 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        private static void Copy(WeatherPreset s, WeatherPreset d) => Lerp(s, s, 0f, d);

        private static void Lerp(WeatherPreset a, WeatherPreset b, float t, WeatherPreset d)
        {
            d.snowDensity = Mathf.Lerp(a.snowDensity, b.snowDensity, t);
            d.snowSpeed = Mathf.Lerp(a.snowSpeed, b.snowSpeed, t);
            d.windStrength = Mathf.Lerp(a.windStrength, b.windStrength, t);
            d.windDirectionDeg = Mathf.LerpAngle(a.windDirectionDeg, b.windDirectionDeg, t);
            d.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);
            d.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            d.frost = Mathf.Lerp(a.frost, b.frost, t);
            d.freezeSeconds = Mathf.Lerp(a.freezeSeconds, b.freezeSeconds, t);
        }

        public override string ToString()
        {
            string phase = Phase == WeatherPhase.None ? "" : $", {(Phase == WeatherPhase.Storm ? "ШТОРМ" : "затишье")} ещё {PhaseTimeLeft:0} с (микс {_phaseMix:0.00})";
            string gust = GustActive ? $", ПОРЫВ {GustStrength01:0.00}" : (_act != null && _act.gusts ? $", порыв через {_nextGustIn:0} с" : "");
            return $"{Act}{phase}: ветер {Current.windStrength:0.0} м/с, снег {Current.snowDensity:0.00}, туман {Current.fogDensity:0.000}, мороз {Current.freezeSeconds:0} с{gust}";
        }
    }
}
