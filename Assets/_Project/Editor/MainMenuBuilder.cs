using LastRide.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

namespace LastRide.Editor
{
    /// <summary>
    /// 11.3: пересобирает сцену MainMenu — диорама заснеженной поляны (рельеф, ёлки, скалы, мастерская, фургон, банка с тлеющей Искрой),
    /// облёт камеры и интерфейс меню. Меню: «The Last Ride/Level/Build Main Menu». Результат детерминирован (фиксированное зерно).
    /// </summary>
    internal static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        public const string RootName = "MenuWorld";
        private const string GroundMeshPath = "Meshes/MenuGround";
        private const float GroundSize = 280f;
        private const int Grid = 110;

        /// <summary>Высота рельефа диорамы в точке (плоская поляна в центре, холмы по краям).</summary>
        private static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 55f, r));
            float n = Mathf.PerlinNoise(x * 0.035f + 40f, z * 0.035f + 40f) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(x * 0.11f + 7f, z * 0.11f + 7f) * 2f - 1f;
            return (n * 9f + n2 * 0.9f) * k + (n2 * 0.12f) * (1f - k);
        }

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Main Menu")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var name in new[] { RootName, "MainMenu" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            var root = new GameObject(RootName).transform;
            BuildGround(root);
            Vector3 jarPos = new Vector3(0f, Height(0f, 0f), 0f);
            BuildProps(root, jarPos);

            var cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var fly = cam.GetComponent<MenuCameraFlyover>();
                if (fly == null) fly = cam.gameObject.AddComponent<MenuCameraFlyover>();
                fly.target = jarPos + Vector3.up * 0.9f;
                cam.farClipPlane = 600f;
            }

            var ui = new GameObject("MainMenu");
            ui.AddComponent<MainMenuView>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Сцена MainMenu пересобрана.");
        }

        private static void BuildGround(Transform root)
        {
            var verts = new Vector3[(Grid + 1) * (Grid + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            float step = GroundSize / Grid;
            for (int j = 0; j <= Grid; j++)
            for (int i = 0; i <= Grid; i++)
            {
                float x = -GroundSize * 0.5f + i * step;
                float z = -GroundSize * 0.5f + j * step;
                int idx = j * (Grid + 1) + i;
                verts[idx] = new Vector3(x, Height(x, z), z);
                float tint = 0.9f + Mathf.PerlinNoise(x * 0.2f, z * 0.2f) * 0.1f;
                cols[idx] = new Color(0.84f * tint, 0.90f * tint, 0.98f * tint, 1f);
                uvs[idx] = new Vector2(1f, 0f); // uv.x шейдера мира = «снегоспособность» поверхности
            }
            var tris = new int[Grid * Grid * 6];
            int t = 0;
            for (int j = 0; j < Grid; j++)
            for (int i = 0; i < Grid; i++)
            {
                int a = j * (Grid + 1) + i, b = a + 1, c = a + Grid + 1, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }

            string path = GeneratedAssets.Root + GroundMeshPath + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "MenuGround";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew)
            {
                GeneratedAssets.EnsureFolder(System.IO.Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject("Ground");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsBuilder.WorldPath);
        }

        private static void BuildProps(Transform root, Vector3 jarPos)
        {
            var rng = new Random(11);

            // Ёлки: кольцами вокруг поляны, чем дальше — тем гуще.
            var trees = new GameObject[4];
            for (int i = 0; i < trees.Length; i++) trees[i] = PrefabBuilder.Load("Nature/Tree_" + i);
            for (int n = 0; n < 120; n++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float rad = 15f + (float)Mathf.Pow((float)rng.NextDouble(), 0.7f) * 85f;
                float x = Mathf.Cos(ang) * rad, z = Mathf.Sin(ang) * rad;
                Place(root, trees[rng.Next(trees.Length)], new Vector3(x, Height(x, z), z), (float)rng.NextDouble() * 360f,
                    0.8f + (float)rng.NextDouble() * 0.7f, "Tree");
            }

            // Скалы.
            var rocks = new[] { "Nature/Rock_L0", "Nature/Rock_L1", "Nature/Rock_M0", "Nature/Rock_M1", "Nature/Rock_S0", "Nature/Rock_S1" };
            for (int n = 0; n < 16; n++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float rad = 9f + (float)rng.NextDouble() * 60f;
                float x = Mathf.Cos(ang) * rad, z = Mathf.Sin(ang) * rad;
                Place(root, PrefabBuilder.Load(rocks[rng.Next(rocks.Length)]), new Vector3(x, Height(x, z) - 0.15f, z),
                    (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble() * 0.6f, "Rock");
            }

            // Мастерская на отдалении, фургон у костра-банки.
            Place(root, PrefabBuilder.Load("Buildings/Workshop"), new Vector3(-26f, Height(-26f, 22f), 22f), 150f, 1f, "Workshop");
            Vector3 vanPos = new Vector3(-8.5f, Height(-8.5f, -4.5f), -4.5f);
            Place(root, PrefabBuilder.Load("Vehicles/Van"), vanPos, 38f, 1f, "Van");

            // Робот-игрок стоит перед Искрой и смотрит на неё.
            Vector3 robotPos = new Vector3(-2.2f, Height(-2.2f, 1.6f), 1.6f);
            Vector3 toJar = jarPos - robotPos;
            Place(root, PrefabBuilder.Load("Characters/Robot"), robotPos, Mathf.Atan2(toJar.x, toJar.z) * Mathf.Rad2Deg, 1f, "Robot");

            var jar = Place(root, PrefabBuilder.Load("Props/SparkJar"), jarPos, 0f, 1f, "SparkJar");
            if (jar != null)
            {
                var ember = jar.AddComponent<MenuEmber>();
                var glow = jar.GetComponentInChildren<Light>(true);
                ember.glow = glow;
                if (glow != null)
                {
                    glow.range = Mathf.Max(glow.range, 10f);
                    ember.baseIntensity = Mathf.Max(glow.intensity, 2.2f);
                }
            }
        }

        private static GameObject Place(Transform parent, GameObject prefab, Vector3 pos, float yaw, float scale, string name)
        {
            if (prefab == null) { Debug.LogWarning("[MainMenuBuilder] Нет префаба для " + name + " — сначала «Generate All»."); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
    }
}
