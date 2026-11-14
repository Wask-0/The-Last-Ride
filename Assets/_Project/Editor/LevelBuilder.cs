using System.Diagnostics;
using System.Text;
using LastRide.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LastRide.Editor
{
    /// <summary>
    /// 7.7: «Build Level» — один пункт меню, который детерминированно пересобирает весь уровень в сцене Game:
    /// ось пути → рельеф и дорога → триггеры → точки интереса → вехи → пропсы. Всё кладётся под корень <c>Level</c>.
    /// Повторный запуск полностью заменяет прежний уровень (то же зерно — то же содержимое).
    /// </summary>
    internal static class LevelBuilder
    {
        public const string RootName = "Level";
        public const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string ReportPath = "Assets/Screenshots/level_report.txt";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Build Level", priority = 10)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var root = Populate(true);
            if (root == null) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
        }

        /// <summary>Собирает уровень в открытой сцене. <paramref name="rebuildPath"/> — заново запечь ось пути.</summary>
        public static Transform Populate(bool rebuildPath)
        {
            var sw = Stopwatch.StartNew();
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            if (rebuildPath) RoadPathBuilder.Build();
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null) { Debug.LogError("[LevelBuilder] Нет RoadPath."); return null; }
            if (PrefabBuilder.Load("Vehicles/Van") == null) ShowcaseBuilder.GenerateAll();

            var root = new GameObject(RootName).transform;
            var parts = new[]
            {
                TerrainBuilder.Rebuild(), TriggerBuilder.Rebuild(), PoiBuilder.Rebuild(),
                TrailBuilder.Rebuild(), PropsBuilder.Rebuild(),
            };
            var r = new StringBuilder();
            foreach (var p in parts)
            {
                if (p == null) { Debug.LogError("[LevelBuilder] Один из шагов сборки не удался — см. консоль."); return null; }
                p.SetParent(root, true);
                r.AppendLine($"{p.name}: {CountChildren(p)} объектов");
            }
            r.Insert(0, $"Путь: {path.PointCount - 1} м; уровень собран за {sw.Elapsed.TotalSeconds:0.0} с\n");
            GeneratedAssets.EnsureFolder("Assets/Screenshots");
            System.IO.File.WriteAllText(ReportPath, r.ToString());
            Debug.Log("[The Last Ride] Level built. " + r.ToString());
            return root;
        }

        private static int CountChildren(Transform t)
        {
            int n = 0;
            foreach (Transform c in t) n += 1 + CountChildren(c);
            return n;
        }
    }
}
