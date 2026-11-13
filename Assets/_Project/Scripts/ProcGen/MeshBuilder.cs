using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastRide.ProcGen
{
    /// <summary>
    /// Финальная сборка меша (этап 2): каждый треугольник получает собственные 3 вершины
    /// и нормаль грани — flat shading. Цвет палитры идёт в vertex color,
    /// свойства поверхности (<see cref="Surface"/>) — в UV0.
    /// Стек трансформаций позволяет собирать модель из частей.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        private Matrix4x4 _current = Matrix4x4.identity;

        public int VertexCount => _positions.Count;
        public int TriangleCount => _positions.Count / 3;

        /// <summary>Добавить локальную трансформацию поверх текущей. Не забыть <see cref="Pop"/>.</summary>
        public MeshBuilder Push(Matrix4x4 local)
        {
            _stack.Push(_current);
            _current = _current * local;
            return this;
        }

        public MeshBuilder Push(Vector3 position, Quaternion rotation, Vector3 scale) => Push(Matrix4x4.TRS(position, rotation, scale));
        public MeshBuilder Push(Vector3 position) => Push(Matrix4x4.Translate(position));

        public MeshBuilder Pop()
        {
            _current = _stack.Count > 0 ? _stack.Pop() : Matrix4x4.identity;
            return this;
        }

        public MeshBuilder Add(Shape shape) => Add(shape, Matrix4x4.identity);

        public MeshBuilder Add(Shape shape, Matrix4x4 local)
        {
            var m = _current * local;
            bool flip = m.determinant < 0f;
            var idx = shape.Indices;
            var verts = shape.Vertices;

            for (int t = 0; t < shape.TriangleCount; t++)
            {
                int i = t * 3;
                Vector3 a = m.MultiplyPoint3x4(verts[idx[i]]);
                Vector3 b = m.MultiplyPoint3x4(verts[idx[i + 1]]);
                Vector3 c = m.MultiplyPoint3x4(verts[idx[i + 2]]);
                if (flip) (b, c) = (c, b);

                Vector3 n = Vector3.Cross(b - a, c - a);
                float len = n.magnitude;
                if (len < 1e-8f) continue; // вырожденный треугольник
                n /= len;

                Color col = shape.TriColors[t];
                Vector2 uv = shape.TriSurfaces[t].ToVector();

                _positions.Add(a); _positions.Add(b); _positions.Add(c);
                _normals.Add(n); _normals.Add(n); _normals.Add(n);
                _colors.Add(col); _colors.Add(col); _colors.Add(col);
                _uv0.Add(uv); _uv0.Add(uv); _uv0.Add(uv);
            }
            return this;
        }

        /// <summary>Добавить уже собранный builder (например, часть модели) с преобразованием.</summary>
        public MeshBuilder Add(MeshBuilder other, Matrix4x4 local)
        {
            var m = _current * local;
            bool flip = m.determinant < 0f;
            var nm = m.inverse.transpose;
            int n = other._positions.Count;
            for (int i = 0; i < n; i += 3)
            {
                // При зеркальном преобразовании меняем порядок 2-й и 3-й вершины, чтобы грань смотрела наружу.
                CopyVertex(other, i, m, nm);
                CopyVertex(other, flip ? i + 2 : i + 1, m, nm);
                CopyVertex(other, flip ? i + 1 : i + 2, m, nm);
            }
            return this;
        }

        private void CopyVertex(MeshBuilder other, int k, Matrix4x4 m, Matrix4x4 normalMatrix)
        {
            _positions.Add(m.MultiplyPoint3x4(other._positions[k]));
            _normals.Add(normalMatrix.MultiplyVector(other._normals[k]).normalized);
            _colors.Add(other._colors[k]);
            _uv0.Add(other._uv0[k]);
        }

        /// <summary>Сдвинуть всё собранное (например, «посадить» модель на землю по габаритам).</summary>
        public MeshBuilder Offset(Vector3 delta)
        {
            for (int i = 0; i < _positions.Count; i++) _positions[i] += delta;
            return this;
        }

        public Bounds ComputeBounds()
        {
            if (_positions.Count == 0) return new Bounds();
            var b = new Bounds(_positions[0], Vector3.zero);
            foreach (var p in _positions) b.Encapsulate(p);
            return b;
        }

        public void Clear()
        {
            _positions.Clear();
            _normals.Clear();
            _colors.Clear();
            _uv0.Clear();
            _stack.Clear();
            _current = Matrix4x4.identity;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            WriteTo(mesh);
            return mesh;
        }

        /// <summary>Записать результат в существующий меш (сохраняет ссылки на него).</summary>
        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            int count = _positions.Count;
            mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_positions);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uv0);

            var tris = new int[count];
            for (int i = 0; i < count; i++) tris[i] = i;
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateBounds();
        }
    }
}
