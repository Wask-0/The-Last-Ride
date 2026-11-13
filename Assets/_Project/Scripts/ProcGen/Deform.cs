using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastRide.ProcGen
{
    /// <summary>
    /// Деформации заготовок. Работают с общими вершинами, поэтому не рвут поверхность.
    /// Все случайные операции детерминированы (Noise3 / Rng с seed).
    /// </summary>
    public static class Deform
    {
        /// <summary>Сместить вершины вдоль их нормали на fbm-шум (скалы, сугробы).</summary>
        public static Shape NoiseAlongNormal(Shape s, Noise3 noise, float amplitude, float frequency, int octaves = 2, Vector3 offset = default)
        {
            var normals = s.ComputeVertexNormals();
            for (int i = 0; i < s.Vertices.Count; i++)
            {
                Vector3 p = s.Vertices[i];
                float n = noise.Fbm(p * frequency + offset, octaves);
                s.Vertices[i] = p + normals[i] * (n * amplitude);
            }
            return s;
        }

        /// <summary>Сместить вершины по осям независимым шумом (неровные ярусы ёлок, кривые доски).</summary>
        public static Shape NoisePerAxis(Shape s, Noise3 noise, Vector3 amplitude, float frequency, Vector3 offset = default)
        {
            for (int i = 0; i < s.Vertices.Count; i++)
            {
                Vector3 p = s.Vertices[i];
                Vector3 q = p * frequency + offset;
                s.Vertices[i] = p + new Vector3(
                    noise.Sample(q) * amplitude.x,
                    noise.Sample(q + new Vector3(31.7f, 0f, 0f)) * amplitude.y,
                    noise.Sample(q + new Vector3(0f, 57.3f, 0f)) * amplitude.z);
            }
            return s;
        }

        /// <summary>Случайный разброс каждой вершины в шаре радиуса amplitude.</summary>
        public static Shape Jitter(Shape s, Rng rng, float amplitude)
        {
            for (int i = 0; i < s.Vertices.Count; i++) s.Vertices[i] += rng.OnUnitSphere * (rng.Value * amplitude);
            return s;
        }

        /// <summary>Масштабировать XZ линейно по высоте: на yMin — scaleAtMin, на yMax — scaleAtMax.</summary>
        public static Shape Taper(Shape s, float yMin, float yMax, float scaleAtMin, float scaleAtMax)
        {
            float range = Mathf.Max(1e-5f, yMax - yMin);
            for (int i = 0; i < s.Vertices.Count; i++)
            {
                Vector3 p = s.Vertices[i];
                float k = Mathf.Lerp(scaleAtMin, scaleAtMax, Mathf.Clamp01((p.y - yMin) / range));
                s.Vertices[i] = new Vector3(p.x * k, p.y, p.z * k);
            }
            return s;
        }

        /// <summary>
        /// Изогнуть вдоль Y в плоскости XY: на высоте <paramref name="height"/> ось повёрнута на <paramref name="degrees"/>
        /// (наклон ёлки, погнутый столб). Положительный угол — изгиб в сторону +X.
        /// </summary>
        public static Shape Bend(Shape s, float degrees, float height)
        {
            if (Mathf.Abs(degrees) < 1e-3f || height <= 0f) return s;
            float total = degrees * Mathf.Deg2Rad;
            float r = height / total; // радиус дуги
            for (int i = 0; i < s.Vertices.Count; i++)
            {
                Vector3 p = s.Vertices[i];
                float theta = p.y / r;
                float rx = r - p.x;
                s.Vertices[i] = new Vector3(r - rx * Mathf.Cos(theta), rx * Mathf.Sin(theta), p.z);
            }
            return s;
        }

        /// <summary>
        /// «Выбить» случайную долю граней (целыми группами — квад не превращается в треугольник).
        /// <paramref name="filter"/> по нормали грани решает, какие грани можно выбивать (например, только стены).
        /// </summary>
        public static Shape PunchFaces(Shape s, Rng rng, float fraction, Func<Vector3, bool> filter = null)
        {
            var groupNormal = new Dictionary<int, Vector3>();
            var order = new List<int>();
            for (int t = 0; t < s.TriangleCount; t++)
            {
                int g = s.TriGroups[t];
                if (groupNormal.ContainsKey(g)) continue;
                groupNormal[g] = s.TriangleNormal(t);
                order.Add(g); // порядок появления — детерминирован
            }

            var removed = new HashSet<int>();
            foreach (int g in order)
            {
                if (filter != null && !filter(groupNormal[g])) continue;
                if (rng.Chance(fraction)) removed.Add(g);
            }
            return s.RemoveTriangles(t => removed.Contains(s.TriGroups[t]));
        }

        /// <summary>Перекрасить грани по условию от нормали (например, верх в цвет снега, низ — в тень).</summary>
        public static Shape PaintWhere(Shape s, Func<Vector3, bool> byNormal, Color color)
        {
            for (int t = 0; t < s.TriangleCount; t++)
                if (byNormal(s.TriangleNormal(t))) s.TriColors[t] = color;
            return s;
        }
    }
}
