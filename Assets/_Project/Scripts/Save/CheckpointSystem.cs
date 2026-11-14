using System.Linq;
using LastRide.Core;
using LastRide.Level;
using LastRide.Narrative;
using LastRide.Player;
using LastRide.Vehicle;
using UnityEngine;

namespace LastRide.Save
{
    /// <summary>
    /// Чекпоинты (ТЗ 4.7): <c>GameManager.Instance.Checkpoints</c>. По <see cref="CheckpointReached"/> снимает состояние игры
    /// (<see cref="Capture"/>), держит его как <see cref="Last"/> и пишет автосохранение (<see cref="SaveSystem"/>).
    /// Восстановление (<see cref="CheckpointRestored"/>) делает 11.2 на основе <see cref="Last"/>.
    /// </summary>
    public sealed class CheckpointSystem
    {
        private readonly GameManager _gm;

        /// <summary>Последний пройденный чекпоинт (в памяти; null — ещё не было).</summary>
        public SaveData Last { get; private set; }

        public string LastId => Last != null ? Last.checkpointId : null;

        /// <summary>Автосохранение выключено (автотесты, не трогать файл игрока).</summary>
        public bool WriteToDisk { get; set; } = true;

        public CheckpointSystem(GameManager gm)
        {
            _gm = gm;
            EventBus<CheckpointReached>.Subscribe(OnCheckpointReached);
        }

        /// <summary>Чекпоинт загружен из файла (или сброшен): считать его последним.</summary>
        public void SetLast(SaveData data) => Last = data;

        public void Clear() => Last = null;

        private void OnCheckpointReached(CheckpointReached e)
        {
            if (string.IsNullOrEmpty(e.CheckpointId)) return;
            Last = Capture(e.CheckpointId);
            if (WriteToDisk) SaveSystem.Save(Last);
        }

        /// <summary>Снять состояние игры «как сейчас» под id чекпоинта.</summary>
        public SaveData Capture(string checkpointId)
        {
            var gm = _gm;
            var d = new SaveData
            {
                checkpointId = checkpointId,
                savedAtUtc = System.DateTime.UtcNow.ToString("o"),
                difficultyId = gm.Difficulty != null ? gm.Difficulty.id : "easy",
                inventory = gm.Inventory.ToArray(),
                spark = new SparkSave
                {
                    state = gm.Spark.State,
                    source = gm.Spark.Source,
                    reserve = gm.Spark.Reserve,
                    embersLeft = gm.Spark.EmbersLeft
                },
                warmth = gm.Warmth.Value,
                playerMode = gm.PlayerMode,
                act = gm.Acts.Current,
                act1Seconds = gm.Acts.GetSeconds(ActId.Act1),
                act2Seconds = gm.Acts.GetSeconds(ActId.Act2),
                act3Seconds = gm.Acts.GetSeconds(ActId.Act3),
                prologue = gm.Tutorial.State,
                playedLogs = gm.AudioLogs.Played.ToArray(),
                shownHints = gm.Tutorial.Shown.ToArray(),
                firedTriggers = LevelTrigger.Fired.ToArray(),
                collectedPickups = LastRide.Fire.PickupRegistry.CollectedIds.ToArray()
            };

            var breakdown = Object.FindAnyObjectByType<BreakdownDirector>();
            d.breakdownDone = breakdown != null && breakdown.Done;

            var van = Object.FindAnyObjectByType<VanController>();
            if (van != null)
            {
                d.hasVan = true;
                d.van = new PoseSave { position = van.transform.position, rotation = van.transform.rotation, yaw = van.transform.eulerAngles.y };
            }
            var robot = RobotController.Active;
            if (robot != null)
            {
                d.hasRobot = true;
                d.robot = new PoseSave { position = robot.transform.position, rotation = robot.transform.rotation, yaw = robot.Yaw, pitch = robot.Pitch };
            }
            return d;
        }

        /// <summary>
        /// Вернуть игру в состояние чекпоинта (ТЗ 4.7): инвентарь, Искра (запас не ниже <c>fire.restoreMinBurnSeconds</c>), тепло,
        /// подобранные предметы, записи/подсказки/триггеры, акт, позы фургона и робота, режим игрока. В конце — <see cref="CheckpointRestored"/>.
        /// Вызывается в состоянии Restoring (<see cref="DeathDirector"/>), экран в этот момент закрыт инеем.
        /// </summary>
        public void Restore(SaveData d)
        {
            var gm = _gm;
            float warmth = gm.Balance != null ? gm.Balance.death.restoreWarmth : 100f;

            // Ресурсы
            gm.Inventory.Restore(d.inventory);
            if (d.spark.state == LastRide.Fire.SparkState.Unlit) gm.Spark.Reset();
            else gm.Spark.Restore(d.spark.source, d.spark.reserve);
            gm.Warmth.Restore(warmth);
            LastRide.Fire.PickupRegistry.ApplyCollected(d.collectedPickups);

            // Сюжет
            gm.AudioLogs.ResetPlayback();
            gm.AudioLogs.ClearPlayed();
            gm.AudioLogs.MarkPlayed(d.playedLogs);
            LevelTrigger.ClearFired();
            LevelTrigger.MarkFired(d.firedTriggers);
            gm.Acts.Restore(d.act, d.act1Seconds, d.act2Seconds, d.act3Seconds);

            // Мир и игрок
            var van = Object.FindAnyObjectByType<VanController>();
            var robot = RobotController.Active;
            var boarding = Object.FindAnyObjectByType<VanBoarding>();
            var breakdown = Object.FindAnyObjectByType<BreakdownDirector>();

            if (breakdown != null) breakdown.Restore(d.breakdownDone);
            else if (van != null) van.CanDrive = !d.breakdownDone;
            if (van != null && d.hasVan) van.ResetTo(d.van.position, d.van.rotation);

            gm.SetPlayerMode(d.playerMode);
            if (robot != null)
            {
                if (d.playerMode == PlayerMode.InVan)
                {
                    if (!robot.Seated && van != null) robot.SetSeated(true, van.transform);
                }
                else
                {
                    if (robot.Seated) robot.SetSeated(false, null);
                    if (d.hasRobot)
                    {
                        robot.Teleport(d.robot.position, d.robot.yaw);
                        robot.SetLook(d.robot.yaw, d.robot.pitch);
                    }
                }
                var lamp = robot.GetComponent<HeldLamp>();
                if (lamp != null) lamp.SetHeld(d.playerMode == PlayerMode.OnFootWithLamp, 1f);
            }
            if (boarding != null && breakdown == null) boarding.boardingLocked = d.breakdownDone;

            // Пролог и банка с Искрой
            gm.Tutorial.Restore(d.prologue, d.shownHints, System.Array.IndexOf(d.playedLogs, "A1_02") >= 0);
            foreach (var jar in Object.FindObjectsByType<SparkJarPickup>(FindObjectsInactive.Include))
                jar.SetTaken(d.prologue != PrologueState.FindJar);

            if (VanCamera.Active != null) VanCamera.Active.Snap();
            gm.Progress.ResetHint(); // телепорт: искать положение на оси заново, иначе на серпантине «прилипает» к старому участку
            Last = d;
            EventBus<CheckpointRestored>.Publish(new CheckpointRestored { CheckpointId = d.checkpointId });
        }
    }
}
