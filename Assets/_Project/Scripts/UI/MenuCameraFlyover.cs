using LastRide.Core;
using UnityEngine;

namespace LastRide.UI
{
    /// <summary>
    /// Фон главного меню: камера медленно облетает Искру по кругу, радиус и высота «дышат». Параметры — <c>GameBalance.menu</c>.
    /// Стоит на главной камере сцены MainMenu; <see cref="target"/> — точка вокруг Искры, <see cref="groundHeight"/> — высота земли в центре.
    /// </summary>
    public sealed class MenuCameraFlyover : MonoBehaviour
    {
        public Vector3 target = new Vector3(0f, 0.8f, 0f);
        public float startAngleDegrees = 200f;

        private float _angle;
        private GameBalance.MenuSettings _fallback;

        private GameBalance.MenuSettings Cfg
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.Balance != null ? gm.Balance.menu : (_fallback ??= new GameBalance.MenuSettings());
            }
        }

        private void OnEnable()
        {
            _angle = startAngleDegrees;
            Apply(0f);
        }

        private void Update() => Apply(Time.unscaledDeltaTime);

        private void Apply(float dt)
        {
            var c = Cfg;
            _angle += c.cameraOrbitDegreesPerSecond * dt;
            float breath = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime / Mathf.Max(1f, c.cameraBreathSeconds) * Mathf.PI * 2f);
            float radius = Mathf.Lerp(c.cameraRadius.x, c.cameraRadius.y, breath);
            float height = Mathf.Lerp(c.cameraHeight.x, c.cameraHeight.y, breath);

            float a = _angle * Mathf.Deg2Rad;
            Vector3 pos = target + new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius);
            transform.position = pos;

            // Смотрим чуть в сторону от Искры: она уходит вправо, а слева остаётся место под меню.
            Vector3 forward = (target - pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            transform.rotation = Quaternion.LookRotation((target - right * c.lookSideOffset) - pos, Vector3.up);
        }
    }
}
