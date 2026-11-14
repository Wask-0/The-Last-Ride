using System.Text;
using LastRide.Level;
using LastRide.ProcGen;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// 7.6: вехи вдоль пешей тропы акта 3 (ТЗ 3, 5): столбики с тряпичными флажками и отражателями каждые 12–15 м,
    /// попеременно по сторонам тропы, от <c>Act3Start</c> до <c>Summit</c>. Корень — <c>Level_Trail</c>.
    /// Укрытия-скалы с топливом строит <see cref="PoiBuilder"/> (они часть раскладки точек интереса).
    /// «Проступание» вех сквозь пургу — от тумана (этап 9) и свечения отражателей (UV0.y в модели).
    /// </summary>
    internal static class TrailBuilder
    {
        public const string RootName = "Level_Trail";
        private const string ReportPath = "Assets/Screenshots/trail_report.txt";
        private const int Seed = 36;
        private const float SpacingMin = 12f, SpacingMax = 15f;
        private const float FirstOffset = 8f;   // от Act3Start
        private const float LastBefore = 6f;    // до Summit

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Trail Markers")]
        public static void BuildMenu()
        {
            var root = Rebuild();
            if (root != null) Selection.activeGameObject = root.gameObject;
        }

        public static Transform Rebuild()
        {
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null) { Debug.LogError("[TrailBuilder] Нет RoadPath."); return null; }
            if (PrefabBuilder.Load("Nature/Marker_0") == null) ShowcaseBuilder.GenerateAll();

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var ts = TerrainBuilder.LoadOrCreateSettings();
            var model = new TerrainModel(path, ts);
            PoiLayout.Build(path, ts, model); // площадки — как у рельефа
            float a3 = path.MarkerOrNegative(RoadPath.Act3StartMarker);
            float summit = path.MarkerOrNegative(RoadPath.SummitMarker);
            if (a3 < 0f || summit < 0f) { Debug.LogError("[TrailBuilder] Нет маркеров Act3Start/Summit."); return null; }

            var root = new GameObject(RootName).transform;
            var rng = new Rng(Seed);
            var prefabs = new[] { PrefabBuilder.Load("Nature/Marker_0"), PrefabBuilder.Load("Nature/Marker_1") };

            int count = 0, side = rng.Chance(0.5f) ? 1 : -1;
            float minGap = float.MaxValue, maxGap = 0f, prev = -1f, minEdge = float.MaxValue;
            for (float d = a3 + FirstOffset; d <= summit - LastBefore; d += rng.Range(SpacingMin, SpacingMax))
            {
                var smp = path.Evaluate(d);
                // У самого края тропы, внутри стен: полуширина + небольшой отступ.
                float lateral = smp.width * 0.5f + 0.9f;
                var pos = smp.position + smp.right * (side * lateral);
                pos.y = model.HeightAt(pos.x, pos.z);
                float yaw = Quaternion.LookRotation(smp.flatTangent).eulerAngles.y + rng.Range(-20f, 20f);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Range(0, prefabs.Length)], root);
                go.name = "Marker_" + count.ToString("00");
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * rng.Range(0.92f, 1.1f);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                if (prev >= 0f) { float gap = d - prev; minGap = Mathf.Min(minGap, gap); maxGap = Mathf.Max(maxGap, gap); }
                prev = d;
                minEdge = Mathf.Min(minEdge, lateral - smp.width * 0.5f);
                side = -side;
                count++;
            }

            var r = new StringBuilder();
            r.AppendLine($"Вех: {count} на отрезке {a3 + FirstOffset:0}–{summit - LastBefore:0} м пути ({summit - a3:0} м тропы)");
            r.AppendLine($"Шаг между вехами: {minGap:0.0}–{maxGap:0.0} м (нужно 12–15)");
            r.AppendLine($"Отступ от края тропы: {minEdge:0.0} м");
            r.AppendLine(minGap >= 11.9f && maxGap <= 15.1f ? "РЕЗУЛЬТАТ: вехи расставлены с нужным шагом" : "РЕЗУЛЬТАТ: ШАГ ВНЕ 12–15 м");
            GeneratedAssets.EnsureFolder("Assets/Screenshots");
            System.IO.File.WriteAllText(ReportPath, r.ToString());
            Debug.Log("[TrailBuilder] " + r.ToString().Split('\n')[0].Trim() + " Отчёт: " + ReportPath);
            return root;
        }
    }
}
