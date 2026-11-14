using LastRide.Core;
using LastRide.Fire;
using UnityEngine;

namespace LastRide.Player
{
    /// <summary>
    /// Тепло робота (ТЗ 4.3): шкала 0..<c>warmth.max</c>, одна на игру (<c>GameManager.Instance.Warmth</c>), тикается из GameManager
    /// только в состоянии Playing.
    /// • В фургоне при горящей печке — растёт до максимума за <c>vanRecoverSeconds</c> (в фургоне без огня или при тлении — не меняется).
    /// • Снаружи — убывает: <c>max / freezeSeconds × ExposureMultiplier</c> в секунду. <see cref="OutsideFreezeSeconds"/> задаёт погода/акт
    ///   (акт 1 — 90 с, акт 2: затишье 40 / шторм 12, акт 3 с лампой 120), <see cref="ExposureMultiplier"/> — порывы ветра (×2).
    ///   С лампой в акте 3 при погасшей лампе — <see cref="LampOutFreezeSeconds"/> (20 с).
    /// • Тепло 0 → робот «застывает»: <see cref="FreezeProgress"/> 0..1 за <c>freezeToStopSeconds</c> (RobotController тормозит по нему, иней/звук — этапы 9–10);
    ///   на 1 публикуется <see cref="PlayerDied"/> (Frozen) — смерть и рестарт обрабатывает 11.2. Согрелись раньше — прогресс откатывается.
    /// События: <see cref="WarmthCritical"/> (порог <c>criticalFraction</c>), <see cref="PlayerFreezing"/> (тепло дошло до 0).
    /// </summary>
    public sealed class WarmthSystem
    {
        private readonly GameBalance _balance;
        private GameBalance.WarmthSettings _fallback;

        public WarmthSystem(GameBalance balance)
        {
            _balance = balance;
            Reset();
        }

        private GameBalance.WarmthSettings Settings => _balance != null ? _balance.warmth : (_fallback ??= new GameBalance.WarmthSettings());

        /// <summary>Текущее тепло, 0..max.</summary>
        public float Value { get; private set; }

        /// <summary>Тепло 0..1.</summary>
        public float Fraction => Settings.max > 0f ? Mathf.Clamp01(Value / Settings.max) : 0f;

        /// <summary>Темп тиканья заводного механизма, тиков/с: от <c>tickRateCold</c> (тепло 0) до <c>tickRateWarm</c> (полное).</summary>
        public float TickRate => Mathf.Lerp(Settings.tickRateCold, Settings.tickRateWarm, Fraction);

        /// <summary>Множитель тона тиканья: <c>tickPitchCold</c> при тепле 0 … 1 при полном (звук 10.3).</summary>
        public float TickPitch => Mathf.Lerp(Settings.tickPitchCold, 1f, Fraction);

        /// <summary>Скрип механизма 0..1: 0 при тепле ≥ <c>jamThreshold</c>, 1 при тепле 0 (звук 10.3 — «чем холоднее, тем громче скрип»).</summary>
        public float Creak => Settings.jamThreshold > 0f ? Mathf.Clamp01(1f - Value / Settings.jamThreshold) : 0f;

        /// <summary>Тепло ниже критического порога (HUD мигает).</summary>
        public bool IsCritical => Fraction < Settings.criticalFraction;

        /// <summary>Прогресс замерзания 0 (в движении) … 1 (стоит, иней на весь экран).</summary>
        public float FreezeProgress { get; private set; }

        /// <summary>Замёрз окончательно: <see cref="PlayerDied"/> уже опубликовано (до <see cref="Reset"/>/<see cref="Restore"/>).</summary>
        public bool IsFrozenDead { get; private set; }

        /// <summary>Бессмертие: тепло не меняется, замерзания нет (отладка F4, автотесты).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Время от max до 0 снаружи, с. Пишет погода/акт (по умолчанию — <c>warmth.defaultFreezeSeconds</c>, акт 1).</summary>
        public float OutsideFreezeSeconds { get; set; } = -1f;

        /// <summary>Время замерзания с лампой в акте 3, когда лампа погасла, с (по умолчанию — <c>warmth.lampOutFreezeSeconds</c>).</summary>
        public float LampOutFreezeSeconds { get; set; } = -1f;

