using LastRide.ProcGen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastRide.Editor
{
    /// <summary>
    /// Тестовая площадка для фургона (подзадача 3.1): «The Last Ride/Test/Build Van Test Track».
    /// Поле 240×240 м: длинная прямая, змейка из камней, участок льда, участок глубокого снега,
    /// «лежачие полицейские», холм с пологим и крутым склоном, бортики-сугробы и невидимые стены по краю.
    /// Зоны поверхностей — объекты Zone_Ice / Zone_DeepSnow с триггер-коллайдерами
    /// (логику поверхности на них навесит подзадача 3.3).
    /// </summary>
    internal static class TestTrackBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Test/VanTestTrack.unity";
        public const string SpawnName = "VanSpawn";
        public const string IceZoneName = "Zone_Ice";
        public const string DeepSnowZoneName = "Zone_DeepSnow";

        private const float Size = 240f;

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Test/Build Van Test Track")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (PrefabBuilder.Load("Vehicles/Van") == null) ShowcaseBuilder.GenerateAll();

            GeneratedAssets.EnsureFolder("Assets/_Project/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("TestTrack").transform;
            var world = MaterialsBuilder.World;

            Color snow = PaletteAsset.Get(PaletteColor.Snow);
            Color ice = PaletteAsset.Get(PaletteColor.Ice);

            // ---- Земля: почти ровная, лёгкая неровность для работы подвески ----
            var ground = Primitives.Grid(Size, Size, 80, 80, snow);
            Deform.NoiseAlongNormal(ground, new Noise3(101), 0.08f, 0.08f, 2);
            Static(MeshObject(root, "Ground", ground, "Track/Ground", world, collider: true));

            // ---- Длинная прямая: колея (чуть темнее) вдоль оси Z ----
            var strip = Primitives.Box(new Vector3(8f, 0.02f, 180f), PaletteAsset.Get(PaletteColor.SnowShadow), Surface.NoSnow);
            Static(MeshObject(root, "Straight", strip, "Track/Straight", world, collider: false)).transform.position = new Vector3(0f, 0.02f, 0f);

            // ---- Змейка из камней справа от прямой ----
            for (int i = 0; i < 8; i++)
            {
                var rock = PlacePrefab(root, i % 2 == 0 ? "Nature/Rock_S0" : "Nature/Rock_S1",
                    new Vector3(20f + (i % 2 == 0 ? -2.5f : 2.5f), 0f, -60f + i * 14f), i * 40f);
                rock.name = "Slalom_" + i;
            }

            // ---- Лёд: 30×24 м, слева от прямой ----
            var iceCenter = new Vector3(-24f, 0f, -30f);
            var iceSlab = Primitives.Box(new Vector3(30f, 0.04f, 24f), ice, Surface.NoSnow);
            Static(MeshObject(root, "IceSurface", iceSlab, "Track/Ice", world, collider: false)).transform.position = iceCenter + Vector3.up * 0.03f;
            Zone(root, IceZoneName, iceCenter, new Vector3(30f, 3f, 24f), LastRide.Core.SurfaceType.Ice);

            // ---- Глубокий снег: 30×24 м, бугристый, без коллайдера (фургон «вязнет», а не упирается) ----
            var deepCenter = new Vector3(-24f, 0f, 20f);
            var deep = Primitives.Grid(30f, 24f, 20, 16, snow);
            Deform.NoiseAlongNormal(deep, new Noise3(55), 0.18f, 0.35f, 2);
            deep.Translate(Vector3.up * 0.18f);
            Static(MeshObject(root, "DeepSnowSurface", deep, "Track/DeepSnow", world, collider: false)).transform.position = deepCenter;
            Zone(root, DeepSnowZoneName, deepCenter, new Vector3(30f, 3f, 24f), LastRide.Core.SurfaceType.DeepSnow);

            // ---- «Лежачие полицейские» на прямой ----
            for (int i = 0; i < 3; i++)
            {
                var bump = Primitives.Cylinder(0.25f, 0.25f, 7f, 10, PaletteAsset.Get(PaletteColor.RockLight)).Translate(new Vector3(0f, -3.5f, 0f));
                bump.Rotate(new Vector3(0f, 0f, 90f));
                var go = Static(MeshObject(root, "Bump_" + i, bump, "Track/Bump", world, collider: true));
                go.transform.position = new Vector3(0f, 0f, 40f + i * 10f);
            }

            // ---- Холм: пологий (~10°) и крутой (~20°) склоны ----
            Hill(root, "Hill_Gentle", new Vector3(40f, 0f, 40f), new Vector3(16f, 3.5f, 20f), world);
            Hill(root, "Hill_Steep", new Vector3(62f, 0f, 40f), new Vector3(16f, 6f, 16f), world);
            // Ровно 14° — максимальный уклон дороги на уровне: должен браться с места.
            Hill(root, "Hill_Max14", new Vector3(84f, 0f, 40f), new Vector3(16f, 16f * Mathf.Tan(14f * Mathf.Deg2Rad), 16f), world);

            // ---- Ориентиры: ёлки, столб ----
            var rng = new Rng(9);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rng.Range(85f, 105f);
                PlacePrefab(root, "Nature/Tree_" + (i % 4), pos, rng.Range(0f, 360f));
            }
            PlacePrefab(root, "Nature/PowerPole", new Vector3(6f, 0f, -20f), 0f);

            // ---- Бортики-сугробы и невидимые стены по краю ----
            float edge = Size * 0.5f - 6f;
            for (int side = 0; side < 4; side++)
            {
                var dir = side switch { 0 => Vector3.forward, 1 => Vector3.right, 2 => Vector3.back, _ => Vector3.left };
                var along = Vector3.Cross(Vector3.up, dir);
                for (int k = -11; k <= 11; k++)
                {
                    var drift = Primitives.Icosphere(1f, 1, snow);
                    Deform.NoiseAlongNormal(drift, new Noise3(side * 31 + k + 50), 0.2f, 1.2f, 2);
                    drift.Scale(new Vector3(6f, 1.6f, 3f));
                    var go = Static(MeshObject(root, $"Drift_{side}_{k}", drift, $"Track/Drift_{side}_{k + 11}", world, collider: false));
                    go.transform.position = dir * (edge + 2f) + along * (k * 10f);
                    go.transform.rotation = Quaternion.LookRotation(dir);
                }
                var wall = new GameObject("Wall_" + side);
                wall.transform.SetParent(root, false);
                wall.transform.position = dir * edge + Vector3.up * 2f;
                wall.transform.rotation = Quaternion.LookRotation(dir);
                wall.AddComponent<BoxCollider>().size = new Vector3(Size, 4f, 1f);
            }

            // ---- Точка спавна и фургон ----
            var spawn = new GameObject(SpawnName);
            spawn.transform.SetParent(root, false);
            spawn.transform.position = new Vector3(0f, 0.2f, -80f);
            GameplayPrefabsBuilder.BuildAll();
            var van = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Van);
            van.transform.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);
            van.name = "Van";
            var bootstrap = new GameObject("TestBootstrap");
            bootstrap.AddComponent<LastRide.DebugTools.TestSceneBootstrap>().startMode = LastRide.Core.PlayerMode.InVan;
            bootstrap.AddComponent<LastRide.DebugTools.VanTestDriver>().van = van.GetComponent<LastRide.Vehicle.VanController>();

            // Робот рядом с фургоном (активен, только когда режим игрока — пешком).
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Robot);
            robot.name = "Robot";
            robot.transform.SetPositionAndRotation(spawn.transform.position + new Vector3(4f, 0.1f, 0f), Quaternion.identity);
            bootstrap.AddComponent<LastRide.DebugTools.RobotTestDriver>().robot = robot.GetComponent<LastRide.Player.RobotController>();
            var boarding = bootstrap.AddComponent<LastRide.Player.VanBoarding>();
            boarding.van = van.GetComponent<LastRide.Vehicle.VanController>();
            boarding.robot = robot.GetComponent<LastRide.Player.RobotController>();
            bootstrap.AddComponent<LastRide.UI.InteractionPromptView>();
            bootstrap.AddComponent<LastRide.UI.StovePanel>();
            bootstrap.AddComponent<LastRide.UI.Hud>();

            // Предметы для проверки луча и подсказок «[E] …» (настоящий подбор — в 5.1).
            var segment = new GameObject("PickupSegment_Test");
            segment.transform.SetParent(root, false);
            segment.AddComponent<LastRide.Fire.PickupSegment>().segmentId = "test";
            var fuelSpots = new[] { ("planks", new Vector3(-4f, 0.15f, -76f)),
                                    ("kindling", new Vector3(-6f, 0.15f, -79f)),
                                    ("briquette", new Vector3(-3f, 0.15f, -83f)),
                                    ("kindling", new Vector3(-8f, 0.15f, -75f)),
                                    ("planks", new Vector3(-7f, 0.15f, -84f)),
                                    ("kindling", new Vector3(-10f, 0.15f, -80f)) };
            for (int i = 0; i < fuelSpots.Length; i++)
            {
                var (fuelId, pos) = fuelSpots[i];
                var item = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Fuel(fuelId), segment.transform);
                item.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 20f + i * 47f, 0f));
                item.name = $"{fuelId}_{i}";
                var pickup = item.GetComponent<LastRide.Fire.FuelPickup>();
                pickup.pickupId = $"test_{fuelId}_{i}";
                pickup.optional = true;
            }

            // ---- Камера, свет (тёплый закат акта 1), туман ----
            var cam = Camera.main;
            Ensure<UniversalAdditionalCameraData>(cam.gameObject).renderPostProcessing = true;
            cam.farClipPlane = 600f;
            CameraRigBuilder.SetupBrain(cam);
            CameraRigBuilder.CreateVanCamera(van.GetComponent<LastRide.Vehicle.VanController>());
            var sun = Object.FindAnyObjectByType<Light>();
            Ensure<UniversalAdditionalLightData>(sun.gameObject);
            sun.transform.rotation = Quaternion.Euler(18f, -40f, 0f);
            sun.color = new Color(1f, 0.78f, 0.55f);
            sun.intensity = 1.2f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.66f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.52f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.38f, 0.5f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.8f, 0.76f, 0.82f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Van test track built: " + ScenePath);
        }

        // ------------------------------------------------------------------

        private static void Hill(Transform root, string name, Vector3 position, Vector3 size, Material mat)
        {
            // Холм = два клина спина к спине (подъём и спуск) + плато.
            var s = new Shape();
            var up = Primitives.Wedge(new Vector3(size.x, size.y, size.z), PaletteAsset.Get(PaletteColor.Snow));
            s.Append(up, Matrix4x4.TRS(new Vector3(0f, size.y * 0.5f, -size.z * 0.5f - 3f), Quaternion.Euler(0f, 180f, 0f), Vector3.one));
            s.Append(Primitives.Box(new Vector3(size.x, size.y, 6f), PaletteAsset.Get(PaletteColor.Snow)), Matrix4x4.Translate(new Vector3(0f, size.y * 0.5f, 0f)));
            s.Append(up.Clone(), Matrix4x4.Translate(new Vector3(0f, size.y * 0.5f, size.z * 0.5f + 3f)));
            Static(MeshObject(root, name, s, "Track/" + name, mat, collider: true)).transform.position = position;
        }

        private static void Zone(Transform root, string name, Vector3 center, Vector3 size, LastRide.Core.SurfaceType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            // Зона чуть уходит под землю, чтобы точки контакта колёс/ног точно в неё попадали.
            go.transform.position = center + Vector3.up * (size.y * 0.5f - 0.5f);
            go.layer = 2; // Ignore Raycast — лучи подвески и прицела сквозь зону
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            go.AddComponent<LastRide.Core.SurfaceZone>().type = type;
        }

        private static GameObject MeshObject(Transform parent, string name, Shape shape, string meshPath, Material mat, bool collider)
        {
            var mesh = GeneratedAssets.SaveMesh(new MeshBuilder().Add(shape), meshPath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static GameObject PlacePrefab(Transform parent, string prefabPath, Vector3 position, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(PrefabBuilder.Load(prefabPath), parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        private static GameObject Static(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
