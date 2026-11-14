using LastRide.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace LastRide.Editor
{
    /// <summary>
    /// Временная сцена для проверки рельефа и дороги (7.2): рельеф по оси пути + фургон и робот на старте.
    /// Настоящую сцену Game собирает «Build Level» в 7.7.
    /// </summary>
    internal static class TerrainTestBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Test/TerrainTest.unity";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Test/Build Terrain Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null) { Debug.LogError("Нет RoadPath."); return; }
            if (PrefabBuilder.Load("Vehicles/Van") == null) ShowcaseBuilder.GenerateAll();

            GeneratedAssets.EnsureFolder("Assets/_Project/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (LevelBuilder.Populate(false) == null) return;

            var start = path.Evaluate(8f);
            var rot = Quaternion.LookRotation(start.flatTangent, Vector3.up);
            GameplayPrefabsBuilder.BuildAll();
            var van = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Van);
            van.transform.SetPositionAndRotation(start.position + Vector3.up * 0.3f, rot);
            van.name = "Van";
            var vanCtrl = van.GetComponent<LastRide.Vehicle.VanController>();

            var bootstrap = new GameObject("TestBootstrap");
            bootstrap.AddComponent<LastRide.DebugTools.TestSceneBootstrap>().startMode = LastRide.Core.PlayerMode.InVan;
            bootstrap.AddComponent<LastRide.DebugTools.VanTestDriver>().van = vanCtrl;

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Robot);
            robot.name = "Robot";
            robot.transform.SetPositionAndRotation(start.position + start.right * 3.5f + Vector3.up * 0.2f, rot);
            var robotCtrl = robot.GetComponent<LastRide.Player.RobotController>();
            bootstrap.AddComponent<LastRide.DebugTools.RobotTestDriver>().robot = robotCtrl;
            var boarding = bootstrap.AddComponent<LastRide.Player.VanBoarding>();
            boarding.van = vanCtrl;
            boarding.robot = robotCtrl;
            bootstrap.AddComponent<LastRide.UI.InteractionPromptView>();
            bootstrap.AddComponent<LastRide.UI.StovePanel>();
            bootstrap.AddComponent<LastRide.UI.Hud>();

            var cam = Camera.main;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.farClipPlane = 1500f;
            CameraRigBuilder.SetupBrain(cam);
            CameraRigBuilder.CreateVanCamera(vanCtrl);
            var sun = Object.FindAnyObjectByType<Light>();
            if (sun.GetComponent<UniversalAdditionalLightData>() == null) sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            sun.transform.rotation = Quaternion.Euler(18f, -40f, 0f);
            sun.color = new Color(1f, 0.78f, 0.55f);
            sun.intensity = 1.2f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.66f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.52f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.38f, 0.5f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.003f;
            RenderSettings.fogColor = new Color(0.8f, 0.76f, 0.82f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Terrain test scene built: " + ScenePath);
        }
    }
}
