using LastRide.Core;
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
            BuildRobot();
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
            PrefabUtility.SaveAsPrefabAsset(root, VanPath);
            PrefabUtility.UnloadPrefabContents(root);
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
