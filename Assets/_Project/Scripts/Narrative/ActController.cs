using LastRide.Core;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Контроллер актов (ТЗ 3): <c>GameManager.Instance.Acts</c>. Единственная точка, через которую акт начинается —
    /// триггер уровня (<c>ActStart</c>), чекпоинт (11.x) или отладка зовут <see cref="StartAct"/>.
    /// • Переключает погоду акта (<c>Weather.SetAct</c> — плавно за <c>transitionSeconds</c> или мгновенно); событие
    ///   <see cref="ActChanged"/> публикует погода при реальной смене акта.
    /// • Считает время игры в каждом акте (только в Playing, по игровому времени) — для настройки пейсинга 5/9/6 минут
    ///   (этап 13), строка «Акт» в F1-оверлее. Время сохраняется чекпоинтом: <see cref="GetSeconds"/> / <see cref="Restore"/>.
    /// • Не откатывается назад: <see cref="StartAct"/> ранее достигнутого акта без <c>force</c> игнорируется
    ///   (повторное срабатывание триггера, выезд фургона обратно через зону).
    /// </summary>
    public sealed class ActController
    {
        private readonly float[] _seconds = new float[3];
        private readonly GameManager _gm;

        public ActController(GameManager gm)
        {
            _gm = gm;
            Current = gm.Weather != null ? gm.Weather.Act : ActId.Act1;
            // Если акт сменили в обход контроллера (отладка, тесты) — не расходимся с погодой.
            EventBus<ActChanged>.Subscribe(e => Current = e.Current);
        }

        /// <summary>Текущий акт игры (по сработавшим триггерам; от акта по положению на оси может отставать).</summary>
        public ActId Current { get; private set; }

        /// <summary>Сколько секунд игры прошло в текущем акте.</summary>
        public float CurrentSeconds => _seconds[Index(Current)];

        private static int Index(ActId act) => Mathf.Clamp((int)act - 1, 0, 2);

        /// <summary>Сколько секунд игры прошло всего.</summary>
        public float TotalSeconds => _seconds[0] + _seconds[1] + _seconds[2];

        public float GetSeconds(ActId act) => _seconds[Index(act)];

        /// <summary>Начать акт. False — акт не ушёл вперёд и вызов проигнорирован.</summary>
        public bool StartAct(ActId act, bool instant = false, bool force = false)
        {
            if (!force && (int)act <= (int)Current) return false;
            Current = act;
            if (_gm.Weather != null) _gm.Weather.SetAct(act, instant);
            return true;
        }

        /// <summary>Восстановить состояние с чекпоинта / новой игры: акт мгновенно и накопленное время по актам.</summary>
        public void Restore(ActId act, float act1Seconds = 0f, float act2Seconds = 0f, float act3Seconds = 0f)
        {
            _seconds[0] = act1Seconds;
            _seconds[1] = act2Seconds;
            _seconds[2] = act3Seconds;
            StartAct(act, true, true);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _seconds[Index(Current)] += dt;
        }

        public override string ToString()
        {
            return $"{Current} {Format(CurrentSeconds)} (всего {Format(TotalSeconds)}; А1 {Format(_seconds[0])} А2 {Format(_seconds[1])} А3 {Format(_seconds[2])})";
        }

        private static string Format(float s) => $"{(int)(s / 60f)}:{(int)(s % 60f):00}";
    }
}
