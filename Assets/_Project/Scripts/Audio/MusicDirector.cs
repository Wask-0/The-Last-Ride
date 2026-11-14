using LastRide.Core;
using LastRide.Fire;
using UnityEngine;

namespace LastRide.Audio
{
    /// <summary>
    /// Настроение музыки (10.4): по акту, фазе шторм/затишье, прогрессу подъёма в акте 3, критическому состоянию
    /// (тление Искры, холод) и событию <see cref="SparkDelivered"/> (светлая кода, держится на титрах). Параметры плавно
    /// догоняют цель (<c>musicMoodSpeed</c>); сам звук — <see cref="MusicLayer"/>. Числа — <c>GameBalance → Audio</c>.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public MusicLayer Layer { get; private set; }
        public bool CodaOn { get; private set; }
        public float Tension { get; private set; }

        // Сглаженные значения настроения.
        private float _pad, _melody, _diss, _bright = 0.7f, _drone, _chord = 9f;
        private float _master;

        private void Start()
        {
            var am = AudioManager.Instance;
            if (am == null) { enabled = false; return; }
            Layer = am.CreateLayer<MusicLayer>("Layer_Music", AudioBus.Music);
        }

        private void OnEnable()
        {
            EventBus<SparkDelivered>.Subscribe(OnDelivered);
            EventBus<CheckpointRestored>.Subscribe(OnRestored);
        }

        private void OnDisable()
        {
            EventBus<SparkDelivered>.Unsubscribe(OnDelivered);
            EventBus<CheckpointRestored>.Unsubscribe(OnRestored);
        }

        private void OnDelivered(SparkDelivered e)
        {
            CodaOn = true;
            var gm = GameManager.Instance;
            if (Layer != null) Layer.TriggerCoda(gm != null && gm.Balance != null ? gm.Balance.audio.codaPad : 0.6f);
        }

        private void OnRestored(CheckpointRestored e) => ResetCoda();

        public void ResetCoda()
        {
            CodaOn = false;
            if (Layer != null) Layer.ResetCoda();
        }

        private static float Smooth01(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
            return t * t * (3f - 2f * t);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Balance == null || Layer == null || gm.Weather == null) return;
            var c = gm.Balance.audio;
            float dt = Time.unscaledDeltaTime;

            if (gm.State == GameState.MainMenu && CodaOn) ResetCoda();

            // Общая громкость по состоянию.
            float tm;
            switch (gm.State)
            {
                case GameState.Playing:
                case GameState.Dying:
                case GameState.Restoring:
                case GameState.Ending:
                case GameState.Credits: tm = 1f; break;
                case GameState.Paused: tm = 0.5f; break;
                default: tm = 0f; break;
            }
            _master = Mathf.MoveTowards(_master, tm, dt * 1.5f);

            // Цели по акту.
            float pad, mel, dis, bri, dro, chord;
            var w = gm.Weather;
            float storm = Mathf.Clamp01(w.StormMix);
            float p = Mathf.Clamp01(w.Progress01);

            if (CodaOn)
            {
                pad = c.codaPad; mel = c.codaMelody; dis = 0f; bri = 0.95f; dro = 0f; chord = c.chordSeconds.z * 0.75f;
            }
            else switch (w.Act)
            {
                case ActId.Act1:
                    pad = c.act1Pad; mel = c.act1Melody; dis = 0f; bri = 0.7f; dro = 0f; chord = c.chordSeconds.x;
                    break;
                case ActId.Act2:
                    pad = Mathf.Lerp(c.act2Pad, c.act2Pad * 0.6f, storm); mel = 0f;
                    dis = Mathf.Lerp(c.act2DissonanceCalm, c.act2DissonanceStorm, storm);
                    bri = Mathf.Lerp(0.3f, 0.18f, storm); dro = c.act2Drone * Mathf.Lerp(0.5f, 1f, storm);
                    chord = c.chordSeconds.y;
                    break;
                default:
                    pad = 0.02f + c.act3PadAtSummit * Smooth01(0.3f, 1f, p);
                    mel = c.act3MelodyAtSummit * Smooth01(0.5f, 1f, p);
                    dis = 0.1f * (1f - p);
                    bri = Mathf.Lerp(0.25f, 0.8f, p);
                    dro = c.act3Drone * (1f - 0.7f * p);
                    chord = c.chordSeconds.z;
                    break;
            }

            // Критическое состояние: тление Искры, критический холод, смерть.
            float tTarget = 0f;
            if (!CodaOn)
            {
                if (gm.State == GameState.Dying) tTarget = 1f;
                else if (gm.State == GameState.Playing)
                {
                    if (gm.Spark != null && gm.Spark.State == SparkState.Embers) tTarget = 1f;
                    else if (gm.Warmth != null && gm.Warmth.IsCritical) tTarget = 0.7f;
                }
            }
            Tension = Mathf.MoveTowards(Tension, tTarget, dt * (tTarget > Tension ? 0.7f : 0.3f));
            float sq = c.tensionSqueeze * Tension;
            pad *= 1f - sq; mel *= 1f - Tension; dis = Mathf.Clamp01(dis + 0.3f * Tension);
            bri *= 1f - 0.5f * sq; dro = Mathf.Max(dro, c.tensionDrone * Tension);

            // Плавно к цели (с постоянной времени ≈ 1/musicMoodSpeed).
            float k = 1f - Mathf.Exp(-c.musicMoodSpeed * dt);
            _pad += (pad - _pad) * k; _melody += (mel - _melody) * k; _diss += (dis - _diss) * k;
            _bright += (bri - _bright) * k; _drone += (dro - _drone) * k; _chord += (chord - _chord) * k;

            Layer.Set(_pad, _melody, _diss, _bright, _drone, _chord);
            Layer.Gain = c.musicGain * _master;
        }
    }
}
