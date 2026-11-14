using System.Collections.Generic;
using LastRide.Core;
using LastRide.Fire;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>Этапы пролога (ТЗ 3, акт 1): найти банку → отнести к фургону → поставить в печку.</summary>
    public enum PrologueState
    {
        FindJar,
        JarTaken,
        SparkPlaced
    }

    /// <summary>
    /// Пролог и контекстное обучение акта 1 (ТЗ 3): <c>GameManager.Instance.Tutorial</c>. Без отдельного туториала —
    /// короткие подсказки по ходу дела (<see cref="TutorialHintRequested"/> → <c>TutorialHintView</c>):
    /// 1) управление и поиск банки с Искрой; 2) банку берут (<c>SparkJarPickup</c>) и ставят в печку (<c>StoveSocket</c>) →
    /// запись A1_01; 3) сесть в фургон, ехать; 4) открыть печку (Tab) и подкинуть топливо → запись A1_02;
    /// 5) остановка у сарая и типы топлива — подсказки приходят от триггеров уровня (<see cref="ShowTriggerHint"/>).
    /// Каждая подсказка показывается один раз за прохождение (<see cref="Shown"/> — набор для чекпоинтов 11.x).
    /// </summary>
    public sealed class TutorialController
    {
        private readonly GameManager _gm;
        private readonly HashSet<string> _shown = new HashSet<string>();
        private GameBalance.TutorialSettings _fallback;

        private bool _begun;
        private float _clock;
        private float _controlsAt = -1f, _findJarAt = -1f, _sitAt = -1f, _drivingAt = -1f;
        private bool _stoveOpened, _firstThrowDone;
        private float _lampHintAt = -1f;

        public TutorialController(GameManager gm)
        {
            _gm = gm;
            EventBus<PlayerModeChanged>.Subscribe(OnModeChanged);
            EventBus<StoveFlapChanged>.Subscribe(OnStoveFlap);
            EventBus<FuelThrown>.Subscribe(OnFuelThrown);
        }

        private GameBalance.TutorialSettings Cfg => _gm.Balance != null ? _gm.Balance.tutorial : (_fallback ??= new GameBalance.TutorialSettings());

        public PrologueState State { get; private set; } = PrologueState.FindJar;
        public IReadOnlyCollection<string> Shown => _shown;

        public void MarkShown(IEnumerable<string> keys)
        {
            if (keys != null) foreach (var k in keys) _shown.Add(k);
        }

        /// <summary>Начало новой игры в сцене Game: пролог с нуля (Искры в печке ещё нет).</summary>
        public void Begin()
        {
            _shown.Clear();
            _stoveOpened = _firstThrowDone = false;
            _clock = 0f;
            State = PrologueState.FindJar;
            _gm.Spark.Reset();
            _begun = true;
            _controlsAt = Cfg.firstHintDelaySeconds;
            _findJarAt = _controlsAt + Cfg.hintSeconds + Cfg.findJarHintDelaySeconds;
            _sitAt = _drivingAt = -1f;
        }

        /// <summary>Продолжение с чекпоинта: пролог пройден, подсказки по таймеру не нужны.</summary>
        public void SkipPrologue()
        {
            State = PrologueState.SparkPlaced;
            _begun = true;
            _controlsAt = _findJarAt = _sitAt = _drivingAt = -1f;
        }

        /// <summary>Загрузка чекпоинта (ТЗ 4.7): состояние пролога, показанные подсказки; таймеры подсказок заново.</summary>
        public void Restore(PrologueState state, IEnumerable<string> shown, bool firstThrowDone)
        {
            _shown.Clear();
            MarkShown(shown);
            _stoveOpened = false;
            _firstThrowDone = firstThrowDone;
            _lampHintAt = -1f;
            _clock = 0f;
            State = state;
            _begun = true;
            _controlsAt = _findJarAt = _sitAt = _drivingAt = -1f;
            if (state == PrologueState.FindJar)
            {
                _controlsAt = Cfg.firstHintDelaySeconds;
                _findJarAt = _controlsAt + Cfg.hintSeconds + Cfg.findJarHintDelaySeconds;
            }
            Hide();
        }

        // ------------------------------------------------------------------ пролог

        public void TakeJar()
        {
            if (State != PrologueState.FindJar) return;
            State = PrologueState.JarTaken;
            _controlsAt = _findJarAt = -1f;
            Hint("jar_taken", "Отнесите Искру к фургону и поставьте её в печку");
        }

        /// <summary>Искра поставлена в печку: зажечь, выдать щепки, запись A1_01.</summary>
        public void PlaceSparkInStove()
        {
            if (State != PrologueState.JarTaken) return;
            State = PrologueState.SparkPlaced;
            float burn = _gm.Balance != null ? _gm.Balance.fire.startBurnSeconds : 120f;
            _gm.Spark.PlaceSpark(FireSourceKind.Stove, burn);

            var kindling = _gm.Config != null ? _gm.Config.GetFuel("kindling") : null;
            if (kindling != null && Cfg.startKindling > 0) _gm.Inventory.Add(kindling, Cfg.startKindling);

            Hide();
            _gm.AudioLogs.Play("A1_01");
            _sitAt = _clock + Cfg.sitHintDelaySeconds;
        }

        // ------------------------------------------------------------------ подсказки

        /// <summary>Показать подсказку один раз за прохождение. Пустой текст — спрятать текущую.</summary>
        public void Hint(string key, string text, float duration = 0f)
        {
            if (!string.IsNullOrEmpty(key) && !_shown.Add(key)) return;
            EventBus<TutorialHintRequested>.Publish(new TutorialHintRequested
            {
                Text = text,
                Duration = duration > 0f ? duration : Cfg.hintSeconds
            });
        }

        public void Hide() => EventBus<TutorialHintRequested>.Publish(new TutorialHintRequested { Text = string.Empty, Duration = 0f });

        /// <summary>Подсказка от триггера уровня (<c>LevelTrigger</c> вида Tutorial). Лишние (уже выполненное игроком) отбрасываются.</summary>
        public void ShowTriggerHint(string id, string text, float duration)
        {
            if (id == "TUT_Stove" && _stoveOpened) return;
            Hint(id, text, duration);
        }

        public void Tick(float dt)
        {
            if (!_begun) return;
            _clock += dt;

            if (_controlsAt >= 0f && _clock >= _controlsAt)
            {
                _controlsAt = -1f;
                if (State == PrologueState.FindJar) Hint("controls", "WASD — идти, мышь — осмотреться");
            }
            if (_findJarAt >= 0f && _clock >= _findJarAt)
            {
                _findJarAt = -1f;
                if (State == PrologueState.FindJar) Hint("find_jar", "Найдите банку с Искрой — она тлеет у мастерской");
            }
            if (_sitAt >= 0f && _clock >= _sitAt)
            {
                _sitAt = -1f;
                if (_gm.PlayerMode != PlayerMode.InVan) Hint("sit", "Подойдите к двери фургона и нажмите [E], чтобы сесть");
            }
            if (_lampHintAt >= 0f && _clock >= _lampHintAt)
            {
                _lampHintAt = -1f;
                var lh = _gm.Balance != null ? _gm.Balance.lampHand : new GameBalance.LampHandSettings();
                if (_gm.PlayerMode == PlayerMode.OnFootWithLamp) Hint("lamp_r", lh.rHintText, Cfg.hintSeconds + 2f);
            }
            if (_drivingAt >= 0f && _clock >= _drivingAt)
            {
                _drivingAt = -1f;
                Hint("driving", "W / S — газ и тормоз, A / D — руль, Space — ручной тормоз, Tab — печка", Cfg.hintSeconds + 2f);
            }
        }

        public override string ToString() => $"пролог: {State}, подсказок показано {_shown.Count}";

        // ------------------------------------------------------------------ события

        private void OnModeChanged(PlayerModeChanged e)
        {
            if (e.Current != PlayerMode.InVan || State != PrologueState.SparkPlaced) return;
            _sitAt = -1f;
            if (!_shown.Contains("driving")) _drivingAt = _clock + Cfg.drivingHintDelaySeconds;
        }

        private void OnStoveFlap(StoveFlapChanged e)
        {
            if (!e.Open) return;
            _stoveOpened = true;
            if (!_firstThrowDone) Hint("stove_keys", "1 · 2 · 3 — подкинуть щепки, доски или брикеты", Cfg.hintSeconds + 2f);
        }

        /// <summary>Лампа взята (акт 3): через <c>lampHand.rHintDelaySeconds</c> напомнить про R, если топливо ещё не подкидывали.</summary>
        public void LampTaken()
        {
            var lh = _gm.Balance != null ? _gm.Balance.lampHand : new GameBalance.LampHandSettings();
            _lampHintAt = _clock + lh.rHintDelaySeconds;
        }

        /// <summary>Панель лампы открыта (R): подсказка про клавиши 1–3.</summary>
        public void LampPanelOpened()
        {
            _lampHintAt = -1f;
            Hint("lamp_keys", "1 · 2 · 3 — подкинуть щепки, доски или брикеты в лампу", Cfg.hintSeconds + 2f);
        }

        private void OnFuelThrown(FuelThrown e)
        {
            if (_gm.Spark.Source == FireSourceKind.Lamp)
            {
                _lampHintAt = -1f;
                _shown.Add("lamp_keys");
            }
            if (_firstThrowDone) return;
            _firstThrowDone = true;
            _gm.AudioLogs.Play("A1_02");
            _shown.Add("stove_keys");
            Hide();
        }
    }
}
