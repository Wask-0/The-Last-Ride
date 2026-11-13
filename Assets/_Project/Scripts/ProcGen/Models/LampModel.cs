using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Переносная лампа для Искры (акт 3) и банка с тлеющим углём (пролог).
    /// Начало координат — низ по центру. Снега нет (лампа горячая, в руке).
    /// </summary>
    public static class LampModel
    {
        public const float Height = 0.36f;
        /// <summary>Где висит point light пламени.</summary>
        public static readonly Vector3 FlamePoint = new Vector3(0f, 0.12f, 0f);
        /// <summary>Верх ручки — за неё лампу держит рука.</summary>
        public static readonly Vector3 HandleTop = new Vector3(0f, 0.36f, 0f);

        /// <summary>Каркас лампы без стекла.</summary>
        public static MeshBuilder BuildFrame()
        {
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color brass = PaletteAsset.Get(PaletteColor.RobotBody);
            var ns = Surface.NoSnow;
            var b = new MeshBuilder();
            b.Add(Primitives.Cylinder(0.1f, 0.095f, 0.04f, 8, dark, ns));                                   // основание
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                b.Add(Primitives.Box(new Vector3(0.014f, 0.16f, 0.014f), brass, ns).Translate(new Vector3(Mathf.Cos(a) * 0.08f, 0.12f, Mathf.Sin(a) * 0.08f)));
            }
            b.Add(Primitives.Cylinder(0.1f, 0.04f, 0.06f, 8, dark, ns).Translate(new Vector3(0f, 0.2f, 0f)));   // крышка
            b.Add(Primitives.Cylinder(0.025f, 0.025f, 0.03f, 6, brass, ns).Translate(new Vector3(0f, 0.26f, 0f)));

            // Ручка — верхняя половина тора в вертикальной плоскости XY.
            var handle = Primitives.Torus(0.07f, 0.008f, 14, 4, dark, ns).Rotate(new Vector3(90f, 0f, 0f));
            handle.RemoveTriangles(t => handle.TriangleCenter(t).y < -0.005f);
            b.Add(handle.Translate(new Vector3(0f, 0.29f, 0f)));
            return b;
        }

        /// <summary>Светящееся стекло лампы. Яркость управляется запасом огня.</summary>
        public static MeshBuilder BuildGlass()
        {
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            return new MeshBuilder().Add(Primitives.Cylinder(0.072f, 0.072f, 0.16f, 8, glow, Surface.Glow(1f)).Translate(new Vector3(0f, 0.04f, 0f)));
        }

        /// <summary>Погасшая лампа (у замёрзших роботов): каркас + тёмное стекло, одним мешем, слегка завалена набок.</summary>
        public static MeshBuilder BuildDead()
        {
            Color glass = PaletteAsset.Get(PaletteColor.MetalDark);
            var b = BuildFrame();
            b.Add(Primitives.Cylinder(0.072f, 0.072f, 0.16f, 8, glass, Surface.NoSnow).Translate(new Vector3(0f, 0.04f, 0f)));
            var tilted = new MeshBuilder().Add(b, Matrix4x4.TRS(new Vector3(0f, 0.02f, 0f), Quaternion.Euler(0f, 0f, 70f), Vector3.one));
            var bounds = tilted.ComputeBounds();
            return tilted.Offset(new Vector3(0f, -bounds.min.y - 0.01f, 0f));
        }

        /// <summary>Банка с тлеющим углём (Искра в прологе). Уголь светится.</summary>
        public static MeshBuilder BuildSparkJar()
        {
            Color tin = PaletteAsset.Get(PaletteColor.Concrete);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            var b = new MeshBuilder();
            // Стенки без крышки: цилиндр без верхнего торца, изнутри виден (двусторонний).
            var can = Primitives.Cylinder(0.07f, 0.07f, 0.12f, 9, tin, Surface.NoSnow, capBottom: true, capTop: false);
            can.MakeDoubleSided(dark);
            b.Add(can);
            b.Add(Primitives.Torus(0.07f, 0.006f, 9, 3, dark, Surface.NoSnow).Translate(new Vector3(0f, 0.12f, 0f)));
            var coal = Primitives.Icosphere(0.045f, 0, glow, Surface.Glow(1f));
            Deform.Jitter(coal, new Rng(21), 0.01f);
            b.Add(coal.Scale(new Vector3(1f, 0.7f, 1f)).Translate(new Vector3(0f, 0.07f, 0f)));
            return b;
        }
    }
}
