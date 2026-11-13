using System.Collections.Generic;
using UnityEngine;

namespace LastRide.ProcGen
{
    /// <summary>
    /// Базовые low-poly примитивы. Все возвращают <see cref="Shape"/> с общими вершинами,
    /// чтобы их можно было деформировать перед сборкой.
    /// Соглашения: коробки, икосфера, капсула, тор — центр в начале координат;
    /// цилиндр/конус стоит основанием на y = 0 (удобно складывать ярусами).
    /// </summary>
    public static class Primitives
    {
        // ---------------- Коробки ----------------

        public static Shape Box(Vector3 size, Color color) => Box(size, color, Surface.Default);

        public static Shape Box(Vector3 size, Color color, Surface surface)
        {
            var s = new Shape();
            Vector3 h = size * 0.5f;
            var v = new int[8];
            for (int i = 0; i < 8; i++)
                v[i] = s.AddVertex(new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));

            // Индекс бита: 1 — x, 2 — y, 4 — z.
            s.AddQuadFacing(v[0], v[2], v[6], v[4], Vector3.left, color, surface);
            s.AddQuadFacing(v[1], v[5], v[7], v[3], Vector3.right, color, surface);
            s.AddQuadFacing(v[0], v[4], v[5], v[1], Vector3.down, color, surface);
            s.AddQuadFacing(v[2], v[3], v[7], v[6], Vector3.up, color, surface);
            s.AddQuadFacing(v[0], v[1], v[3], v[2], Vector3.back, color, surface);
            s.AddQuadFacing(v[4], v[6], v[7], v[5], Vector3.forward, color, surface);
            return s;
        }

        /// <summary>Коробка со скошенными рёбрами (фаской) — кузов фургона, бетон, ящики.</summary>
        public static Shape BeveledBox(Vector3 size, float bevel, Color color) => BeveledBox(size, bevel, color, Surface.Default);

        public static Shape BeveledBox(Vector3 size, float bevel, Color color, Surface surface)
        {
            Vector3 h = size * 0.5f;
            bevel = Mathf.Clamp(bevel, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.99f);
            if (bevel <= 1e-4f) return Box(size, color, surface);

            var s = new Shape();
            // Для каждого угла (sx, sy, sz) и каждой оси — своя вершина, утопленная по двум другим осям.
            var idx = new Dictionary<int, int>();
            int Key(int axis, int sx, int sy, int sz) => axis * 8 + (sx > 0 ? 1 : 0) + (sy > 0 ? 2 : 0) + (sz > 0 ? 4 : 0);
            int V(int axis, int sx, int sy, int sz)
            {
                int k = Key(axis, sx, sy, sz);
                if (idx.TryGetValue(k, out int i)) return i;
                Vector3 p = new Vector3(
                    sx * (axis == 0 ? h.x : h.x - bevel),
                    sy * (axis == 1 ? h.y : h.y - bevel),
                    sz * (axis == 2 ? h.z : h.z - bevel));
                i = s.AddVertex(p);
                idx[k] = i;
                return i;
            }

            int[] signs = { -1, 1 };
            // Основные грани.
            foreach (int sg in signs)
            {
                s.AddQuadFacing(V(0, sg, -1, -1), V(0, sg, 1, -1), V(0, sg, 1, 1), V(0, sg, -1, 1), new Vector3(sg, 0, 0), color, surface);
                s.AddQuadFacing(V(1, -1, sg, -1), V(1, 1, sg, -1), V(1, 1, sg, 1), V(1, -1, sg, 1), new Vector3(0, sg, 0), color, surface);
                s.AddQuadFacing(V(2, -1, -1, sg), V(2, 1, -1, sg), V(2, 1, 1, sg), V(2, -1, 1, sg), new Vector3(0, 0, sg), color, surface);
            }
            // Фаски рёбер.
            foreach (int a in signs)
            foreach (int b in signs)
            {
                // Ребро вдоль Z между гранями X и Y.
                s.AddQuadFacing(V(0, a, b, -1), V(0, a, b, 1), V(1, a, b, 1), V(1, a, b, -1), new Vector3(a, b, 0), color, surface);
                // Ребро вдоль Y между гранями X и Z.
                s.AddQuadFacing(V(0, a, -1, b), V(0, a, 1, b), V(2, a, 1, b), V(2, a, -1, b), new Vector3(a, 0, b), color, surface);
                // Ребро вдоль X между гранями Y и Z.
                s.AddQuadFacing(V(1, -1, a, b), V(1, 1, a, b), V(2, 1, a, b), V(2, -1, a, b), new Vector3(0, a, b), color, surface);
            }
            // Уголки.
            foreach (int x in signs)
            foreach (int y in signs)
            foreach (int z in signs)
                s.AddTriangleFacing(V(0, x, y, z), V(1, x, y, z), V(2, x, y, z), new Vector3(x, y, z), color, surface);
            return s;
        }

