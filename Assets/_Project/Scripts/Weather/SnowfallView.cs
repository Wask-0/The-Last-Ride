using LastRide.Core;
using UnityEngine;

namespace LastRide.Weather
{
    /// <summary>
    /// Метель (ТЗ 8.2, этап 9.1): две системы частиц, следующие за главной камерой.
    /// Дальний слой — мелкий снег в большой коробке вокруг камеры; ближний — крупные размытые хлопья у самой камеры
    /// (полная доля в акте 3, часть — в шторме акта 2). Плотность и скорость берутся из <see cref="WeatherSystem.Current"/>,
    /// снос — по ветру (<see cref="WeatherSystem.WindDirection"/>) с усилением на порывах. Частицы в мировых координатах,
    /// поэтому при езде снег «пролетает мимо»; область появления смещена вперёд по движению камеры.
    /// Создаётся в <c>GameManager.Awake</c>. Параметры — <c>GameBalance → Snowfall</c>.
    /// </summary>
    public sealed class SnowfallView : MonoBehaviour
    {
        private ParticleSystem _far, _near;
        private Transform _farT, _nearT;
        private Camera _cam;
        private Vector3 _lastCamPos;
        private Vector3 _camVelocity;
        private float _nearWeight;
        private Texture2D _fallbackTex;
        private Material _fallbackMat;
        private GameBalance.SnowfallSettings _fallback;

        private GameBalance.SnowfallSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.snowfall;
                return _fallback ??= new GameBalance.SnowfallSettings();
            }
        }

        private void Awake()
        {
            _far = Create("Snow_Far", out _farT);
            _near = Create("Snow_Near", out _nearT);
            ApplyStatic();
        }

        private void OnDestroy()
        {
            if (_fallbackTex != null) Destroy(_fallbackTex);
            if (_fallbackMat != null) Destroy(_fallbackMat);
        }

        private ParticleSystem Create(string name, out Transform t)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            t = go.transform;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = false;
            main.gravityModifier = 0f;
            main.maxParticles = 20000;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }

        private Material FlakeMaterial
        {
            get
            {
                var cfg = Cfg;
                if (cfg.flakeMaterial != null) return cfg.flakeMaterial;
                if (_fallbackMat != null) return _fallbackMat;
                // Запасной вариант без ассета: процедурный мягкий круг на прозрачном Unlit-шейдере.
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (sh == null) return null;
                _fallbackTex = MakeSoftCircle(32);
                _fallbackMat = new Material(sh) { name = "M_Snowflake_Runtime", mainTexture = _fallbackTex };
                _fallbackMat.SetFloat("_Surface", 1f);
                _fallbackMat.SetFloat("_Blend", 0f);
                _fallbackMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _fallbackMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _fallbackMat.SetFloat("_ZWrite", 0f);
                _fallbackMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _fallbackMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return _fallbackMat;
            }
        }

        /// <summary>Мягкий белый круг (альфа спадает к краю) — для запасного материала и ассета M_Snowflake.</summary>
        public static Texture2D MakeSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_SnowSoft" };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return tex;
        }

        private void ApplyStatic()
        {
            var c = Cfg;
            var mat = FlakeMaterial;
            Setup(_far, _farT, c.farVolume, c.farLifetime, c.farSize, c.farAlpha, mat);
            Setup(_near, _nearT, c.nearVolume, c.nearLifetime, c.nearSize, c.nearAlpha, mat);
        }

        private static void Setup(ParticleSystem ps, Transform t, Vector3 volume, float life, Vector2 size, float alpha, Material mat)
        {
            var main = ps.main;
            main.startLifetime = life;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = 0f;
            main.startColor = new Color(1f, 1f, 1f, alpha);
            var shape = ps.shape;
            shape.scale = volume;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (mat != null) r.sharedMaterial = mat;
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Weather == null) return;
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
                _lastCamPos = _cam.transform.position;
            }

            float dt = Time.deltaTime;
            var c = Cfg;
            var w = gm.Weather;
            var cur = w.Current;
            Vector3 camPos = _cam.transform.position;
            if (dt > 1e-5f)
            {
                Vector3 v = (camPos - _lastCamPos) / dt;
                // Рывки (телепорт) не должны улетать в смещение — ограничиваем и сглаживаем.
                v = Vector3.ClampMagnitude(v, 30f);
                _camVelocity = Vector3.Lerp(_camVelocity, v, 1f - Mathf.Exp(-4f * dt));
            }
            _lastCamPos = camPos;

            // Доля ближнего слоя: акт 3 — полностью, шторм акта 2 — часть.
            bool act3 = gm.Acts != null && gm.Acts.Current == ActId.Act3;
            float nearTarget = act3 ? 1f : w.StormMix * c.nearStormShare;
            _nearWeight = Mathf.MoveTowards(_nearWeight, nearTarget, c.nearBlendSpeed * dt);

            // Скорость частиц в мировых координатах: снос ветром + падение.
            float gust = 1f + c.gustBoost * (w.GustActive ? w.GustStrength01 : 0f);
            Vector3 wind = w.WindDirection * (cur.windStrength * c.windCarry * gust);
            float fall = Mathf.Max(c.minFallSpeed, cur.snowSpeed * 0.35f);

            _farT.position = camPos + new Vector3(_camVelocity.x, 0f, _camVelocity.z) * c.leadSeconds + wind * -0.5f + Vector3.up * (c.farVolume.y * 0.25f);
            _nearT.position = camPos + _cam.transform.forward * 1.5f;

            Drive(_far, cur.snowDensity * c.farRate, wind, fall, c.turbulence);
            Drive(_near, cur.snowDensity * c.nearRate * _nearWeight, wind * 1.15f, fall * 0.8f, c.turbulence * 1.5f);
        }

        private static void Drive(ParticleSystem ps, float rate, Vector3 wind, float fall, float turbulence)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
            if (rate > 0.5f && !ps.isPlaying) ps.Play();
            else if (rate <= 0.5f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            var vel = ps.velocityOverLifetime;
            vel.x = wind.x;
            vel.y = -fall + wind.y;
            vel.z = wind.z;
            var noise = ps.noise;
            noise.strength = turbulence;
        }
    }
}
