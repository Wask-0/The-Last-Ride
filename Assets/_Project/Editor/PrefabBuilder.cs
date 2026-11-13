using System.Collections.Generic;
using LastRide.ProcGen;
using LastRide.ProcGen.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastRide.Editor
{
    /// <summary>
    /// Префабы моделей (визуал + коллайдеры + якоря) в <c>Generated/Prefabs/</c>.
    /// Это «чистые» модели без игровой логики: игровые компоненты навешивают билдеры уровня
    /// и игровые префабы следующих этапов. Пересборка перезаписывает префабы на месте (GUID сохраняется).
    /// </summary>
    internal static class PrefabBuilder
    {
        public const string Root = "Assets/_Project/Generated/Prefabs/";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "ProcGen/Build Prefabs")]
        public static void BuildAll()
        {
            int count = 0;
            // ---- Персонажи и транспорт ----
            Save(BuildVan(), "Vehicles/Van"); count++;
            Save(BuildRobot(), "Characters/Robot"); count++;
            Save(BuildFirstPersonArms(), "Characters/FPArms"); count++;
            Save(BuildLamp(), "Props/Lamp"); count++;
            Save(BuildSparkJar(), "Props/SparkJar"); count++;
            Save(Simple("Lamp_Dead", "Lamp_Dead", MaterialsBuilder.World, ColliderKind.None, isStatic: true), "Props/Lamp_Dead"); count++;

            // ---- Топливо (триггер-коллайдер — для наведения прицелом) ----
            foreach (var n in new[] { "Fuel_Kindling", "Fuel_Planks", "Fuel_Briquette" })
            {
                Save(Simple(n, n, MaterialsBuilder.World, ColliderKind.TriggerBox, isStatic: false), "Fuel/" + n); count++;
            }

            // ---- Природа ----
            for (int i = 0; i < 4; i++)
            {
                var tree = Simple("Tree_" + i, "Tree_" + i, MaterialsBuilder.World, ColliderKind.None, isStatic: true);
                var cap = tree.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 1.5f, 0f);
                cap.radius = 0.35f;
                cap.height = 3f;
                Save(tree, "Nature/Tree_" + i); count++;
            }
            foreach (var n in new[] { "Rock_S0", "Rock_S1", "Rock_M0", "Rock_M1", "Rock_L0", "Rock_L1" })
            {
                Save(Simple(n, n, MaterialsBuilder.World, ColliderKind.ConvexMesh, isStatic: true), "Nature/" + n); count++;
            }
            var pole = Simple("PowerPole", "PowerPole", MaterialsBuilder.World, ColliderKind.None, isStatic: true);
            var pc = pole.AddComponent<CapsuleCollider>();
            pc.center = new Vector3(0f, 3.5f, 0f);
            pc.radius = 0.18f;
            pc.height = 7f;
            Save(pole, "Nature/PowerPole"); count++;
            foreach (var n in new[] { "Marker_0", "Marker_1" })
            {
                Save(Simple(n, n, MaterialsBuilder.World, ColliderKind.None, isStatic: true), "Nature/" + n); count++;
            }

            // ---- Постройки и точки интереса ----
            foreach (var n in new[] { "Workshop", "Shed", "Ruin_0", "Ruin_1", "Ruin_2", "TruckWreck" })
            {
                Save(Simple(n, n, MaterialsBuilder.World, ColliderKind.Mesh, isStatic: true), "Buildings/" + n); count++;
            }
            foreach (var n in new[] { "FrozenRobot_Sitting", "FrozenRobot_Lying", "FrozenRobot_Reaching" })
            {
                Save(Simple(n, n, MaterialsBuilder.Frosted, ColliderKind.ConvexMesh, isStatic: true), "Buildings/" + n); count++;
            }

            Save(BuildBeacon(), "Beacon/Beacon"); count++;
            Save(BuildFlower(), "Props/Flower"); count++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[The Last Ride] Built {count} prefabs in {Root}");
        }

        public static GameObject Load(string relativePath) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + relativePath + ".prefab");

        // ------------------------------------------------------------------

        private enum ColliderKind { None, Mesh, ConvexMesh, TriggerBox }

        private static GameObject Simple(string name, string meshName, Material material, ColliderKind collider, bool isStatic)
        {
            var go = new GameObject(name);
            var mesh = ModelLibrary.Load(meshName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            switch (collider)
            {
                case ColliderKind.Mesh:
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    break;
                case ColliderKind.ConvexMesh:
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = true;
                    break;
                case ColliderKind.TriggerBox:
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = mesh.bounds.center;
                    bc.size = mesh.bounds.size + Vector3.one * 0.05f;
                    bc.isTrigger = true;
                    break;
            }
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static GameObject BuildVan()
        {
            var holder = new GameObject("tmp");
            var van = ModelAssembly.Van(holder.transform);
            van.transform.SetParent(null, false);
            Object.DestroyImmediate(holder);

            // Коллайдеры: кузов и кабина (аркадной физике фургона хватит составного из коробок).
            var body = van.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 1.45f, -0.75f);
            body.size = new Vector3(2.0f, 1.8f, 2.9f);
            var cab = van.AddComponent<BoxCollider>();
            cab.center = new Vector3(0f, 1.1f, 1.5f);
            cab.size = new Vector3(1.9f, 1.2f, 1.7f);

            PointLight(van.transform, Anchors.StoveLight, VanModel.StoveLightPoint, PaletteAsset.Get(PaletteColor.WarmGlow), 2.5f, 4f);
            Anchor(van.transform, Anchors.ExitPoint, VanModel.ExitPoint);
            Anchor(van.transform, Anchors.DoorPoint, VanModel.DoorPoint);
            Anchor(van.transform, Anchors.ChimneyTop, VanModel.ChimneyTop);
            return van;
        }

        private static GameObject BuildRobot()
        {
            var holder = new GameObject("tmp");
            var robot = ModelAssembly.Robot(holder.transform, "Robot", RobotModel.Pose.Standing, MaterialsBuilder.Robot, MaterialsBuilder.Glow);
            robot.transform.SetParent(null, false);
            Object.DestroyImmediate(holder);
            Anchor(robot.transform, Anchors.RobotEye, RobotModel.Eye);
            return robot;
        }

        private static GameObject BuildFirstPersonArms()
        {
            var root = new GameObject("FPArms");
            ModelAssembly.Part(root.transform, Anchors.FPArmRight, "Robot_FPArm", new Vector3(0.2f, -0.3f, 0.1f), new Vector3(6f, -4f, 0f), MaterialsBuilder.Robot);
            var left = ModelAssembly.Part(root.transform, Anchors.FPArmLeft, "Robot_FPArm", new Vector3(-0.2f, -0.3f, 0.1f), new Vector3(6f, 4f, 0f), MaterialsBuilder.Robot);
            left.transform.localScale = new Vector3(-1f, 1f, 1f);
            return root;
        }

        private static GameObject BuildLamp()
        {
            var holder = new GameObject("tmp");
            var lamp = ModelAssembly.Lamp(holder.transform);
            lamp.transform.SetParent(null, false);
            Object.DestroyImmediate(holder);
            PointLight(lamp.transform, Anchors.FlameLight, LampModel.FlamePoint, PaletteAsset.Get(PaletteColor.WarmGlow), 1.5f, 6f);
            Anchor(lamp.transform, Anchors.HandleTop, LampModel.HandleTop);
            return lamp;
        }

        private static GameObject BuildSparkJar()
        {
            var jar = Simple("SparkJar", "SparkJar", MaterialsBuilder.World, ColliderKind.TriggerBox, isStatic: false);
            PointLight(jar.transform, Anchors.JarLight, new Vector3(0f, 0.1f, 0f), PaletteAsset.Get(PaletteColor.WarmGlow), 0.8f, 2f);
            return jar;
        }

        private static GameObject BuildBeacon()
        {
            var root = new GameObject("Beacon");
            var tower = ModelAssembly.Part(root.transform, Anchors.BeaconTower, "Beacon_Tower", Vector3.zero, Vector3.zero, MaterialsBuilder.Beacon);
            tower.AddComponent<MeshCollider>().sharedMesh = ModelLibrary.Load("Beacon_Tower");
            GameObjectUtility.SetStaticEditorFlags(tower, StaticEditorFlags.BatchingStatic);
            ModelAssembly.Part(root.transform, Anchors.BeaconLantern, "Beacon_Lantern", Vector3.zero, Vector3.zero, MaterialsBuilder.BeaconLight);
            ModelAssembly.Part(root.transform, Anchors.BeaconLiftCage, "Beacon_LiftCage", BeaconModel.LiftBottom, Vector3.zero, MaterialsBuilder.World);
            for (int i = 0; i < BeaconModel.LightRows; i++)
                ModelAssembly.Part(root.transform, Anchors.BeaconLightRowPrefix + i, "Beacon_LightRow_" + i, Vector3.zero, Vector3.zero, MaterialsBuilder.BeaconLight);

            var station = ModelAssembly.Part(root.transform, Anchors.BeaconStation, "Beacon_Station", BeaconModel.StationOffset, Vector3.zero, MaterialsBuilder.Beacon);
            station.AddComponent<MeshCollider>().sharedMesh = ModelLibrary.Load("Beacon_Station");
            GameObjectUtility.SetStaticEditorFlags(station, StaticEditorFlags.BatchingStatic);
            ModelAssembly.Part(station.transform, Anchors.BeaconStationWindows, "Beacon_StationWindows", Vector3.zero, Vector3.zero, MaterialsBuilder.BeaconLight);

            Anchor(root.transform, Anchors.BeaconSocket, BeaconModel.SocketPoint);
            Anchor(root.transform, Anchors.BeaconLiftBottom, BeaconModel.LiftBottom);
            Anchor(root.transform, Anchors.BeaconLiftTop, BeaconModel.LiftTop);
            return root;
        }

        private static GameObject BuildFlower()
        {
            var flower = Simple("Flower", "Flower_Bloom", MaterialsBuilder.World, ColliderKind.None, isStatic: false);
            ModelAssembly.Part(flower.transform, Anchors.FlowerMound, "Flower_Mound", Vector3.zero, Vector3.zero, MaterialsBuilder.World).transform.localScale = Vector3.one * 0.25f;
            return flower;
        }

        // ------------------------------------------------------------------

        private static void Anchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
        }

        private static void PointLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            go.AddComponent<UniversalAdditionalLightData>();
        }

        private static void Save(GameObject go, string relativePath)
        {
            string path = Root + relativePath + ".prefab";
            GeneratedAssets.EnsureFolder(System.IO.Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
