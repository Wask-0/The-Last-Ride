using LastRide.Core;
using LastRide.UI;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Player
{
    /// <summary>
    /// Выход из фургона и посадка по E (ТЗ 2.2). Режим игрока (<see cref="PlayerMode"/>) — источник истины:
    /// как только он становится InVan, робот «садится» (капсула выключена, привязан к фургону),
    /// как только OnFoot — встаёт у <c>ExitPoint</c>. Камеры переключаются по приоритету (Cinemachine делает бленд).
    /// Выйти можно, только если фургон почти стоит (<c>movement.maxExitVanSpeed</c>); сесть — рядом с <c>DoorPoint</c>.
    /// Подсказки «[E] Сесть» — подзадача 4.4.
    /// </summary>
    public sealed class VanBoarding : MonoBehaviour
    {
        public VanController van;
        public RobotController robot;

        private Transform _exitPoint, _doorPoint;
        private float _cooldown;
        private float _sitRequestedAt = -1f;
        private Interactor _interactor;

        /// <summary>Рядом ли робот с дверью (для подсказки 4.4).</summary>
        public bool RobotNearDoor =>
            van != null && robot != null && !robot.Seated && _doorPoint != null &&
            HorizontalDistance(robot.transform.position, _doorPoint.position) <= Settings.interactDistance;

        /// <summary>Фургон стоит достаточно медленно, чтобы выйти.</summary>
        public bool VanSlowEnough => van != null && van.Speed <= Settings.maxExitVanSpeed;

        private GameBalance.MovementSettings _fallback;
        private GameBalance.MovementSettings Settings
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Balance != null) return gm.Balance.movement;
                return _fallback ??= new GameBalance.MovementSettings();
            }
        }

        private void Start()
        {
            if (van == null) van = FindAnyObjectByType<VanController>();
            if (robot == null) robot = FindAnyObjectByType<RobotController>();
            if (robot != null) _interactor = robot.GetComponent<Interactor>();
            _exitPoint = FindDeep(van != null ? van.transform : null, LastRide.ProcGen.Models.Anchors.ExitPoint);
            _doorPoint = FindDeep(van != null ? van.transform : null, LastRide.ProcGen.Models.Anchors.DoorPoint);
            if (_exitPoint == null || _doorPoint == null)
                Debug.LogError("[VanBoarding] У фургона нет якорей ExitPoint/DoorPoint.");
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || van == null || robot == null) return;

            SyncSeat(gm.PlayerMode);

            if (!gm.IsGameplayActive) { InteractionPrompt.Hide(this); return; }
            _cooldown -= Time.unscaledDeltaTime;

            if (gm.PlayerMode == PlayerMode.InVan)
            {
                bool ready = robot.Seated && _cooldown <= 0f;
                bool pressed = ready && GameInput.ExitVanPressed;
                if (!ready)
                {
                    InteractionPrompt.Hide(this);
                }
                else if (VanSlowEnough)
                {
                    // Подсказка «[E] Выйти» — только когда фургон почти стоит.
                    InteractionPrompt.Show(this, InteractionPrompt.Key("E", "Выйти"), DoorPromptPriority);
                    if (pressed)
                    {
                        _cooldown = Settings.boardingCooldown;
                        InteractionPrompt.Hide(this);
                        gm.SetPlayerMode(PlayerMode.OnFoot);
                        SyncSeat(gm.PlayerMode);
                    }
                }
                else if (pressed)
                {
                    _movingHintUntil = Time.unscaledTime + MovingHintSeconds;
                    InteractionPrompt.Show(this, "Остановите фургон, чтобы выйти", DoorPromptPriority, MovingHintSeconds);
                }
                else if (Time.unscaledTime >= _movingHintUntil)
                {
                    InteractionPrompt.Hide(this);
                }
            }
            else if (!robot.Seated && RobotNearDoor)
            {
                bool aimingAtSomething = _interactor != null && _interactor.Target != null;
                if (aimingAtSomething || _cooldown > 0f)
                {
                    InteractionPrompt.Hide(this);
                }
                else
                {
                    InteractionPrompt.Show(this, InteractionPrompt.Key("E", "Сесть"), DoorPromptPriority);
                    if (GameInput.InteractPressed)
                    {
                        _cooldown = Settings.boardingCooldown;
                        InteractionPrompt.Hide(this);
                        gm.SetPlayerMode(PlayerMode.InVan);
                        SyncSeat(gm.PlayerMode);
                    }
                }
            }
            else
            {
                InteractionPrompt.Hide(this);
            }
        }

        private const int DoorPromptPriority = 5;
        private const float MovingHintSeconds = 1.5f;
        private float _movingHintUntil;

        private void SyncSeat(PlayerMode mode)
        {
            bool shouldSit = mode == PlayerMode.InVan;
            if (!shouldSit || robot.Seated) _sitRequestedAt = -1f;

            if (shouldSit && !robot.Seated)
            {
                // Камера робота — источник бленда к камере фургона: пока бленд идёт, робот стоит на месте,
                // иначе источник «прыгает» в фургон и кадр дёргается.
                if (_sitRequestedAt < 0f) _sitRequestedAt = Time.unscaledTime;
                var gm = GameManager.Instance;
                float blend = gm != null && gm.Balance != null ? gm.Balance.presentation.cameraBlendSeconds : 0.6f;
                if (Time.unscaledTime - _sitRequestedAt >= blend + 0.05f)
                    robot.SetSeated(true, van.transform);
            }
            else if (!shouldSit && robot.Seated)
            {
                robot.SetSeated(false, null);
                Vector3 p = _exitPoint != null ? _exitPoint.position : van.transform.position + van.transform.right * -1.6f;
                float yaw = van.transform.eulerAngles.y;
                robot.Teleport(p + Vector3.up * 0.05f, yaw);
            }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
