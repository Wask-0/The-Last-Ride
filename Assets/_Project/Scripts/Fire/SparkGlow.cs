using LastRide.Core;
using UnityEngine;

namespace LastRide.Fire
{
    /// <summary>
    /// Визуал Искры у одного источника — печки в фургоне или лампы (ТЗ 4.2): яркость — функция запаса горения.
    /// Управляет точечным светом (с мерцанием шума), свечением стёкол (через MaterialPropertyBlock: <c>_EmissionIntensity</c>
    /// и <c>_EmissionTint</c> шейдера мира) и частицами искр. Тление — тусклый красный пульсирующий свет.
    /// Источник, где Искры сейчас нет (<see cref="SparkFire.Source"/> другой, Unlit, Extinguished), — выключен.
    /// Параметры — <c>GameBalance → Spark Visual</c>, читаются каждый кадр.
    /// </summary>
    public sealed class SparkGlow : MonoBehaviour
    {
        public FireSourceKind kind = FireSourceKind.Stove;
        public Light glowLight;
        public Renderer[] glassRenderers;
        public ParticleSystem sparks;
        [Tooltip("Свечение стекла при яркости 1 (HDR-множитель).")]
        public float glassEmission = 3f;

        /// <summary>Текущая яркость 0..1 (для HUD/звука при необходимости).</summary>
        public float Brightness { get; private set; }

        /// <summary>Текущий цвет света (пламя ↔ уголь) — панель печки красит им окошко.</summary>
        public Color CurrentColor { get; private set; } = Color.white;

        private static SparkGlow _stove, _lamp;

        /// <summary>Визуал печки / лампы в сцене (null, если нет).</summary>
        public static SparkGlow For(FireSourceKind k) => k == FireSourceKind.Stove ? _stove : _lamp;

        private void OnEnable()
        {
            if (kind == FireSourceKind.Stove) _stove = this; else _lamp = this;
        }

        private void OnDisable()
        {
            if (_stove == this) _stove = null;
            if (_lamp == this) _lamp = null;
        }

        private static readonly int EmissionIntensity = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int EmissionTint = Shader.PropertyToID("_EmissionTint");

        private MaterialPropertyBlock _block;
        private float _baseLightIntensity;
        private float _colorBlend = 1f; // 1 — пламя, 0 — уголь
        private float _noiseSeed;
        private GameBalance.SparkVisualSettings _fallback;

        private GameBalance.SparkVisualSettings Settings
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.sparkVisual;
                return _fallback ??= new GameBalance.SparkVisualSettings();
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseLightIntensity = glowLight != null ? glowLight.intensity : 1f;
            _noiseSeed = Random.value * 100f;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            var spark = gm != null ? gm.Spark : null;
            var s = Settings;

            float target = 0f;
            float blendTarget = 1f;
            float rate = 0f;
            float flicker = 1f;

            if (spark != null && spark.Source == kind)
            {
                if (spark.State == SparkState.Burning)
                {
                    float f = Mathf.Pow(spark.Fraction, s.brightnessExponent);
                    target = Mathf.Lerp(s.minBurnBrightness, 1f, f);
                    rate = s.maxSparksPerSecond * target * target;
                    float n = Mathf.PerlinNoise(_noiseSeed, Time.time * s.flickerHz) * 2f - 1f;
                    flicker = 1f + n * s.flickerAmount;
                }
                else if (spark.State == SparkState.Embers)
                {
                    // Тревожное пульсирование тускло-красного угля; к концу тления слабеет.
                    float pulse = 1f - s.embersPulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * s.embersPulseHz * Mathf.PI * 2f));
                    target = s.embersBrightness * pulse * Mathf.Lerp(0.5f, 1f, spark.EmbersFraction);
                    blendTarget = 0f;
                    rate = s.embersSparksPerSecond;
                }
            }

            float dt = Time.deltaTime;
            Brightness = Mathf.MoveTowards(Brightness, target, s.brightnessSpeed * dt);
            _colorBlend = Mathf.MoveTowards(_colorBlend, blendTarget, s.colorBlendSpeed * dt);
            Color color = Color.Lerp(s.embersColor, s.flameColor, _colorBlend);
            CurrentColor = color;

            if (glowLight != null)
            {
                glowLight.enabled = Brightness > 0.001f;
                glowLight.intensity = _baseLightIntensity * Brightness * flicker;
                glowLight.color = color;
            }

            if (glassRenderers != null)
            {
                // Тон: пламя — белый (цвет берётся из вершин, тёплый), уголь — красный.
                Color tint = Color.Lerp(s.embersColor * 1.6f, Color.white, _colorBlend);
                _block.SetFloat(EmissionIntensity, glassEmission * Brightness * flicker);
                _block.SetColor(EmissionTint, tint);
                foreach (var r in glassRenderers) if (r != null) r.SetPropertyBlock(_block);
            }

            if (sparks != null)
            {
                var emission = sparks.emission;
                emission.rateOverTime = rate;
                if (rate > 0.01f && !sparks.isPlaying) sparks.Play();
                else if (rate <= 0.01f && sparks.isPlaying) sparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
