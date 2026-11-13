using LastRide.Core;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Для тестовых сцен: при старте переключает режим игрока (например, сразу «в фургоне»),
    /// чтобы включилась нужная карта ввода, ставит Искру в печку (в настоящей игре это делает пролог, 8.4)
    /// и выдаёт немного топлива для проверки панели печки.
    /// Важно: имя файла совпадает с именем класса — иначе Unity не сохранит компонент в сцене.
    /// </summary>
    public sealed class TestSceneBootstrap : MonoBehaviour
    {
        public PlayerMode startMode = PlayerMode.InVan;
        [Tooltip("Зажечь Искру в печке на старте (иначе панель печки не откроется).")]
        public bool lightSparkOnStart = true;
        [Tooltip("Стартовое топливо по типам GameConfig: щепки, доски, брикеты.")]
        public int[] startFuel = { 3, 2, 1 };

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.SetPlayerMode(startMode);

            if (lightSparkOnStart && gm.Spark.State == LastRide.Fire.SparkState.Unlit)
                gm.Spark.PlaceSpark(FireSourceKind.Stove, gm.Balance != null ? gm.Balance.fire.startBurnSeconds : 120f);

            var cfg = GameConfig.Load();
            if (cfg != null && startFuel != null)
                for (int i = 0; i < startFuel.Length && i < cfg.fuelTypes.Length; i++)
                    if (startFuel[i] > 0) gm.Inventory.Add(cfg.fuelTypes[i], startFuel[i]);
        }
    }
}