        /// <summary>
        /// Клин: низ — полный прямоугольник, высокая сторона сзади (−Z), скат вниз к +Z.
        /// Центр габаритов — в начале координат.
        /// </summary>
        public static Shape Wedge(Vector3 size, Color color) => Wedge(size, color, Surface.Default);

        public static Shape Wedge(Vector3 size, Color color, Surface surface)
        {
            var s = new Shape();
            Vector3 h = size * 0.5f;
            int b0 = s.AddVertex(new Vector3(-h.x, -h.y, -h.z));
            int b1 = s.AddVertex(new Vector3(h.x, -h.y, -h.z));
            int b2 = s.AddVertex(new Vector3(h.x, -h.y, h.z));
            int b3 = s.AddVertex(new Vector3(-h.x, -h.y, h.z));
            int t0 = s.AddVertex(new Vector3(-h.x, h.y, -h.z));
            int t1 = s.AddVertex(new Vector3(h.x, h.y, -h.z));

            Vector3 slopeNormal = new Vector3(0f, size.z, size.y);
            s.AddQuadFacing(b0, b1, b2, b3, Vector3.down, color, surface);
            s.AddQuadFacing(b0, t0, t1, b1, Vector3.back, color, surface);
            s.AddQuadFacing(t0, b3, b2, t1, slopeNormal, color, surface);
            s.AddTriangleFacing(b0, b3, t0, Vector3.left, color, surface);
            s.AddTriangleFacing(b1, t1, b2, Vector3.right, color, surface);
            return s;
        }

        // ---------------- Тела вращения ----------------

        /// <summary>
        /// Цилиндр / усечённый конус / конус (<paramref name="radiusTop"/> = 0), основание на y = 0.
        /// </summary>
        public static Shape Cylinder(float radiusBottom, float radiusTop, float height, int sides, Color color,
            bool capBottom = true, bool capTop = true) =>
            Cylinder(radiusBottom, radiusTop, height, sides, color, Surface.Default, capBottom, capTop);

        public static Shape Cylinder(float radiusBottom, float radiusTop, float height, int sides, Color color, Surface surface,
            bool capBottom = true, bool capTop = true)
        {
            sides = Mathf.Max(3, sides);
            var s = new Shape();
            bool apex = radiusTop <= 1e-4f;
            var bottom = new int[sides];
            var top = new int[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bottom[i] = s.AddVertex(dir * radiusBottom);
                if (!apex) top[i] = s.AddVertex(dir * radiusTop + Vector3.up * height);
            }
            int apexIndex = apex ? s.AddVertex(Vector3.up * height) : -1;

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                float am = (i + 0.5f) * Mathf.PI * 2f / sides;
                var outward = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
                // Наклон боковины учитываем, чтобы проверка направления была надёжной у конусов.
                outward.y = (radiusBottom - radiusTop) / Mathf.Max(height, 1e-4f);
                if (apex) s.AddTriangleFacing(bottom[i], bottom[j], apexIndex, outward, color, surface);
                else s.AddQuadFacing(bottom[i], bottom[j], top[j], top[i], outward, color, surface);
            }

