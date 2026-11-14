using LastRide.Core;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Weather
{
    /// <summary>
    /// Порывы ветра физически толкают фургон и робота (ТЗ 4.5, 4.6) — только пока идёт порыв, постоянный ветер никого не сносит.
    /// Фургон: боковая сила <c>gusts.vanForce</c> × огибающая × <see cref="WeatherSystem.GustPower"/> и на время порыва сниженное боковое
    /// сцепление (<c>gusts.vanGripLoss</c>) — иначе шины гасят любую силу ветра. Робот: снос <c>gusts.robotPush</c> м/с.
    /// Добавляется к GameManager автоматически и сам находит фургон и робота в сцене.
    /// </summary>
    public sealed class WindPusher : MonoBehaviour
    {
        private VanController _van;
        private float _nextSearch;

        private void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Weather == null || gm.Balance == null) return;

            if (_van == null && Time.unscaledTime >= _nextSearch)
            {
                _van = FindAnyObjectByType<VanController>();
                _nextSearch = Time.unscaledTime + 1f;
            }
            if (_van == null) return;

            var w = gm.Weather;
            var g = gm.Balance.gusts;
            if (!gm.IsGameplayActive)
            {
                _van.GripMultiplier = 1f;
                return;
            }

            // Раскачивание: в шторме кузов плавно «ходит» ветром по крену, порыв добавляет толчок по направлению ветра.
            var c = gm.Balance.weather;
            float sway = (Mathf.PerlinNoise(Time.time * c.vanSwayHz, 3.3f) - 0.5f) * 2f * c.vanSwayTorque * w.StormMix;
            float gustRoll = -Vector3.Dot(w.GustDirection, _van.transform.right) * c.vanGustRollTorque * w.GustStrength01 * w.GustPower;
            float torque = sway + gustRoll;
            if (Mathf.Abs(torque) > 0.01f) _van.Body.AddTorque(_van.transform.forward * torque, ForceMode.Force);

            if (!w.GustActive)
            {
                _van.GripMultiplier = 1f;
                return;
            }

            _van.GripMultiplier = 1f - g.vanGripLoss * w.GustStrength01;
            _van.AddForce(w.GustDirection * (g.vanForce * w.GustStrength01 * w.GustPower));
        }

        private void Update()
        {
            var robot = RobotController.Active;
            if (robot == null) return;
            var gm = GameManager.Instance;
            bool push = gm != null && gm.Weather != null && gm.Balance != null && gm.IsGameplayActive
                        && gm.Weather.GustActive && gm.PlayerMode != PlayerMode.InVan && !robot.Seated;
            robot.ExternalVelocity = push
                ? gm.Weather.GustDirection * (gm.Balance.gusts.robotPush * gm.Weather.GustStrength01 * gm.Weather.GustPower)
                : Vector3.zero;
        }
    }
}
