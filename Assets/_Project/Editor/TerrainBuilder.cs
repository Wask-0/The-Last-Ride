using System.Collections.Generic;
using System.IO;
using System.Text;
using LastRide.Level;
using LastRide.ProcGen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastRide.Editor
{
    /// <summary>
    /// 7.2: собирает рельеф (ближние чанки с коллайдером + дальние без), дорогу с обочинами, невидимые стены и сугробы-бортики
    /// по оси пути (<see cref="RoadPath"/>) под корневым объектом <c>Level_Terrain</c> в текущей сцене.
    /// Детерминированно: параметры — в <see cref="TerrainSettings"/>, seed фиксирован. Меши сохраняются в
    /// <c>Generated/Terrain</c> (папка в .gitignore — пересобирается этим же меню).
    /// </summary>
    internal static class TerrainBuilder
    {
        public const string RootName = "Level_Terrain";
        public const string SettingsPath = "Assets/_Project/Data/Resources/TerrainSettings.asset";
        private const string MeshFolder = GeneratedAssets.Root + "Terrain";
        private const string ReportPath = "Assets/Screenshots/terrain_report.txt";
        private const int IgnoreRaycastLayer = 2;

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Terrain")]
        public static void BuildMenu()
        {
            var root = Rebuild();
            if (root != null) Selection.activeGameObject = root.gameObject;
        }

        public static TerrainSettings LoadOrCreateSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<TerrainSettings>(SettingsPath);
            if (s != null) return s;
            GeneratedAssets.EnsureFolder(Path.GetDirectoryName(SettingsPath));
            s = ScriptableObject.CreateInstance<TerrainSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        /// <summary>Пересобрать рельеф в активной сцене. Возвращает корневой объект.</summary>
        public static Transform Rebuild()
        {
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null)
            {
                Debug.LogError("[TerrainBuilder] Нет RoadPath. Сначала: The Last Ride/Level/Build Road Path.");
                return null;
            }

            var s = LoadOrCreateSettings();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var model = new TerrainModel(path, s);
            var layout = PoiLayout.Build(path, s, model); // площадки под здания (7.5)
            var noise = new Noise3(s.seed + 101);
            var mat = MaterialsBuilder.World;
            var report = new StringBuilder();

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            if (AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.DeleteAsset(MeshFolder);
            GeneratedAssets.EnsureFolder(MeshFolder);

            var root = new GameObject(RootName).transform;
            int meshCount = 0; long vertCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                float a3 = path.MarkerOrNegative(RoadPath.Act3StartMarker);
                var roads = BuildRoad(root, path, s, mat, ref meshCount, ref vertCount);
                BuildWalls(root, path, s, a3, layout, ref meshCount, ref vertCount);
                BuildBerms(root, path, s, noise, mat, ref meshCount, ref vertCount);
                int near = BuildTerrain(root, "Terrain_Near", model, path, s, mat, false, ref meshCount, ref vertCount);
                int far = BuildTerrain(root, "Terrain_Far", model, path, s, mat, true, ref meshCount, ref vertCount);
                report.AppendLine($"Чанков рельефа: ближних {near} (коллайдер), дальних {far}; кусков дороги {roads}; мешей {meshCount}, вершин {vertCount:N0}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate(path, s, model, report);
            report.AppendLine($"Время сборки: {sw.Elapsed.TotalSeconds:0.0} с");
            GeneratedAssets.EnsureFolder("Assets/Screenshots");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("[TerrainBuilder] Готово. " + report.ToString().Split('\n')[0].Trim() + " Отчёт: " + ReportPath);
            return root;
        }

        // ------------------------------------------------------------------ дорога

        private static int BuildRoad(Transform root, RoadPath path, TerrainSettings s, Material mat, ref int meshCount, ref long vertCount)
        {
            var parent = new GameObject("Road").transform;
            parent.SetParent(root, false);
            Color snow = PaletteAsset.Get(PaletteColor.Snow);
            Color shade = PaletteAsset.Get(PaletteColor.SnowShadow);
            Color edge = Color.Lerp(snow, shade, 0.3f);

            int chunks = Mathf.CeilToInt(path.Length / s.roadChunkLength);
            for (int c = 0; c < chunks; c++)
            {
                float d0 = c * s.roadChunkLength, d1 = Mathf.Min(path.Length, d0 + s.roadChunkLength);
                var m = new TriMesh();
                int steps = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / s.roadStep));
                RoadSample prev = path.Evaluate(d0);
                for (int i = 1; i <= steps; i++)
                {
                    var cur = path.Evaluate(Mathf.Min(d1, d0 + i * (d1 - d0) / steps));
                    float h0 = prev.width * 0.5f, h1 = cur.width * 0.5f;
                    // Поперечные смещения (в долях полуширины; обочины — в метрах).
                    // [левая обочина | левый край | колея | правый край | правая обочина]
                    float[] f = { -1f, -0.6f, 0.6f, 1f };
                    float yRoad0 = prev.position.y + s.roadLift, yRoad1 = cur.position.y + s.roadLift;

                    Quad(m, P(prev, -h0, yRoad0), P(cur, -h1, yRoad1), P(cur, f[1] * h1, yRoad1), P(prev, f[1] * h0, yRoad0), Vector3.up, edge, 0.5f);
                    Quad(m, P(prev, f[1] * h0, yRoad0), P(cur, f[1] * h1, yRoad1), P(cur, f[2] * h1, yRoad1), P(prev, f[2] * h0, yRoad0), Vector3.up, shade, 0.25f);
                    Quad(m, P(prev, f[2] * h0, yRoad0), P(cur, f[2] * h1, yRoad1), P(cur, h1, yRoad1), P(prev, h0, yRoad0), Vector3.up, edge, 0.5f);

                    // Обочины: от кромки вниз на roadSink.
                    float sh = s.shoulderWidth;
                    foreach (int side in new[] { -1, 1 })
                    {
                        var a = P(prev, side * h0, yRoad0 - 0.03f);
                        var b = P(cur, side * h1, yRoad1 - 0.03f);
                        var c1 = P(cur, side * (h1 + sh), cur.position.y - s.roadSink + 0.02f);
                        var d1p = P(prev, side * (h0 + sh), prev.position.y - s.roadSink + 0.02f);
                        Quad(m, a, b, c1, d1p, Vector3.up, snow, 1f);
                    }
                    prev = cur;
                }
                var go = MeshObject(parent, $"Road_{c:00}", m, $"Road_{c:00}", mat, collider: true, ref meshCount, ref vertCount);
                go.layer = 0;
            }
            return chunks;
        }

        private static Vector3 P(RoadSample sample, float lateral, float y)
        {
            var p = sample.position + sample.right * lateral;
            p.y = y;
            return p;
        }

        // ------------------------------------------------------------------ стены и сугробы

        private static void BuildWalls(Transform root, RoadPath path, TerrainSettings s, float act3Start, PoiLayout.Result layout, ref int meshCount, ref long vertCount)
        {
            var parent = new GameObject("Walls").transform;
            parent.SetParent(root, false);
            int chunks = Mathf.CeilToInt(path.Length / s.roadChunkLength);
            for (int c = 0; c < chunks; c++)
            {
                float d0 = c * s.roadChunkLength, d1 = Mathf.Min(path.Length, d0 + s.roadChunkLength);
                var m = new TriMesh();
                int steps = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / s.wallStep));
                foreach (int side in new[] { -1, 1 })
                {
                    bool prevOpen = true;
                    Vector3 pIn = default, pOut = default, bIn = default, bOut = default; // верх/низ предыдущего сечения
                    for (int i = 0; i <= steps; i++)
                    {
                        var smp = path.Evaluate(d0 + i * (d1 - d0) / steps);
                        float offset = WallOffset(smp.distance, s, act3Start);
                        float oi = smp.width * 0.5f + s.shoulderWidth + offset;
                        float oo = oi + s.wallThickness;
                        float yTop = smp.position.y + s.wallHeight, yBot = smp.position.y - s.roadSink - 1.5f;
                        var tIn = P(smp, side * oi, yTop); var tOut = P(smp, side * oo, yTop);
                        var lIn = P(smp, side * oi, yBot); var lOut = P(smp, side * oo, yBot);
                        bool open = IsShelterGap(layout, smp.distance, side);
                        if (i > 0 && !open && !prevOpen)
                        {
                            Vector3 toRoad = -side * smp.right, away = side * smp.right;
                            Quad(m, bIn, lIn, tIn, pIn, toRoad, Color.white, 0f);   // внутренняя грань
                            Quad(m, bOut, lOut, tOut, pOut, away, Color.white, 0f);  // внешняя грань
                            Quad(m, pIn, tIn, tOut, pOut, Vector3.up, Color.white, 0f); // верх
                        }
                        pIn = tIn; pOut = tOut; bIn = lIn; bOut = lOut;
                        prevOpen = open;
                    }
                }
                var go = MeshObject(parent, $"Wall_{c:00}", m, $"Wall_{c:00}", null, collider: true, ref meshCount, ref vertCount);
                go.layer = IgnoreRaycastLayer; // сталкивается с фургоном и роботом, но не мешает камере и лучам подвески
            }
        }

        /// <summary>Проём в стене у укрытия акта 3: ±6 м вдоль тропы на стороне укрытия.</summary>
        private static bool IsShelterGap(PoiLayout.Result layout, float distance, int side)
        {
            foreach (var poi in layout.items)
                if (poi.kind == PoiLayout.Kind.Shelter && poi.side == side && Mathf.Abs(poi.distance - distance) < 6f) return true;
            return false;
        }

        internal static float WallOffset(float distance, TerrainSettings s, float act3Start)
        {
            if (act3Start < 0f || distance <= act3Start) return s.wallOffset;
            float t = Mathf.Clamp01((distance - act3Start) / 40f);
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(s.wallOffset, s.trailWallOffset, t);
        }

        private static void BuildBerms(Transform root, RoadPath path, TerrainSettings s, Noise3 noise, Material mat, ref int meshCount, ref long vertCount)
        {
            var parent = new GameObject("Berms").transform;
            parent.SetParent(root, false);
            Color snow = PaletteAsset.Get(PaletteColor.Snow);
            int chunks = Mathf.CeilToInt(path.Length / s.roadChunkLength);
            for (int c = 0; c < chunks; c++)
            {
                float d0 = c * s.roadChunkLength, d1 = Mathf.Min(path.Length, d0 + s.roadChunkLength);
                var m = new TriMesh();
                int steps = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / s.wallStep));
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3[] prev = null;
                    for (int i = 0; i <= steps; i++)
                    {
                        var smp = path.Evaluate(d0 + i * (d1 - d0) / steps);
                        float o0 = smp.width * 0.5f + s.shoulderWidth;
                        float baseY = smp.position.y - s.roadSink + 0.02f;
                        float crest = s.bermHeight * (0.55f + 0.45f * Mathf.Clamp01(noise.Fbm(new Vector3(smp.distance / 14f, side * 3.1f, 0.5f), 2) * 0.5f + 0.5f));
                        var sec = new[]
                        {
                            P(smp, side * o0, baseY),
                            P(smp, side * (o0 + s.bermWidth * 0.45f), baseY + crest),
                            P(smp, side * (o0 + s.bermWidth), baseY + crest * 0.25f),
                            P(smp, side * (o0 + s.bermWidth), baseY - 2f) // «юбка» вниз: на стороне обрыва не виден подвес
                        };
                        if (prev != null)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                Vector3 reference = k < 2 ? Vector3.up : side * smp.right;
                                Quad(m, prev[k], sec[k], sec[k + 1], prev[k + 1], reference, snow, 1f);
                            }
                        }
                        prev = sec;
                    }
                }
                MeshObject(parent, $"Berm_{c:00}", m, $"Berm_{c:00}", mat, collider: false, ref meshCount, ref vertCount);
            }
        }

        // ------------------------------------------------------------------ рельеф

        private static int BuildTerrain(Transform root, string name, TerrainModel model, RoadPath path, TerrainSettings s, Material mat, bool far, ref int meshCount, ref long vertCount)
        {
            var parent = new GameObject(name).transform;
            parent.SetParent(root, false);
            float size = far ? s.farChunkSize : s.nearChunkSize;
            float spacing = far ? s.farSpacing : s.nearSpacing;
            float band = far ? s.farBand : s.nearBand;
            int cells = Mathf.Max(1, Mathf.RoundToInt(size / spacing));
            float cell = size / cells;
            float sink = far ? s.farSink : 0f;

            var pts = new List<Vector3>();
            for (int i = 0; i < path.PointCount; i += 4) pts.Add(path.PointAt(i));
            pts.Add(path.PointAt(path.PointCount - 1));
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            int cx0 = Mathf.FloorToInt((minX - band) / size), cx1 = Mathf.FloorToInt((maxX + band) / size);
            int cz0 = Mathf.FloorToInt((minZ - band) / size), cz1 = Mathf.FloorToInt((maxZ + band) / size);

            int built = 0;
            var heights = new float[(cells + 1) * (cells + 1)];
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cz = cz0; cz <= cz1; cz++)
                {
                    float ox = cx * size, oz = cz * size;
                    float cxm = ox + size * 0.5f, czm = oz + size * 0.5f;
                    float best = float.MaxValue;
                    foreach (var p in pts) { float dx = p.x - cxm, dz = p.z - czm; best = Mathf.Min(best, dx * dx + dz * dz); }
                    if (Mathf.Sqrt(best) > band + size * 0.7072f) continue;

                    for (int i = 0; i <= cells; i++)
                        for (int j = 0; j <= cells; j++)
                            heights[i * (cells + 1) + j] = model.HeightAt(ox + i * cell, oz + j * cell) - sink;

                    var m = new TriMesh();
                    for (int i = 0; i < cells; i++)
                    {
                        for (int j = 0; j < cells; j++)
                        {
                            var v00 = new Vector3(ox + i * cell, heights[i * (cells + 1) + j], oz + j * cell);
                            var v10 = new Vector3(ox + (i + 1) * cell, heights[(i + 1) * (cells + 1) + j], oz + j * cell);
                            var v01 = new Vector3(ox + i * cell, heights[i * (cells + 1) + j + 1], oz + (j + 1) * cell);
                            var v11 = new Vector3(ox + (i + 1) * cell, heights[(i + 1) * (cells + 1) + j + 1], oz + (j + 1) * cell);
                            int hash = unchecked((cx * 73856093) ^ (cz * 19349663) ^ (i * 83492791) ^ (j * 668265263));
                            if (((i + j) & 1) == 0)
                            {
                                Facet(m, v00, v01, v11, hash);
                                Facet(m, v00, v11, v10, hash * 31 + 7);
                            }
                            else
                            {
                                Facet(m, v00, v01, v10, hash);
                                Facet(m, v01, v11, v10, hash * 31 + 7);
                            }
                        }
                    }
                    MeshObject(parent, $"{(far ? "Far" : "Near")}_{cx}_{cz}", m, $"{(far ? "Far" : "Near")}_{cx}_{cz}", mat, collider: !far, ref meshCount, ref vertCount);
                    built++;
                }
            }
            return built;
        }

        private static void Facet(TriMesh m, Vector3 a, Vector3 b, Vector3 c, int hash)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            float ny = n.y;
            Color col; float snow;
            if (ny >= 0.78f) { col = PaletteAsset.Get(PaletteColor.Snow); snow = 1f; }
            else if (ny >= 0.55f)
            {
                float t = (ny - 0.55f) / 0.23f;
                col = Color.Lerp(PaletteAsset.Get(PaletteColor.RockLight), PaletteAsset.Get(PaletteColor.SnowShadow), t);
                snow = t;
            }
            else { col = PaletteAsset.Get(((hash & 1) == 0) ? PaletteColor.RockDark : PaletteColor.RockLight); snow = 0f; }
            float jitter = 0.95f + 0.07f * ((hash >> 3 & 255) / 255f);
            col = new Color(col.r * jitter, col.g * jitter, col.b * jitter, 1f);
            m.Tri(a, b, c, col, snow);
        }

        // ------------------------------------------------------------------ проверка

        private static void Validate(RoadPath path, TerrainSettings s, TerrainModel model, StringBuilder report)
        {
            float minY = float.MaxValue, maxY = float.MinValue, worst = float.MinValue, worstEdge = float.MinValue, maxLift = 0f;
            for (float d = 0f; d <= path.Length; d += 3f)
            {
                var smp = path.Evaluate(d);
                float half = smp.width * 0.5f;
                float roadY = smp.position.y + s.roadLift;
                foreach (float off in new[] { -half - s.shoulderWidth, -half, 0f, half, half + s.shoulderWidth })
                {
                    var p = smp.position + smp.right * off;
                    float h = model.HeightAt(p.x, p.z);
                    worst = Mathf.Max(worst, h - roadY);
                    if (Mathf.Abs(off) >= half) worstEdge = Mathf.Max(worstEdge, h - roadY);
                    minY = Mathf.Min(minY, h); maxY = Mathf.Max(maxY, h);
                }
                // Рельеф над осью должен быть ниже дороги примерно на roadSink (проверка отсутствия «подъёма» от соседних участков).
                maxLift = Mathf.Max(maxLift, model.HeightAt(smp.position.x, smp.position.z) - (smp.position.y - s.roadSink));
            }
            report.AppendLine($"Высота рельефа у дороги: {minY:0.0}…{maxY:0.0} м");
            report.AppendLine($"Рельеф относительно дорожного меша (по оси/краям/обочинам): макс. {worst:+0.000;-0.000} м (нужно < 0), на кромках и обочинах макс. {worstEdge:+0.000;-0.000} м");
            report.AppendLine($"Подъём рельефа над осью от соседних участков: до {maxLift * 100f:0.0} см");
            report.AppendLine(worst < -0.05f ? "РЕЗУЛЬТАТ: рельеф под дорогой — без прорезей" : "РЕЗУЛЬТАТ: ВНИМАНИЕ — рельеф выше дороги");
        }

        // ------------------------------------------------------------------ меш

        private static void Quad(TriMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 reference, Color color, float snow)
        {
            m.TriOriented(a, b, c, reference, color, snow);
            m.TriOriented(a, c, d, reference, color, snow);
        }

        private static GameObject MeshObject(Transform parent, string name, TriMesh data, string assetName, Material mat, bool collider, ref int meshCount, ref long vertCount)
        {
            string assetPath = MeshFolder + "/" + assetName + ".asset";
            var mesh = new Mesh { name = assetName };
            data.WriteTo(mesh);
            AssetDatabase.CreateAsset(mesh, assetPath);
            meshCount++; vertCount += mesh.vertexCount;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            if (mat != null)
            {
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        /// <summary>Плоский (flat-shading) накопитель треугольников: вершины не разделяются между гранями.</summary>
        private sealed class TriMesh
        {
            private readonly List<Vector3> _pos = new List<Vector3>();
            private readonly List<Vector3> _nrm = new List<Vector3>();
            private readonly List<Color32> _col = new List<Color32>();
            private readonly List<Vector2> _uv = new List<Vector2>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, float snow)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) return;
                n.Normalize();
                _pos.Add(a); _pos.Add(b); _pos.Add(c);
                for (int i = 0; i < 3; i++) { _nrm.Add(n); _col.Add(color); _uv.Add(new Vector2(snow, 0f)); }
            }

            /// <summary>Треугольник, развёрнутый так, чтобы нормаль смотрела в сторону <paramref name="reference"/>.</summary>
            public void TriOriented(Vector3 a, Vector3 b, Vector3 c, Vector3 reference, Color color, float snow)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), reference) < 0f) (b, c) = (c, b);
                Tri(a, b, c, color, snow);
            }

            public void WriteTo(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = _pos.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_pos);
                mesh.SetNormals(_nrm);
                mesh.SetColors(_col);
                mesh.SetUVs(0, _uv);
                var tris = new int[_pos.Count];
                for (int i = 0; i < tris.Length; i++) tris[i] = i;
                mesh.SetTriangles(tris, 0, true);
                mesh.RecalculateBounds();
            }
        }
    }
}
