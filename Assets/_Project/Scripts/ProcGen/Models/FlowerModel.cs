using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Цветок для титров (ТЗ 3, 12.3): пробивается из снега и раскрывается.
    /// <see cref="BuildFlower"/> строит меш для стадии роста 0..1 — вызывается в рантайме каждый кадр
    /// анимации (меш маленький, ~300 треугольников). Начало координат — точка выхода из снега.
    /// </summary>
    public static class FlowerModel
    {
        public const float StemLength = 0.24f;
        public const int PetalCount = 6;

        /// <param name="growth">0 — ещё под снегом, 1 — полностью раскрылся.</param>
        public static MeshBuilder BuildFlower(float growth, MeshBuilder reuse = null)
        {
            growth = Mathf.Clamp01(growth);
            var b = reuse ?? new MeshBuilder();
            b.Clear();

            Color stemColor = PaletteAsset.Get(PaletteColor.Pine);
            Color leafColor = Color.Lerp(stemColor, PaletteAsset.Get(PaletteColor.WoodLight), 0.25f);
            Color petalColor = PaletteAsset.Get(PaletteColor.WarmGlow);
            Color heartColor = PaletteAsset.Get(PaletteColor.BeaconLight);
            var ns = Surface.NoSnow;

            // Фазы роста (с перекрытием, чтобы было плавно).
            float stemT = Smooth(Mathf.InverseLerp(0.0f, 0.4f, growth));
            float leafT = Smooth(Mathf.InverseLerp(0.25f, 0.6f, growth));
            float budT = Smooth(Mathf.InverseLerp(0.45f, 0.7f, growth));
            float openT = Smooth(Mathf.InverseLerp(0.65f, 1.0f, growth));

            float len = Mathf.Max(0.001f, StemLength * stemT);
            if (stemT <= 0.001f) return b;

            // Стебель: слегка изогнутый цилиндр.
            var stem = Primitives.Cylinder(0.009f, 0.006f, len, 5, stemColor, ns);
            Deform.Bend(stem, 14f * stemT, len);
            b.Add(stem);

            // Вершина стебля после изгиба (по формуле Bend).
            float bendRad = 14f * stemT * Mathf.Deg2Rad;
            Vector3 tip = bendRad > 1e-4f
                ? new Vector3(len / bendRad * (1f - Mathf.Cos(bendRad)), len / bendRad * Mathf.Sin(bendRad), 0f)
                : new Vector3(0f, len, 0f);
            var tipRot = Quaternion.Euler(0f, 0f, -14f * stemT);

            // Два листа у основания.
            if (leafT > 0.001f)
            {
                var leafShape = new[] { new Vector2(0f, 0f), new Vector2(0.032f, 0.05f), new Vector2(0f, 0.12f), new Vector2(-0.032f, 0.05f) };
                for (int i = 0; i < 2; i++)
                {
                    var leaf = Primitives.ExtrudeConvex(leafShape, 0.003f, leafColor, ns);
                    float yaw = i * 180f + 30f;
                    float tilt = Mathf.Lerp(10f, 62f, leafT);
                    b.Add(leaf, Matrix4x4.TRS(new Vector3(0f, 0.035f + i * 0.02f, 0f), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f), Vector3.one * leafT));
                }
            }

            // Бутон → лепестки. Лепестки вращаются от сомкнутого (почти вертикально) к раскрытому.
            if (budT > 0.001f)
            {
                var petalShape = new[] { new Vector2(0f, 0f), new Vector2(0.026f, 0.034f), new Vector2(0.016f, 0.075f), new Vector2(-0.016f, 0.075f), new Vector2(-0.026f, 0.034f) };
                float open = Mathf.Lerp(8f, 78f, openT);
                float scale = Mathf.Lerp(0.5f, 1f, openT) * budT;
                var petalSurface = new Surface(0f, Mathf.Lerp(0.05f, 0.35f, openT));
                for (int i = 0; i < PetalCount; i++)
                {
                    var petal = Primitives.ExtrudeConvex(petalShape, 0.0025f, petalColor, petalSurface);
                    var rot = tipRot * Quaternion.Euler(0f, i * (360f / PetalCount), 0f) * Quaternion.Euler(open, 0f, 0f);
                    b.Add(petal, Matrix4x4.TRS(tip, rot, Vector3.one * scale));
                }
                var heart = Primitives.Icosphere(0.016f, 1, heartColor, new Surface(0f, Mathf.Lerp(0.2f, 0.8f, openT)));
                b.Add(heart, Matrix4x4.TRS(tip + tipRot * new Vector3(0f, 0.004f, 0f), tipRot, Vector3.one * budT));
            }
            return b;
        }

        /// <summary>Сугробик, из которого растёт цветок (статичный).</summary>
        public static MeshBuilder BuildMound()
        {
            var mound = Primitives.Icosphere(0.5f, 2, PaletteAsset.Get(PaletteColor.Snow));
            Deform.NoiseAlongNormal(mound, new Noise3(2026), 0.08f, 3f, 2);
            mound.Scale(new Vector3(0.9f, 0.18f, 0.9f)).Translate(new Vector3(0f, -0.06f, 0f));
            return new MeshBuilder().Add(mound);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
