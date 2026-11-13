using UnityEngine;

namespace LastRide.ProcGen
{
    /// <summary>
    /// 3D градиентный шум (improved Perlin) с перестановкой от seed. Значения примерно в [-1, 1].
    /// </summary>
    public sealed class Noise3
    {
        private readonly int[] _p = new int[512];

        public Noise3(int seed)
        {
            var perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            var rng = new Rng(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }
            for (int i = 0; i < 512; i++) _p[i] = perm[i & 255];
        }

        public float Sample(Vector3 v) => Sample(v.x, v.y, v.z);

        public float Sample(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float xf = x - xi, yf = y - yi, zf = z - zi;
            int X = xi & 255, Y = yi & 255, Z = zi & 255;
            float u = Fade(xf), v = Fade(yf), w = Fade(zf);

            int a = _p[X] + Y, aa = _p[a] + Z, ab = _p[a + 1] + Z;
            int b = _p[X + 1] + Y, ba = _p[b] + Z, bb = _p[b + 1] + Z;

            float r = Lerp(w,
                Lerp(v, Lerp(u, Grad(_p[aa], xf, yf, zf), Grad(_p[ba], xf - 1, yf, zf)),
                        Lerp(u, Grad(_p[ab], xf, yf - 1, zf), Grad(_p[bb], xf - 1, yf - 1, zf))),
                Lerp(v, Lerp(u, Grad(_p[aa + 1], xf, yf, zf - 1), Grad(_p[ba + 1], xf - 1, yf, zf - 1)),
                        Lerp(u, Grad(_p[ab + 1], xf, yf - 1, zf - 1), Grad(_p[bb + 1], xf - 1, yf - 1, zf - 1))));
            return r;
        }

        /// <summary>Фрактальный шум: сумма октав, нормирован примерно в [-1, 1].</summary>
        public float Fbm(Vector3 p, int octaves = 3, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample(p) * amp;
                norm += amp;
                amp *= gain;
                p *= lacunarity;
            }
            return sum / norm;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
