using LastRide.Core;
using LastRide.ProcGen.Models;
using LastRide.Vehicle;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Игровые префабы — Prefab Variant-ы от сгенерированных моделей (Generated/Prefabs).
    /// Вариант хранит игровые компоненты и их настройки; при перегенерации модели
    /// вариант подхватывает новый меш и сохраняет свои компоненты.
    /// Меню: «The Last Ride/Setup/Build Gameplay Prefabs». Существующие варианты не пересоздаются —
    /// только добавляются недостающие компоненты.
    /// </summary>
    internal static class GameplayPrefabsBuilder
    {
        public const string Root = "Assets/_Project/Prefabs/";
        public const string VanPath = Root + "Van.prefab";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Build Gameplay Prefabs")]
        public static void BuildAll()
        {
            if (PrefabBuilder.Load("Vehicles/Van") == null) PrefabBuilder.BuildAll();
            GeneratedAssets.EnsureFolder(Root.TrimEnd('/'));
            BuildVan();
            BuildLamp(); // раньше робота: лампа в руке (HeldLamp) — вложенный экземпляр префаба Lamp
            BuildRobot();
            BuildSparkJar();
            foreach (var id in FuelIds) BuildFuel(id);
            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Gameplay prefabs ready: " + Root);
        }

        public static GameObject Van => AssetDatabase.LoadAssetAtPath<GameObject>(VanPath);

        private static void BuildVan()
        {
            if (Van == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(PrefabBuilder.Load("Vehicles/Van"));
                instance.name = "Van";
                PrefabUtility.SaveAsPrefabAsset(instance, VanPath); // экземпляр префаба → сохраняется как Variant
                Object.DestroyImmediate(instance);
            }

            var root = PrefabUtility.LoadPrefabContents(VanPath);
            SetLayerRecursive(root, LayerMask.NameToLayer(Layers.Vehicle));
            if (root.GetComponent<Rigidbody>() == null) root.AddComponent<Rigidbody>();
            if (root.GetComponent<VanController>() == null) root.AddComponent<VanController>();
            if (root.GetComponent<LastRide.Narrative.StoveSocket>() == null) root.AddComponent<LastRide.Narrative.StoveSocket>(); // пролог: поставить Искру в печку
            // Печка: свет в окне, свечение стекла и искры из трубы зависят от запаса огня.
            AttachSparkGlow(root, LastRide.Core.FireSourceKind.Stove, Anchors.StoveLight, "Window", Anchors.ChimneyTop, new Vector3(0f, 0.05f, 0f), 0.06f);
            PrefabUtility.SaveAsPrefabAsset(root, VanPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>id типов топлива (совпадают с FuelType.id и именами префабов Fuel_*).</summary>
        public static readonly string[] FuelIds = { "kindling", "planks", "briquette" };

        private static string FuelModelName(string id) => id switch
        {
            "kindling" => "Fuel_Kindling",
            "planks" => "Fuel_Planks",
            _ => "Fuel_Briquette"
        };

        public static string FuelPath(string id) => Root + FuelModelName(id) + ".prefab";
        public static GameObject Fuel(string id) => AssetDatabase.LoadAssetAtPath<GameObject>(FuelPath(id));

        /// <summary>Подбираемое топливо: Variant от Generated/Prefabs/Fuel/Fuel_*, слой Interactable, компонент FuelPickup.</summary>
        private static void BuildFuel(string id)
        {
            string path = FuelPath(id);
            if (Fuel(id) == null)
            {
                var src = PrefabBuilder.Load("Fuel/" + FuelModelName(id));
                if (src == null) { Debug.LogError("[The Last Ride] Нет модели топлива " + FuelModelName(id)); return; }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
                instance.name = FuelModelName(id);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            SetLayerRecursive(root, LayerMask.NameToLayer(Layers.Interactable));
            var pickup = root.GetComponent<LastRide.Fire.FuelPickup>();
            if (pickup == null) pickup = root.AddComponent<LastRide.Fire.FuelPickup>();
            var cfg = GameConfig.Load();
            if (cfg != null) pickup.fuel = cfg.GetFuel(id);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        public const string SparkJarPath = Root + "SparkJar.prefab";
        public static GameObject SparkJar => AssetDatabase.LoadAssetAtPath<GameObject>(SparkJarPath);

        /// <summary>Банка с Искрой (пролог): Variant от Props/SparkJar, слой Interactable, компонент SparkJarPickup.</summary>
        private static void BuildSparkJar()
        {
            if (SparkJar == null)
            {
                var src = PrefabBuilder.Load("Props/SparkJar");
                if (src == null) { Debug.LogError("[The Last Ride] Нет модели банки Props/SparkJar."); return; }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
                instance.name = "SparkJar";
                PrefabUtility.SaveAsPrefabAsset(instance, SparkJarPath);
                Object.DestroyImmediate(instance);
            }

            var root = PrefabUtility.LoadPrefabContents(SparkJarPath);
            SetLayerRecursive(root, LayerMask.NameToLayer(Layers.Interactable));
            if (root.GetComponent<LastRide.Narrative.SparkJarPickup>() == null) root.AddComponent<LastRide.Narrative.SparkJarPickup>();
            PrefabUtility.SaveAsPrefabAsset(root, SparkJarPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        public const string LampPath = Root + "Lamp.prefab";
        public static GameObject Lamp => AssetDatabase.LoadAssetAtPath<GameObject>(LampPath);

        /// <summary>Лампа (акт 3): Variant от Props/Lamp с визуалом Искры (свет пламени, стекло, искры).</summary>
        private static void BuildLamp()
        {
            if (Lamp == null)
            {
                var src = PrefabBuilder.Load("Props/Lamp");
                if (src == null) { Debug.LogError("[The Last Ride] Нет модели лампы Props/Lamp."); return; }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
                instance.name = "Lamp";
                PrefabUtility.SaveAsPrefabAsset(instance, LampPath);
                Object.DestroyImmediate(instance);
            }

            var root = PrefabUtility.LoadPrefabContents(LampPath);
            AttachSparkGlow(root, LastRide.Core.FireSourceKind.Lamp, Anchors.FlameLight, "Glass", Anchors.FlameLight, new Vector3(0f, 0.04f, 0f), 0.035f);
            PrefabUtility.SaveAsPrefabAsset(root, LampPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Добавляет (или обновляет ссылки) SparkGlow: свет, стекло и система частиц у якоря.</summary>
        private static void AttachSparkGlow(GameObject root, LastRide.Core.FireSourceKind kind, string lightAnchor, string glassPart,
                                            string sparksAnchor, Vector3 sparksOffset, float sparkSize)
        {
            var glow = root.GetComponent<LastRide.Fire.SparkGlow>();
            if (glow == null) glow = root.AddComponent<LastRide.Fire.SparkGlow>();
            glow.kind = kind;

            var lightT = FindDeep(root.transform, lightAnchor);
            glow.glowLight = lightT != null ? lightT.GetComponent<Light>() : null;

            var glass = FindDeep(root.transform, glassPart);
            glow.glassRenderers = glass != null && glass.GetComponent<Renderer>() != null
                ? new[] { glass.GetComponent<Renderer>() }
                : new Renderer[0];

            var anchor = FindDeep(root.transform, sparksAnchor);
            var sparksT = FindDeep(root.transform, "Sparks_" + kind);
            if (sparksT == null)
            {
                sparksT = new GameObject("Sparks_" + kind).transform;
                sparksT.SetParent(anchor != null ? anchor : root.transform, false);
            }
            sparksT.localPosition = sparksOffset;
            sparksT.localRotation = Quaternion.Euler(-90f, 0f, 0f); // конус смотрит вверх
            var ps = sparksT.GetComponent<ParticleSystem>();
            if (ps == null) ps = sparksT.gameObject.AddComponent<ParticleSystem>();
            ConfigureSparks(ps, sparkSize);
            glow.sparks = ps;

            if (glow.glowLight == null || glow.glassRenderers.Length == 0)
                Debug.LogWarning($"[The Last Ride] SparkGlow {kind}: не найден свет '{lightAnchor}' или стекло '{glassPart}'.");
        }

        private static void ConfigureSparks(ParticleSystem ps, float size)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = -0.05f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // скорость задаёт SparkGlow

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = size * 0.5f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(0.55f, 0.12f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.8f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialsBuilder.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        public const string RobotPath = Root + "Robot.prefab";
        public static GameObject Robot => AssetDatabase.LoadAssetAtPath<GameObject>(RobotPath);

        private static void BuildRobot()
        {
            if (Robot == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(PrefabBuilder.Load("Characters/Robot"));
                instance.name = "Robot";
                PrefabUtility.SaveAsPrefabAsset(instance, RobotPath);
                Object.DestroyImmediate(instance);
            }

            var root = PrefabUtility.LoadPrefabContents(RobotPath);

            // Капсула: низ на земле, глаза на 1.32 м.
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.8f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;

            // Точка глаз с камерой и руками 1-го лица.
            var pivot = root.transform.Find("CameraPivot");
            if (pivot == null)
            {
                pivot = new GameObject("CameraPivot").transform;
                pivot.SetParent(root.transform, false);
            }
            pivot.localPosition = new Vector3(0f, LastRide.ProcGen.Models.RobotModel.Eye.y, 0.12f);

            var cmGo = pivot.Find("RobotCamera");
            if (cmGo == null)
            {
                cmGo = new GameObject("RobotCamera").transform;
                cmGo.SetParent(pivot, false);
            }
            var cm = cmGo.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            if (cm == null) cm = cmGo.gameObject.AddComponent<Unity.Cinemachine.CinemachineCamera>();
            cm.Priority = new Unity.Cinemachine.PrioritySettings { Enabled = true, Value = 20 };
            var lens = cm.Lens;
            lens.FieldOfView = 70f;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 600f;
            cm.Lens = lens;

            if (pivot.Find("FPArms") == null)
            {
                var armsSrc = PrefabBuilder.Load("Characters/FPArms");
                if (armsSrc != null)
                {
                    var arms = (GameObject)PrefabUtility.InstantiatePrefab(armsSrc, pivot);
                    arms.name = "FPArms";
                    arms.transform.localPosition = Vector3.zero;
                    arms.transform.localRotation = Quaternion.identity;
                }
            }

            // Лампа в руке (акт 3): вложенный экземпляр префаба Lamp под точкой глаз, выключена до сцены с лампой (8.5).
            var heldLamp = pivot.Find("HeldLamp");
            if (heldLamp == null && Lamp != null)
            {
                var lampInstance = (GameObject)PrefabUtility.InstantiatePrefab(Lamp, pivot);
                lampInstance.name = "HeldLamp";
                lampInstance.transform.localPosition = new Vector3(0.3f, -0.85f, 0.3f);
                lampInstance.transform.localRotation = Quaternion.identity;
                lampInstance.SetActive(false);
                heldLamp = lampInstance.transform;
            }
            var held = root.GetComponent<LastRide.Player.HeldLamp>();
            if (held == null) held = root.AddComponent<LastRide.Player.HeldLamp>();
            held.lamp = heldLamp;
            held.rightArm = FindDeep(pivot, Anchors.FPArmRight);

            // Тело от 1-го лица не рисуется, но отбрасывает тень.
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                bool arms = r.transform.IsChildOf(pivot);
                r.shadowCastingMode = arms ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            var ctrl = root.GetComponent<LastRide.Player.RobotController>();
            if (ctrl == null) ctrl = root.AddComponent<LastRide.Player.RobotController>();
            ctrl.cameraPivot = pivot;
            ctrl.cinemachineCamera = cm;
            if (root.GetComponent<LastRide.Player.Interactor>() == null) root.AddComponent<LastRide.Player.Interactor>();

            SetLayerRecursive(root, LayerMask.NameToLayer(Layers.Player));
            PrefabUtility.SaveAsPrefabAsset(root, RobotPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }
    }
}
