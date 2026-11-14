using LastRide.Core;
using LastRide.ProcGen.Models;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Огни Маяка в финале (ТЗ 3, этап 12.2): после вставки Искры загораются окна электростанции, затем ряды огней башни
    /// снизу вверх — каждый, когда кабинка лифта проходит его высоту, а на вершине разгорается фонарь (стекло + свет).
    /// Стоит на корне Маяка (ставит <c>FinaleBuilder</c>), читает состояние <see cref="BeaconFinale"/>. Свечение — через
    /// MaterialPropertyBlock (<c>_EmissionIntensity</c>, <c>_BaseTint</c> шейдера мира), материалы ассета не трогаются.
    /// Параметры — <c>GameBalance.finale</c>.
    /// </summary>
    public sealed class BeaconLights : MonoBehaviour
    {
        private static readonly int EmissionIntensity = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int BaseTint = Shader.PropertyToID("_BaseTint");
        private static readonly Color DarkGlass = new Color(0.22f, 0.24f, 0.28f, 1f);

        public BeaconFinale finale;
        public Renderer[] rows = new Renderer[0];
        public Renderer lantern;
        public Renderer stationWindows;
        public Light lanternLight;

        private float[] _rowLevel;
        private float _lanternLevel, _stationLevel;
        private MaterialPropertyBlock _block;
        private GameBalance.FinaleSettings _fallback;

        /// <summary>Сколько рядов огней уже горит полностью (для отладки и тестов).</summary>
        public int RowsLit
        {
            get
            {
                int n = 0;
                if (_rowLevel != null) foreach (var l in _rowLevel) if (l >= 0.999f) n++;
                return n;
            }
        }

        public float LanternLevel => _lanternLevel;
        public float StationLevel => _stationLevel;

        private GameBalance.FinaleSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.finale;
                return _fallback ??= new GameBalance.FinaleSettings();
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _rowLevel = new float[rows.Length];
            if (lanternLight != null) { lanternLight.intensity = 0f; lanternLight.range = Cfg.lanternLightRange; lanternLight.enabled = false; }
            ApplyAll();
        }

        private void Update()
        {
            if (finale == null) return;
            var c = Cfg;
            float dt = Time.deltaTime;

            _stationLevel = Mathf.MoveTowards(_stationLevel, finale.Delivered ? 1f : 0f, dt / c.stationRampSeconds);
            _lanternLevel = Mathf.MoveTowards(_lanternLevel, finale.Arrived ? 1f : 0f, dt / c.lanternRampSeconds);

            float cageY = finale.cage != null ? transform.InverseTransformPoint(finale.cage.position).y : 0f;
            for (int i = 0; i < _rowLevel.Length; i++)
            {
                bool on = finale.Delivered && (finale.Arrived || cageY >= BeaconModel.LightRowHeight(i));
                _rowLevel[i] = Mathf.MoveTowards(_rowLevel[i], on ? 1f : 0f, dt / c.lightRowRampSeconds);
            }
            ApplyAll();
        }

        private void ApplyAll()
        {
            var c = Cfg;
            if (rows != null)
                for (int i = 0; i < rows.Length && i < _rowLevel.Length; i++) Apply(rows[i], _rowLevel[i], c.lightEmission);
            Apply(stationWindows, _stationLevel, c.lightEmission * 0.6f);
            Apply(lantern, _lanternLevel, c.lightEmission * 1.4f);
            if (lanternLight != null)
            {
                lanternLight.enabled = _lanternLevel > 0.001f;
                lanternLight.intensity = c.lanternLightIntensity * _lanternLevel;
                lanternLight.range = c.lanternLightRange;
            }
        }

        private void Apply(Renderer r, float level, float emission)
        {
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetFloat(EmissionIntensity, emission * level);
            _block.SetColor(BaseTint, Color.Lerp(DarkGlass, Color.white, level));
            r.SetPropertyBlock(_block);
        }
    }
}
