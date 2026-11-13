using LastRide.ProcGen;
using LastRide.ProcGen.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastRide.Editor
{
    /// <summary>
    /// «The Last Ride/Generate All» — полный цикл процедурных ассетов:
    /// конфиги (палитра) → материалы → меши → префабы → сцена-витрина.
    /// «The Last Ride/ProcGen/Build Showcase» — только витрина из уже собранных префабов.
    /// </summary>
    internal static class ShowcaseBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Test/Showcase.unity";
        private const string VolumePath = "Assets/_Project/Scenes/Test/ProcGenTest_Volume.asset";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Generate All", priority = 0)]
        public static void GenerateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var start = System.DateTime.Now;
            ConfigAssetsBuilder.CreateAll();
            MaterialsBuilder.CreateAll();
            ModelLibrary.GenerateAll();
            PrefabBuilder.BuildAll();
            BuildScene();
            Debug.Log($"[The Last Ride] Generate All done in {(System.DateTime.Now - start).TotalSeconds:0.0} s");
        }

        [MenuItem(EditorAssemblyMarker.MenuRoot + "ProcGen/Build Showcase")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        private static void BuildScene()
        {
            GeneratedAssets.EnsureFolder("Assets/_Project/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("Showcase").transform;

            // Пол.
            var ground = Primitives.Grid(90f, 60f, 45, 30, PaletteAsset.Get(PaletteColor.Snow));
            Deform.NoiseAlongNormal(ground, new Noise3(3), 0.15f, 0.2f, 2);
            ground.Translate(new Vector3(5f, 0f, 15f));
            var groundMesh = GeneratedAssets.SaveMesh(new MeshBuilder().Add(ground), "Test/ShowcaseGround");
            var g = new GameObject("Ground");
            g.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = MaterialsBuilder.World;
            g.AddComponent<MeshCollider>().sharedMesh = groundMesh;

            // ---- Фургон, робот, лампа, банка, руки ----
            Place(root, "Vehicles/Van", new Vector3(-1.5f, 0f, 1f), 35f);
            Place(root, "Characters/Robot", new Vector3(2.6f, 0f, -0.5f), 200f);
            Place(root, "Props/Lamp", new Vector3(3.4f, 0f, -1.6f), 0f, 2f);
            Place(root, "Props/SparkJar", new Vector3(3.9f, 0f, -1.9f), 0f, 2f);
            Place(root, "Characters/FPArms", new Vector3(1.2f, 0.9f, -2.3f), 160f, 2f);

            // ---- Замёрзшие роботы, погасшая лампа, топливо, цветок ----
            Place(root, "Buildings/FrozenRobot_Sitting", new Vector3(4.6f, 0f, 0.4f), 160f);
            Place(root, "Buildings/FrozenRobot_Lying", new Vector3(6.6f, 0f, 0.8f), 200f);
            Place(root, "Buildings/FrozenRobot_Reaching", new Vector3(8.8f, 0f, 0.6f), 190f);
            Place(root, "Props/Lamp_Dead", new Vector3(5.4f, 0f, -0.2f), 30f);
            string[] fuels = { "Fuel_Kindling", "Fuel_Planks", "Fuel_Briquette" };
            for (int i = 0; i < fuels.Length; i++) Place(root, "Fuel/" + fuels[i], new Vector3(5.2f + i * 1.1f, 0f, -2.2f), 20f + i * 15f);
            Place(root, "Props/Flower", new Vector3(8.5f, 0.03f, -3.2f), 0f, 2f);

            // ---- Окружение ----
            Place(root, "Buildings/Workshop", new Vector3(-14f, 0f, 16f), 20f);
            Place(root, "Buildings/Shed", new Vector3(-5f, 0f, 13f), -15f);
            Place(root, "Buildings/Ruin_0", new Vector3(2f, 0f, 16f), 10f);
            Place(root, "Buildings/Ruin_1", new Vector3(9.5f, 0f, 17f), -20f);
            Place(root, "Buildings/Ruin_2", new Vector3(17f, 0f, 15f), 35f);
            Place(root, "Buildings/TruckWreck", new Vector3(25f, 0f, 12f), 70f);
            for (int i = 0; i < 4; i++) Place(root, "Nature/Tree_" + i, new Vector3(-12f + i * 6f, 0f, 28f), i * 47f);
            string[] rocks = { "Rock_S0", "Rock_S1", "Rock_M0", "Rock_M1", "Rock_L0", "Rock_L1" };
            for (int i = 0; i < rocks.Length; i++) Place(root, "Nature/" + rocks[i], new Vector3(14f + i * 4.5f, 0f, 28f), i * 30f);
            Place(root, "Nature/PowerPole", new Vector3(10f, 0f, 6f), 0f);
            var fallen = Place(root, "Nature/PowerPole", new Vector3(14f, 0.1f, 6f), 30f);
            fallen.transform.rotation = Quaternion.Euler(0f, 30f, 84f);
            for (int i = 0; i < 4; i++) Place(root, "Nature/Marker_" + (i % 2), new Vector3(7f + i * 1.2f, 0f, 3f), i * 25f);

            // ---- Маяк: нижние 3 ряда огней и фонарь включены для демонстрации ----
            var beacon = Place(root, "Beacon/Beacon", new Vector3(40f, 0f, 70f), 200f);
            for (int i = 0; i < 3; i++)
                beacon.transform.Find(Anchors.BeaconLightRowPrefix + i).GetComponent<MeshRenderer>().sharedMaterial = MaterialsBuilder.Glow;
            beacon.transform.Find(Anchors.BeaconLantern).GetComponent<MeshRenderer>().sharedMaterial = MaterialsBuilder.Glow;

            // ---- Камера, свет, пост ----
            var cam = Camera.main;
            Ensure<UniversalAdditionalCameraData>(cam.gameObject).renderPostProcessing = true;
            cam.transform.position = new Vector3(4f, 9f, -9f);
            cam.transform.LookAt(new Vector3(5f, 0.5f, 8f));
            cam.fieldOfView = 55f;
            cam.farClipPlane = 500f;
            var sun = Object.FindAnyObjectByType<Light>();
            Ensure<UniversalAdditionalLightData>(sun.gameObject);
            sun.transform.rotation = Quaternion.Euler(30f, -35f, 0f);
            sun.color = new Color(1f, 0.82f, 0.65f);

            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(VolumePath);
            if (profile != null)
            {
                var vol = new GameObject("Volume").AddComponent<UnityEngine.Rendering.Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.34f, 0.42f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.88f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[The Last Ride] Showcase built: " + ScenePath);
        }

        private static GameObject Place(Transform parent, string prefabPath, Vector3 position, float yaw, float scale = 1f)
        {
            var prefab = PrefabBuilder.Load(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Showcase] Prefab not found: " + prefabPath);
                return new GameObject("MISSING " + prefabPath);
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
