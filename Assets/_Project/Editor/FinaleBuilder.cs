using LastRide.Core;
using LastRide.Narrative;
using LastRide.ProcGen.Models;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// 12.1: оснащает Маяк в сцене Game игровой логикой финала — <see cref="BeaconFinale"/> на корне, триггер взаимодействия у гнезда
    /// (<see cref="BeaconSocket"/>) и лампа в кабинке лифта (в ней оживёт Искра). Вызывается из <c>PlayerRigBuilder.Rebuild</c>
    /// (то есть из «Build Level»); можно запускать отдельно: «The Last Ride/Level/Build Finale».
    /// </summary>
    internal static class FinaleBuilder
    {
        public const string BeaconName = "POI_Beacon";
        private const string SocketTriggerName = "SocketInteract";
        private const string CageLampName = "CageLamp";
        private const string LanternLightName = "LanternLight";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Finale")]
        public static void BuildMenu() => Rebuild();

        public static BeaconFinale Rebuild()
        {
            var beacon = GameObject.Find(BeaconName);
            if (beacon == null) { Debug.LogWarning("[FinaleBuilder] Нет " + BeaconName + " — сначала Build Level."); return null; }

            var finale = beacon.GetComponent<BeaconFinale>();
            if (finale == null) finale = beacon.AddComponent<BeaconFinale>();

            finale.cage = Find(beacon.transform, Anchors.BeaconLiftCage);
            finale.liftBottom = Find(beacon.transform, Anchors.BeaconLiftBottom);
            finale.liftTop = Find(beacon.transform, Anchors.BeaconLiftTop);
            finale.socket = Find(beacon.transform, Anchors.BeaconSocket);
            if (finale.cage == null || finale.liftBottom == null || finale.liftTop == null || finale.socket == null)
            {
                Debug.LogError("[FinaleBuilder] У Маяка нет якорей лифта/гнезда — пересобери префабы (Generate All).");
                return null;
            }

            // Триггер взаимодействия: чуть больше постамента, чтобы луч прицела попадал в него раньше, чем в коллайдер башни.
            var old = beacon.transform.Find(SocketTriggerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var trigger = new GameObject(SocketTriggerName);
            trigger.transform.SetParent(beacon.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.6f, BeaconModel.SocketPoint.z);
            trigger.layer = LayerMask.NameToLayer(Layers.Interactable);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.2f, 3.2f, 2.0f);
            trigger.AddComponent<BeaconSocket>();

            // Лампа в кабинке (выключена до вставки Искры).
            var cageLamp = finale.cage.Find(CageLampName);
            if (cageLamp != null) Object.DestroyImmediate(cageLamp.gameObject);
            var lampPrefab = GameplayPrefabsBuilder.Lamp;
            if (lampPrefab == null) { Debug.LogError("[FinaleBuilder] Нет префаба лампы (Setup/Build Gameplay Prefabs)."); return null; }
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, finale.cage);
            lamp.name = CageLampName;
            lamp.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            lamp.transform.localRotation = Quaternion.identity;
            lamp.SetActive(false);
            finale.cageLamp = lamp;

            // Огни Маяка (12.2): ряды башни, фонарь + его свет, окна станции.
            var lights = beacon.GetComponent<BeaconLights>();
            if (lights == null) lights = beacon.AddComponent<BeaconLights>();
            lights.finale = finale;
            var rows = new Renderer[BeaconModel.LightRows];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = Find(beacon.transform, Anchors.BeaconLightRowPrefix + i);
                rows[i] = row != null ? row.GetComponent<Renderer>() : null;
            }
            lights.rows = rows;
            var lantern = Find(beacon.transform, Anchors.BeaconLantern);
            lights.lantern = lantern != null ? lantern.GetComponent<Renderer>() : null;
            var windows = Find(beacon.transform, Anchors.BeaconStationWindows);
            lights.stationWindows = windows != null ? windows.GetComponent<Renderer>() : null;

            var oldLight = beacon.transform.Find(LanternLightName);
            if (oldLight != null) Object.DestroyImmediate(oldLight.gameObject);
            var lightGo = new GameObject(LanternLightName);
            lightGo.transform.SetParent(beacon.transform, false);
            lightGo.transform.localPosition = BeaconModel.LanternCenter;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.93f, 0.78f);
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            light.enabled = false;
            lights.lanternLight = light;

            BuildCreditsStage();

            EditorUtility.SetDirty(finale);
            EditorUtility.SetDirty(lights);
            Debug.Log("[FinaleBuilder] Маяк оснащён: гнездо, кабинка с лампой, BeaconFinale.");
            return finale;
        }

        public const string StageName = "CreditsStage";
        private static readonly Vector3 StagePosition = new Vector3(-3000f, 0f, -3000f);

        /// <summary>
        /// 12.3: полянка для титров вдали от уровня — снег (меш меню-диорамы), сугробик, цветок с анимацией роста, тёплый свет,
        /// точки камеры. Корень выключен (включает <c>CreditsDirector</c>).
        /// </summary>
        private static void BuildCreditsStage()
        {
            var old = GameObject.Find(StageName);
            if (old == null)
                foreach (var s in Object.FindObjectsByType<CreditsStage>(FindObjectsInactive.Include)) old = s.gameObject;
            if (old != null) Object.DestroyImmediate(old);

            var world = AssetDatabase.LoadAssetAtPath<Material>(MaterialsBuilder.WorldPath);
            var root = new GameObject(StageName);
            root.transform.position = StagePosition;

            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + "Meshes/MenuGround.asset");
            ground.AddComponent<MeshRenderer>().sharedMaterial = world;

            var mound = new GameObject("Mound");
            mound.transform.SetParent(root.transform, false);
            mound.transform.localScale = Vector3.one * 0.25f;
            mound.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.Load("Flower_Mound");
            mound.AddComponent<MeshRenderer>().sharedMaterial = world;

            var flowerGo = new GameObject("Flower");
            flowerGo.transform.SetParent(root.transform, false);
            var growth = flowerGo.AddComponent<FlowerGrowth>();
            growth.material = world;

            Transform Point(string name, Vector3 local)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root.transform, false);
                t.localPosition = local;
                return t;
            }

            var lightGo = new GameObject("WarmLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0.8f, 0.9f, -0.9f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.62f);
            light.intensity = 3f;
            light.range = 8f;
            light.shadows = LightShadows.None;

            var stage = root.AddComponent<CreditsStage>();
            stage.flower = growth;
            stage.cameraStart = Point("CameraStart", new Vector3(0.28f, 0.1f, -0.5f));
            stage.cameraEnd = Point("CameraEnd", new Vector3(0.7f, 0.42f, -1.5f));
            stage.lookTarget = Point("LookTarget", new Vector3(0f, 0.13f, 0f));
            root.SetActive(false);
            EditorUtility.SetDirty(stage);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
