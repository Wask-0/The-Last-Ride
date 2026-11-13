using System.Collections.Generic;
using UnityEngine;

namespace LastRide.ProcGen
{
    /// <summary>
    /// Свойства поверхности, которые попадают в UV0 меша.
    /// x — «снежность» (1 — на верхних гранях лежит снег, 0 — нет: стекло, пламя, ткань),
    /// y — свечение (0..1: окна, огни Маяка, отражатели).
    /// </summary>
    public struct Surface
    {
        public float snow;
        public float emission;

        public Surface(float snow, float emission)
        {
            this.snow = snow;
            this.emission = emission;
        }

        public static readonly Surface Default = new Surface(1f, 0f);
        public static readonly Surface NoSnow = new Surface(0f, 0f);
        public static Surface Glow(float emission) => new Surface(0f, emission);

        public Vector2 ToVector() => new Vector2(snow, emission);
    }

    /// <summary>
    /// Заготовка меша с ОБЩИМИ вершинами (этап 1). Её создают примитивы и меняют деформации;
    /// затем <see cref="MeshBuilder"/> разбивает её на независимые грани (flat shading).
    /// У каждого треугольника — свой цвет, свойства поверхности и номер группы
    /// (группа = логическая грань, например квад; используется для «выбивания» граней).
    /// </summary>
    public sealed class Shape
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Indices = new List<int>();
        public readonly List<Color> TriColors = new List<Color>();
        public readonly List<Surface> TriSurfaces = new List<Surface>();
        public readonly List<int> TriGroups = new List<int>();

        private int _nextGroup;

        public int TriangleCount => TriColors.Count;
        public int VertexCount => Vertices.Count;

        public int AddVertex(Vector3 v)
        {
            Vertices.Add(v);
            return Vertices.Count - 1;
        }

        public int NewGroup() => _nextGroup++;

