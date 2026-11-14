using System.Collections.Generic;
using LastRide.Core;
using UnityEngine;

namespace LastRide.Level
{
    /// <summary>Точка на оси пути.</summary>
    public struct RoadSample
    {
        public float distance;
        public Vector3 position;
        /// <summary>Единичная касательная (с учётом уклона).</summary>
        public Vector3 tangent;
        /// <summary>Горизонтальное направление движения (единичное).</summary>
        public Vector3 flatTangent;
        /// <summary>Горизонтальное «вправо» от направления движения.</summary>
        public Vector3 right;
        public Vector3 up;
        public float width;
        /// <summary>Уклон, градусов (положительный — подъём).</summary>
        public float slopeDeg;
    }

    /// <summary>Результат проекции точки мира на ось пути.</summary>
    public struct RoadProjection
    {
        /// <summary>Расстояние вдоль оси, м.</summary>
        public float distance;
        /// <summary>Боковое смещение от оси, м (положительное — справа по ходу).</summary>
        public float lateral;
        /// <summary>Высота точки над осью, м.</summary>
        public float height;
        /// <summary>Горизонтальное расстояние до оси, м.</summary>
        public float axisDistance;
        public Vector3 axisPoint;
    }

    /// <summary>
    /// Ось пути уровня (ТЗ 5) для рантайма и редактора: <see cref="Evaluate"/> — точка по расстоянию, <see cref="Project"/> —
    /// расстояние и смещение для мировой точки, границы актов и именованные маркеры (<see cref="TryGetMarker"/>).
    /// Данные запекает <c>The Last Ride/Level/Build Road Path</c> в <c>Data/Resources/RoadPath.asset</c>.
    /// Расстояние везде — длина дуги вдоль оси (с учётом уклона), метры.
    /// </summary>
    public sealed class RoadPath
    {
        public const string ResourcePath = "RoadPath";
        public const string Act2StartMarker = "Act2Start";
        public const string Act3StartMarker = "Act3Start";
        public const string SummitMarker = "Summit";

        private static RoadPath _cached;

        /// <summary>Ось пути из Resources (null, если ассет ещё не собран).</summary>
        public static RoadPath Load()
        {
            if (_cached == null)
            {
                var data = Resources.Load<RoadPathData>(ResourcePath);
                if (data != null && data.points != null && data.points.Length >= 2) _cached = new RoadPath(data);
            }
            return _cached;
        }

        /// <summary>Сбросить кэш (после пересборки пути в редакторе).</summary>
        public static void ResetCache() => _cached = null;

        private readonly RoadPathData _data;
        private readonly Vector3[] _p;
        private readonly Vector3[] _tangents;
        private readonly float _step;
        private readonly Dictionary<string, float> _markers = new Dictionary<string, float>();

        public RoadPath(RoadPathData data)
        {
            _data = data;
            _p = data.points;
            _step = Mathf.Max(0.01f, data.step);
            _tangents = new Vector3[_p.Length];
            for (int i = 0; i < _p.Length; i++)
            {
                var a = _p[Mathf.Max(0, i - 1)];
                var b = _p[Mathf.Min(_p.Length - 1, i + 1)];
                _tangents[i] = (b - a).normalized;
            }
            if (data.markers != null) foreach (var m in data.markers) _markers[m.id] = m.distance;
        }

        /// <summary>Длина всей оси, м.</summary>
        public float Length => (_p.Length - 1) * _step;

        public int PointCount => _p.Length;
        public float Step => _step;

        /// <summary>Узел оси по индексу (для построителей рельефа).</summary>
        public Vector3 PointAt(int index) => _p[index];

        public float WidthAt(int index) => _data.widths != null && _data.widths.Length == _p.Length ? _data.widths[index] : 7f;

        // ------------------------------------------------------------------ точки

        /// <summary>Точка оси на расстоянии <paramref name="distance"/> (за пределами — зажимается в начало/конец).</summary>
        public RoadSample Evaluate(float distance)
        {
            float f = Mathf.Clamp(distance, 0f, Length) / _step;
            int i = Mathf.Min((int)f, _p.Length - 2);
            float t = f - i;

            var s = new RoadSample();
            s.distance = Mathf.Clamp(distance, 0f, Length);
            s.position = Vector3.Lerp(_p[i], _p[i + 1], t);
            s.tangent = Vector3.Slerp(_tangents[i], _tangents[i + 1], t).normalized;
            var flat = new Vector3(s.tangent.x, 0f, s.tangent.z);
            s.flatTangent = flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.forward;
            s.right = new Vector3(s.flatTangent.z, 0f, -s.flatTangent.x);
            s.up = Vector3.Cross(s.tangent, s.right).normalized;
            s.slopeDeg = Mathf.Asin(Mathf.Clamp(s.tangent.y, -1f, 1f)) * Mathf.Rad2Deg;
            s.width = _data.widths != null && _data.widths.Length == _p.Length
                ? Mathf.Lerp(_data.widths[i], _data.widths[i + 1], t)
                : 7f;
            return s;
        }

