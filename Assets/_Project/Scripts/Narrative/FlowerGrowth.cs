using LastRide.ProcGen;
using LastRide.ProcGen.Models;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Цветок титров (ТЗ 3, 8.1): процедурная анимация роста — меш пересобирается из <see cref="FlowerModel.BuildFlower"/>
    /// по стадии роста 0..1 (стебель → листья → бутон → раскрытие лепестков). Меш мелкий (~300 треугольников), пересборка дёшева;
    /// перестраивается только при заметном изменении стадии. Материал — <c>M_World</c> (цвета запечены в вершины).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FlowerGrowth : MonoBehaviour
    {
        public Material material;

        private Mesh _mesh;
        private MeshBuilder _builder;
        private float _built = -1f;
        private MaterialPropertyBlock _block;
        private float _emission = -1f;
        private static readonly int EmissionIntensity = Shader.PropertyToID("_EmissionIntensity");

        /// <summary>Текущая стадия роста 0..1.</summary>
        public float Growth01 { get; private set; }

        private void Awake()
        {
            _mesh = new Mesh { name = "Flower_Growing" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
            _builder = new MeshBuilder();
            _block = new MaterialPropertyBlock();
            ApplyEmission();
            SetGrowth(0f);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate() => ApplyEmission();

        /// <summary>Свечение цветка из <c>GameBalance.finale.flowerEmission</c> (по умолчанию слишком яркое — засвечивалось).</summary>
        private void ApplyEmission()
        {
            var gm = LastRide.Core.GameManager.Instance;
            float e = gm != null && gm.Balance != null ? gm.Balance.finale.flowerEmission : 0.25f;
            if (Mathf.Approximately(e, _emission)) return;
            _emission = e;
            var r = GetComponent<MeshRenderer>();
            r.GetPropertyBlock(_block);
            _block.SetFloat(EmissionIntensity, e);
            r.SetPropertyBlock(_block);
        }

        /// <summary>Выставить стадию роста (меш пересобирается, если она сдвинулась заметно или достигла краёв).</summary>
        public void SetGrowth(float growth01)
        {
            growth01 = Mathf.Clamp01(growth01);
            Growth01 = growth01;
            bool edge = growth01 <= 0f || growth01 >= 1f;
            if (_built >= 0f && !edge && Mathf.Abs(growth01 - _built) < 0.003f) return;
            if (_built == growth01) return;
            _built = growth01;

            FlowerModel.BuildFlower(growth01, _builder);
            if (_builder.VertexCount == 0) { _mesh.Clear(); return; }
            _builder.WriteTo(_mesh);
        }
    }
}
