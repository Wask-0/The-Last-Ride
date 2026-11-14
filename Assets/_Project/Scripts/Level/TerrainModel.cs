using LastRide.Core;
using LastRide.ProcGen;
using UnityEngine;

namespace LastRide.Level
{
    /// <summary>
    /// Высота рельефа в любой точке плана: <see cref="HeightAt"/>. Чистая функция от оси пути и <see cref="TerrainSettings"/>
    /// (детерминирована), поэтому годится и для построителя чанков, и для расстановки пропсов (7.4).
    /// Модель: под дорогой и обочиной рельеф «утоплен» на <c>roadSink</c>, дальше от оси — гора на внутренней стороне поворота
    /// и обрыв на внешней. Где рядом проходят разные участки пути (серпантин), высоты ближайших участков смешиваются
    /// с весом 1/(расстояние+2)⁴, поэтому швов нет. Не потокобезопасна (внутренний буфер).
    /// </summary>
    /// <summary>Ровная площадка под постройку: рельеф плавно сходится к <see cref="height"/> внутри радиуса.</summary>
    public struct TerrainPad
    {
        public Vector2 center;
        public float radius;
        public float blend;
        public float height;
        /// <summary>Площадка на уровне дороги (врезана в склон): влияние не гаснет у кромки.</summary>
        public bool ignoreRoadFade;
    }

    public sealed class TerrainModel
    {
        private const int Stride = 2;     // шаг поиска локальных минимумов расстояния, узлов
        private const int MaxCandidates = 4;
        private const float CandidateRange = 250f; // участки дальше, чем (ближайший + это), не учитываются

        private readonly TerrainSettings _s;
        private readonly Vector3[] _p;
        private readonly float[] _half;
        private readonly Vector3[] _right;
        private readonly float[] _bias;
        private readonly Noise3 _noise;
        private readonly Vector3 _end;
        private readonly float[] _sq;
        private readonly Span4 _best = new Span4();
        private TerrainPad[] _pads = new TerrainPad[0];

        public TerrainModel(RoadPath path, TerrainSettings settings)
        {
            _s = settings;
            int n = path.PointCount;
            _p = new Vector3[n];
            _half = new float[n];
            _right = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                _p[i] = path.PointAt(i);
                _half[i] = path.WidthAt(i) * 0.5f;
            }
            for (int i = 0; i < n; i++)
            {
                var t = _p[Mathf.Min(n - 1, i + 1)] - _p[Mathf.Max(0, i - 1)];
                t.y = 0f;
                t = t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.forward;
                _right[i] = new Vector3(t.z, 0f, -t.x);
            }

            _bias = ComputeBias(n, path.Step);
            _noise = new Noise3(settings.seed);
            _end = _p[n - 1];
            _sq = new float[n / Stride + 2];
        }

        /// <summary>Задать площадки под постройки (7.5). Пустой список — рельеф без площадок.</summary>
        public void SetPads(System.Collections.Generic.IEnumerable<TerrainPad> pads)
        {
            _pads = new System.Collections.Generic.List<TerrainPad>(pads ?? new TerrainPad[0]).ToArray();
        }

