using LastRide.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastRide.Editor
{
    /// <summary>
    /// 8.4: игрок в сцене Game — фургон на старте, робот у мастерской, банка с Искрой, камеры, UI и запуск пролога.
    /// Всё под корнем <c>PlayerRig</c>; повторный запуск заменяет прежний. Вызывается из «Build Level» после сборки уровня
    /// (нужны <c>POI_Workshop</c> и ось пути) или отдельно: <c>The Last Ride/Level/Build Player Rig</c>.
    /// </summary>
    internal static class PlayerRigBuilder
    {
        public const string RootName = "PlayerRig";

        // Расстояния вдоль оси от начала пути, м.
        private const float VanDistance = 10f;
        private const float JarDistance = 14f;
        private const float RobotDistance = 4f;
        // Между краем дороги и стеной мастерской всего ~3 м — банка у дороги, робот ближе к фургону.
        private const float JarOffsetFromRoadEdge = 1.5f; // только для выбора стороны, куда смотрит робот
        // Банка с Искрой — на столе в гараже (позиция относительно POI_Workshop; задана вручную автором, не менять без нужды).
        private static readonly Vector3 JarLocalInWorkshop = new Vector3(-2.074f, 1.025f, -2.376f);
        private const float JarYawInWorkshop = 295f;
        private const float RobotOffsetFromRoadEdge = 1.2f;

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Player Rig")]
        public static void BuildMenu()
        {
            var root = Rebuild();
            if (root != null) Selection.activeGameObject = root.gameObject;
        }

        public static Transform Rebuild()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null) { Debug.LogError("[PlayerRigBuilder] Нет RoadPath."); return null; }
            var workshop = GameObject.Find("POI_Workshop");
            if (workshop == null) { Debug.LogError("[PlayerRigBuilder] Нет POI_Workshop — сначала Build Level."); return null; }

            GameplayPrefabsBuilder.BuildAll();
            var root = new GameObject(RootName).transform;

            // --- Фургон на дороге у мастерской ---
            var vs = path.Evaluate(VanDistance);
            var vanRot = Quaternion.LookRotation(vs.flatTangent, Vector3.up);
            var van = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Van, root);
            van.name = "Van";
            van.transform.SetPositionAndRotation(vs.position + Vector3.up * 0.3f, vanRot);
            var vanCtrl = van.GetComponent<LastRide.Vehicle.VanController>();

            // --- Банка с Искрой: на столе в гараже ---
            var js = path.Evaluate(JarDistance);
            Vector3 toWorkshop = workshop.transform.position - js.position;
            toWorkshop.y = 0f;
            Vector3 side = toWorkshop.sqrMagnitude > 0.01f ? toWorkshop.normalized : js.right;
            Vector3 jarPos = workshop.transform.TransformPoint(JarLocalInWorkshop);
            var jar = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.SparkJar, root);
            jar.name = "SparkJar";
            jar.transform.SetPositionAndRotation(jarPos, workshop.transform.rotation * Quaternion.Euler(0f, JarYawInWorkshop, 0f));

            // --- Робот на обочине напротив фургона, лицом к банке ---
            var rs = path.Evaluate(RobotDistance);
            Vector3 robotPos = Ground(rs.position + side * (rs.width * 0.5f + RobotOffsetFromRoadEdge), rs.position.y);
            Vector3 look = jarPos - robotPos;
            look.y = 0f;
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(GameplayPrefabsBuilder.Robot, root);
            robot.name = "Robot";
            robot.transform.SetPositionAndRotation(robotPos + Vector3.up * 0.2f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : vs.flatTangent, Vector3.up));
            var robotCtrl = robot.GetComponent<LastRide.Player.RobotController>();

            // --- Системы сцены: посадка, UI, пролог ---
            var systems = new GameObject("Systems");
            systems.transform.SetParent(root, false);
            var boarding = systems.AddComponent<LastRide.Player.VanBoarding>();
            boarding.van = vanCtrl;
            boarding.robot = robotCtrl;
            systems.AddComponent<LastRide.UI.InteractionPromptView>();
            systems.AddComponent<LastRide.UI.StovePanel>();
            systems.AddComponent<LastRide.UI.LampPanel>();
            systems.AddComponent<LastRide.UI.Hud>();
            systems.AddComponent<LastRide.Narrative.PrologueStart>();
            var breakdown = systems.AddComponent<LastRide.Narrative.BreakdownDirector>();
            breakdown.van = vanCtrl;
            breakdown.robot = robotCtrl;
            breakdown.boarding = boarding;

            // --- Камеры: Brain на главной камере, камера фургона ---
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                CameraRigBuilder.SetupBrain(cam);
            }
            else Debug.LogWarning("[PlayerRigBuilder] В сцене нет Main Camera.");
            var oldCam = GameObject.Find(CameraRigBuilder.VanCameraName);
            if (oldCam != null) Object.DestroyImmediate(oldCam);
            var vanCam = CameraRigBuilder.CreateVanCamera(vanCtrl);
            vanCam.transform.SetParent(root, true);

            FinaleBuilder.Rebuild(); // гнездо и лифт Маяка (12.1)

            Debug.Log($"[The Last Ride] Player rig built: фургон на {VanDistance} м, банка {jarPos}, робот {robotPos}.");
            return root;
        }

        private static Vector3 Ground(Vector3 p, float fallbackY)
        {
            Physics.SyncTransforms();
            // Луч с небольшой высоты над дорогой: крыши и навесы мастерской не должны перехватить его.
            if (Physics.Raycast(new Vector3(p.x, fallbackY + 2f, p.z), Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, fallbackY, p.z);
        }
    }
}
