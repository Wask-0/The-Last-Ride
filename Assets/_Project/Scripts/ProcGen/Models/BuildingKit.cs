using System.Collections.Generic;
using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>Проём в стене: по длине стены [x0, x1], по высоте [y0, y1].</summary>
    public struct Opening
    {
        public float x0, x1, y0, y1;

        public Opening(float x0, float x1, float y0, float y1)
        {
            this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1;
        }

        public static Opening Door(float center, float width, float height) => new Opening(center - width * 0.5f, center + width * 0.5f, -1f, height);
        public static Opening Window(float center, float width, float sill, float top) => new Opening(center - width * 0.5f, center + width * 0.5f, sill, top);
    }

    /// <summary>
    /// Конструктор построек: стены-плиты с проёмами (у стен есть толщина — изнутри видно),
    /// двускатные и односкатные крыши, фронтоны.
    /// Соглашение для стены: идёт вдоль +X от 0 до length, толщина по Z (центр z = 0), низ на y = 0.
    /// </summary>
    public static class BuildingKit
    {
        /// <param name="jagged">
        /// Для руин: ГСЧ, задающий «обломанный» верх стены; null — ровная стена.
        /// Профиль облома — плавный шум вдоль стены, у углов стена почти целая (как у настоящих руин).
        /// </param>
        public static Shape Wall(float length, float height, float thickness, IList<Opening> openings, Color color, Surface surface,
            Rng jagged = null, float jaggedDepth = 0f, float pieceLength = 0.35f)
        {
            Noise3 profile = jagged != null ? new Noise3((int)jagged.NextUInt()) : null;
            float phase = jagged != null ? jagged.Range(0f, 100f) : 0f;
            var s = new Shape();
            openings = openings ?? new Opening[0];

            // Точки разбиения по X.
            var xs = new List<float> { 0f, length };
            foreach (var o in openings)
            {
                xs.Add(Mathf.Clamp(o.x0, 0f, length));
                xs.Add(Mathf.Clamp(o.x1, 0f, length));
            }
            if (jagged != null)
            {
                int pieces = Mathf.Max(1, Mathf.RoundToInt(length / pieceLength));
                for (int i = 1; i < pieces; i++) xs.Add(length * i / pieces);
            }
            xs.Sort();

            for (int i = 0; i < xs.Count - 1; i++)
            {
                float a = xs[i], b = xs[i + 1];
                if (b - a < 1e-3f) continue;
                float mid = (a + b) * 0.5f;
                float top = height;
                if (profile != null)
                {
                    float n = 0.5f + 0.5f * profile.Fbm(new Vector3(mid * 0.55f + phase, 0f, 0f), 2);
                    float cornerKeep = Mathf.Sin(Mathf.Clamp01(mid / length) * Mathf.PI); // 0 у углов, 1 в середине
                    top = Mathf.Max(0.3f, height - Mathf.Clamp01(n * 1.3f) * jaggedDepth * Mathf.Lerp(0.15f, 1f, cornerKeep));
                }

                // Сплошные участки по Y = [0, top] минус проёмы, покрывающие mid.
                var spans = new List<Vector2> { new Vector2(0f, top) };
                foreach (var o in openings)
                {
                    if (mid <= o.x0 || mid >= o.x1) continue;
                    var next = new List<Vector2>();
                    foreach (var sp in spans)
                    {
                        if (o.y1 <= sp.x || o.y0 >= sp.y) { next.Add(sp); continue; }
                        if (o.y0 > sp.x) next.Add(new Vector2(sp.x, o.y0));
                        if (o.y1 < sp.y) next.Add(new Vector2(o.y1, sp.y));
                    }
                    spans = next;
                }

                foreach (var sp in spans)
                {
                    float h = sp.y - sp.x;
                    if (h < 0.02f) continue;
                    var box = Primitives.Box(new Vector3(b - a, h, thickness), color, surface);
                    s.Append(box, Matrix4x4.Translate(new Vector3(mid, sp.x + h * 0.5f, 0f)));
                }
            }
            return s;
        }

        /// <summary>Стена, повёрнутая и поставленная: начало в <paramref name="from"/>, идёт к <paramref name="to"/> (по XZ).</summary>
        public static Shape WallBetween(Vector3 from, Vector3 to, float height, float thickness, IList<Opening> openings, Color color, Surface surface,
            Rng jagged = null, float jaggedDepth = 0f)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float len = d.magnitude;
            var w = Wall(len, height, thickness, openings, color, surface, jagged, jaggedDepth);
            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg; // стена изначально вдоль +X
            return w.Transform(Matrix4x4.TRS(from, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
        }

        /// <summary>
        /// Двускатная крыша: конёк вдоль Z, ширина по X = <paramref name="width"/>, длина по Z = <paramref name="length"/>,
        /// карниз на высоте <paramref name="eaveHeight"/>. Скаты с толщиной и свесами.
        /// </summary>
        public static Shape GableRoof(float width, float length, float eaveHeight, float pitchDeg, float thickness, float overhang, Color color)
        {
            var s = new Shape();
            float half = width * 0.5f + overhang;
            float pitch = pitchDeg * Mathf.Deg2Rad;
            float slopeLen = half / Mathf.Cos(pitch);
            float rise = (width * 0.5f) * Mathf.Tan(pitch);
            foreach (float side in new[] { -1f, 1f })
            {
                var slab = Primitives.Box(new Vector3(slopeLen, thickness, length + overhang * 2f), color, Surface.Default);
                // Центр ската: на середине между коньком и карнизом.
                var center = new Vector3(side * half * 0.5f, eaveHeight + rise - Mathf.Tan(pitch) * half * 0.5f + thickness * 0.5f, 0f);
                s.Append(slab, Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, -side * pitchDeg), Vector3.one));
            }
            return s;
        }

        /// <summary>Треугольный фронтон под двускатной крышей (в плоскости XY, толщина по Z, центр z = 0).</summary>
        public static Shape Gable(float width, float eaveHeight, float pitchDeg, float thickness, Color color, Surface surface)
        {
            float rise = (width * 0.5f) * Mathf.Tan(pitchDeg * Mathf.Deg2Rad);
            var tri = new[] { new Vector2(-width * 0.5f, eaveHeight), new Vector2(width * 0.5f, eaveHeight), new Vector2(0f, eaveHeight + rise) };
            return Primitives.ExtrudeConvex(tri, thickness, color, surface);
        }

        /// <summary>Пол-настил (доски) — без снега, т.к. под крышей.</summary>
        public static Shape Floor(float width, float length, float height, Color color)
        {
            return Primitives.Box(new Vector3(width, height, length), color, Surface.NoSnow).Translate(new Vector3(0f, height * 0.5f, 0f));
        }
    }
}
