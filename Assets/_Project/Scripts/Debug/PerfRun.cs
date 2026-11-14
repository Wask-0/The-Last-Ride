using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LastRide.Core;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Автозамер производительности в собранном плеере (этап 13.3). Запуск: <c>TheLastRide.exe -lr-perf</c> (любые другие аргументы окна
    /// допустимы). Игра сама начинает новую игру, проезжает фургоном всю дорогу (акты 1–2) и проходит тропу пешком (акт 3) с
    /// <see cref="PacingBot"/> в реальном времени, считает кадры по фазам (акт 1, акт 2 затишье/шторм, акт 3) и пишет отчёт
    /// <c>perf_report.txt</c> рядом с exe и в <c>persistentDataPath</c>, затем выходит. Без аргумента — ничего не делает.
    /// </summary>
    public sealed class PerfRun : MonoBehaviour
    {
        public const string Arg = "-lr-perf";

        private sealed class Bucket
        {
            public readonly List<float> Frames = new List<float>(20000);
            public int Hitches; // кадры дольше 100 мс
        }

        private readonly Dictionary<string, Bucket> _buckets = new Dictionary<string, Bucket>();
        private readonly List<string> _order = new List<string>();
        private string _phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Arg) < 0) return;
            Launch();
        }

        /// <summary>Запустить замер (из плеера по аргументу, из редактора — вручную в Play Mode).</summary>
        public static PerfRun Launch()
        {
            Application.runInBackground = true;
            var go = new GameObject("[PerfRun]");
            DontDestroyOnLoad(go);
            return go.AddComponent<PerfRun>();
        }

        public bool Finished { get; private set; }
        public string ReportText { get; private set; }

        private void Start() => StartCoroutine(Run());

        private void Update()
        {
            if (_phase == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            if (!_buckets.TryGetValue(_phase, out var b)) { b = new Bucket(); _buckets[_phase] = b; _order.Add(_phase); }
            if (dt > 1f) return; // загрузка сцены — не считаем
            b.Frames.Add(dt);
            if (dt > 0.1f) b.Hitches++;
        }

        private string CurrentPhase(GameManager gm)
        {
            if (gm.State != GameState.Playing) return null;
            switch (gm.Acts.Current)
            {
                case ActId.Act1: return "Акт 1";
                case ActId.Act2: return gm.Weather.Phase == WeatherPhase.Storm ? "Акт 2 шторм" : "Акт 2 затишье";
                default: return "Акт 3";
            }
        }

        private IEnumerator Run()
        {
            var gm = GameManager.Instance;
            yield return new WaitForSecondsRealtime(2f);
            gm.Flow.StartNewGame();
            float wait = 0f;
            while ((SceneNameNow() != SceneNames.Game || gm.State != GameState.Playing || gm.Flow.Busy) && wait < 60f)
            { wait += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(2f);

            gm.Tutorial.TakeJar();
            gm.Tutorial.PlaceSparkInStove();
            gm.Spark.Invulnerable = true;
            gm.Warmth.Invulnerable = true;

            var bot = gm.gameObject.AddComponent<PacingBot>();
            bot.reportEvery = 200f;

            // ---- Акты 1–2: фургон ----
            bot.DriveVan(10f, 3181f, 1f);
            yield return Sampling(gm, bot);

            // ---- Акт 3: пешком с лампой ----
            gm.Acts.StartAct(ActId.Act3, true, true);
            gm.Spark.PlaceSpark(FireSourceKind.Stove, 200f);
            gm.Spark.MoveToLamp();
            gm.SetPlayerMode(PlayerMode.OnFootWithLamp);
            var robot = LastRide.Player.RobotController.Active;
            robot.GetComponent<LastRide.Player.HeldLamp>().SetHeld(true, 1f);
            foreach (var f in gm.Config.fuelTypes) gm.Inventory.Add(f, 6);
            gm.Warmth.Reset();
            gm.Warmth.Invulnerable = false;
            gm.Spark.Invulnerable = false;
            bot.WalkRobot(3226f, 3675f, 1f, true, true);
            yield return Sampling(gm, bot);

            _phase = null;
            WriteReport(bot);
            if (!Application.isEditor) Application.Quit();
        }

        private IEnumerator Sampling(GameManager gm, PacingBot bot)
        {
            yield return null;
            while (bot.Running)
            {
                _phase = CurrentPhase(gm);
                yield return null;
            }
        }

        private static string SceneNameNow() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        private void WriteReport(PacingBot bot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("THE LAST RIDE — замер производительности");
            sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Разрешение: {Screen.width}×{Screen.height}, режим {Screen.fullScreenMode}, VSync {QualitySettings.vSyncCount}, TargetFPS {Application.targetFrameRate}");
            sb.AppendLine($"Качество: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} МБ), API {SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"CPU: {SystemInfo.processorType} ×{SystemInfo.processorCount}, ОЗУ {SystemInfo.systemMemorySize} МБ");
            sb.AppendLine($"Сборка: development={Debug.isDebugBuild}, версия {Application.version}, Unity {Application.unityVersion}");
            sb.AppendLine();
            sb.AppendLine("Фаза              кадров   средний FPS   1% худших FPS   макс кадр, мс   кадров >100 мс");
            var all = new List<float>();
            foreach (var key in _order)
            {
                var b = _buckets[key];
                if (b.Frames.Count == 0) continue;
                all.AddRange(b.Frames);
                sb.AppendLine(Row(key, b.Frames, b.Hitches));
            }
            int hitchTotal = 0; foreach (var b in _buckets.Values) hitchTotal += b.Hitches;
            if (all.Count > 0) sb.AppendLine(Row("ВСЁ", all, hitchTotal));
            sb.AppendLine();
            sb.AppendLine("Бот: " + (bot.Failed ? "СБОЙ" : "ок"));
            sb.AppendLine(bot.Report);

            string text = sb.ToString();
            ReportText = text;
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "perf_report.txt"), text); } catch (Exception e) { Debug.LogWarning(e.Message); }
            string beside = Application.isEditor ? Path.Combine(Application.dataPath, "Screenshots") : Path.GetDirectoryName(Application.dataPath) ?? ".";
            try { Directory.CreateDirectory(beside); File.WriteAllText(Path.Combine(beside, "perf_report.txt"), text); } catch (Exception e) { Debug.LogWarning(e.Message); }
            Finished = true;
        }

        private static string Row(string name, List<float> frames, int hitches)
        {
            var sorted = new List<float>(frames);
            sorted.Sort();
            double sum = 0; foreach (var f in frames) sum += f;
            float avgFps = (float)(frames.Count / sum);
            float p99 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))];
            float max = sorted[sorted.Count - 1];
            return $"{name,-16} {frames.Count,8} {avgFps,12:0.0} {1f / p99,15:0.0} {max * 1000f,15:0.0} {hitches,16}";
        }
    }
}
