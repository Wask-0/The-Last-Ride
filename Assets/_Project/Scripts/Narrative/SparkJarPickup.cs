using LastRide.Core;
using LastRide.Player;
using UnityEngine;

namespace LastRide.Narrative
{
    /// <summary>
    /// Банка с тлеющим углём — Искра (пролог, ТЗ 3): E → робот берёт её, банка «улетает» в робота.
    /// Дальше её нужно поставить в печку фургона (<see cref="StoveSocket"/>).
    /// </summary>
    public sealed class SparkJarPickup : MonoBehaviour, IInteractable
    {
        private bool _flying, _taken;
        private float _t, _duration;
        private Vector3 _from, _homeScale, _homePosition;
        private Collider[] _colliders;

        public string PromptText => "Взять лампу с Искрой";

        public bool CanInteract
        {
            get
            {
                var gm = GameManager.Instance;
                return !_taken && gm != null && gm.Tutorial.State == PrologueState.FindJar;
            }
        }

        private void Awake()
        {
            _homeScale = transform.localScale;
            _homePosition = transform.position;
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        /// <summary>Загрузка чекпоинта: банка снова на месте (<paramref name="taken"/> = false) или уже у робота.</summary>
        public void SetTaken(bool taken)
        {
            if (_colliders == null) Awake();
            _taken = taken;
            _flying = false;
            transform.localScale = _homeScale;
            if (!taken) transform.position = _homePosition;
            foreach (var c in _colliders) if (c != null) c.enabled = !taken;
            gameObject.SetActive(!taken);
        }

        public void Interact(Interactor who)
        {
            if (!CanInteract) return;
            var gm = GameManager.Instance;
            _taken = true;
            foreach (var c in _colliders) if (c != null) c.enabled = false;
            gm.Tutorial.TakeJar();

            _flying = true;
            _t = 0f;
            _from = transform.position;
            _duration = gm.Balance != null ? gm.Balance.presentation.pickupFlySeconds : 0.35f;
        }

        private void Update()
        {
            if (!_flying) return;
            _t += Time.unscaledDeltaTime;
            float k = _duration > 0f ? Mathf.Clamp01(_t / _duration) : 1f;
            var robot = RobotController.Active;
            Vector3 target = robot != null && robot.cameraPivot != null ? robot.cameraPivot.position + Vector3.down * 0.25f : _from;
            transform.position = Vector3.Lerp(_from, target, k * k);
            transform.localScale = _homeScale * Mathf.Lerp(1f, 0.15f, k * k);
            if (k >= 1f)
            {
                _flying = false;
                gameObject.SetActive(false);
            }
        }
    }
}