        /// <summary>Множитель скорости потери тепла (порыв ветра в акте 3 — ×2).</summary>
        public float ExposureMultiplier { get; set; } = 1f;

        /// <summary>Скорость изменения тепла на прошлом шаге, ед./с (для отладки).</summary>
        public float LastRate { get; private set; }

        private float EffectiveOutsideSeconds => OutsideFreezeSeconds > 0f ? OutsideFreezeSeconds : Settings.defaultFreezeSeconds;
        private float EffectiveLampOutSeconds => LampOutFreezeSeconds > 0f ? LampOutFreezeSeconds : Settings.lampOutFreezeSeconds;

        private bool _wasCritical, _wasZero;

        /// <summary>Полное тепло, сбросить замерзание (новая игра).</summary>
        public void Reset()
        {
            Value = Settings.max;
            FreezeProgress = 0f;
            IsFrozenDead = false;
            _wasCritical = false;
            _wasZero = false;
            ExposureMultiplier = 1f;
        }

        /// <summary>Загрузка чекпоинта (ТЗ 4.7): тепло, замерзание сброшено.</summary>
        public void Restore(float value)
        {
            Reset();
            Value = Mathf.Clamp(value, 0f, Settings.max);
            _wasCritical = IsCritical;
            _wasZero = Value <= 0f;
        }

        /// <summary>Выставить значение напрямую (отладка, тесты), без сброса замерзания.</summary>
        public void SetValue(float value)
        {
            Value = Mathf.Clamp(value, 0f, Settings.max);
            PublishEdges();
        }

        /// <summary>Шаг симуляции. Вызывает GameManager каждый кадр в состоянии Playing.</summary>
        public void Tick(float dt, PlayerMode mode, SparkFire spark)
        {
            if (dt <= 0f) return;
            var s = Settings;

            if (Invulnerable)
            {
                LastRate = 0f;
                FreezeProgress = Mathf.MoveTowards(FreezeProgress, 0f, dt / Mathf.Max(0.1f, s.freezeToStopSeconds));
                return;
            }

            float rate = 0f; // ед./с
            if (mode == PlayerMode.InVan)
            {
                if (spark != null && spark.State == SparkState.Burning && spark.Source == FireSourceKind.Stove)
                    rate = s.max / Mathf.Max(0.1f, s.vanRecoverSeconds);
            }
            else
            {
                bool lampBurning = spark != null && spark.State == SparkState.Burning && spark.Source == FireSourceKind.Lamp;
                float seconds = mode == PlayerMode.OnFootWithLamp && !lampBurning ? EffectiveLampOutSeconds : EffectiveOutsideSeconds;
                rate = -s.max / Mathf.Max(0.1f, seconds) * Mathf.Max(0f, ExposureMultiplier);
            }

            LastRate = rate;
            Value = Mathf.Clamp(Value + rate * dt, 0f, s.max);
            PublishEdges();

            // Замерзание: при 0 тепла нарастает, при >0 откатывается (если ещё не умерли).
            if (!IsFrozenDead)
            {
                float step = dt / Mathf.Max(0.1f, s.freezeToStopSeconds);
                FreezeProgress = Mathf.Clamp01(FreezeProgress + (Value <= 0f ? step : -step));
                if (FreezeProgress >= 1f)
                {
                    IsFrozenDead = true;
                    EventBus<PlayerDied>.Publish(new PlayerDied { Cause = DeathCause.Frozen });
                }
            }
        }

        private void PublishEdges()
        {
            bool critical = IsCritical;
            if (critical != _wasCritical)
            {
                _wasCritical = critical;
                EventBus<WarmthCritical>.Publish(new WarmthCritical { Entered = critical });
            }
            bool zero = Value <= 0f;
            if (zero && !_wasZero) EventBus<PlayerFreezing>.Publish(new PlayerFreezing());
            _wasZero = zero;
        }

        public override string ToString()
        {
            string where = LastRate > 0f ? "греется" : LastRate < 0f ? "остывает" : "ровно";
            string extra = IsFrozenDead ? ", ЗАМЁРЗ" : FreezeProgress > 0f ? $", застывает {FreezeProgress:P0}" : "";
            return $"{Value:0.0}/{Settings.max:0} ({where} {Mathf.Abs(LastRate):0.00}/с){(Invulnerable ? ", бессмертие" : "")}{extra}";
        }
    }
}
