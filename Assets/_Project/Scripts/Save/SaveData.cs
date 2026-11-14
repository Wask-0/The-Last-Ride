using System;
using LastRide.Core;
using LastRide.Fire;
using LastRide.Narrative;
using UnityEngine;

namespace LastRide.Save
{
    /// <summary>Положение и поворот объекта на чекпоинте.</summary>
    [Serializable]
    public struct PoseSave
    {
        public Vector3 position;
        public Quaternion rotation;
        public float yaw;
        public float pitch;
    }

    /// <summary>Состояние Искры (печка/лампа, запас, тление).</summary>
    [Serializable]
    public struct SparkSave
    {
        public SparkState state;
        public FireSourceKind source;
        public float reserve;
        public float embersLeft;
    }

    /// <summary>
    /// Данные чекпоинта (ТЗ 4.7, 6.3): всё, что нужно, чтобы вернуть игру в момент прохождения чекпоинта.
    /// Пишется в JSON (<see cref="SaveSystem"/>). Положения сохранены прямо — чекпоинт возвращает игрока туда, где он его взял.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string checkpointId;
        public string savedAtUtc;
        public string difficultyId;
        public bool completed;

        // Ресурсы и состояние игрока
        public FuelStack[] inventory = new FuelStack[0];
        public SparkSave spark;
        public float warmth;
        public PlayerMode playerMode;

        // Прогресс сюжета
        public ActId act;
        public float act1Seconds, act2Seconds, act3Seconds;
        public PrologueState prologue;
        public bool breakdownDone;
        public string[] playedLogs = new string[0];
        public string[] shownHints = new string[0];
        public string[] firedTriggers = new string[0];

        // Мир
        public string[] collectedPickups = new string[0];
        public PoseSave van;
        public PoseSave robot;
        public bool hasVan, hasRobot;
    }
}
