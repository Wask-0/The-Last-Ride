using LastRide.Core;
using UnityEngine;

namespace LastRide.Weather
{
    /// <summary>
    /// Иней на экране (ТЗ 8.2, этап 9.4): считает силу эффекта и отдаёт шейдеру полноэкранного прохода
    /// «The Last Ride/Frost» (<c>FullScreenPassRendererFeature</c> в рендерере) через глобальные <c>_LR_Frost</c> и <c>_LR_Fill</c>.
    /// Иней = max(базовый иней погоды <c>Weather.Current.frost</c>, холод <c>PostFxView.Cold</c>) — «холод добавляется сверху».
    /// Заполнение (<c>_LR_Fill</c>): растёт с <c>Warmth.FreezeProgress</c> (замерзание), а при смерти (<c>GameState.Dying</c>)
    /// экран полностью покрывается за <c>frost.deathFillSeconds</c>; после восстановления тает за <c>frost.meltSeconds</c>.
    /// Параметры — <c>GameBalance → Frost</c>. Создаётся в <c>GameManager.Awake</c>.
    /// </summary>
    public sealed class FrostView : MonoBehaviour
    {
        private static readonly int FrostId = Shader.PropertyToID("_LR_Frost");
        private static readonly int FillId = Shader.PropertyToID("_LR_Fill");

        private PostFxView _post;
        private float _frost, _fill, _death;
        private GameBalance.FrostSettings _fallback;

        /// <summary>Текущая сила инея 0..1 и заполнение 0..1 (для отладки/других слоёв).</summary>
        public float Frost => _frost;
        public float Fill => _fill;

        private GameBalance.FrostSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.frost;
                return _fallback ??= new GameBalance.FrostSettings();
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Weather == null) return;
            if (_post == null) _post = GetComponent<PostFxView>();

            var c = Cfg;
            float dt = Time.unscaledDeltaTime;

            float cold = _post != null ? _post.Cold : 0f;
            float coldFrost = c.coldStrength * cold * cold * (3f - 2f * cold);
            float baseFrost = gm.Weather.Current.frost * c.weatherStrength;
            float target = Mathf.Clamp01(Mathf.Max(baseFrost, coldFrost));
            _frost = Mathf.MoveTowards(_frost, target, c.growSpeed * dt);
            if (_frost > target) _frost = Mathf.MoveTowards(_frost, target, c.meltSpeed * dt);

            // Смерть: полное заполнение; иначе — по прогрессу замерзания.
            bool dying = gm.State == GameState.Dying;
            _death = Mathf.MoveTowards(_death, dying ? 1f : 0f, dt / Mathf.Max(0.05f, dying ? c.deathFillSeconds : c.meltSeconds));
            float freeze = gm.Warmth != null ? gm.Warmth.FreezeProgress : 0f;
            _fill = Mathf.Max(_death, freeze);

            Shader.SetGlobalFloat(FrostId, _frost);
            Shader.SetGlobalFloat(FillId, _fill);
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(FrostId, 0f);
            Shader.SetGlobalFloat(FillId, 0f);
        }
    }
}
