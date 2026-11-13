using System.Collections.Generic;
using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Постройки и крупные точки интереса (ТЗ 5, 8.1): стартовая мастерская, сарай с дровами,
    /// домики-руины, остов грузовика. Внутрь можно зайти — стены с толщиной, проёмы под рост робота.
    /// Начало координат — на земле в центре; вход — со стороны +Z.
    /// Полы и мебель под крышей — без снега; крыши — со снегом.
    /// </summary>
    public static class BuildingModels
    {
        private const float WallThickness = 0.16f;

        // ---- Мастерская ----
        public static readonly Vector3 WorkshopSize = new Vector3(7f, 3.2f, 9f);
        /// <summary>Где в мастерской стоит верстак (на нём — банка с Искрой в прологе).</summary>
        public static readonly Vector3 WorkshopBenchTop = new Vector3(-2.1f, 1.02f, -2.5f);

        /// <summary>
        /// Мастерская-гараж: широкие открытые ворота спереди (+Z), боковая дверь, окна, двускатная крыша,
        /// пол-настил, верстак с чайником и лампой, стеллаж.
        /// </summary>
        public static MeshBuilder BuildWorkshop()
        {
            Color wall = PaletteAsset.Get(PaletteColor.WoodLight);
            Color trim = PaletteAsset.Get(PaletteColor.WoodDark);
            Color roof = PaletteAsset.Get(PaletteColor.Rust);
            Color metal = PaletteAsset.Get(PaletteColor.MetalDark);
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            var ns = Surface.NoSnow;

            float w = WorkshopSize.x, h = WorkshopSize.y, l = WorkshopSize.z;
            float hx = w * 0.5f, hz = l * 0.5f;
            var s = new Shape();

            // Стены по периметру (против часовой, если смотреть сверху): перед, правый бок, зад, левый бок.
            s.Append(BuildingKit.WallBetween(new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), h, WallThickness,
                new[] { Opening.Door(w * 0.5f, 3.6f, 2.7f) }, wall, Surface.Default));
            s.Append(BuildingKit.WallBetween(new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), h, WallThickness,
                new[] { Opening.Door(2.2f, 1.0f, 2.1f), Opening.Window(5.5f, 1.4f, 1.1f, 2.1f) }, wall, Surface.Default));
            s.Append(BuildingKit.WallBetween(new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), h, WallThickness,
                new[] { Opening.Window(w * 0.5f, 1.6f, 1.2f, 2.2f) }, wall, Surface.Default));
            s.Append(BuildingKit.WallBetween(new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), h, WallThickness,
                new[] { Opening.Window(3.0f, 1.4f, 1.1f, 2.1f), Opening.Window(6.0f, 1.4f, 1.1f, 2.1f) }, wall, Surface.Default));

            // Угловые стойки и обвязка — тёмное дерево.
            foreach (float x in new[] { -hx, hx })
            foreach (float z in new[] { -hz, hz })
                s.Append(Primitives.Box(new Vector3(0.24f, h, 0.24f), trim, ns), Matrix4x4.Translate(new Vector3(x, h * 0.5f, z)));
            s.Append(Primitives.Box(new Vector3(3.9f, 0.2f, 0.24f), trim, ns), Matrix4x4.Translate(new Vector3(0f, 2.8f, hz)));

            // Фронтоны и крыша (конёк вдоль Z).
            const float pitch = 28f;
            foreach (float z in new[] { -hz, hz })
                s.Append(BuildingKit.Gable(w, h, pitch, WallThickness, wall, ns), Matrix4x4.Translate(new Vector3(0f, 0f, z)));
            s.Append(BuildingKit.GableRoof(w, l, h, pitch, 0.14f, 0.45f, roof));

            // Пол-настил.
            s.Append(BuildingKit.Floor(w - 0.1f, l - 0.1f, 0.08f, trim));

            // Верстак у задней левой стены.
            var bench = new Shape();
            bench.Append(Primitives.Box(new Vector3(2.4f, 0.08f, 0.8f), trim, ns), Matrix4x4.Translate(new Vector3(0f, 0.9f, 0f)));
            foreach (float x in new[] { -1.1f, 1.1f })
            foreach (float z in new[] { -0.32f, 0.32f })
                bench.Append(Primitives.Box(new Vector3(0.08f, 0.86f, 0.08f), trim, ns), Matrix4x4.Translate(new Vector3(x, 0.43f, z)));
            bench.Append(Primitives.Box(new Vector3(2.3f, 0.05f, 0.7f), trim, ns), Matrix4x4.Translate(new Vector3(0f, 0.3f, 0f)));
            // Чайник.
            bench.Append(Primitives.Cylinder(0.12f, 0.09f, 0.18f, 8, metal, ns), Matrix4x4.Translate(new Vector3(0.6f, 0.94f, 0.05f)));
            bench.Append(Primitives.Cylinder(0.025f, 0.015f, 0.14f, 5, metal, ns), Matrix4x4.TRS(new Vector3(0.71f, 1.0f, 0.05f), Quaternion.Euler(0f, 0f, -55f), Vector3.one));
            bench.Append(Primitives.Torus(0.07f, 0.01f, 8, 3, metal, ns), Matrix4x4.TRS(new Vector3(0.6f, 1.17f, 0.05f), Quaternion.Euler(90f, 0f, 0f), Vector3.one));
            // Настольная лампа (тёплый абажур — слабое свечение: «лампа, чайник, скрип пола»).
            bench.Append(Primitives.Cylinder(0.08f, 0.08f, 0.03f, 6, metal, ns), Matrix4x4.Translate(new Vector3(-0.8f, 0.94f, -0.15f)));
            bench.Append(Primitives.Cylinder(0.015f, 0.015f, 0.35f, 4, metal, ns), Matrix4x4.Translate(new Vector3(-0.8f, 0.97f, -0.15f)));
            bench.Append(Primitives.Cylinder(0.16f, 0.06f, 0.14f, 7, glow, Surface.Glow(0.4f)), Matrix4x4.Translate(new Vector3(-0.8f, 1.27f, -0.15f)));
            s.Append(bench, Matrix4x4.Translate(new Vector3(WorkshopBenchTop.x, 0.08f, WorkshopBenchTop.z)));

            // Стеллаж у правой стены.
            var shelf = new Shape();
            foreach (float y in new[] { 0.4f, 1.0f, 1.6f, 2.2f })
                shelf.Append(Primitives.Box(new Vector3(0.5f, 0.05f, 2.0f), trim, ns), Matrix4x4.Translate(new Vector3(0f, y, 0f)));
            foreach (float z in new[] { -0.95f, 0.95f })
                shelf.Append(Primitives.Box(new Vector3(0.5f, 2.3f, 0.06f), trim, ns), Matrix4x4.Translate(new Vector3(0f, 1.15f, z)));
            shelf.Append(Primitives.Box(new Vector3(0.35f, 0.3f, 0.4f), roof, ns), Matrix4x4.Translate(new Vector3(0f, 1.18f, -0.4f)));
            shelf.Append(Primitives.Cylinder(0.14f, 0.14f, 0.35f, 7, metal, ns), Matrix4x4.Translate(new Vector3(0f, 0.43f, 0.5f)));
            s.Append(shelf, Matrix4x4.Translate(new Vector3(hx - 0.45f, 0.08f, -1.8f)));

            return new MeshBuilder().Add(s);
        }

        // ---- Сарай ----
        public static readonly Vector3 ShedSize = new Vector3(3.4f, 2.5f, 2.6f);

        /// <summary>Сарай с дровами: три стены, открытый перед (+Z), односкатная крыша, поленница внутри.</summary>
        public static MeshBuilder BuildShed()
        {
            Color plank = PaletteAsset.Get(PaletteColor.WoodDark);
            Color log = PaletteAsset.Get(PaletteColor.WoodLight);
            Color roof = PaletteAsset.Get(PaletteColor.RockDark);
            var ns = Surface.NoSnow;
            float w = ShedSize.x, hFront = ShedSize.y, l = ShedSize.z;
            float hBack = hFront - 0.5f;
            float hx = w * 0.5f, hz = l * 0.5f;
            var s = new Shape();

            s.Append(BuildingKit.WallBetween(new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), hBack, 0.1f, null, plank, Surface.Default));
            // Боковые стены — трапеции: две плиты разной высоты.
            foreach (float x in new[] { -hx, hx })
            {
                s.Append(Primitives.Box(new Vector3(0.1f, hBack, l), plank, Surface.Default), Matrix4x4.Translate(new Vector3(x, hBack * 0.5f, 0f)));
                var tri = new[] { new Vector2(-hz, hBack), new Vector2(hz, hBack), new Vector2(hz, hFront) };
                s.Append(Primitives.ExtrudeConvex(tri, 0.1f, plank, ns), Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), Vector3.one));
            }
            // Стойки спереди.
            foreach (float x in new[] { -hx, hx })
                s.Append(Primitives.Box(new Vector3(0.14f, hFront, 0.14f), plank, ns), Matrix4x4.Translate(new Vector3(x, hFront * 0.5f, hz)));

            // Односкатная крыша: высокая сторона спереди.
            float slope = Mathf.Atan2(hFront - hBack, l) * Mathf.Rad2Deg;
            float roofLen = Mathf.Sqrt(l * l + (hFront - hBack) * (hFront - hBack)) + 0.6f;
            s.Append(Primitives.Box(new Vector3(w + 0.5f, 0.1f, roofLen), roof, Surface.Default),
                Matrix4x4.TRS(new Vector3(0f, (hFront + hBack) * 0.5f + 0.06f, 0f), Quaternion.Euler(-slope, 0f, 0f), Vector3.one));

            // Поленница у задней стены: ряды брёвен вдоль X.
            var rng = new Rng(77);
            for (int row = 0; row < 4; row++)
            {
                int count = 6 - row;
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) * 0.5f) * 0.42f + (row % 2) * 0.05f;
                    var logShape = Primitives.Cylinder(0.12f, 0.12f, 0.7f, 6, log, ns);
                    s.Append(logShape, Matrix4x4.TRS(new Vector3(x, 0.12f + row * 0.21f, -hz + 0.15f), Quaternion.Euler(90f, rng.Range(-5f, 5f), 0f), Vector3.one));
                }
            }
            return new MeshBuilder().Add(s);
        }

        // ---- Руины ----

        /// <summary>
        /// Домик-руина: обломанный верх стен, дверной проём спереди (+Z), окна, часть стен выбита,
        /// обрушенный кусок крыши внутри. Материал стен — камень или дерево (по seed).
        /// </summary>
        public static MeshBuilder BuildRuin(int seed, bool stone)
        {
            var rng = new Rng(seed);
            Color wall = stone ? PaletteAsset.Get(PaletteColor.Concrete) : PaletteAsset.Get(PaletteColor.WoodDark);
            Color beam = PaletteAsset.Get(PaletteColor.WoodDark);
            Color roof = PaletteAsset.Get(PaletteColor.Rust);

            float w = rng.Range(4.2f, 6f), l = rng.Range(4f, 5.5f), h = rng.Range(2.5f, 2.9f);
            float hx = w * 0.5f, hz = l * 0.5f;
            float t = stone ? 0.28f : 0.14f;
            var s = new Shape();

            var corners = new[] { new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz) };
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                float len = Vector3.Distance(a, b);
                var openings = new List<Opening>();
                if (i == 0) openings.Add(Opening.Door(len * rng.Range(0.35f, 0.65f), 1.1f, 2.0f));
                else if (rng.Chance(0.7f)) openings.Add(Opening.Window(len * rng.Range(0.3f, 0.7f), 1.0f, 1.0f, 1.8f));
                // Одну боковую стену частично разрушаем — большой пролом.
                if (i != 0 && rng.Chance(0.35f)) openings.Add(new Opening(len * 0.15f, len * rng.Range(0.4f, 0.6f), rng.Range(0.4f, 1.0f), h + 1f));
                s.Append(BuildingKit.WallBetween(a, b, h, t, openings, wall, Surface.Default, rng.Fork(i), rng.Range(0.6f, 1.4f)));
            }

            // Остатки балок перекрытия.
            for (int i = 0; i < 3; i++)
            {
                if (!rng.Chance(0.6f)) continue;
                float z = Mathf.Lerp(-hz + 0.6f, hz - 0.6f, i / 2f);
                s.Append(Primitives.Box(new Vector3(w * rng.Range(0.5f, 1f), 0.14f, 0.16f), beam),
                    Matrix4x4.TRS(new Vector3(rng.Range(-0.4f, 0.4f), h - 0.6f - rng.Value * 0.4f, z), Quaternion.Euler(0f, rng.Range(-8f, 8f), rng.Range(-10f, 10f)), Vector3.one));
            }

            // Обрушенный кусок крыши: наклонная плита, один край на земле.
            var slab = Primitives.Box(new Vector3(w * 0.6f, 0.12f, l * 0.55f), roof);
            s.Append(slab, Matrix4x4.TRS(new Vector3(rng.Range(-0.6f, 0.6f), 0.9f, -hz * 0.35f), Quaternion.Euler(rng.Range(15f, 25f), rng.Range(-20f, 20f), rng.Range(-12f, 12f)), Vector3.one));

            // Мусор: несколько камней/обломков у стен.
            for (int i = 0; i < 4; i++)
            {
                var debris = Primitives.Icosphere(rng.Range(0.15f, 0.3f), 0, wall);
                Deform.Jitter(debris, rng.Fork(20 + i), 0.05f);
                var p = new Vector3(rng.Range(-hx, hx) * 0.8f, 0.05f, rng.Range(-hz, hz) * 0.8f);
                s.Append(debris, Matrix4x4.Translate(p));
            }
            return new MeshBuilder().Add(s);
        }

        // ---- Остов грузовика ----

        /// <summary>Остов грузовика: ржавая кабина с выбитыми стёклами, рама, кузов с выломанными досками, два колеса. Длина ~6 м вдоль Z.</summary>
        public static MeshBuilder BuildTruckWreck()
        {
            var rng = new Rng(404);
            Color rust = PaletteAsset.Get(PaletteColor.Rust);
            Color rustDark = rust * 0.7f;
            rustDark.a = 1f;
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color wood = PaletteAsset.Get(PaletteColor.WoodDark);
            var s = new Shape();

            // Рама.
            foreach (float x in new[] { -0.55f, 0.55f })
                s.Append(Primitives.Box(new Vector3(0.18f, 0.25f, 5.8f), dark), Matrix4x4.Translate(new Vector3(x, 0.55f, 0f)));

            // Кабина спереди (+Z) с выбитыми гранями (двусторонняя — видно внутрь).
            var cab = Primitives.BeveledBox(new Vector3(2.1f, 1.6f, 1.7f), 0.12f, rust);
            Deform.PaintWhere(cab, n => n.z > 0.7f || Mathf.Abs(n.x) > 0.7f, rustDark);
            Deform.PunchFaces(cab, rng.Fork(1), 1f, n => n.z > 0.9f);                       // лобовое стекло выбито всегда
            Deform.PunchFaces(cab, rng.Fork(2), 0.5f, n => Mathf.Abs(n.x) > 0.9f);          // боковые окна — через раз
            cab.MakeDoubleSided(dark);
            s.Append(cab, Matrix4x4.Translate(new Vector3(0f, 1.55f, 2.0f)));
            var hood = Primitives.BeveledBox(new Vector3(1.9f, 0.8f, 1.2f), 0.1f, rust);
            s.Append(hood, Matrix4x4.Translate(new Vector3(0f, 1.1f, 3.3f)));

            // Кузов: пол и борта из досок, часть досок выломана.
            s.Append(Primitives.Box(new Vector3(2.2f, 0.1f, 3.8f), wood), Matrix4x4.Translate(new Vector3(0f, 0.78f, -1.0f)));
            foreach (float x in new[] { -1.08f, 1.08f })
            {
                for (int i = 0; i < 3; i++)
                {
                    if (rng.Chance(0.3f)) continue;
                    s.Append(Primitives.Box(new Vector3(0.06f, 0.18f, 3.8f * rng.Range(0.5f, 1f)), wood),
                        Matrix4x4.TRS(new Vector3(x, 1.0f + i * 0.22f, -1.0f + rng.Range(-0.4f, 0.4f)), Quaternion.Euler(rng.Range(-3f, 3f), 0f, 0f), Vector3.one));
                }
            }

            // Два уцелевших колеса (остальные — на ступицах).
            foreach (var p in new[] { new Vector3(-1.05f, 0.5f, 2.6f), new Vector3(1.05f, 0.45f, -2.0f) })
            {
                var wheel = Primitives.Cylinder(0.5f, 0.5f, 0.35f, 9, dark).Translate(new Vector3(0f, -0.175f, 0f));
                wheel.Rotate(new Vector3(0f, 0f, 90f));
                s.Append(wheel, Matrix4x4.Translate(p));
            }
            foreach (var p in new[] { new Vector3(1.0f, 0.35f, 2.6f), new Vector3(-1.0f, 0.3f, -2.0f) })
                s.Append(Primitives.Cylinder(0.18f, 0.18f, 0.2f, 6, dark).Rotate(new Vector3(0f, 0f, 90f)), Matrix4x4.Translate(p));

            // Остов осел на один бок.
            s.Transform(Matrix4x4.Rotate(Quaternion.Euler(2f, 0f, -5f)));
            return new MeshBuilder().Add(s);
        }
    }
}
