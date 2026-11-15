using LastRide.Core;
using LastRide.UI;
using UnityEngine;

namespace LastRide.Player
{
    /// <summary>
    /// Взаимодействие пешком (ТЗ 2.2): луч из прицела (центр камеры) на <c>movement.interactDistance</c>;
    /// первый объект на пути (триггеры тоже) — если это <see cref="IInteractable"/>, показываем «[E] …»
    /// и по E вызываем <see cref="IInteractable.Interact"/>. Стена между роботом и предметом закрывает его.
    /// </summary>
    [RequireComponent(typeof(RobotController))]
    public sealed class Interactor : MonoBehaviour
    {
        public const int PromptPriority = 10;

        private RobotController _robot;
        private IInteractable _target;
        private GameBalance.MovementSettings _fallback;

        /// <summary>Объект под прицелом (null — ничего).</summary>
        public IInteractable Target => _target;

        private GameBalance.MovementSettings Settings
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.movement;
                return _fallback ??= new GameBalance.MovementSettings();
            }
        }

        private void Awake() => _robot = GetComponent<RobotController>();
        private void OnDisable() { _target = null; InteractionPrompt.Hide(this); }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool active = _robot.Seated == false && (gm == null || (gm.IsGameplayActive && gm.PlayerMode != PlayerMode.InVan));
            _target = active ? Aim() : null;

            if (_target == null) { InteractionPrompt.Hide(this); return; }
            InteractionPrompt.Show(this, InteractionPrompt.Key("E", _target.PromptText), PromptPriority);

            if (GameInput.InteractPressed) _target.Interact(this);
        }

        private IInteractable Aim()
        {
            Transform eye = _robot.cameraPivot != null ? _robot.cameraPivot : transform;
            int mask = ~LayerMask.GetMask(Layers.Player, Layers.RoadWall, "Ignore Raycast");
            if (!Physics.Raycast(eye.position, eye.forward, out var hit, Settings.interactDistance, mask, QueryTriggerInteraction.Collide))
                return null;
            var found = hit.collider.GetComponentInParent<IInteractable>();
            return found != null && found.CanInteract ? found : null;
        }
    }
}
