using LastRide.ProcGen.Models;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Сборка составных моделей из сгенерированных мешей по опорным точкам моделей.
    /// Используется превью-сценой (2.3) и будет использоваться для префабов (2.6).
    /// </summary>
    internal static class ModelAssembly
    {
        public static GameObject Part(Transform parent, string name, string meshName, Vector3 localPos, Vector3 localEuler, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.Load(meshName);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Van(Transform parent)
        {
            var root = new GameObject("Van");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            Part(t, "Body", "Van_Body", Vector3.zero, Vector3.zero, MaterialsBuilder.World);
            Part(t, "Window", "Van_Window", Vector3.zero, Vector3.zero, MaterialsBuilder.Glow);
            Part(t, "Wheel_FL", "Van_Wheel", VanModel.WheelFrontLeft, new Vector3(0f, 180f, 0f), MaterialsBuilder.World);
            Part(t, "Wheel_FR", "Van_Wheel", VanModel.WheelFrontRight, Vector3.zero, MaterialsBuilder.World);
            Part(t, "Wheel_RL", "Van_Wheel", VanModel.WheelRearLeft, new Vector3(0f, 180f, 0f), MaterialsBuilder.World);
            Part(t, "Wheel_RR", "Van_Wheel", VanModel.WheelRearRight, Vector3.zero, MaterialsBuilder.World);
            return root;
        }

        /// <summary>
        /// Робот из отдельных частей (подвижный — для игрока). Корпус — родитель головы, рук и ключа;
        /// ноги крепятся к корню. Неподвижных замёрзших роботов берём готовыми мешами FrozenRobot_*.
        /// </summary>
        public static GameObject Robot(Transform parent, string name, RobotModel.Pose pose, Material body, Material glass)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localEulerAngles = pose.rootEuler;
            var t = root.transform;

            var hips = new Vector3(0f, pose.hipHeight, 0f);
            var torso = Part(t, "Torso", "Robot_Torso", hips, pose.torsoEuler, body).transform;
            var neckLocal = RobotModel.Neck - RobotModel.TorsoPivot;
            var head = Part(torso, "Head", "Robot_Head", neckLocal, pose.headEuler, body).transform;
            Part(head, "HeadGlass", "Robot_HeadGlass", Vector3.zero, Vector3.zero, glass);
            Part(torso, "Key", "Robot_Key", RobotModel.KeyMount - RobotModel.TorsoPivot, Vector3.zero, body);
            Part(torso, "Arm_L", "Robot_Arm", RobotModel.ShoulderLeft - RobotModel.TorsoPivot, pose.armLeftEuler, body);
            Part(torso, "Arm_R", "Robot_Arm", RobotModel.ShoulderRight - RobotModel.TorsoPivot, pose.armRightEuler, body);
            Part(t, "Leg_L", "Robot_Leg", RobotModel.HipLeft - RobotModel.TorsoPivot + hips, pose.legLeftEuler, body);
            Part(t, "Leg_R", "Robot_Leg", RobotModel.HipRight - RobotModel.TorsoPivot + hips, pose.legRightEuler, body);
            return root;
        }

        public static GameObject Lamp(Transform parent)
        {
            var root = new GameObject("Lamp");
            root.transform.SetParent(parent, false);
            Part(root.transform, "Frame", "Lamp_Frame", Vector3.zero, Vector3.zero, MaterialsBuilder.World);
            Part(root.transform, "Glass", "Lamp_Glass", Vector3.zero, Vector3.zero, MaterialsBuilder.Glow);
            return root;
        }
    }
}
