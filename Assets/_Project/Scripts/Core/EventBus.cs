using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastRide.Core
{
    /// <summary>Маркер для всех игровых событий. События — небольшие struct-ы без логики.</summary>
    public interface IGameEvent { }

    /// <summary>
    /// Типизированная шина событий: <c>EventBus&lt;T&gt;.Subscribe(handler)</c>, <c>EventBus&lt;T&gt;.Publish(evt)</c>.
    /// Подписка и отписка во время рассылки безопасны. Исключение в одном обработчике
    /// логируется и не мешает остальным.
    /// </summary>
    public static class EventBus<T> where T : struct, IGameEvent
    {
        private static readonly List<Action<T>> Handlers = new List<Action<T>>();
        private static int _publishDepth;
        private static bool _dirty;

        static EventBus()
        {
            EventBusRegistry.Register(Clear);
        }

        public static void Subscribe(Action<T> handler)
        {
            if (handler == null || Handlers.Contains(handler)) return;
            Handlers.Add(handler);
        }

        public static void Unsubscribe(Action<T> handler)
        {
            int i = Handlers.IndexOf(handler);
            if (i < 0) return;
            if (_publishDepth > 0)
            {
                // Нельзя менять список во время обхода — помечаем на удаление.
                Handlers[i] = null;
                _dirty = true;
            }
            else
            {
                Handlers.RemoveAt(i);
            }
        }

        public static void Publish(T evt)
        {
            _publishDepth++;
            try
            {
                // Подписчики, добавленные во время рассылки, получат только следующие события.
                int count = Handlers.Count;
                for (int i = 0; i < count; i++)
                {
                    var h = Handlers[i];
                    if (h == null) continue;
                    try { h(evt); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            finally
            {
                _publishDepth--;
                if (_publishDepth == 0 && _dirty)
                {
                    Handlers.RemoveAll(h => h == null);
                    _dirty = false;
                }
            }
        }

        public static int SubscriberCount
        {
            get
            {
                int n = 0;
                foreach (var h in Handlers) if (h != null) n++;
                return n;
            }
        }

        private static void Clear()
        {
            Handlers.Clear();
            _publishDepth = 0;
            _dirty = false;
        }
    }

    /// <summary>
    /// Сбрасывает все шины при старте Play Mode — нужно, если в настройках отключена
    /// перезагрузка домена (Enter Play Mode Options), иначе остаются подписчики прошлого запуска.
    /// </summary>
    public static class EventBusRegistry
    {
        private static readonly List<Action> Clearers = new List<Action>();

        internal static void Register(Action clear) => Clearers.Add(clear);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAll()
        {
            foreach (var c in Clearers) c();
        }
    }
}