        public void AddTriangle(int a, int b, int c, Color color, Surface surface, int group = -1)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
            TriColors.Add(color);
            TriSurfaces.Add(surface);
            TriGroups.Add(group < 0 ? NewGroup() : group);
        }

        /// <summary>
        /// Треугольник, гарантированно смотрящий наружу: если его нормаль направлена против
        /// <paramref name="outward"/>, порядок вершин меняется. Удобно для выпуклых примитивов.
        /// </summary>
        public void AddTriangleFacing(int a, int b, int c, Vector3 outward, Color color, Surface surface, int group = -1)
        {
            Vector3 n = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (Vector3.Dot(n, outward) < 0f) (b, c) = (c, b);
            AddTriangle(a, b, c, color, surface, group);
        }

        /// <summary>Квад a-b-c-d (по периметру) одной группой, смотрит в сторону <paramref name="outward"/>.</summary>
        public void AddQuadFacing(int a, int b, int c, int d, Vector3 outward, Color color, Surface surface)
        {
            int g = NewGroup();
            AddTriangleFacing(a, b, c, outward, color, surface, g);
            AddTriangleFacing(a, c, d, outward, color, surface, g);
        }

        public Vector3 TriangleCenter(int tri)
        {
            int i = tri * 3;
            return (Vertices[Indices[i]] + Vertices[Indices[i + 1]] + Vertices[Indices[i + 2]]) / 3f;
        }

        public Vector3 TriangleNormal(int tri)
        {
            int i = tri * 3;
            Vector3 a = Vertices[Indices[i]], b = Vertices[Indices[i + 1]], c = Vertices[Indices[i + 2]];
            return Vector3.Cross(b - a, c - a).normalized;
        }

        // ---- Операции над всей заготовкой (возвращают this для цепочек) ----

        public Shape Transform(Matrix4x4 m)
        {
            for (int i = 0; i < Vertices.Count; i++) Vertices[i] = m.MultiplyPoint3x4(Vertices[i]);
            if (m.determinant < 0f) FlipWinding();
            return this;
        }

        public Shape Translate(Vector3 offset) => Transform(Matrix4x4.Translate(offset));
        public Shape Rotate(Vector3 euler) => Transform(Matrix4x4.Rotate(Quaternion.Euler(euler)));
        public Shape Scale(Vector3 scale) => Transform(Matrix4x4.Scale(scale));

        public Shape FlipWinding()
        {
            for (int i = 0; i < Indices.Count; i += 3) (Indices[i + 1], Indices[i + 2]) = (Indices[i + 2], Indices[i + 1]);
            return this;
        }

        public Shape Paint(Color color)
        {
            for (int i = 0; i < TriColors.Count; i++) TriColors[i] = color;
            return this;
        }

        public Shape SetSurface(Surface surface)
        {
            for (int i = 0; i < TriSurfaces.Count; i++) TriSurfaces[i] = surface;
            return this;
        }

        /// <summary>Добавить другую заготовку (с преобразованием). Группы получают новые номера.</summary>
        public Shape Append(Shape other, Matrix4x4? transform = null)
        {
            var m = transform ?? Matrix4x4.identity;
            bool flip = m.determinant < 0f;
            int baseIndex = Vertices.Count;
            foreach (var v in other.Vertices) Vertices.Add(m.MultiplyPoint3x4(v));

            var groupMap = new Dictionary<int, int>();
            for (int t = 0; t < other.TriangleCount; t++)
            {
                int i = t * 3;
                int a = other.Indices[i] + baseIndex, b = other.Indices[i + 1] + baseIndex, c = other.Indices[i + 2] + baseIndex;
                if (flip) (b, c) = (c, b);
                int og = other.TriGroups[t];
                if (!groupMap.TryGetValue(og, out int g)) { g = NewGroup(); groupMap[og] = g; }
                AddTriangle(a, b, c, other.TriColors[t], other.TriSurfaces[t], g);
            }
            return this;
        }

        /// <summary>
        /// Сделать грани двусторонними: к каждому треугольнику добавляется копия с обратным обходом
        /// (своя нормаль внутрь). Нужно для «тонких» объектов, которые видно изнутри: руины с выбитыми стенами.
        /// Вершины дублируются — деформации после этого применять уже нельзя (стороны разойдутся).
        /// </summary>
        public Shape MakeDoubleSided(Color? innerColor = null)
        {
            int triCount = TriangleCount;
            int baseIndex = Vertices.Count;
            for (int i = 0; i < baseIndex; i++) Vertices.Add(Vertices[i]);
            for (int t = 0; t < triCount; t++)
            {
                int i = t * 3;
                AddTriangle(Indices[i] + baseIndex, Indices[i + 2] + baseIndex, Indices[i + 1] + baseIndex,
                    innerColor ?? TriColors[t], TriSurfaces[t], TriGroups[t]);
            }
            return this;
        }

        public Shape Clone()
        {
            var s = new Shape();
            s.Append(this);
            return s;
        }

        /// <summary>Удалить треугольники по условию (индексы треугольников). Неиспользуемые вершины остаются — на результат не влияют.</summary>
        public Shape RemoveTriangles(System.Predicate<int> shouldRemove)
        {
            var idx = new List<int>(Indices.Count);
            var col = new List<Color>(TriColors.Count);
            var srf = new List<Surface>(TriSurfaces.Count);
            var grp = new List<int>(TriGroups.Count);
            for (int t = 0; t < TriangleCount; t++)
            {
                if (shouldRemove(t)) continue;
                idx.Add(Indices[t * 3]);
                idx.Add(Indices[t * 3 + 1]);
                idx.Add(Indices[t * 3 + 2]);
                col.Add(TriColors[t]);
                srf.Add(TriSurfaces[t]);
                grp.Add(TriGroups[t]);
            }
            Indices.Clear(); Indices.AddRange(idx);
            TriColors.Clear(); TriColors.AddRange(col);
            TriSurfaces.Clear(); TriSurfaces.AddRange(srf);
            TriGroups.Clear(); TriGroups.AddRange(grp);
            return this;
        }

        /// <summary>Сглаженные нормали вершин (среднее по граням) — для деформаций вдоль нормали.</summary>
        public Vector3[] ComputeVertexNormals()
        {
            var n = new Vector3[Vertices.Count];
            for (int t = 0; t < TriangleCount; t++)
            {
                int i = t * 3;
                int a = Indices[i], b = Indices[i + 1], c = Indices[i + 2];
                Vector3 fn = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                n[a] += fn; n[b] += fn; n[c] += fn;
            }
            for (int i = 0; i < n.Length; i++) n[i] = n[i].sqrMagnitude > 1e-12f ? n[i].normalized : Vector3.up;
            return n;
        }

        public Bounds ComputeBounds()
        {
            if (Vertices.Count == 0) return new Bounds();
            var b = new Bounds(Vertices[0], Vector3.zero);
            foreach (var v in Vertices) b.Encapsulate(v);
            return b;
        }
    }
}