            if (capBottom) AddCap(s, bottom, Vector3.zero, Vector3.down, color, surface);
            if (capTop && !apex) AddCap(s, top, Vector3.up * height, Vector3.up, color, surface);
            return s;
        }

        private static void AddCap(Shape s, int[] ring, Vector3 center, Vector3 normal, Color color, Surface surface)
        {
            int c = s.AddVertex(center);
            int g = s.NewGroup();
            for (int i = 0; i < ring.Length; i++)
                s.AddTriangleFacing(c, ring[i], ring[(i + 1) % ring.Length], normal, color, surface, g);
        }

        /// <summary>Икосфера (для скал — 0–1 уровень дробления, для головы робота — 1–2).</summary>
        public static Shape Icosphere(float radius, int subdivisions, Color color) => Icosphere(radius, subdivisions, color, Surface.Default);

        public static Shape Icosphere(float radius, int subdivisions, Color color, Surface surface)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (int sub = 0; sub < Mathf.Clamp(subdivisions, 0, 4); sub++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                    m = verts.Count - 1;
                    cache[key] = m;
                    return m;
                }
                var next = new List<int>(faces.Count * 4);
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }

            var s = new Shape();
            foreach (var v in verts) s.AddVertex(v * radius);
            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 center = (verts[faces[i]] + verts[faces[i + 1]] + verts[faces[i + 2]]) / 3f;
                s.AddTriangleFacing(faces[i], faces[i + 1], faces[i + 2], center, color, surface);
            }
            return s;
        }

        /// <summary>Капсула низкой детализации (корпус робота). Общая высота — <paramref name="height"/>, центр в начале координат.</summary>
        public static Shape Capsule(float radius, float height, int sides, int hemisphereRings, Color color) =>
            Capsule(radius, height, sides, hemisphereRings, color, Surface.Default);

        public static Shape Capsule(float radius, float height, int sides, int hemisphereRings, Color color, Surface surface)
        {
            sides = Mathf.Max(3, sides);
            hemisphereRings = Mathf.Max(1, hemisphereRings);
            float cyl = Mathf.Max(0f, height * 0.5f - radius); // половина высоты цилиндрической части
            var s = new Shape();

            // Кольца снизу вверх (без полюсов).
            var rings = new List<int[]>();
            void Ring(float y, float r)
            {
                var ring = new int[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    ring[i] = s.AddVertex(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
                rings.Add(ring);
            }

            for (int k = 1; k <= hemisphereRings; k++)
            {
                float phi = -Mathf.PI / 2f + k * (Mathf.PI / 2f) / hemisphereRings;
                Ring(-cyl + Mathf.Sin(phi) * radius, Mathf.Cos(phi) * radius);
            }
            for (int k = 0; k < hemisphereRings; k++)
            {
                float phi = k * (Mathf.PI / 2f) / hemisphereRings;
                if (k == 0 && cyl <= 1e-4f) continue; // нет цилиндра — экватор уже добавлен
                Ring(cyl + Mathf.Sin(phi) * radius, Mathf.Cos(phi) * radius);
            }

            int bottomPole = s.AddVertex(new Vector3(0f, -cyl - radius, 0f));
            int topPole = s.AddVertex(new Vector3(0f, cyl + radius, 0f));

            Vector3 Outward(Vector3 p) => p - new Vector3(0f, Mathf.Clamp(p.y, -cyl, cyl), 0f);

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                var first = rings[0];
                var last = rings[rings.Count - 1];
                s.AddTriangleFacing(bottomPole, first[i], first[j], Vector3.down, color, surface);
                s.AddTriangleFacing(topPole, last[j], last[i], Vector3.up, color, surface);
                for (int r = 0; r < rings.Count - 1; r++)
                {
                    var lo = rings[r];
                    var hi = rings[r + 1];
                    Vector3 c = (s.Vertices[lo[i]] + s.Vertices[lo[j]] + s.Vertices[hi[i]] + s.Vertices[hi[j]]) * 0.25f;
                    s.AddQuadFacing(lo[i], lo[j], hi[j], hi[i], Outward(c), color, surface);
                }
            }
            return s;
        }

        /// <summary>Тор вокруг оси Y (шины, ручка лампы).</summary>
        public static Shape Torus(float majorRadius, float minorRadius, int segments, int sides, Color color) =>
            Torus(majorRadius, minorRadius, segments, sides, color, Surface.Default);

        public static Shape Torus(float majorRadius, float minorRadius, int segments, int sides, Color color, Surface surface)
        {
            segments = Mathf.Max(3, segments);
            sides = Mathf.Max(3, sides);
            var s = new Shape();
            var grid = new int[segments, sides];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < sides; k++)
                {
                    float b = k * Mathf.PI * 2f / sides;
                    var p = radial * (majorRadius + Mathf.Cos(b) * minorRadius) + Vector3.up * (Mathf.Sin(b) * minorRadius);
                    grid[i, k] = s.AddVertex(p);
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int i2 = (i + 1) % segments;
                float am = (i + 0.5f) * Mathf.PI * 2f / segments;
                var ringCenter = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * majorRadius;
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    Vector3 c = (s.Vertices[grid[i, k]] + s.Vertices[grid[i2, k]] + s.Vertices[grid[i2, k2]] + s.Vertices[grid[i, k2]]) * 0.25f;
                    s.AddQuadFacing(grid[i, k], grid[i2, k], grid[i2, k2], grid[i, k2], c - ringCenter, color, surface);
                }
            }
            return s;
        }

        // ---------------- Плоские ----------------

        /// <summary>Плоская сетка в XZ, центр в начале координат, лицом вверх (основа сугробов, площадок).</summary>
        public static Shape Grid(float sizeX, float sizeZ, int cellsX, int cellsZ, Color color) =>
            Grid(sizeX, sizeZ, cellsX, cellsZ, color, Surface.Default);

        public static Shape Grid(float sizeX, float sizeZ, int cellsX, int cellsZ, Color color, Surface surface)
        {
            cellsX = Mathf.Max(1, cellsX);
            cellsZ = Mathf.Max(1, cellsZ);
            var s = new Shape();
            var v = new int[cellsX + 1, cellsZ + 1];
            for (int x = 0; x <= cellsX; x++)
            for (int z = 0; z <= cellsZ; z++)
                v[x, z] = s.AddVertex(new Vector3((x / (float)cellsX - 0.5f) * sizeX, 0f, (z / (float)cellsZ - 0.5f) * sizeZ));

            for (int x = 0; x < cellsX; x++)
            for (int z = 0; z < cellsZ; z++)
            {
                // Чередуем диагональ — меньше заметной «ряби» после деформации.
                int g = s.NewGroup();
                if (((x + z) & 1) == 0)
                {
                    s.AddTriangleFacing(v[x, z], v[x + 1, z], v[x + 1, z + 1], Vector3.up, color, surface, g);
                    s.AddTriangleFacing(v[x, z], v[x + 1, z + 1], v[x, z + 1], Vector3.up, color, surface, g);
                }
                else
                {
                    s.AddTriangleFacing(v[x, z], v[x + 1, z], v[x, z + 1], Vector3.up, color, surface, g);
                    s.AddTriangleFacing(v[x + 1, z], v[x + 1, z + 1], v[x, z + 1], Vector3.up, color, surface, g);
                }
            }
            return s;
        }

        /// <summary>
        /// Выдавливание ВЫПУКЛОГО многоугольника (в плоскости XY, любой порядок обхода) на глубину по Z,
        /// по центру z = 0. Флажки, треугольные фронтоны, профили.
        /// </summary>
        public static Shape ExtrudeConvex(Vector2[] polygon, float depth, Color color) => ExtrudeConvex(polygon, depth, color, Surface.Default);

        public static Shape ExtrudeConvex(Vector2[] polygon, float depth, Color color, Surface surface)
        {
            int n = polygon.Length;
            var s = new Shape();
            if (n < 3) return s;
            float hz = depth * 0.5f;
            Vector2 centroid = Vector2.zero;
            foreach (var p in polygon) centroid += p;
            centroid /= n;

            var front = new int[n];
            var back = new int[n];
            for (int i = 0; i < n; i++)
            {
                front[i] = s.AddVertex(new Vector3(polygon[i].x, polygon[i].y, -hz));
                back[i] = s.AddVertex(new Vector3(polygon[i].x, polygon[i].y, hz));
            }

            int gf = s.NewGroup(), gb = s.NewGroup();
            for (int i = 1; i < n - 1; i++)
            {
                s.AddTriangleFacing(front[0], front[i], front[i + 1], Vector3.back, color, surface, gf);
                s.AddTriangleFacing(back[0], back[i], back[i + 1], Vector3.forward, color, surface, gb);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 mid = (polygon[i] + polygon[j]) * 0.5f - centroid;
                s.AddQuadFacing(front[i], front[j], back[j], back[i], new Vector3(mid.x, mid.y, 0f), color, surface);
            }
            return s;
        }
    }
}
