using LastRide.Core;
using LastRide.Player;
using UnityEngine;

namespace LastRide.Fire
{
    /// <summary>
    /// Подбираемое топливо на земле (ТЗ 4.1): E → в инвентарь, предмет «улетает» в робота за
    /// <c>presentation.pickupFlySeconds</c> и пропадает. Звук подбора — этап 10.
    /// <see cref="pickupId"/> — стабильный id для сохранений (задаёт билдер уровня); <see cref="optional"/> — предмет
    /// входит в «необязательные» и может быть убран профилем сложности через <see cref="PickupSegment"/>.
    /// </summary>
    public sealed class FuelPickup : MonoBehaviour, IInteractable
    {
        public FuelType fuel;
        [Min(1)] public int amount = 1;
        [Tooltip("Стабильный id для сохранений. Пусто — предмет не запоминается (возвращается при перезагрузке сцены).")]
        public string pickupId = "";
        [Tooltip("Необязательный предмет: профиль сложности может его убрать (ТЗ 9).")]
        public bool optional = true;

        private Vector3 _homePos, _homeScale;
        private Quaternion _homeRot;
        private Collider[] _colliders;
        private bool _suppressed;
        private bool _flying;
        private float _flyT, _flyDuration;
        private Vector3 _flyFrom;

        public bool Collected => PickupRegistry.IsCollected(pickupId) || _collectedLocal;
        public bool Suppressed => _suppressed;
        private bool _collectedLocal;

        public string PromptText => fuel != null ? "Подобрать " + fuel.promptName : "Подобрать";
        public bool CanInteract => fuel != null && !_flying && !Collected && !_suppressed;

        private void Awake()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _homeScale = transform.localScale;
            _colliders = GetComponentsInChildren<Collider>(true);
            PickupRegistry.Register(this);
        }

        private void OnDestroy() => PickupRegistry.Unregister(this);

        /// <summary>Профиль сложности убрал / вернул предмет.</summary>
        public void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            Refresh();
        }

        /// <summary>Привести объект в соответствие с состоянием «подобран / убран».</summary>
        public void Refresh()
        {
            if (!Collected) _collectedLocal = false;
            bool visible = !Collected && !_suppressed;
            _flying = false;
            if (visible)
            {
                transform.SetPositionAndRotation(_homePos, _homeRot);
                transform.localScale = _homeScale;
            }
            SetCollidersEnabled(visible);
            gameObject.SetActive(visible);
        }

        public void Interact(Interactor who)
        {
            if (!CanInteract) return;
            var gm = GameManager.Instance;
            if (gm != null) gm.Inventory.Add(fuel, amount);

            if (string.IsNullOrEmpty(pickupId)) _collectedLocal = true;
            else PickupRegistry.MarkCollected(pickupId);

            SetCollidersEnabled(false); // сразу перестаёт ловиться прицелом
            _flying = true;
            _flyT = 0f;
            _flyFrom = transform.position;
            _flyDuration = gm != null && gm.Balance != null ? gm.Balance.presentation.pickupFlySeconds : 0.35f;
        }

        private void Update()
        {
            if (!_flying) return;
            _flyT += Time.unscaledDeltaTime;
            float k = _flyDuration > 0f ? Mathf.Clamp01(_flyT / _flyDuration) : 1f;
            float eased = k * k; // разгоняется к роботу
            var robot = RobotController.Active;
            Vector3 target = robot != null && robot.cameraPivot != null
                ? robot.cameraPivot.position + Vector3.down * 0.25f
                : _flyFrom;
            transform.position = Vector3.Lerp(_flyFrom, target, eased);
            transform.localScale = _homeScale * Mathf.Lerp(1f, 0.15f, eased);
            if (k >= 1f)
            {
                _flying = false;
                gameObject.SetActive(false);
            }
        }

        private void SetCollidersEnabled(bool on)
        {
            if (_colliders == null) return;
            foreach (var c in _colliders) if (c != null) c.enabled = on;
        }
    }
}
