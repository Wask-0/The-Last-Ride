using LastRide.Core;
using LastRide.Vehicle;
using Unity.Cinemachine;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>Сборка камер Cinemachine в сценах (тестовые площадки, уровень).</summary>
    internal static class CameraRigBuilder
    {
        public const string VanCameraName = "VanCamera";

        /// <summary>CinemachineBrain на главной камере; бленд по умолчанию — из GameBalance.presentation.</summary>
        public static CinemachineBrain SetupBrain(Camera cam)
        {
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = cam.gameObject.AddComponent<CinemachineBrain>();
            var balance = GameConfig.Load() != null ? GameConfig.Load().balance : null;
            float blend = balance != null ? balance.presentation.cameraBlendSeconds : 0.6f;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend);
            brain.IgnoreTimeScale = true;
            return brain;
        }

        /// <summary>Камера 3-го лица за фургоном (CinemachineCamera без Body/Aim + VanCamera).</summary>
        public static VanCamera CreateVanCamera(VanController van)
        {
            var go = new GameObject(VanCameraName);
            var cm = go.AddComponent<CinemachineCamera>();
            cm.Priority = new PrioritySettings { Enabled = true, Value = 10 };
            var lens = cm.Lens;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 600f;
            cm.Lens = lens;
            var vc = go.AddComponent<VanCamera>();
            vc.van = van;
            if (van != null)
            {
                go.transform.position = van.transform.position + new Vector3(0f, 4.3f, -5f);
                go.transform.rotation = Quaternion.Euler(20f, van.transform.eulerAngles.y, 0f);
            }
            return vc;
        }
    }
}
