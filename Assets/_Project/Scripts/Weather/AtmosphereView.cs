using LastRide.Core;
using UnityEngine;

namespace LastRide.Weather
{
    /// <summary>
    /// Туман, небо и свет сцены по актам (ТЗ 8.2, этап 9.2). Читает <see cref="WeatherSystem"/> и пишет в <see cref="RenderSettings"/>:
    /// <list type="bullet">
    /// <item>Exp2-туман — плотность и цвет из <c>Weather.Current</c> (уже плавно интерполируются погодой);</item>
    /// <item>Directional Light (цвет, интенсивность, угол) и трёхцветное окружение — из <c>ActSettings.lighting</c> текущего акта,
    /// плавно (скорость ~ 1/transitionSeconds акта); шторм приглушает солнце и небо;</item>
    /// <item>небо (шейдер «The Last Ride/Sky», своя копия материала): градиент зенит–горизонт из <c>lighting.skyZenith/skyHorizon</c>,
    /// по мере густоты тумана сливается с ним — белая мгла.</item>
    /// </list>
    /// Цветокоррекцию и виньетку акта читает пост-обработка (9.3). Создаётся в <c>GameManager.Awake</c>; параметры — <c>GameBalance → Atmosphere</c>.
    /// </summary>
    public sealed class AtmosphereView : MonoBehaviour
    {
        private static readonly int Zenith = Shader.PropertyToID("_Zenith");
        private static readonly int Horizon = Shader.PropertyToID("_Horizon");
        private static readonly int Ground = Shader.PropertyToID("_Ground");
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int WhiteoutId = Shader.PropertyToID("_Whiteout");

        private Light _sun;
        private Material _sky;
        private bool _skyTried, _init;

        // Сглаженное освещение.
        private Color _sunColor, _skyA, _equatorA, _groundA, _zenith, _horizon;
        private float _sunIntensity;
        private Vector3 _sunEuler;

        /// <summary>Доля «мглы» неба 0..1 (для других слоёв).</summary>
        public float Whiteout { get; private set; }

        private GameBalance.AtmosphereSettings _fallback;

        private GameBalance.AtmosphereSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.atmosphere;
                return _fallback ??= new GameBalance.AtmosphereSettings();
            }
        }

        private void Awake()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        private void OnDestroy()
        {
            if (_sky != null) Destroy(_sky);
        }

        private void EnsureSun()
        {
            if (_sun != null) return;
            _sun = RenderSettings.sun;
            if (_sun != null) return;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { _sun = l; break; }
        }

        private void EnsureSky()
        {
            if (_sky != null || _skyTried) return;
            _skyTried = true;
            var src = Cfg.skyMaterial;
            if (src == null) return; // нет своего неба — остаётся небо сцены
            _sky = new Material(src) { name = src.name + "_Runtime" };
            RenderSettings.skybox = _sky;
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Weather == null) return;
            var act = gm.Weather.ActSettings;
            if (act == null) return;
            EnsureSun();
            EnsureSky();

            var c = Cfg;
            var l = act.lighting;
            float dt = Time.unscaledDeltaTime;

            if (!_init)
            {
                _init = true;
                _sunColor = l.sunColor; _sunIntensity = l.sunIntensity; _sunEuler = l.sunEuler;
                _skyA = l.ambientSky; _equatorA = l.ambientEquator; _groundA = l.ambientGround;
                _zenith = l.skyZenith; _horizon = l.skyHorizon;
            }
            else
            {
                float k = 1f - Mathf.Exp(-c.lightingSmoothing / Mathf.Max(0.5f, act.transitionSeconds) * dt);
                _sunColor = Color.Lerp(_sunColor, l.sunColor, k);
                _sunIntensity = Mathf.Lerp(_sunIntensity, l.sunIntensity, k);
                _sunEuler = new Vector3(
                    Mathf.LerpAngle(_sunEuler.x, l.sunEuler.x, k),
                    Mathf.LerpAngle(_sunEuler.y, l.sunEuler.y, k),
                    Mathf.LerpAngle(_sunEuler.z, l.sunEuler.z, k));
                _skyA = Color.Lerp(_skyA, l.ambientSky, k);
                _equatorA = Color.Lerp(_equatorA, l.ambientEquator, k);
                _groundA = Color.Lerp(_groundA, l.ambientGround, k);
                _zenith = Color.Lerp(_zenith, l.skyZenith, k);
                _horizon = Color.Lerp(_horizon, l.skyHorizon, k);
            }

            float storm = gm.Weather.StormMix;
            float sunDim = 1f - c.stormSunDim * storm;
            float ambDim = 1f - c.stormAmbientDim * storm;

            if (_sun != null)
            {
                _sun.color = _sunColor;
                _sun.intensity = _sunIntensity * sunDim;
                _sun.transform.rotation = Quaternion.Euler(_sunEuler);
            }
            RenderSettings.ambientSkyColor = _skyA * ambDim;
            RenderSettings.ambientEquatorColor = _equatorA * ambDim;
            RenderSettings.ambientGroundColor = _groundA * ambDim;

            // Туман: погода уже сгладила значения.
            var cur = gm.Weather.Current;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = cur.fogDensity;
            RenderSettings.fogColor = cur.fogColor;

            // Небо сливается с туманом.
            Whiteout = Mathf.Clamp01(cur.fogDensity / Mathf.Max(0.001f, c.whiteoutFogDensity));
            Whiteout = Whiteout * Whiteout * (3f - 2f * Whiteout);
            if (_sky != null)
            {
                _sky.SetColor(Zenith, _zenith * ambDim);
                _sky.SetColor(Horizon, _horizon * ambDim);
                _sky.SetColor(Ground, cur.fogColor);
                _sky.SetColor(FogColorId, cur.fogColor);
                _sky.SetFloat(WhiteoutId, Whiteout);
            }
        }
    }
}
