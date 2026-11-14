using System.Collections.Generic;
using System.Text;
using LastRide.Core;
using LastRide.Level;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// 7.3: расставляет триггерные зоны (<see cref="LevelTrigger"/>) поперёк пути по маркерам оси: начала актов, чекпоинты,
    /// аудиозаписи (id = <c>A1_03</c>…), подсказки обучения, поломка фургона, подход к Вершине.
    /// Зоны лежат под <c>Level_Triggers</c> в текущей сцене; пересборка заменяет их целиком.
    /// Положения заданы маркерами и смещениями в метрах — при перестройке оси (<c>Build Road Path</c>) едут вместе с ней.
    /// </summary>
    internal static class TriggerBuilder
    {
        public const string RootName = "Level_Triggers";
        private const float Depth = 6f;      // толщина зоны вдоль пути
        private const float Height = 12f;
        private const float Margin = 14f;    // запас по ширине сверх дороги (стены, обочины)
        private const string ReportPath = "Assets/Screenshots/trigger_report.txt";

        private struct Def
        {
            public string marker; public float offset;
            public LevelTriggerKind kind; public string id; public ActId act; public string hint;
        }

        private static Def D(string marker, float offset, LevelTriggerKind kind, string id, ActId act = ActId.Act1, string hint = null)
            => new Def { marker = marker, offset = offset, kind = kind, id = id, act = act, hint = hint };

        // Порядок по ходу пути проверяется при сборке. Тексты подсказок — черновики для этапа 8.
        private static readonly Def[] Table =
        {
            // ---- Акт 1 ----
            D("Start", 10f, LevelTriggerKind.Checkpoint, "CP_A1_Start"),
            D("Start", 60f, LevelTriggerKind.Tutorial, "TUT_Stove", hint: "[Tab] — открыть печку и подкинуть топливо"),
            D("Stop_A1_1", -45f, LevelTriggerKind.Tutorial, "TUT_Stop1", hint: "Остановитесь [Space], выйдите [E] и соберите ветки и доски"),
            D("Stop_A1_1", 70f, LevelTriggerKind.Checkpoint, "CP_A1_Mid"),
            D("Stop_A1_2", -45f, LevelTriggerKind.Tutorial, "TUT_FuelTypes", hint: "Щепки горят быстро, доски — дольше, брикеты — дольше всего"),
            D("Act2Start", -60f, LevelTriggerKind.Story, "A1_03"),
            // ---- Акт 2 ----
            D("Act2Start", 0f, LevelTriggerKind.ActStart, "ACT2_START", ActId.Act2),
            D("Act2Start", 8f, LevelTriggerKind.Checkpoint, "CP_A2_Start"),
            D("Act2Start", 40f, LevelTriggerKind.Story, "A2_01"),
            D("Hairpin_1", -50f, LevelTriggerKind.Story, "A2_02"),
            D("Stop_A2_2", 0f, LevelTriggerKind.Checkpoint, "CP_A2_1"),
            D("RampRunUp", -75f, LevelTriggerKind.Story, "A2_03"),
            D("Stop_A2_4", 0f, LevelTriggerKind.Checkpoint, "CP_A2_2"),
            D("Hairpin_4", 70f, LevelTriggerKind.Story, "A2_04"),
            D("Breakdown", 0f, LevelTriggerKind.Breakdown, "BREAKDOWN"),
            // ---- Акт 3 ----
            D("Act3Start", 0f, LevelTriggerKind.ActStart, "ACT3_START", ActId.Act3),
            D("Act3Start", 6f, LevelTriggerKind.Checkpoint, "CP_A3_Start"),
            D("Act3Start", 30f, LevelTriggerKind.Story, "A3_02"),
            D("Shelter_2", 0f, LevelTriggerKind.Checkpoint, "CP_A3_Mid"),
            D("Summit", -30f, LevelTriggerKind.Summit, "SUMMIT_NEAR"),
        };

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Triggers")]
        public static void BuildMenu()
        {
            var root = Rebuild();
            if (root != null) Selection.activeGameObject = root.gameObject;
        }

        public static Transform Rebuild()
        {
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null)
            {
                Debug.LogError("[TriggerBuilder] Нет RoadPath. Сначала: The Last Ride/Level/Build Road Path.");
                return null;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;

            var report = new StringBuilder();
            var ids = new HashSet<string>();
            float last = -1f; bool ordered = true; int errors = 0;
            foreach (var def in Table)
            {
                if (!ids.Add(def.id)) { report.AppendLine("ДУБЛЬ id: " + def.id); errors++; }
                if (!path.TryGetMarker(def.marker, out float baseDist))
                {
                    report.AppendLine($"НЕТ маркера {def.marker} для {def.id}");
                    errors++;
                    continue;
                }
                float dist = Mathf.Clamp(baseDist + def.offset, 0f, path.Length);
                var smp = path.Evaluate(dist);

                var go = new GameObject($"{def.kind}_{def.id}");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(smp.position + Vector3.up * (Height * 0.35f), Quaternion.LookRotation(smp.flatTangent, Vector3.up));
                go.layer = 2; // Ignore Raycast: лучи подвески, камеры и прицела проходят сквозь
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(smp.width + Margin * 2f, Height, Depth);

                var t = go.AddComponent<LevelTrigger>();
                t.kind = def.kind; t.id = def.id; t.act = def.act; t.distance = dist;
                t.hintText = def.hint; t.hintDuration = 7f;

                if (dist < last) ordered = false;
                last = dist;
                report.AppendLine($"{dist,7:0} м  {def.kind,-10} {def.id,-14} ({def.marker}{def.offset:+0;-0;+0}) {(def.kind == LevelTriggerKind.ActStart ? path.ActAt(dist).ToString() : "")}");
            }
            report.Insert(0, $"Зон: {root.childCount}, ошибок: {errors}, порядок по ходу пути: {(ordered ? "верный" : "НАРУШЕН")}\n");
            report.AppendLine(errors == 0 && ordered ? "РЕЗУЛЬТАТ: все зоны на месте" : "РЕЗУЛЬТАТ: ЕСТЬ ПРОБЛЕМЫ");

            GeneratedAssets.EnsureFolder("Assets/Screenshots");
            System.IO.File.WriteAllText(ReportPath, report.ToString());
            Debug.Log($"[TriggerBuilder] Зон: {root.childCount}, ошибок: {errors}. Отчёт: {ReportPath}");
            return root;
        }
    }
}