        /// <summary>
        /// Ближайшая к <paramref name="position"/> точка оси (в плане, по XZ). С подсказкой <paramref name="hintDistance"/>
        /// ищет только рядом (быстро, каждый кадр); без неё — по всему пути.
        /// </summary>
        public RoadProjection Project(Vector3 position, float hintDistance = -1f, float window = 80f)
        {
            int segCount = _p.Length - 1;
            int from = 0, to = segCount - 1, stride = 4;
            if (hintDistance >= 0f)
            {
                int c = Mathf.RoundToInt(hintDistance / _step);
                int w = Mathf.CeilToInt(window / _step);
                from = Mathf.Max(0, c - w);
                to = Mathf.Min(segCount - 1, c + w);
                stride = 1;
            }

            int best = from;
            float bestSq = float.MaxValue;
            for (int i = from; i <= to; i += stride)
            {
                float sq = SqDistXZ(position, _p[i]);
                if (sq < bestSq) { bestSq = sq; best = i; }
            }

            // Уточнение вокруг лучшего узла: ближайшая точка на отрезках [i-s, i+s].
            int lo = Mathf.Max(0, best - stride - 1), hi = Mathf.Min(segCount - 1, best + stride + 1);
            float bestT = 0f; int bestSeg = best; bestSq = float.MaxValue;
            for (int i = lo; i <= hi; i++)
            {
                float t = ClosestT(position, _p[i], _p[i + 1]);
                var q = Vector3.Lerp(_p[i], _p[i + 1], t);
                float sq = SqDistXZ(position, q);
                if (sq < bestSq) { bestSq = sq; bestSeg = i; bestT = t; }
            }

            var axis = Vector3.Lerp(_p[bestSeg], _p[bestSeg + 1], bestT);
            float dist = (bestSeg + bestT) * _step;
            var sample = Evaluate(dist);
            var delta = position - axis;
            return new RoadProjection
            {
                distance = dist,
                lateral = Vector3.Dot(delta, sample.right),
                height = delta.y,
                axisDistance = Mathf.Sqrt(bestSq),
                axisPoint = axis
            };
        }

        private static float SqDistXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float ClosestT(Vector3 p, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x, abz = b.z - a.z;
            float len = abx * abx + abz * abz;
            if (len < 1e-8f) return 0f;
            return Mathf.Clamp01(((p.x - a.x) * abx + (p.z - a.z) * abz) / len);
        }

        // ------------------------------------------------------------------ маркеры и акты

        /// <summary>Расстояние именованного маркера (граница акта, стоянка, шпилька…).</summary>
        public bool TryGetMarker(string id, out float distance) => _markers.TryGetValue(id, out distance);

        /// <summary>Расстояние маркера или -1, если такого нет.</summary>
        public float MarkerOrNegative(string id) => _markers.TryGetValue(id, out var d) ? d : -1f;

        /// <summary>Все маркеры (для построителей уровня, 7.3–7.6).</summary>
        public IEnumerable<KeyValuePair<string, float>> Markers => _markers;

        /// <summary>Акт, которому принадлежит расстояние вдоль оси (по маркерам Act2Start / Act3Start).</summary>
        public ActId ActAt(float distance)
        {
            if (_markers.TryGetValue(Act3StartMarker, out var a3) && distance >= a3) return ActId.Act3;
            if (_markers.TryGetValue(Act2StartMarker, out var a2) && distance >= a2) return ActId.Act2;
            return ActId.Act1;
        }

        /// <summary>Прогресс пешего участка акта 3: 0 — начало тропы (завал), 1 — Вершина. Для <c>Weather.SetProgress01</c>.</summary>
        public float TrailProgress01(float distance)
        {
            if (!_markers.TryGetValue(Act3StartMarker, out var a3)) return 0f;
            float end = _markers.TryGetValue(SummitMarker, out var s) ? s : Length;
            return end > a3 ? Mathf.Clamp01((distance - a3) / (end - a3)) : 0f;
        }
    }
}
