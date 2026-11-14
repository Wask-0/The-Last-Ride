using UnityEngine;

namespace LastRide.UI
{
    /// <summary>Тлеющий огонёк главного меню: мерцание света банки с Искрой (шум), без привязки к игровой логике.</summary>
    public sealed class MenuEmber : MonoBehaviour
    {
        public Light glow;
        public float baseIntensity = 2.2f;
        public float flicker = 0.35f;
        public float speed = 1.7f;

        private float _seed;

        private void Awake()
        {
            _seed = Random.value * 100f;
            if (glow == null) glow = GetComponentInChildren<Light>();
            if (glow != null && baseIntensity <= 0f) baseIntensity = glow.intensity;
        }

        private void Update()
        {
            if (glow == null) return;
            float t = Time.unscaledTime * speed + _seed;
            float n = Mathf.PerlinNoise(t, 0.37f) * 0.7f + Mathf.PerlinNoise(t * 3.1f, 7.1f) * 0.3f;
            glow.intensity = baseIntensity * (1f - flicker + 2f * flicker * n);
        }
    }
}
