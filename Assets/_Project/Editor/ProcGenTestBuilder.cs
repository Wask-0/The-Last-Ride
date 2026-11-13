using System.Collections.Generic;
using LastRide.ProcGen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Витрина примитивов и деформаций (подзадача 2.1): «The Last Ride/ProcGen/Test Primitives».
    /// Собирает сцену Scenes/Test/ProcGenTest.unity, меши кладёт в Generated/Test/.
    /// </summary>
    internal static class ProcGenTestBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Test/ProcGenTest.unity";
        private const float Spacing = 4f;

        private struct Item
        {
            public string name;
            public Shape shape;
            public int row;
            public Material material;
        }

        [MenuItem(EditorAssemblyMarker.MenuRoot + "ProcGen/Test Primitives")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var items = CreateItems();
            var material = MaterialsBuilder.World;

            GeneratedAssets.EnsureFolder("Assets/_Project/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Пол-сугроб.
            var ground = Primitives.Grid(80f, 30f, 40, 15, PaletteAsset.Get(PaletteColor.Snow));
            Deform.NoiseAlongNormal(ground, new Noise3(7), 0.35f, 0.15f, 2);
            Place("Ground", ground, new Vector3(Spacing * 3.5f, -0.05f, 4f), material);

            var perRow = new Dictionary<int, int>();
            foreach (var it in items)
            {
                perRow.TryGetValue(it.row, out int col);
                perRow[it.row] = col + 1;
                var b = it.shape.ComputeBounds();
                // Ставим на пол: низ габаритов на y = 0.
                var pos = new Vector3(col * Spacing, -b.min.y, it.row * Spacing * 1.6f);
                Place(it.name, it.shape, pos, it.material != null ? it.material : material);
            }

            // Тёплый точечный свет у «светящейся» капсулы — проверка доп. источников (Forward+).
            var warm = new GameObject("Warm Point Light");
            warm.transform.position = new Vector3(6 * Spacing, 1.5f, Spacing * 1.6f - 1.2f);
            var pl = warm.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = PaletteAsset.Get(PaletteColor.WarmGlow);
            pl.intensity = 6f;
            pl.range = 6f;
            pl.shadows = LightShadows.Soft;
            EnsureComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(warm);

            // Временный пост для проверки свечения (настоящий — на этапе 9).
            var profilePath = "Assets/_Project/Scenes/Test/ProcGenTest_Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
                bloom.intensity.Override(1.2f);
                bloom.threshold.Override(1.0f);
                EditorUtility.SetDirty(profile);
            }
            var volGo = new GameObject("Test Volume");
            var vol = volGo.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.88f);

            var cam = Camera.main;
            cam.transform.position = new Vector3(Spacing * 3.5f, 9f, -13f);
            cam.transform.LookAt(new Vector3(Spacing * 3.5f, 0.5f, 3f));
            EnsureComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(cam.gameObject);
            var light = Object.FindAnyObjectByType<Light>();
            EnsureComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(light.gameObject);
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.34f, 0.42f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[The Last Ride] ProcGen test scene built: {items.Count} objects. Determinism: {(CheckDeterminism() ? "OK" : "FAILED")}");
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void Place(string name, Shape shape, Vector3 position, Material material)
        {
            var mb = new MeshBuilder().Add(shape);
            var mesh = GeneratedAssets.SaveMesh(mb, "Test/" + name);
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static List<Item> CreateItems()
        {
            Color rock = PaletteAsset.Get(PaletteColor.RockLight);
            Color rockDark = PaletteAsset.Get(PaletteColor.RockDark);
            Color wood = PaletteAsset.Get(PaletteColor.WoodLight);
            Color woodDark = PaletteAsset.Get(PaletteColor.WoodDark);
            Color rust = PaletteAsset.Get(PaletteColor.Rust);
            Color metal = PaletteAsset.Get(PaletteColor.MetalDark);
            Color body = PaletteAsset.Get(PaletteColor.RobotBody);
            Color pine = PaletteAsset.Get(PaletteColor.Pine);
            Color fabric = PaletteAsset.Get(PaletteColor.Fabric);
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            Color concrete = PaletteAsset.Get(PaletteColor.Concrete);
            Color snow = PaletteAsset.Get(PaletteColor.Snow);

            var list = new List<Item>();
            void Add(string n, Shape s, int row, Material m = null) => list.Add(new Item { name = n, shape = s, row = row, material = m });

            // ---- Ряд 0: чистые примитивы ----
            Add("Box", Primitives.Box(new Vector3(2f, 1.5f, 2f), wood), 0);
            Add("BeveledBox", Primitives.BeveledBox(new Vector3(2.4f, 1.6f, 1.8f), 0.25f, rust), 0);
            Add("Wedge", Primitives.Wedge(new Vector3(2f, 1.4f, 2f), woodDark), 0);
            Add("Cylinder", Primitives.Cylinder(0.8f, 0.8f, 1.8f, 8, metal), 0);
            Add("Frustum", Primitives.Cylinder(1f, 0.5f, 1.6f, 6, concrete), 0);
            Add("Cone", Primitives.Cylinder(1f, 0f, 2f, 7, pine), 0);
            Add("Icosphere0", Primitives.Icosphere(1f, 0, rock), 0);
            Add("Icosphere2", Primitives.Icosphere(1f, 2, body), 0);

            // ---- Ряд 1: составные и деформированные ----
            Add("Capsule", Primitives.Capsule(0.6f, 2f, 8, 3, body), 1);
            Add("Torus", Primitives.Torus(0.8f, 0.3f, 12, 6, metal), 1);

            var flag = Primitives.ExtrudeConvex(new[] { new Vector2(0f, 0f), new Vector2(1.2f, 0.3f), new Vector2(0f, 0.6f) }, 0.04f, fabric, Surface.NoSnow);
            var pole = Primitives.Cylinder(0.05f, 0.05f, 2f, 5, woodDark);
            pole.Append(flag, Matrix4x4.Translate(new Vector3(0.05f, 1.3f, 0f)));
            Add("Flag", pole, 1);

            var rockShape = Primitives.Icosphere(1.1f, 1, rock);
            Deform.NoiseAlongNormal(rockShape, new Noise3(42), 0.35f, 1.3f, 2);
            rockShape.Scale(new Vector3(1.3f, 0.8f, 1f));
            Deform.PaintWhere(rockShape, n => n.y < -0.3f, rockDark);
            Add("Rock_Noise", rockShape, 1);

            var tree = new Shape();
            var tierNoise = new Noise3(5);
            for (int i = 0; i < 4; i++)
            {
                float r = 1.2f - i * 0.25f;
                var tier = Primitives.Cylinder(r, 0f, 1.2f, 7, pine);
                Deform.NoisePerAxis(tier, tierNoise, new Vector3(0.12f, 0.08f, 0.12f), 1.7f, new Vector3(i * 3.1f, 0f, 0f));
                tree.Append(tier, Matrix4x4.Translate(Vector3.up * (0.6f + i * 0.7f)));
            }
            tree.Append(Primitives.Cylinder(0.18f, 0.14f, 0.8f, 5, woodDark));
            Deform.Bend(tree, 12f, 4f);
            Add("Tree_Bent", tree, 1);

            var ruin = Primitives.BeveledBox(new Vector3(2.4f, 1.8f, 2f), 0.08f, concrete);
            Deform.PunchFaces(ruin, new Rng(3), 0.45f, n => Mathf.Abs(n.y) < 0.5f);
            ruin.MakeDoubleSided(PaletteAsset.Get(PaletteColor.RockDark));
            Add("Ruin_Punched", ruin, 1);

            var lantern = Primitives.Capsule(0.5f, 1.2f, 6, 2, glow, Surface.Glow(1f));
            Add("Glow_Capsule", lantern, 1, MaterialsBuilder.Glow);

            // Тот же корпус, что и Capsule, но с «заметённым» материалом — как у замёрзших роботов.
            Add("Capsule_Frosted", Primitives.Capsule(0.6f, 2f, 8, 3, body), 1, MaterialsBuilder.Frosted);

            var drift = Primitives.Grid(3f, 3f, 8, 8, snow);
            Deform.NoiseAlongNormal(drift, new Noise3(11), 0.5f, 0.8f, 2);
            Deform.Taper(drift, -1f, 1f, 1f, 1f);
            Add("Drift_Grid", drift, 1);

            return list;
        }

        /// <summary>Два прогона с одинаковым seed должны дать побайтно одинаковые вершины.</summary>
        private static bool CheckDeterminism()
        {
            Mesh Make()
            {
                var s = Primitives.Icosphere(1f, 2, Color.gray);
                Deform.NoiseAlongNormal(s, new Noise3(1234), 0.3f, 1.5f, 3);
                Deform.Jitter(s, new Rng(99), 0.05f);
                Deform.PunchFaces(s, new Rng(7), 0.2f);
                return new MeshBuilder().Add(s).ToMesh("det");
            }
            var a = Make().vertices;
            var b = Make().vertices;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
