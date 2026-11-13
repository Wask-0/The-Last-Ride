using System;
using System.Collections.Generic;
using LastRide.ProcGen;
using LastRide.ProcGen.Models;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Реестр всех процедурных моделей: имя меша → генератор.
    /// «The Last Ride/ProcGen/Generate Models» сохраняет всё в Generated/Meshes/ (перезапись на месте).
    /// Новые модели (2.4, 2.5) добавляются в <see cref="Entries"/>.
    /// </summary>
    internal static class ModelLibrary
    {
        public const string MeshFolder = "Meshes/";

        public static readonly List<KeyValuePair<string, Func<MeshBuilder>>> Entries = new List<KeyValuePair<string, Func<MeshBuilder>>>
        {
            // ---- Фургон ----
            E("Van_Body", VanModel.BuildBody),
            E("Van_Wheel", VanModel.BuildWheel),
            E("Van_Window", VanModel.BuildWindow),
            // ---- Робот ----
            E("Robot_Torso", RobotModel.BuildTorso),
            E("Robot_Head", RobotModel.BuildHead),
            E("Robot_HeadGlass", RobotModel.BuildHeadGlass),
            E("Robot_Key", RobotModel.BuildKey),
            E("Robot_Arm", RobotModel.BuildArm),
            E("Robot_Leg", RobotModel.BuildLeg),
            E("Robot_FPArm", RobotModel.BuildFirstPersonArm),
            // ---- Лампа и Искра ----
            E("Lamp_Frame", LampModel.BuildFrame),
            E("Lamp_Glass", LampModel.BuildGlass),
            E("SparkJar", LampModel.BuildSparkJar),
            // ---- Природа и мелочи вдоль дороги ----
            E("Tree_0", () => NatureModels.BuildTree(101)),
            E("Tree_1", () => NatureModels.BuildTree(202)),
            E("Tree_2", () => NatureModels.BuildTree(303)),
            E("Tree_3", () => NatureModels.BuildTree(404)),
            E("Rock_S0", () => NatureModels.BuildRock(11, 1.0f)),
            E("Rock_S1", () => NatureModels.BuildRock(12, 1.3f)),
            E("Rock_M0", () => NatureModels.BuildRock(21, 2.2f)),
            E("Rock_M1", () => NatureModels.BuildRock(22, 2.6f)),
            E("Rock_L0", () => NatureModels.BuildRock(31, 4.5f)),
            E("Rock_L1", () => NatureModels.BuildRock(32, 6.0f)),
            E("PowerPole", NatureModels.BuildPowerPole),
            E("Marker_0", () => NatureModels.BuildMarker(1)),
            E("Marker_1", () => NatureModels.BuildMarker(2)),
            // ---- Постройки ----
            E("Workshop", BuildingModels.BuildWorkshop),
            E("Shed", BuildingModels.BuildShed),
            E("Ruin_0", () => BuildingModels.BuildRuin(7, stone: true)),
            E("Ruin_1", () => BuildingModels.BuildRuin(8, stone: false)),
            E("Ruin_2", () => BuildingModels.BuildRuin(9, stone: true)),
            E("TruckWreck", BuildingModels.BuildTruckWreck),
            // ---- Замёрзшие роботы и погасшие лампы ----
            E("FrozenRobot_Sitting", () => RobotModel.BuildFrozen(RobotModel.FrozenPose.Sitting, 1)),
            E("FrozenRobot_Lying", () => RobotModel.BuildFrozen(RobotModel.FrozenPose.Lying, 2)),
            E("FrozenRobot_Reaching", () => RobotModel.BuildFrozen(RobotModel.FrozenPose.Reaching, 3)),
            E("Lamp_Dead", LampModel.BuildDead),
            // ---- Топливо ----
            E("Fuel_Kindling", () => FuelModels.BuildKindling(5)),
            E("Fuel_Planks", () => FuelModels.BuildPlanks(6)),
            E("Fuel_Briquette", FuelModels.BuildBriquette),
            // ---- Маяк, станция, лифт ----
            E("Beacon_Tower", BeaconModel.BuildTower),
            E("Beacon_Lantern", BeaconModel.BuildLantern),
            E("Beacon_LiftCage", BeaconModel.BuildLiftCage),
            E("Beacon_Station", BeaconModel.BuildStation),
            E("Beacon_StationWindows", BeaconModel.BuildStationWindows),
            // ---- Цветок (рантайм перестраивает меш по мере роста; здесь — полностью раскрытый и сугробик) ----
            E("Flower_Bloom", () => FlowerModel.BuildFlower(1f)),
            E("Flower_Mound", FlowerModel.BuildMound),
        };

        static ModelLibrary()
        {
            for (int i = 0; i < BeaconModel.LightRows; i++)
            {
                int row = i;
                Entries.Add(E("Beacon_LightRow_" + row, () => BeaconModel.BuildLightRow(row)));
            }
        }

        private static KeyValuePair<string, Func<MeshBuilder>> E(string name, Func<MeshBuilder> build) =>
            new KeyValuePair<string, Func<MeshBuilder>>(name, build);

        [MenuItem(EditorAssemblyMarker.MenuRoot + "ProcGen/Generate Models")]
        public static void GenerateAll()
        {
            int verts = 0;
            foreach (var e in Entries)
            {
                var builder = e.Value();
                verts += builder.VertexCount;
                GeneratedAssets.SaveMesh(builder, MeshFolder + e.Key);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[The Last Ride] Generated {Entries.Count} meshes ({verts} vertices) in {GeneratedAssets.Root}{MeshFolder}");
        }

        public static Mesh Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.Root + MeshFolder + name + ".asset");
        }
    }
}