        /// <summary>Сторона горы/обрыва вдоль пути: −1…+1; плюс — гора справа по ходу (поворот вправо), минус — гора слева.</summary>
        private float[] ComputeBias(int n, float step)
        {
            // Поворот на отрезке ±5 узлов (≈10 м), знак «вправо» = плюс.
            const int h = 5;
            var turn = new float[n];
            for (int i = 0; i < n; i++)
            {
                var a = Flat(_p[Mathf.Min(n - 1, i)] - _p[Mathf.Max(0, i - h)]);
                var b = Flat(_p[Mathf.Min(n - 1, i + h)] - _p[Mathf.Min(n - 1, i)]);
                turn[i] = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad; // за h узлов
            }
            int w = Mathf.Max(1, Mathf.RoundToInt(_s.biasWindow / step));
            var bias = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int j = Mathf.Max(0, i - w); j <= Mathf.Min(n - 1, i + w); j++)
                    sum += turn[j] * (1f - Mathf.Abs(j - i) / (float)w);
                // turn[j] — поворот на h узлов, каждый узел входит в h таких отрезков → делим на h.
                bias[i] = (float)System.Math.Tanh(_s.biasGain * sum / h);
            }
            return bias;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
        }

        /// <summary>Сторона-гора для узла (для отладки и тестов).</summary>
        public float BiasAt(int index) => _bias[Mathf.Clamp(index, 0, _bias.Length - 1)];

        public float HeightAt(float x, float z)
        {
            int n = _p.Length;
            int cnt = 0;
            for (int i = 0, k = 0; i < n; i += Stride, k++)
            {
                float dx = x - _p[i].x, dz = z - _p[i].z;
                _sq[k] = dx * dx + dz * dz;
                cnt = k + 1;
            }

            // Локальные минимумы расстояния = ближайшие точки разных участков пути.
            var best = _best;
            best.count = 0;
            float nearest = float.MaxValue;
            for (int k = 0; k < cnt; k++)
            {
                float v = _sq[k];
                bool lowerThanPrev = k == 0 || v <= _sq[k - 1];
                bool lowerThanNext = k == cnt - 1 || v < _sq[k + 1];
                if (!lowerThanPrev || !lowerThanNext) continue;
                if (v < nearest) nearest = v;
                best.Insert(k, v);
            }

            float nearestD = Mathf.Sqrt(nearest);
            float hSum = 0f, eSum = 0f, wSum = 0f;
            for (int c = 0; c < best.count; c++)
            {
                if (Mathf.Sqrt(best.sq[c]) > nearestD + CandidateRange) continue;
                Candidate(x, z, best.index[c] * Stride, out float h, out float e, out float d);
                float w = 1f / Mathf.Pow(d + 2f, 4f);
                hSum += h * w; eSum += e * w; wSum += w;
            }
            float height = hSum / wSum;
            float eBlend = eSum / wSum;

            // Шум: у дороги ровно, дальше нарастает.
            float f = Smooth01(eBlend / Mathf.Max(0.01f, _s.noiseFadeDistance));
            if (f > 0f)
            {
                float fine = _noise.Fbm(new Vector3(x / _s.noiseScale, 0.37f, z / _s.noiseScale), 3);
                float ridge = _noise.Sample(x / _s.ridgeScale, 5.1f, z / _s.ridgeScale);
                height += f * (fine * _s.noiseAmplitude + ridge * _s.ridgeAmplitude);
            }

            // Плоская площадка под Маяк вокруг конца пути.
            float ds = Mathf.Sqrt((x - _end.x) * (x - _end.x) + (z - _end.z) * (z - _end.z));
            if (ds < _s.summitBlendRadius)
            {
                float t = 1f - Smooth01((ds - _s.summitPlateauRadius) / Mathf.Max(0.01f, _s.summitBlendRadius - _s.summitPlateauRadius));
                t *= Smooth01(eBlend / 10f); // у самой дороги рельеф остаётся под ней, площадка — чуть дальше
                height = Mathf.Lerp(height, _end.y - _s.roadSink, t);
            }

            // Площадки под постройки: у самой дороги влияние гаснет, чтобы не трогать полотно и обочину.
            for (int i = 0; i < _pads.Length; i++)
            {
                var pad = _pads[i];
                float pd = Mathf.Sqrt((x - pad.center.x) * (x - pad.center.x) + (z - pad.center.y) * (z - pad.center.y));
                if (pd >= pad.radius + pad.blend) continue;
                float t = 1f - Smooth01((pd - pad.radius) / Mathf.Max(0.01f, pad.blend));
                if (!pad.ignoreRoadFade) t *= Smooth01(eBlend / 3f);
                height = Mathf.Lerp(height, pad.height, t);
            }
            return height;
        }

        private void Candidate(float x, float z, int center, out float height, out float edge, out float dist)
        {
            int n = _p.Length;
            int from = Mathf.Max(0, center - Stride), to = Mathf.Min(n - 2, center + Stride - 1);
            int seg = from; float bt = 0f, bsq = float.MaxValue;
            for (int j = from; j <= to; j++)
            {
                float abx = _p[j + 1].x - _p[j].x, abz = _p[j + 1].z - _p[j].z;
                float len = abx * abx + abz * abz;
                float t = len < 1e-8f ? 0f : Mathf.Clamp01(((x - _p[j].x) * abx + (z - _p[j].z) * abz) / len);
                float qx = _p[j].x + abx * t - x, qz = _p[j].z + abz * t - z;
                float sq = qx * qx + qz * qz;
                if (sq < bsq) { bsq = sq; seg = j; bt = t; }
            }

            var a = Vector3.Lerp(_p[seg], _p[seg + 1], bt);
            float half = Mathf.Lerp(_half[seg], _half[seg + 1], bt);
            var right = Vector3.Lerp(_right[seg], _right[seg + 1], bt);
            float bias = Mathf.Lerp(_bias[seg], _bias[seg + 1], bt);
            float lateral = (x - a.x) * right.x + (z - a.z) * right.z;
            dist = Mathf.Sqrt(bsq);

            float q = (lateral >= 0f ? 1f : -1f) * bias; // > 0 — здесь гора, < 0 — обрыв
            edge = Mathf.Max(0f, dist - half - _s.shoulderWidth);
            height = a.y - _s.roadSink + SlopeOffset(edge, q);
        }

        private float SlopeOffset(float e, float q)
        {
            if (e <= 0f) return 0f;
            float aq = Mathf.Abs(q);
            if (aq < 1e-4f) return 0f;
            if (q > 0f)
            {
                float cap = _s.mountainRise * aq;
                float slope = Mathf.Lerp(_s.mountainSlopeMin, _s.mountainSlopeMax, aq);
                return cap * (1f - Mathf.Exp(-e * slope / cap)) + e * 0.08f * aq; // гора продолжает медленно расти
            }
            else
            {
                float cap = _s.cliffDepth * aq;
                float slope = Mathf.Lerp(_s.cliffSlopeMin, _s.cliffSlopeMax, aq);
                return -cap * (1f - Mathf.Exp(-e * slope / cap));
            }
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Четыре лучших (ближайших) локальных минимума.</summary>
        private sealed class Span4
        {
            public int count;
            public int[] index = new int[MaxCandidates];
            public float[] sq = new float[MaxCandidates];

            public void Insert(int idx, float value)
            {
                int pos = count;
                if (count == MaxCandidates)
                {
                    if (value >= sq[MaxCandidates - 1]) return;
                    pos = MaxCandidates - 1;
                }
                else count++;
                while (pos > 0 && sq[pos - 1] > value)
                {
                    sq[pos] = sq[pos - 1]; index[pos] = index[pos - 1];
                    pos--;
                }
                sq[pos] = value; index[pos] = idx;
            }
        }
    }
}
