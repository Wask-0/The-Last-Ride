using LastRide.Core;
using UnityEngine;

namespace LastRide.Vehicle
{
    /// <summary>
    /// Оттенок снега на крыше фургона: белый снег кузова сливался с окружением. Красит только «снежные» поверхности кузова —
    /// свойство шейдера мира <c>_SnowColor</c> через MaterialPropertyBlock (общий материал мира и снег на земле не трогаются).
    /// Цвет — <c>GameBalance.van.roofSnowColor</c>, применяется на лету (можно подбирать в Play Mode).
    /// Ставится VanController-ом автоматически на корень фургона.
    /// </summary>
    public sealed class VanSnowTint : MonoBehaviour
    {
        private static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");

        private Renderer _body;
        private MaterialPropertyBlock _block;
        private Color _applied = new Color(-1f, 0f, 0f, 0f);

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name == LastRide.ProcGen.Models.Anchors.VanBody) { _body = r; break; }
        }

        private void LateUpdate()
        {
            if (_body == null) return;
            var gm = GameManager.Instance;
            Color c = gm != null && gm.Balance != null ? gm.Balance.van.roofSnowColor : new GameBalance.VanSettings().roofSnowColor;
            if (c == _applied) return;
            _applied = c;
            _body.GetPropertyBlock(_block);
            _block.SetColor(SnowColorId, c);
            _body.SetPropertyBlock(_block);
        }
    }
}
