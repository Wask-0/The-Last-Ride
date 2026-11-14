using System.Collections.Generic;
using LastRide.Core;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Очередь аудиозаписей (ТЗ 7.1): <c>GameManager.Instance.AudioLogs</c>, тикается из GameManager только в Playing.
    /// • <see cref="Play"/> ставит запись в очередь; новая запись не перебивает текущую. Прослушанные не играют повторно.
    /// • Записи не мешают управлению — это только события: <see cref="AudioLogStarted"/>, <see cref="AudioLogLineChanged"/>,
    ///   <see cref="AudioLogFinished"/>. Субтитры (8.2), голос/помехи/ducking (этап 10) подписываются на них.
    /// • Сюжетный триггер <see cref="StoryTriggerFired"/> с id записи запускает её сам (id триггеров A1_03, A2_01… совпадают с id записей).
    /// • Прослушанные запоминаются на сессию: <see cref="Played"/> / <see cref="MarkPlayed"/> / <see cref="ClearPlayed"/>
    ///   (чекпоинты 11.x сохраняют и восстанавливают набор), <see cref="ResetPlayback"/> — сброс очереди при смерти.
    /// </summary>
    public sealed class AudioLogPlayer
    {
        private readonly GameConfig _config;
        private readonly GameBalance _balance;
        private GameBalance.SubtitleSettings _fallback;

        private readonly HashSet<string> _played = new HashSet<string>();
        private readonly Queue<AudioLogData> _queue = new Queue<AudioLogData>();

        private AudioLogData _current;
        private float[] _durations;
        private int _line;
        private float _lineElapsed;
        private float _gapLeft;
        private bool _holdingLast;

        public AudioLogPlayer(GameConfig config, GameBalance balance)
        {
            _config = config;
            _balance = balance;
            EventBus<StoryTriggerFired>.Subscribe(OnStoryTrigger);
        }

        private GameBalance.SubtitleSettings Cfg => _balance != null ? _balance.subtitles : (_fallback ??= new GameBalance.SubtitleSettings());

        // ------------------------------------------------------------------ состояние

        public AudioLogData Current => _current;
        public int QueuedCount => _queue.Count;
        public bool IsBusy => _current != null || _queue.Count > 0;
        public IReadOnlyCollection<string> Played => _played;

        public bool HasPlayed(string id) => _played.Contains(id);

        public void MarkPlayed(IEnumerable<string> ids)
        {
            if (ids != null) foreach (var i in ids) _played.Add(i);
        }

        public void ClearPlayed() => _played.Clear();

        // ------------------------------------------------------------------ управление

        /// <summary>Поставить запись по id в очередь. False — записи нет, она уже звучала/стоит в очереди.</summary>
        public bool Play(string id)
        {
            var log = _config != null ? _config.GetAudioLog(id) : null;
            if (log == null)
            {
                Debug.LogWarning($"[AudioLogs] Запись «{id}» не найдена в GameConfig.audioLogs.");
                return false;
            }
            return Play(log);
        }

        public bool Play(AudioLogData log)
        {
            if (log == null || string.IsNullOrEmpty(log.id)) return false;
            if (_played.Contains(log.id)) return false; // «прослушана» ставится сразу при постановке в очередь — повтор невозможен
            _played.Add(log.id);
            _queue.Enqueue(log);
            return true;
        }

        /// <summary>
        /// Оборвать текущую запись и очистить очередь без пометки «прослушано» (смерть/рестарт с чекпоинта).
        /// Набор прослушанных восстанавливает чекпоинт через <see cref="ClearPlayed"/> + <see cref="MarkPlayed"/>.
        /// </summary>
        public void ResetPlayback()
        {
            if (_current != null) Finish();
            _queue.Clear();
            _gapLeft = 0f;
        }

        public void Tick(float dt)
        {
            if (_current == null)
            {
                if (_queue.Count == 0) return;
                if (_gapLeft > 0f)
                {
                    _gapLeft -= dt;
                    return;
                }
                Begin(_queue.Dequeue());
                return;
            }

            _lineElapsed += dt;
            if (_holdingLast)
            {
                if (_lineElapsed >= Cfg.lastLineHoldSeconds) Finish();
                return;
            }

            // Строка могла закончиться несколько раз за кадр при ускорении времени (F5) — идём циклом.
            while (_line < _durations.Length && _lineElapsed >= _durations[_line])
            {
                _lineElapsed -= _durations[_line];
                _line++;
                if (_line < _durations.Length) PublishLine();
                else
                {
                    EventBus<AudioLogLineChanged>.Publish(new AudioLogLineChanged { Log = _current, LineIndex = -1, Text = string.Empty });
                    _holdingLast = true;
                    _lineElapsed = 0f;
                    if (Cfg.lastLineHoldSeconds <= 0f) Finish();
                    return;
                }
            }
        }

        public override string ToString()
        {
            if (_current == null) return _queue.Count > 0 ? $"пауза, в очереди {_queue.Count}" : $"нет (прослушано {_played.Count})";
            return $"{_current.id} строка {Mathf.Min(_line + 1, _durations.Length)}/{_durations.Length}, в очереди {_queue.Count}";
        }

        // ------------------------------------------------------------------ внутреннее

        private void Begin(AudioLogData log)
        {
            _current = log;
            _durations = log.ResolveLineDurations(Cfg.secondsPerChar, Cfg.minLineSeconds);
            _line = 0;
            _lineElapsed = 0f;
            _holdingLast = false;
            EventBus<AudioLogStarted>.Publish(new AudioLogStarted { Log = log });

            if (_durations.Length == 0)
            {
                Finish();
                return;
            }
            PublishLine();
        }

        private void PublishLine()
        {
            EventBus<AudioLogLineChanged>.Publish(new AudioLogLineChanged
            {
                Log = _current,
                LineIndex = _line,
                Text = _current.lines[_line].text,
                Duration = _durations[_line]
            });
        }

        private void Finish()
        {
            var log = _current;
            _current = null;
            _durations = null;
            _holdingLast = false;
            _gapLeft = Cfg.queueGapSeconds;
            if (log != null) EventBus<AudioLogFinished>.Publish(new AudioLogFinished { Log = log });
        }

        private void OnStoryTrigger(StoryTriggerFired e)
        {
            if (_config != null && _config.GetAudioLog(e.TriggerId) != null) Play(e.TriggerId);
        }
    }
}
