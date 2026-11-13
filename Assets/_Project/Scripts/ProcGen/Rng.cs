using UnityEngine;

namespace LastRide.ProcGen
{
    /// <summary>
    /// Детерминированный ГСЧ (xorshift32). Один и тот же seed всегда даёт одну и ту же
    /// последовательность — независимо от платформы и версии .NET.
    /// </summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            // Перемешиваем seed, чтобы соседние seed-ы давали непохожие последовательности. 0 недопустим.
            uint s = (uint)seed * 2654435761u + 0x9E3779B9u;
            s ^= s >> 16;
            _state = s == 0 ? 0x6D2B79F5u : s;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[0, 1).</summary>
        public float Value => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value;

        public float Range(Vector2 minMax) => Range(minMax.x, minMax.y);

        /// <summary>[min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public bool Chance(float p) => Value < p;

        public float Sign => Value < 0.5f ? -1f : 1f;

        public Vector2 InsideUnitCircle
        {
            get
            {
                float a = Value * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Value);
                return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
        }

        public Vector3 OnUnitSphere
        {
            get
            {
                float z = Range(-1f, 1f);
                float a = Value * Mathf.PI * 2f;
                float r = Mathf.Sqrt(1f - z * z);
                return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
            }
        }

        /// <summary>Независимый дочерний ГСЧ — чтобы добавление случайностей в одном месте не сдвигало другие.</summary>
        public Rng Fork(int salt) => new Rng((int)(NextUInt() ^ (uint)(salt * 0x27d4eb2d)));

        public T Pick<T>(T[] items) => items[Range(0, items.Length)];
    }
}
