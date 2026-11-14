using System.Collections.Generic;
using System.Text;
using LastRide.Level;
using LastRide.ProcGen;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// 7.4: расставляет ёлки, камни, скалы и столбы ЛЭП вдоль пути по правилам из <see cref="PropsSettings"/> (плотность по актам,
    /// поляны на стоянках, просвет до стен, ограничения по уклону). Высота — по <see cref="TerrainModel"/>. Seed фиксирован,
    /// результат детерминирован (контрольная сумма в отчёте). Корень — <c>Level_Props</c> в текущей сцене.
    /// </summary>
    internal static class PropsBuilder
    {
        public const string RootName = "Level_Props";
        public const string SettingsPath = "Assets/_Project/Data/Resources/PropsSettings.asset";
        private const string ReportPath = "Assets/Screenshots/props_report.txt";
        private const float GroupSize = 250f;

        private sealed class Ctx
        {
            public RoadPath path; public TerrainSettings ts; public PropsSettings ps; public TerrainModel model; public Rng rng;
            public Transform root; public float a3; public List<PoiLayout.Poi> pois; public float[] clearings;
            public Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
            public Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            public int[,] counts = new int[3, 4]; // акт × (деревья, камни, скалы, столбы)
            public int rejected; public long hash = 17; public float minClear = float.MaxValue;
        }

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Props")]
        public static void BuildMenu()
        {
            var root = Rebuild();
            if (root != null) Selection.activeGameObject = root.gameObject;
        }

        public static PropsSettings LoadOrCreateSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<PropsSettings>(SettingsPath);
            if (s != null) return s;
            GeneratedAssets.EnsureFolder(System.IO.Path.GetDirectoryName(SettingsPath));
            s = ScriptableObject.CreateInstance<PropsSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        public static Transform Rebuild()
        {
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null) { Debug.LogError("[PropsBuilder] Нет RoadPath."); return null; }
            if (PrefabBuilder.Load("Nature/Tree_0") == null) ShowcaseBuilder.GenerateAll();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var c = new Ctx
            {
                path = path, ts = TerrainBuilder.LoadOrCreateSettings(), ps = LoadOrCreateSettings(),
                root = new GameObject(RootName).transform, a3 = path.MarkerOrNegative(RoadPath.Act3StartMarker)
            };
            c.model = new TerrainModel(path, c.ts);
            c.pois = PoiLayout.Build(path, c.ts, c.model).items;
            c.rng = new Rng(c.ps.seed);
            var cl = new List<float>();
            foreach (var m in c.ps.clearingMarkers) { float d = path.MarkerOrNegative(m); if (d >= 0f) cl.Add(d); }
            c.clearings = cl.ToArray();

            ScatterAlongRoad(c);
            PlacePoles(c);

            var r = new StringBuilder();
            string[] names = { "Акт 1", "Акт 2", "Акт 3" };
            int total = 0;
            for (int a = 0; a < 3; a++)
            {
                int sum = c.counts[a, 0] + c.counts[a, 1] + c.counts[a, 2] + c.counts[a, 3];
                total += sum;
                r.AppendLine($"{names[a]}: ёлок {c.counts[a, 0]}, камней {c.counts[a, 1]}, скал {c.counts[a, 2]}, столбов {c.counts[a, 3]} (всего {sum})");
            }
            r.Insert(0, $"Всего пропсов: {total}, отброшено проверками {c.rejected}\n");
            r.AppendLine($"Минимальный просвет пропс–стена: {c.minClear:0.00} м (нужно ≥ {c.ps.roadClearance:0.0})");
            r.AppendLine($"Контрольная сумма расстановки: {c.hash:X}");
            r.AppendLine($"Время: {sw.Elapsed.TotalSeconds:0.0} с");
            r.AppendLine(c.minClear >= c.ps.roadClearance - 0.001f ? "РЕЗУЛЬТАТ: пропсы не заходят на дорогу" : "РЕЗУЛЬТАТ: ЕСТЬ ПРОПСЫ СЛИШКОМ БЛИЗКО К ДОРОГЕ");
            GeneratedAssets.EnsureFolder("Assets/Screenshots");
            System.IO.File.WriteAllText(ReportPath, r.ToString());
            Debug.Log($"[PropsBuilder] Пропсов: {total}, сумма {c.hash:X}. Отчёт: {ReportPath}");
            return c.root;
        }

        // ------------------------------------------------------------------ правила

        private static void ScatterAlongRoad(Ctx c)
        {
            var ps = c.ps; var path = c.path;
            for (float d = ps.step * 0.5f; d < path.Length; d += ps.step)
            {
                var smp = path.Evaluate(d);
                int act = (int)path.ActAt(d);
                var cfg = ps.acts[Mathf.Clamp(act - 1, 0, ps.acts.Length - 1)];
                float inner = smp.width * 0.5f + c.ts.shoulderWidth + TerrainBuilder.WallOffset(d, c.ts, c.a3);
                float k = ps.step / 10f;

                foreach (int side in new[] { -1, 1 })
                {
                    // Группы ёлок у дороги.
                    if (c.rng.Chance(cfg.treeGroups * k))
                        TreeGroup(c, smp, side, inner + 1.5f + Mathf.Pow(c.rng.Value, 1.5f) * ps.nearDepth, act);
                    // Мелкие и средние камни.
                    if (c.rng.Chance(cfg.rocks * k))
                        Rock(c, smp, side, inner + 1.2f + Mathf.Pow(c.rng.Value, 2f) * ps.nearDepth, act, c.rng.Chance(0.35f) ? 2 : 0);
                    // Крупные скалы.
                    if (c.rng.Chance(cfg.bigRocks * k))
                        Rock(c, smp, side, inner + 3f + c.rng.Value * ps.nearDepth, act, 4);
                    // Фон: ёлки и скалы вдали.
                    if (c.rng.Chance(cfg.farTreeGroups * k))
                        TreeGroup(c, smp, side, inner + c.rng.Range(ps.farRange), act);
                    if (c.rng.Chance(cfg.farRocks * k))
                        Rock(c, smp, side, inner + c.rng.Range(ps.farRange), act, c.rng.Chance(0.5f) ? 2 : 4);
                }
            }
        }

        private static void TreeGroup(Ctx c, RoadSample smp, int side, float lateral, int act)
        {
            var center = smp.position + smp.flatTangent * c.rng.Range(-c.ps.step * 0.5f, c.ps.step * 0.5f) + smp.right * (side * lateral);
            int n = c.rng.Range(2, 6);
            for (int i = 0; i < n; i++)
            {
                var off = c.rng.InsideUnitCircle * 5f;
                var p = new Vector3(center.x + off.x, 0f, center.z + off.y);
                string name = "Tree_" + c.rng.Range(0, 4);
                float scale = c.rng.Range(0.8f, 1.4f);
                float yaw = c.rng.Range(0f, 360f);
                float tilt = c.rng.Range(0f, 4f);
                float tiltYaw = c.rng.Range(0f, 360f);
                if (!Validate(c, ref p, 0.6f * scale, c.ps.treeMaxSlopeDeg, true)) { c.rejected++; continue; }
                var go = Spawn(c, "Trees", name, p, Quaternion.Euler(0f, tiltYaw, 0f) * Quaternion.Euler(tilt, 0f, 0f) * Quaternion.Euler(0f, yaw - tiltYaw, 0f), Vector3.one * scale, smp.distance);
                if (go != null) c.counts[act - 1, 0]++;
            }
        }

        /// <summary>sizeClass: 0 — мелкие (S), 2 — средние (M), 4 — крупные (L).</summary>
        private static void Rock(Ctx c, RoadSample smp, int side, float lateral, int act, int sizeClass)
        {
            var p = smp.position + smp.flatTangent * c.rng.Range(-c.ps.step * 0.5f, c.ps.step * 0.5f) + smp.right * (side * lateral);
            string name = sizeClass == 0 ? "Rock_S" + c.rng.Range(0, 2) : sizeClass == 2 ? "Rock_M" + c.rng.Range(0, 2) : "Rock_L" + c.rng.Range(0, 2);
            float radius = sizeClass == 0 ? 0.8f : sizeClass == 2 ? 1.6f : 3.6f;
            float scale = c.rng.Range(0.8f, 1.3f);
            var rot = Quaternion.Euler(c.rng.Range(-10f, 10f), c.rng.Range(0f, 360f), c.rng.Range(-10f, 10f));
            if (!Validate(c, ref p, radius * scale, c.ps.bigRockMaxSlopeDeg, true)) { c.rejected++; return; }
            var go = Spawn(c, "Rocks", name, p, rot, Vector3.one * scale, smp.distance);
            if (go != null) c.counts[act - 1, sizeClass == 4 ? 2 : 1]++;
        }

        private static void PlacePoles(Ctx c)
        {
            var ps = c.ps; var path = c.path;
            if (!ps.poles) return;
            float from = path.MarkerOrNegative(ps.polesFromMarker), to = path.MarkerOrNegative(ps.polesToMarker);
            if (from < 0f || to < 0f) return;
            from += ps.polesFromOffset; to += ps.polesToOffset;
            int side = ps.polesSide < 0f ? -1 : 1;
            for (float d = from; d <= to; d += ps.polesSpacing)
            {
                var smp = path.Evaluate(d);
                float inner = smp.width * 0.5f + c.ts.shoulderWidth + TerrainBuilder.WallOffset(d, c.ts, c.a3);
                var p = smp.position + smp.right * (side * (inner + Mathf.Abs(ps.polesSide)));
                float tilt = c.rng.Chance(ps.leaningShare) ? c.rng.Range(8f, 13f) : c.rng.Range(0f, 2.5f);
                float tiltDir = c.rng.Range(0f, 360f);
                if (!Validate(c, ref p, 0.4f, 90f, false)) { c.rejected++; continue; }
                var rot = Quaternion.Euler(0f, tiltDir, 0f) * Quaternion.Euler(tilt, 0f, 0f) * Quaternion.Euler(0f, -tiltDir, 0f) * Quaternion.LookRotation(smp.flatTangent, Vector3.up);
                var go = Spawn(c, "Poles", "PowerPole", p, rot, Vector3.one, d);
                if (go != null) c.counts[0, 3]++;
            }
        }

        /// <summary>Проверка места и посадка на землю: просвет до стен, поляны, уклон. Меняет высоту <paramref name="p"/>.</summary>
        private static bool Validate(Ctx c, ref Vector3 p, float radius, float maxSlopeDeg, bool respectClearings)
        {
            var pr = c.path.Project(p);
            var smp = c.path.Evaluate(pr.distance);
            float inner = smp.width * 0.5f + c.ts.shoulderWidth + TerrainBuilder.WallOffset(pr.distance, c.ts, c.a3);
            float clear = pr.axisDistance - inner - radius;
            if (clear < c.ps.roadClearance) return false;

            if (respectClearings)
            {
                foreach (float m in c.clearings)
                    if (Mathf.Abs(pr.distance - m) < c.ps.clearingAlong && pr.axisDistance < c.ps.clearingAcross) return false;
            }

            foreach (var poi in c.pois)
            {
                float dx0 = p.x - poi.position.x, dz0 = p.z - poi.position.z;
                float rr = poi.exclusionRadius + radius;
                if (dx0 * dx0 + dz0 * dz0 < rr * rr) return false;
            }

            float y = c.model.HeightAt(p.x, p.z);
            const float e = 1.2f;
            float dx = c.model.HeightAt(p.x + e, p.z) - c.model.HeightAt(p.x - e, p.z);
            float dz = c.model.HeightAt(p.x, p.z + e) - c.model.HeightAt(p.x, p.z - e);
            float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / (2f * e)) * Mathf.Rad2Deg;
            if (slope > maxSlopeDeg) return false;

            c.minClear = Mathf.Min(c.minClear, clear);
            p.y = y;
            return true;
        }

        private static GameObject Spawn(Ctx c, string category, string prefabName, Vector3 pos, Quaternion rot, Vector3 scale, float distance)
        {
            if (!c.prefabs.TryGetValue(prefabName, out var prefab))
            {
                prefab = PrefabBuilder.Load("Nature/" + prefabName);
                c.prefabs[prefabName] = prefab;
            }
            if (prefab == null) return null;

            string key = category + "_" + Mathf.FloorToInt(distance / GroupSize).ToString("00");
            if (!c.groups.TryGetValue(key, out var parent))
            {
                var catKey = category;
                if (!c.groups.TryGetValue("#" + catKey, out var cat))
                {
                    cat = new GameObject(catKey).transform;
                    cat.SetParent(c.root, false);
                    c.groups["#" + catKey] = cat;
                }
                parent = new GameObject(key).transform;
                parent.SetParent(cat, false);
                c.groups[key] = parent;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            unchecked
            {
                c.hash = c.hash * 31 + prefabName.GetHashCode() * 0 + prefabName.Length * 7 + prefabName[prefabName.Length - 1];
                c.hash = c.hash * 31 + Mathf.RoundToInt(pos.x * 10f);
                c.hash = c.hash * 31 + Mathf.RoundToInt(pos.y * 10f);
                c.hash = c.hash * 31 + Mathf.RoundToInt(pos.z * 10f);
                c.hash = c.hash * 31 + Mathf.RoundToInt(rot.eulerAngles.y * 10f);
            }
            return go;
        }
    }
}
