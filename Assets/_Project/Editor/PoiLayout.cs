using System.Collections.Generic;
using LastRide.Level;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Раскладка точек интереса (7.5, ТЗ 5): мастерская, сараи, руины, остов грузовика, замёрзшие роботы, поваленные столбы,
    /// завал на конце пути фургона, Маяк. Положения — маркеры оси + смещение; сторона дороги выбирается «горной» (где
    /// рельеф поднимается), чтобы площадки не висели над обрывом. Строится детерминированно и до рельефа:
    /// <see cref="Build"/> задаёт площадки в <see cref="TerrainModel"/> (<c>SetPads</c>), поэтому рельеф, пропсы и сами точки
    /// интереса используют одну и ту же раскладку.
    /// </summary>
    internal static class PoiLayout
    {
        public enum Kind { Workshop, Shed, Ruin, Truck, Shelter, Barrier, Beacon, FrozenRobot, FallenPole, Roadside }

        public struct FuelSpot
        {
            public string fuel;      // kindling / planks / briquette
            public Vector3 local;    // x — вправо, z — вперёд (лицом к дороге), от центра площадки
            public bool optional;
        }

        public sealed class Poi
        {
            public string id;
            public Kind kind;
            public string prefab;          // путь внутри Generated/Prefabs, например "Buildings/Shed"
            public Vector3 position;       // y — высота площадки
            public float yaw;
            public float distance;         // по оси пути
            public int side;
            public float padRadius;        // 0 — без площадки
            public float exclusionRadius;  // сюда пропсы не ставятся
            public bool lamp;              // рядом погасшая лампа (замёрзшие роботы)
            public List<FuelSpot> fuel = new List<FuelSpot>();
        }

        public sealed class Result
        {
            public List<Poi> items = new List<Poi>();
        }

        private struct Def
        {
            public string id; public Kind kind; public string prefab; public string marker; public float offset;
            public int sideHint; public float pad; public float exclusion;
        }

        private static Def B(string id, Kind kind, string prefab, string marker, float offset, int sideHint, float pad, float exclusion)
            => new Def { id = id, kind = kind, prefab = prefab, marker = marker, offset = offset, sideHint = sideHint, pad = pad, exclusion = exclusion };

        private static readonly Def[] Buildings =
        {
            B("Workshop",    Kind.Workshop, "Buildings/Workshop", "Start",      14f,  -1, 8f, 12f),
            B("Shed_A1_1",   Kind.Shed,     "Buildings/Shed",     "Stop_A1_1",  0f,   1,  5f, 9f),
            B("Ruin_A1_2",   Kind.Ruin,     "Buildings/Ruin_1",   "Stop_A1_2",  0f,   1,  7f, 11f),
            B("Ruin_A2_0",   Kind.Ruin,     "Buildings/Ruin_2",   "Act2Start",  130f, -1, 7f, 11f),
            B("Ruin_A2_1",   Kind.Ruin,     "Buildings/Ruin_0",   "Stop_A2_1",  0f,   1,  7f, 11f),
            B("Truck_A2",    Kind.Truck,    "Buildings/TruckWreck", "Hairpin_2", -75f, -1, 6f, 10f),
            B("Ruin_A2_2",   Kind.Ruin,     "Buildings/Ruin_1",   "Stop_A2_2",  0f,   -1, 7f, 11f),
            B("Ruin_A2_3",   Kind.Ruin,     "Buildings/Ruin_2",   "Stop_A2_3",  0f,   1,  7f, 11f),
            B("Ruin_A2_4",   Kind.Ruin,     "Buildings/Ruin_0",   "Stop_A2_4",  0f,   -1, 7f, 11f),
            B("Shelter_1",   Kind.Shelter,  null,                 "Shelter_1",  0f,   1,  5f, 9f),
            B("Shelter_2",   Kind.Shelter,  null,                 "Shelter_2",  0f,   -1, 5f, 9f),
            B("Shelter_3",   Kind.Shelter,  null,                 "Shelter_3",  0f,   1,  5f, 9f),
        };

        private struct RobotDef { public float d; public string pose; public string[] fuel; public bool lamp; }

        private static readonly RobotDef[] Robots =
        {
            new RobotDef { d = 1470, pose = "Sitting",  fuel = new[] { "kindling", "planks" }, lamp = true },
            new RobotDef { d = 1600, pose = "Lying",    fuel = new string[0], lamp = false },
            new RobotDef { d = 1795, pose = "Reaching", fuel = new string[0], lamp = true },
            new RobotDef { d = 2000, pose = "Sitting",  fuel = new[] { "briquette" }, lamp = true },
            new RobotDef { d = 2230, pose = "Lying",    fuel = new string[0], lamp = false },
            new RobotDef { d = 2480, pose = "Reaching", fuel = new[] { "planks", "kindling" }, lamp = true },
            new RobotDef { d = 2730, pose = "Sitting",  fuel = new string[0], lamp = true },
            new RobotDef { d = 2960, pose = "Lying",    fuel = new[] { "briquette", "planks" }, lamp = false },
        };

        private static readonly float[] RoadsideKindling = { 150f, 270f, 380f, 760f, 1010f };
        private static readonly float[] FallenPoles = { 1110f, 2590f, 3060f };

        public static Result Build(RoadPath path, TerrainSettings ts, TerrainModel model)
        {
            var result = new Result();
            model.SetPads(null); // базовая высота — без площадок
            float a3 = path.MarkerOrNegative(RoadPath.Act3StartMarker);

            // ---- Здания: площадки ----
            var pads = new List<TerrainPad>();
            foreach (var def in Buildings)
            {
                if (!path.TryGetMarker(def.marker, out float baseD)) { Debug.LogWarning("[PoiLayout] нет маркера " + def.marker); continue; }
                float d = Mathf.Clamp(baseD + def.offset, 0f, path.Length);
                var smp = path.Evaluate(d);
                int side = ChooseSide(path, model, d, def.sideHint);
                float inner = Inner(smp, ts, a3);
                // Укрытие — рядом с тропой (стена вдоль тропы в этом месте открыта, см. TerrainBuilder.BuildWalls).
                float lateral = def.kind == Kind.Shelter ? smp.width * 0.5f + 5.5f : inner + 1.2f + def.pad * 0.8f;
                var pos = smp.position + smp.right * (side * lateral);
                pos.y = model.HeightAt(pos.x, pos.z);
                // Укрытие врезано в склон на уровне тропы, чтобы робот мог зайти (без крутого подъёма от кромки).
                bool cut = def.kind == Kind.Shelter;
                if (cut) pos.y = smp.position.y - ts.roadSink;

                var poi = new Poi
                {
                    id = def.id, kind = def.kind, prefab = def.prefab, position = pos, distance = d, side = side,
                    yaw = Quaternion.LookRotation(-side * smp.right, Vector3.up).eulerAngles.y,
                    padRadius = def.pad, exclusionRadius = def.exclusion
                };
                AddBuildingFuel(poi, result.items.Count);
                result.items.Add(poi);
                pads.Add(new TerrainPad { center = new Vector2(pos.x, pos.z), radius = def.pad, blend = 7f, height = pos.y, ignoreRoadFade = cut });
            }

            // ---- Завал на конце пути фургона ----
            float barrierD = path.MarkerOrNegative("Breakdown") + 27f;
            var bs = path.Evaluate(barrierD);
            result.items.Add(new Poi { id = "Barrier", kind = Kind.Barrier, prefab = "Nature/Rock_L1", position = bs.position, distance = barrierD, yaw = Quaternion.LookRotation(bs.flatTangent).eulerAngles.y, exclusionRadius = 16f });

            // ---- Маяк на вершине ----
            var end = path.Evaluate(path.Length);
            var beaconPos = end.position + end.flatTangent * 15f;
            result.items.Add(new Poi
            {
                id = "Beacon", kind = Kind.Beacon, prefab = "Beacon/Beacon", position = beaconPos, distance = path.Length,
                yaw = Quaternion.LookRotation(-end.flatTangent, Vector3.up).eulerAngles.y, exclusionRadius = 70f
            });

            // ---- Замёрзшие роботы ----
            for (int i = 0; i < Robots.Length; i++)
            {
                var r = Robots[i];
                var smp = path.Evaluate(r.d);
                int side = ChooseSide(path, model, r.d, i % 2 == 0 ? 1 : -1);
                float lateral = Inner(smp, ts, a3) + 2.2f + 0.5f * (i % 3);
                var pos = smp.position + smp.right * (side * lateral);
                pos.y = model.HeightAt(pos.x, pos.z);
                float yaw;
                switch (r.pose)
                {
                    case "Sitting": yaw = Quaternion.LookRotation(-side * smp.right).eulerAngles.y; break;      // сидит лицом к дороге
                    case "Lying": yaw = Quaternion.LookRotation(smp.flatTangent).eulerAngles.y + (i % 2 == 0 ? 25f : -30f); break;
                    default: yaw = Quaternion.LookRotation(smp.flatTangent).eulerAngles.y + (side > 0 ? -12f : 12f); break; // тянется вперёд, вверх по дороге
                }
                var poi = new Poi
                {
                    id = $"Frozen_{i + 1}", kind = Kind.FrozenRobot, prefab = "Buildings/FrozenRobot_" + r.pose, position = pos, yaw = yaw,
                    distance = r.d, side = side, exclusionRadius = 4f, lamp = r.lamp
                };
                for (int k = 0; k < r.fuel.Length; k++)
                    poi.fuel.Add(new FuelSpot { fuel = r.fuel[k], local = new Vector3(k == 0 ? 1.1f : -1.0f, 0f, 0.6f + 0.4f * k), optional = k > 0 });
                result.items.Add(poi);
            }

            // ---- Поваленные столбы ЛЭП ----
            for (int i = 0; i < FallenPoles.Length; i++)
            {
                float d = FallenPoles[i];
                var smp = path.Evaluate(d);
                int side = ChooseSide(path, model, d, i % 2 == 0 ? -1 : 1);
                var pos = smp.position + smp.right * (side * (Inner(smp, ts, a3) + 3.5f));
                pos.y = model.HeightAt(pos.x, pos.z) + 0.25f;
                float yaw = Quaternion.LookRotation(smp.flatTangent).eulerAngles.y + (i == 1 ? -18f : 14f);
                result.items.Add(new Poi { id = "FallenPole_" + (i + 1), kind = Kind.FallenPole, prefab = "Nature/PowerPole", position = pos, yaw = yaw, distance = d, side = side, exclusionRadius = 4f });
            }

            // ---- Мелкие подборы вдоль дороги акта 1 ----
            for (int i = 0; i < RoadsideKindling.Length; i++)
            {
                float d = RoadsideKindling[i];
                var smp = path.Evaluate(d);
                int side = ChooseSide(path, model, d, i % 2 == 0 ? 1 : -1);
                var pos = smp.position + smp.right * (side * (Inner(smp, ts, a3) + 1.6f));
                pos.y = model.HeightAt(pos.x, pos.z);
                var poi = new Poi { id = "Roadside_" + (i + 1), kind = Kind.Roadside, position = pos, distance = d, side = side, exclusionRadius = 2f, yaw = 0f };
                poi.fuel.Add(new FuelSpot { fuel = "kindling", local = Vector3.zero, optional = false });
                poi.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(0.8f, 0f, 0.5f), optional = true });
                result.items.Add(poi);
            }

            model.SetPads(pads);
            return result;
        }

        private static float Inner(RoadSample smp, TerrainSettings ts, float a3) =>
            smp.width * 0.5f + ts.shoulderWidth + TerrainBuilder.WallOffset(smp.distance, ts, a3);

        /// <summary>Сторона «горы» (где рельеф поднимается); если сторона неочевидна — подсказка.</summary>
        private static int ChooseSide(RoadPath path, TerrainModel model, float d, int hint)
        {
            float bias = model.BiasAt(Mathf.RoundToInt(d / path.Step));
            if (Mathf.Abs(bias) < 0.15f) return hint;
            return bias > 0f ? 1 : -1;
        }

        // ------------------------------------------------------------------ топливо у построек

        private static void AddBuildingFuel(Poi p, int index)
        {
            switch (p.kind)
            {
                case Kind.Workshop:
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(-2.5f, 0f, 6.0f), optional = false });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(-1.5f, 0f, 6.6f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(2.5f, 0f, 6.2f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(3.2f, 0f, 5.4f), optional = true });
                    break;
                case Kind.Shed:
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(0f, 0f, 0.2f), optional = false });
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(-1.0f, 0f, 0.6f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(1.0f, 0f, -0.2f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(-1.2f, 0f, 2.6f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(0.6f, 0f, 3.0f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(1.4f, 0f, 2.4f), optional = true });
                    break;
                case Kind.Ruin:
                    // Внутри, иногда глубже: ближний предмет у порога, остальные в глубине руины.
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(0.6f, 0f, 1.2f), optional = false });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(-1.2f, 0f, -0.8f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(1.4f, 0f, -1.6f), optional = true });
                    if (p.id == "Ruin_A2_2" || p.id == "Ruin_A2_4")
                        p.fuel.Add(new FuelSpot { fuel = "briquette", local = new Vector3(-1.5f, 0f, -2.2f), optional = true });
                    if (p.id == "Ruin_A1_2")
                        p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(1.0f, 0f, 3.2f), optional = true });
                    break;
                case Kind.Shelter:
                    // Внутри «подковы» из скал: у задней стенки, ближе к входу — щепки.
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(0f, 0f, -0.6f), optional = false });
                    p.fuel.Add(new FuelSpot { fuel = p.id == "Shelter_2" ? "planks" : "kindling", local = new Vector3(-0.9f, 0f, -0.9f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "briquette", local = new Vector3(0.9f, 0f, -1.0f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(0.2f, 0f, 0.6f), optional = true });
                    break;
                case Kind.Truck:
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(1.2f, 0f, -2.8f), optional = false });
                    p.fuel.Add(new FuelSpot { fuel = "planks", local = new Vector3(-1.2f, 0f, -3.4f), optional = true });
                    p.fuel.Add(new FuelSpot { fuel = "kindling", local = new Vector3(0.2f, 0f, -1.0f), optional = true });
                    break;
            }
        }
    }
}
