using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Фургон (ТЗ 8.1): кабина с капотом и скошенным стеклом, жилой кузов-коробка,
    /// труба печки, окно со свечением, колёса.
    /// Ось: вперёд +Z, начало координат — на земле по центру. Снег — только на крыше кузова.
    /// Части: Body (статичный корпус), Wheel (одно колесо, центр — ось вращения X), Window (свечение печки).
    /// </summary>
    public static class VanModel
    {
        // ---- Габариты и опорные точки (для префаба и контроллера) ----
        public const float WheelRadius = 0.42f;
        public const float WheelWidth = 0.3f;
        public static readonly Vector3 WheelFrontLeft = new Vector3(-0.92f, WheelRadius, 1.55f);
        public static readonly Vector3 WheelFrontRight = new Vector3(0.92f, WheelRadius, 1.55f);
        public static readonly Vector3 WheelRearLeft = new Vector3(-0.92f, WheelRadius, -1.35f);
        public static readonly Vector3 WheelRearRight = new Vector3(0.92f, WheelRadius, -1.35f);
        /// <summary>Точка, где встаёт робот после выхода (слева от двери водителя).</summary>
        public static readonly Vector3 ExitPoint = new Vector3(-1.6f, 0f, 1.1f);
        /// <summary>Центр двери водителя (зона «[E] Сесть»).</summary>
        public static readonly Vector3 DoorPoint = new Vector3(-1.0f, 1.1f, 1.1f);
        /// <summary>Где висит тёплый point light печки (внутри кузова, у окна).</summary>
        public static readonly Vector3 StoveLightPoint = new Vector3(0f, 1.45f, -0.9f);
        /// <summary>Верх трубы печки (дым/искры).</summary>
        public static readonly Vector3 ChimneyTop = new Vector3(0.6f, 2.98f, -1.7f);
        public static readonly Vector3 Size = new Vector3(2.0f, 2.4f, 4.7f);

        public static MeshBuilder BuildBody()
        {
            Color paint = PaletteAsset.Get(PaletteColor.Rust);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color glass = PaletteAsset.Get(PaletteColor.Ice) * 0.55f;
            glass.a = 1f;
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            Color roofPaint = paint * 0.9f;
            roofPaint.a = 1f;

            var noSnow = Surface.NoSnow;
            var b = new MeshBuilder();

            // Рама.
            b.Add(Primitives.Box(new Vector3(1.7f, 0.25f, 4.3f), dark, noSnow).Translate(new Vector3(0f, 0.48f, 0.05f)));

            // Жилой кузов: z −2.2 … 0.7, y 0.55 … 2.3.
            b.Add(Primitives.BeveledBox(new Vector3(2.0f, 1.75f, 2.9f), 0.08f, paint, noSnow).Translate(new Vector3(0f, 1.425f, -0.75f)));
            // Крыша кузова — единственная «снежная» часть.
            b.Add(Primitives.BeveledBox(new Vector3(2.04f, 0.08f, 2.94f), 0.03f, roofPaint, Surface.Default).Translate(new Vector3(0f, 2.33f, -0.75f)));
            // Рёбра жёсткости на боках кузова.
            for (int i = 0; i < 3; i++)
            {
                float z = -1.9f + i * 0.6f;
                foreach (float sx in new[] { -1f, 1f })
                    b.Add(Primitives.Box(new Vector3(0.03f, 1.5f, 0.06f), roofPaint, noSnow).Translate(new Vector3(sx * 1.01f, 1.4f, z)));
            }

            // Кабина: низ с капотом (z 0.7 … 2.3, y 0.55 … 1.15).
            b.Add(Primitives.BeveledBox(new Vector3(1.9f, 0.6f, 1.6f), 0.08f, paint, noSnow).Translate(new Vector3(0f, 0.85f, 1.5f)));
            // Верх кабины — клин со скошенным лобовым стеклом (z 0.7 … 1.6, y 1.15 … 1.75).
            var cabTop = Primitives.Wedge(new Vector3(1.86f, 0.6f, 0.9f), paint, noSnow).Translate(new Vector3(0f, 1.45f, 1.15f));
            Deform.PaintWhere(cabTop, n => n.z > 0.3f && n.y > 0.3f, glass);   // лобовое стекло
            Deform.PaintWhere(cabTop, n => Mathf.Abs(n.x) > 0.9f, glass);      // боковые стёкла
            b.Add(cabTop);

            // Дверь водителя — тёмный контур на левом боку кабины.
            b.Add(Primitives.Box(new Vector3(0.02f, 0.55f, 0.75f), dark, noSnow).Translate(new Vector3(-0.96f, 0.88f, 1.1f)));
            // Задняя дверь кузова — контур.
            b.Add(Primitives.Box(new Vector3(1.2f, 1.4f, 0.02f), roofPaint, noSnow).Translate(new Vector3(0f, 1.35f, -2.21f)));

            // Рамки окон кузова (само свечение — отдельная часть Window).
            foreach (float sx in new[] { -1f, 1f })
                b.Add(Primitives.Box(new Vector3(0.03f, 0.6f, 0.9f), dark, noSnow).Translate(new Vector3(sx * 1.005f, 1.6f, -0.9f)));

            // Бамперы.
            b.Add(Primitives.Box(new Vector3(1.95f, 0.18f, 0.15f), dark, noSnow).Translate(new Vector3(0f, 0.62f, 2.35f)));
            b.Add(Primitives.Box(new Vector3(1.95f, 0.18f, 0.15f), dark, noSnow).Translate(new Vector3(0f, 0.62f, -2.27f)));

            // Фары (слабое собственное свечение).
            foreach (float sx in new[] { -0.68f, 0.68f })
            {
                var lamp = Primitives.Cylinder(0.11f, 0.11f, 0.06f, 8, glow, Surface.Glow(0.6f));
                b.Add(lamp.Rotate(new Vector3(90f, 0f, 0f)).Translate(new Vector3(sx, 0.92f, 2.29f)));
            }

            // Труба печки с колпаком.
            b.Add(Primitives.Cylinder(0.09f, 0.09f, 0.55f, 7, dark, noSnow).Translate(new Vector3(0.6f, 2.37f, -1.7f)));
            b.Add(Primitives.Cylinder(0.17f, 0.02f, 0.1f, 7, dark, noSnow).Translate(new Vector3(0.6f, 2.88f, -1.7f)));

            // Брызговики-арки над колёсами (тёмные полосы).
            foreach (var w in new[] { WheelFrontLeft, WheelFrontRight, WheelRearLeft, WheelRearRight })
                b.Add(Primitives.Box(new Vector3(0.36f, 0.08f, 1.0f), dark, noSnow).Translate(new Vector3(w.x, 0.93f, w.z)));

            return b;
        }

        /// <summary>Одно колесо; центр — в начале координат, ось вращения — X.</summary>
        public static MeshBuilder BuildWheel()
        {
            Color tire = PaletteAsset.Get(PaletteColor.MetalDark);
            Color hub = PaletteAsset.Get(PaletteColor.Concrete);
            var s = Primitives.Cylinder(WheelRadius, WheelRadius, WheelWidth, 10, tire, Surface.NoSnow).Translate(new Vector3(0f, -WheelWidth * 0.5f, 0f));
            s.Append(Primitives.Cylinder(0.2f, 0.2f, WheelWidth + 0.02f, 6, hub, Surface.NoSnow).Translate(new Vector3(0f, -WheelWidth * 0.5f - 0.01f, 0f)));
            s.Rotate(new Vector3(0f, 0f, 90f));
            return new MeshBuilder().Add(s);
        }

        /// <summary>Светящиеся стёкла окон кузова (обе стороны). Яркость управляется огнём печки.</summary>
        public static MeshBuilder BuildWindow()
        {
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            var b = new MeshBuilder();
            foreach (float sx in new[] { -1f, 1f })
                b.Add(Primitives.Box(new Vector3(0.02f, 0.5f, 0.8f), glow, Surface.Glow(1f)).Translate(new Vector3(sx * 1.02f, 1.6f, -0.9f)));
            return b;
        }
    }
}
