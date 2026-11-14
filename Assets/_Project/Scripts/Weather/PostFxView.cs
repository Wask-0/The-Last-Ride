using LastRide.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastRide.Weather
{
    /// <summary>
    /// Пост-обработка (ТЗ 8.2, этап 9.3): глобальный <see cref="Volume"/> с профилем, создаваемым в рантайме.
    /// Bloom (Искра, огни Маяка), Vignette, Color Adjustments (тёплый грейд акта 1 → холодный акта 2 → белый акта 3),
    /// White Balance, Film Grain и Depth of Field (Gaussian) — как «размытие» при сильном холоде.
    /// Значения акта берутся из <c>ActSettings.lighting</c> (плавно, как свет в <see cref="AtmosphereView"/>);
    /// холод (<c>1 − Warmth.Fraction</c>) усиливает виньетку, обесцвечивает и размывает кадр. Параметры — <c>GameBalance → Post Fx</c>.
    /// Иней на экране — отдельный проход (9.4). Создаётся в <c>GameManager.Awake</c>.
    /// </summary>
    public sealed class PostFxView : MonoBehaviour
    {
        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private Vignette _vignette;
        private ColorAdjustments _color;
        private WhiteBalance _wb;
        private FilmGrain _grain;
        private DepthOfField _dof;

        private bool _init;
        private float _bloomV, _exposure, _grainV, _saturation, _contrast, _temperature, _vignetteV;
        private Color _filter;
        private float _cold; // сглаженный холод 0..1

        private GameBalance.PostFxSettings _fallback;

        /// <summary>Сглаженный холод 0..1, которым пользуются виньетка/размытие (и иней в 9.4).</summary>
        public float Cold => _cold;

        private GameBalance.PostFxSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.postFx;
                return _fallback ??= new GameBalance.PostFxSettings();
            }
        }

        private void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "PostFx_Runtime";
            _bloom = _profile.Add<Bloom>(true);
            _vignette = _profile.Add<Vignette>(true);
            _color = _profile.Add<ColorAdjustments>(true);
            _wb = _profile.Add<WhiteBalance>(true);
            _grain = _profile.Add<FilmGrain>(true);
            _dof = _profile.Add<DepthOfField>(true);

            _bloom.threshold.Override(1f);
            _bloom.scatter.Override(0.6f);
            _vignette.smoothness.Override(0.55f);
            _vignette.rounded.Override(false);
            _grain.type.Override(FilmGrainLookup.Thin1);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.highQualitySampling.Override(false);

            var go = new GameObject("PostFxVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.sharedProfile = _profile;
        }

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Weather == null) return;
            var act = gm.Weather.ActSettings;
            if (act == null) return;

            var l = act.lighting;
            var c = Cfg;
            float dt = Time.unscaledDeltaTime;

            if (!_init)
            {
                _init = true;
                _bloomV = l.bloom; _exposure = l.postExposure; _grainV = l.filmGrain; _saturation = l.saturation;
                _contrast = l.contrast; _temperature = l.colorTemperature; _vignetteV = l.vignette; _filter = l.colorFilter;
            }
            else
            {
                float k = 1f - Mathf.Exp(-3f / Mathf.Max(0.5f, act.transitionSeconds) * dt);
                _bloomV = Mathf.Lerp(_bloomV, l.bloom, k);
                _exposure = Mathf.Lerp(_exposure, l.postExposure, k);
                _grainV = Mathf.Lerp(_grainV, l.filmGrain, k);
                _saturation = Mathf.Lerp(_saturation, l.saturation, k);
                _contrast = Mathf.Lerp(_contrast, l.contrast, k);
                _temperature = Mathf.Lerp(_temperature, l.colorTemperature, k);
                _vignetteV = Mathf.Lerp(_vignetteV, l.vignette, k);
                _filter = Color.Lerp(_filter, l.colorFilter, k);
            }

            // Холод: 0 — в тепле, 1 — полностью замёрз. Начинается с coldStart.
            float cold = 0f;
            if (gm.IsGameplayActive || gm.State == GameState.Dying)
                cold = Mathf.Clamp01((1f - gm.Warmth.Fraction - c.coldStart) / Mathf.Max(0.01f, 1f - c.coldStart));
            _cold = Mathf.Lerp(_cold, cold, 1f - Mathf.Exp(-c.coldSmoothing * dt));
            float coldCurve = _cold * _cold * (3f - 2f * _cold);

            _bloom.threshold.value = c.bloomThreshold;
            _bloom.scatter.value = c.bloomScatter;
            _bloom.intensity.Override(_bloomV);

            _vignette.intensity.Override(Mathf.Clamp01(_vignetteV + c.coldVignette * coldCurve));

            _color.postExposure.Override(_exposure);
            _color.colorFilter.Override(_filter);
            _color.contrast.Override(_contrast);
            _color.saturation.Override(_saturation - c.coldDesaturation * coldCurve);
            _wb.temperature.Override(_temperature);

            _grain.intensity.Override(_grainV);

            // Размытие при сильном холоде.
            float blur = Mathf.Clamp01((_cold - c.blurStart) / Mathf.Max(0.01f, 1f - c.blurStart));
            _dof.active = blur > 0.01f;
            if (_dof.active)
            {
                _dof.gaussianStart.Override(Mathf.Lerp(c.blurStartDistance.x, c.blurStartDistance.y, blur));
                _dof.gaussianEnd.Override(Mathf.Lerp(c.blurEndDistance.x, c.blurEndDistance.y, blur));
                _dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, c.blurMaxRadius, blur));
            }
        }
    }
}
