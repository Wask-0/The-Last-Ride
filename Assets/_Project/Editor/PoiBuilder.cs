using System.Collections.Generic;
using System.Text;
using LastRide.Fire;
using LastRide.Level;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// 7.5: ставит точки интереса из <see cref="PoiLayout"/> под <c>Level_POI</c>: мастерскую, сараи, руины, остов грузовика,
    /// замёрзших роботов с погасшими лампами, поваленные столбы, завал на конце пути фургона и Маяк, а также топливо
    /// (<see cref="FuelPickup"/> в <see cref="PickupSegment"/> на каждую точку — множитель сложности из 9).
    /// Сначала должен быть собран рельеф (<c>Build Terrain</c>), площадки под здания он берёт из той же раскладки.
    /// </summary>
    internal static class PoiBuilder
    {
        public const string RootName = "Level_POI";
        private const string ReportPath = "Assets/Screenshots/poi_report.txt";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Points Of Interest")]
        public static void BuildMenu()
        {
            var root = Rebuild();
            if (root != null) Selection.activeGameObject = root.gameObject;
        }

        public static Transform Rebuild()
        {
            RoadPath.ResetCache();
            var path = RoadPath.Load();
            if (path == null) { Debug.LogError("[PoiBuilder] Нет RoadPath."); return null; }
            if (PrefabBuilder.Load("Buildings/Workshop") == null) ShowcaseBuilder.GenerateAll();
            GameplayPrefabsBuilder.BuildAll();

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var ts = TerrainBuilder.LoadOrCreateSettings();
            var model = new TerrainModel(path, ts);
            var layout = PoiLayout.Build(path, ts, model);
            var root = new GameObject(RootName).transform;

            var counts = new Dictionary<PoiLayout.Kind, int>();
            var fuelByAct = new int[3, 3];
            var fuelMandatory = 0; var fuelOptional = 0;
            string[] fuelIds = GameplayPrefabsBuilder.FuelIds;
            var lampMesh = ModelLibrary.Load("Lamp_Dead");
            var world = MaterialsBuilder.World;

            foreach (var poi in layout.items)
            {
                counts[poi.kind] = counts.TryGetValue(poi.kind, out int n) ? n + 1 : 1;
                var parent = new GameObject("POI_" + poi.id).transform;
                parent.SetParent(root, false);
                parent.SetPositionAndRotation(poi.position, Quaternion.Euler(0f, poi.yaw, 0f));

                switch (poi.kind)
                {
                    case PoiLayout.Kind.Barrier: BuildBarrier(parent, model); break;
                    case PoiLayout.Kind.Shelter: BuildShelter(parent); break;
                    case PoiLayout.Kind.FallenPole:
                        Place(poi.prefab, parent, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), "Pole");
                        break;
                    case PoiLayout.Kind.Roadside: break; // только топливо
                    default:
                        if (poi.prefab != null) Place(poi.prefab, parent, Vector3.zero, Quaternion.identity, "Model");
                        break;
                }

                if (poi.lamp)
                {
                    var lamp = new GameObject("Lamp_Dead");
                    lamp.transform.SetParent(parent, false);
                    var lp = parent.TransformPoint(new Vector3(-1.0f, 0f, 0.3f));
                    lamp.transform.SetPositionAndRotation(new Vector3(lp.x, model.HeightAt(lp.x, lp.z), lp.z), Quaternion.Euler(0f, poi.yaw + 70f, 0f));
                    lamp.AddComponent<MeshFilter>().sharedMesh = lampMesh;
                    lamp.AddComponent<MeshRenderer>().sharedMaterial = world;
                    GameObjectUtility.SetStaticEditorFlags(lamp, StaticEditorFlags.BatchingStatic);
                }

                if (poi.fuel.Count > 0)
                {
                    parent.gameObject.AddComponent<PickupSegment>().segmentId = poi.id;
                    int act = (int)path.ActAt(poi.distance) - 1;
                    for (int k = 0; k < poi.fuel.Count; k++)
                    {
                        var spot = poi.fuel[k];
                        var prefab = GameplayPrefabsBuilder.Fuel(spot.fuel);
                        if (prefab == null) { Debug.LogError("[PoiBuilder] нет префаба топлива " + spot.fuel); continue; }
                        var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                        var wp = parent.TransformPoint(spot.local);
                        item.transform.SetPositionAndRotation(new Vector3(wp.x, model.HeightAt(wp.x, wp.z) + 0.15f, wp.z), Quaternion.Euler(0f, poi.yaw + 40f + k * 53f, 0f));
                        item.name = $"{spot.fuel}_{k}";
                        var pickup = item.GetComponent<FuelPickup>();
                        pickup.pickupId = $"{poi.id}_{spot.fuel}_{k}";
                        pickup.optional = spot.optional;
                        fuelByAct[act, System.Array.IndexOf(fuelIds, spot.fuel)]++;
                        if (spot.optional) fuelOptional++; else fuelMandatory++;
                    }
                }
            }

            var r = new StringBuilder();
            r.AppendLine($"Точек интереса: {layout.items.Count} — " + string.Join(", ", Format(counts)));
            string[] actNames = { "Акт 1", "Акт 2", "Акт 3" };
            for (int a = 0; a < 3; a++)
                r.AppendLine($"Топливо, {actNames[a]}: щепки {fuelByAct[a, 0]}, доски {fuelByAct[a, 1]}, брикеты {fuelByAct[a, 2]}");
            r.AppendLine($"Подборы: обязательных {fuelMandatory}, необязательных {fuelOptional} (множитель сложности применяется к необязательным)");
            GeneratedAssets.EnsureFolder("Assets/Screenshots");
            System.IO.File.WriteAllText(ReportPath, r.ToString());
            Debug.Log("[PoiBuilder] " + r.ToString().Split('\n')[0].Trim() + " Отчёт: " + ReportPath);
            return root;
        }

        private static IEnumerable<string> Format(Dictionary<PoiLayout.Kind, int> counts)
        {
            foreach (var kv in counts) yield return $"{kv.Key} {kv.Value}";
        }

        private static GameObject Place(string prefabPath, Transform parent, Vector3 localPos, Quaternion localRot, string name)
        {
            var prefab = PrefabBuilder.Load(prefabPath);
            if (prefab == null) { Debug.LogError("[PoiBuilder] нет префаба " + prefabPath); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.name = name;
            return go;
        }

        /// <summary>
        /// Укрытие акта 3: «подкова» из трёх больших скал, открытая к тропе (локальный +Z — к тропе), сверху камень поменьше —
        /// от ветра закрывает с трёх сторон, внутри лежит топливо.
        /// </summary>
        private static void BuildShelter(Transform parent)
        {
            var rocks = new (string prefab, Vector3 local, float scale, float yaw)[]
            {
                ("Nature/Rock_L1", new Vector3(0f, 0f, -4.7f), 1.15f, 15f),
                ("Nature/Rock_L0", new Vector3(-3.7f, 0f, -1.9f), 0.95f, 100f),
                ("Nature/Rock_L0", new Vector3(3.7f, 0f, -1.9f), 0.95f, 250f),
                ("Nature/Rock_M1", new Vector3(-1.8f, 1.3f, -3.6f), 0.9f, 40f),
            };
            int i = 0;
            foreach (var r in rocks)
            {
                var go = Place(r.prefab, parent, r.local, Quaternion.Euler(0f, r.yaw, 0f), "Rock_" + i++);
                if (go != null) go.transform.localScale = Vector3.one * r.scale;
            }
        }

        /// <summary>
        /// Завал на дороге, где фургон останавливается: слева и справа глыбы, посередине проход шириной ~1.4 м —
        /// пройдёт робот, фургон нет. Центр «завала» — на оси пути, локальный X — поперёк дороги.
        /// </summary>
        private static void BuildBarrier(Transform parent, TerrainModel model)
        {
            var rocks = new (string prefab, float lateral, float along, float scale, float yaw)[]
            {
                ("Nature/Rock_L1", -2.6f, 0f, 1.25f, 20f),
                ("Nature/Rock_L0", 5.2f, 1.0f, 1.1f, 140f),
                ("Nature/Rock_M1", -5.2f, 3.5f, 1.2f, 75f),
                ("Nature/Rock_M0", 6.2f, -3.0f, 1.1f, 200f),
                ("Nature/Rock_S1", -4.4f, -3.0f, 1.0f, 10f),
                ("Nature/Rock_S0", 6.6f, 2.0f, 1.1f, 300f),
            };
            int i = 0;
            foreach (var r in rocks)
            {
                var go = Place(r.prefab, parent, new Vector3(r.lateral, 0f, r.along), Quaternion.Euler(0f, r.yaw, 0f), "Rock_" + i++);
                if (go != null) go.transform.localScale = Vector3.one * r.scale;
            }
        }
    }
}
