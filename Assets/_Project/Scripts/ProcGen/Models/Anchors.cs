namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Имена дочерних объектов-«якорей» и частей в сгенерированных префабах.
    /// Игровой код находит их через transform.Find(...), поэтому имена — только отсюда.
    /// </summary>
    public static class Anchors
    {
        // ---- Фургон ----
        public const string VanBody = "Body";
        public const string VanWindow = "Window";
        public const string WheelFrontLeft = "Wheel_FL";
        public const string WheelFrontRight = "Wheel_FR";
        public const string WheelRearLeft = "Wheel_RL";
        public const string WheelRearRight = "Wheel_RR";
        public const string StoveLight = "StoveLight";
        public const string ExitPoint = "ExitPoint";
        public const string DoorPoint = "DoorPoint";
        public const string ChimneyTop = "ChimneyTop";

        // ---- Робот ----
        public const string RobotTorso = "Torso";
        public const string RobotHead = "Head";
        public const string RobotHeadGlass = "HeadGlass";
        public const string RobotKey = "Key";
        public const string RobotArmLeft = "Arm_L";
        public const string RobotArmRight = "Arm_R";
        public const string RobotLegLeft = "Leg_L";
        public const string RobotLegRight = "Leg_R";
        public const string RobotEye = "Eye";
        public const string FPArmLeft = "FPArm_L";
        public const string FPArmRight = "FPArm_R";

        // ---- Лампа / банка ----
        public const string LampFrame = "Frame";
        public const string LampGlass = "Glass";
        public const string FlameLight = "FlameLight";
        public const string HandleTop = "HandleTop";
        public const string JarLight = "CoalLight";

        // ---- Маяк ----
        public const string BeaconTower = "Tower";
        public const string BeaconLantern = "Lantern";
        public const string BeaconLiftCage = "LiftCage";
        public const string BeaconStation = "Station";
        public const string BeaconStationWindows = "StationWindows";
        public const string BeaconLightRowPrefix = "LightRow_";
        public const string BeaconSocket = "Socket";
        public const string BeaconLiftBottom = "LiftBottom";
        public const string BeaconLiftTop = "LiftTop";

        // ---- Цветок ----
        public const string FlowerMound = "Mound";
    }
}
