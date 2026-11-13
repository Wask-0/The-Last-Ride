using System;
using UnityEngine;

namespace LastRide.Core
{
    /// <summary>Набор параметров погоды. Контроллер погоды плавно интерполирует между такими наборами.</summary>
    [Serializable]
    public sealed class WeatherPreset
    {
        [Tooltip("Плотность снега, 0..1 (масштаб эмиссии частиц).")]
        [Range(0, 1)] public float snowDensity = 0.2f;
        [Tooltip("Скорость падения/сноса снега, м/с.")]
        [Min(0)] public float snowSpeed = 1f;
        [Tooltip("Сила ветра, м/с.")]
        [Min(0)] public float windStrength = 2f;
        [Tooltip("Направление ветра в градусах (0 = вдоль +Z мира).")]
        public float windDirectionDeg = 90f;
        [Tooltip("Плотность тумана (Exp2).")]
        [Min(0)] public float fogDensity = 0.004f;
        public Color fogColor = new Color(0.75f, 0.78f, 0.86f);
        [Tooltip("Базовый иней на экране, 0..1 (холод добавляется сверху).")]
        [Range(0, 1)] public float frost = 0f;
        [Tooltip("Время от полного тепла до 0 снаружи, с.")]
        [Min(0.1f)] public float freezeSeconds = 90f;
    }

    /// <summary>Свет и цветокоррекция акта.</summary>
    [Serializable]
    public sealed class ActLighting
    {
        public Color sunColor = new Color(1f, 0.8f, 0.6f);
        [Min(0)] public float sunIntensity = 1.1f;
        public Vector3 sunEuler = new Vector3(15f, -40f, 0f);
        public Color ambientSky = new Color(0.7f, 0.65f, 0.7f);
        public Color ambientEquator = new Color(0.55f, 0.52f, 0.58f);
        public Color ambientGround = new Color(0.35f, 0.38f, 0.5f);
        [Tooltip("Color Adjustments / White Balance: температура (−100..100).")]
        [Range(-100, 100)] public float colorTemperature = 0f;
        [Range(-100, 100)] public float saturation = 0f;
        [Range(-100, 100)] public float contrast = 0f;
        [Tooltip("Базовая виньетка (холод добавляется сверху).")]
        [Range(0, 1)] public float vignette = 0.2f;
    }

    /// <summary>Параметры одного акта (ТЗ 3, 4.3, 4.6).</summary>
    [CreateAssetMenu(menuName = "The Last Ride/Act Settings", fileName = "ActSettings")]
    public sealed class ActSettings : ScriptableObject
    {
        public ActId act = ActId.Act1;

        [Tooltip("Длительность плавного перехода к параметрам этого акта, с.")]
        [Min(0)] public float transitionSeconds = 15f;

        [Header("Погода")]
        [Tooltip("Основная погода акта (в акте 2 — фаза «Затишье», в акте 3 — у подножия).")]
        public WeatherPreset calm = new WeatherPreset();

        [Tooltip("Включить цикл «Шторм ↔ Затишье» (только акт 2).")]
        public bool stormCycle;
        public WeatherPreset storm = new WeatherPreset();
        [Tooltip("Длительность шторма (мин, макс), с.")]
        public Vector2 stormSeconds = new Vector2(40f, 60f);
        [Tooltip("Длительность затишья (мин, макс), с.")]
        public Vector2 calmSeconds = new Vector2(30f, 40f);

        [Tooltip("Погода стихает к концу акта: интерполяция calm → summit по прогрессу пути (акт 3).")]
        public bool blendToSummit;
        public WeatherPreset summit = new WeatherPreset();

        [Tooltip("Порывы ветра в этом акте.")]
        public bool gusts;

        [Header("Лампа (акт 3)")]
        [Tooltip("Время замерзания, если лампа погасла, с. Пока лампа горит — используется freezeSeconds погоды.")]
        [Min(0.1f)] public float freezeSecondsLampOut = 20f;

        [Header("Свет и цвет")]
        public ActLighting lighting = new ActLighting();
    }
}
