using LastRide.Core;
using UnityEngine;

namespace LastRide.Fire
{
    /// <summary>Состояние Искры.</summary>
    public enum SparkState
    {
        /// <summary>Искры ещё нет в печке (пролог: банка с углём не поставлена).</summary>
        Unlit,
        /// <summary>Горит: запас горения &gt; 0.</summary>
        Burning,
        /// <summary>Запас кончился — тлеющий уголь, <c>fire.embersSeconds</c> до угасания.</summary>
        Embers,
        /// <summary>Погасла окончательно (смерть, ТЗ 4.7).</summary>
        Extinguished
    }

    /// <summary>
    /// Логика огня (ТЗ 4.2): запас горения в секундах у печки или лампы, подкидывание топлива, тление, угасание.
    /// Одна Искра на игру (<c>GameManager.Instance.Spark</c>), тикается из GameManager только в состоянии Playing
    /// и горит независимо от того, где робот (в фургоне, снаружи). Переходы идут событиями
    /// (<see cref="FireEmbersStarted"/>, <see cref="FireRekindled"/>, <see cref="FireExtinguished"/>, <see cref="SparkMoved"/>);
    /// запас читают напрямую: <see cref="Reserve"/>, <see cref="Fraction"/>.
    /// </summary>
    public sealed class SparkFire
    {
        private readonly GameBalance _balance;
        private GameBalance.FireSettings _fallback;

        public SparkState State { get; private set; } = SparkState.Unlit;
        public FireSourceKind Source { get; private set; } = FireSourceKind.Stove;

        /// <summary>Запас горения, с.</summary>
        public float Reserve { get; private set; }

        /// <summary>Сколько осталось тлеть, с (только в Embers).</summary>
        public float EmbersLeft { get; private set; }

        /// <summary>Бессмертие: запас и тление не убывают (отладка F4, 11.6).</summary>
        public bool Invulnerable { get; set; }

        public SparkFire(GameBalance balance) { _balance = balance; }

        private GameBalance.FireSettings Settings => _balance != null ? _balance.fire : (_fallback ??= new GameBalance.FireSettings());

        public bool IsLit => State == SparkState.Burning || State == SparkState.Embers;

        /// <summary>Максимум запаса у текущего источника.</summary>
        public float MaxReserve => Source == FireSourceKind.Stove ? Settings.stoveMaxBurnSeconds : Settings.lampMaxBurnSeconds;

        /// <summary>Запас 0..1 от максимума источника — по нему считаются яркость, свет, треск (5.3, 10.2).</summary>
        public float Fraction => MaxReserve > 0f ? Mathf.Clamp01(Reserve / MaxReserve) : 0f;

        /// <summary>Доля оставшегося тления 0..1 (1 — только что начало тлеть).</summary>
        public float EmbersFraction => Settings.embersSeconds > 0f ? Mathf.Clamp01(EmbersLeft / Settings.embersSeconds) : 0f;

        /// <summary>Поставить Искру в источник и зажечь с запасом <paramref name="reserveSeconds"/> (пролог, загрузка чекпоинта).</summary>
        public void PlaceSpark(FireSourceKind source, float reserveSeconds)
        {
            Source = source;
            Reserve = Mathf.Clamp(reserveSeconds, 0f, MaxReserve);
            if (Reserve > 0f) { State = SparkState.Burning; EmbersLeft = 0f; }
            else { State = SparkState.Embers; EmbersLeft = Settings.embersSeconds; }
        }

        /// <summary>
        /// Загрузка чекпоинта (ТЗ 4.7): точное состояние, а запас не ниже <c>fire.restoreMinBurnSeconds</c> (защита от тупика).
        /// </summary>
        public void Restore(FireSourceKind source, float reserveSeconds, bool applyDeadlockGuard = true)
        {
            float r = applyDeadlockGuard ? Mathf.Max(reserveSeconds, Settings.restoreMinBurnSeconds) : reserveSeconds;
            PlaceSpark(source, r);
        }

        /// <summary>Подкинуть топливо: берёт предмет из инвентаря и добавляет его время горения (лишнее сгорает).</summary>
        public bool ThrowFuel(FuelType fuel)
        {
            var gm = GameManager.Instance;
            if (fuel == null || gm == null || !IsLit || gm.Inventory.Count(fuel) <= 0) return false;
            gm.Inventory.TryTake(fuel);
            float added = AddBurnSeconds(fuel.burnSeconds);
            EventBus<FuelThrown>.Publish(new FuelThrown { Fuel = fuel, Source = Source, AddedSeconds = added });
            return true;
        }

        /// <summary>Добавить время горения. Возвращает, сколько секунд реально добавилось (остальное сгорело). Работает для горящей и тлеющей Искры.</summary>
        public float AddBurnSeconds(float seconds)
        {
            if (!IsLit || seconds <= 0f) return 0f;
            float before = Reserve;
            Reserve = Mathf.Min(Reserve + seconds, MaxReserve);
            if (State == SparkState.Embers)
            {
                State = SparkState.Burning;
                EmbersLeft = 0f;
                EventBus<FireRekindled>.Publish(new FireRekindled { Source = Source });
            }
            return Reserve - before;
        }

        /// <summary>Перенести Искру из печки в лампу (акт 3): запас переходит как есть, но не выше максимума лампы.</summary>
        public bool MoveToLamp()
        {
            if (!IsLit || Source == FireSourceKind.Lamp) return false;
            var from = Source;
            Source = FireSourceKind.Lamp;
            Reserve = Mathf.Min(Reserve, MaxReserve);
            EventBus<SparkMoved>.Publish(new SparkMoved { From = from, To = Source });
            return true;
        }

        /// <summary>Сбросить в начальное состояние (новая игра).</summary>
        public void Reset()
        {
            State = SparkState.Unlit;
            Source = FireSourceKind.Stove;
            Reserve = 0f;
            EmbersLeft = 0f;
        }

        /// <summary>Шаг симуляции. Вызывает GameManager каждый кадр в состоянии Playing.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || Invulnerable) return;
            switch (State)
            {
                case SparkState.Burning:
                    Reserve -= dt;
                    if (Reserve <= 0f)
                    {
                        Reserve = 0f;
                        State = SparkState.Embers;
                        EmbersLeft = Settings.embersSeconds;
                        EventBus<FireEmbersStarted>.Publish(new FireEmbersStarted { Source = Source });
                    }
                    break;

                case SparkState.Embers:
                    EmbersLeft -= dt;
                    if (EmbersLeft <= 0f)
                    {
                        EmbersLeft = 0f;
                        State = SparkState.Extinguished;
                        EventBus<FireExtinguished>.Publish(new FireExtinguished { Source = Source });
                    }
                    break;
            }
        }

        public override string ToString() =>
            State switch
            {
                SparkState.Unlit => "Искры нет",
                SparkState.Burning => $"{Source}: горит, запас {Reserve:0.0}/{MaxReserve:0} с",
                SparkState.Embers => $"{Source}: ТЛЕЕТ, осталось {EmbersLeft:0.0} с",
                _ => "ПОГАСЛА"
            };
    }
}
