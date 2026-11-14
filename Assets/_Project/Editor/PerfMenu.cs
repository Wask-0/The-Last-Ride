using LastRide.DebugTools;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// 13.3: «The Last Ride/Debug/Run Perf Test» — входит в Play Mode и запускает <see cref="PerfRun"/> (бот проезжает дорогу и проходит тропу
    /// в реальном времени, ~8 минут). Отчёт: <c>Assets/Screenshots/perf_report.txt</c>. Для честных цифр: окно Game в нужном разрешении
    /// (например 1920×1080), окно Unity в фокусе (в фоне редактор обновляется редко), VSync в Game view выключен.
    /// В собранном плеере то же самое делает аргумент <c>-lr-perf</c>.
    /// </summary>
    [InitializeOnLoad]
    internal static class PerfMenu
    {
        private const string PendingKey = "LastRide.PerfPending";

        static PerfMenu() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Debug/Run Perf Test")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { PerfRun.Launch(); return; }
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            PerfRun.Launch();
        }
    }
}
