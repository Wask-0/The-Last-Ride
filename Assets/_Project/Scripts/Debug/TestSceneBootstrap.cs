using LastRide.Core;
using UnityEngine;

namespace LastRide.DebugTools
{
    /// <summary>
    /// Для тестовых сцен: при старте переключает режим игрока (например, сразу «в фургоне»),
    /// чтобы включилась нужная карта ввода.
    /// Важно: имя файла совпадает с именем класса — иначе Unity не сохранит компонент в сцене.
    /// </summary>
    public sealed class TestSceneBootstrap : MonoBehaviour
    {
        public PlayerMode startMode = PlayerMode.InVan;

        private void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetPlayerMode(startMode);
        }
    }
}
