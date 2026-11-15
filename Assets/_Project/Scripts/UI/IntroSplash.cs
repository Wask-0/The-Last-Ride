using System;
using System.Collections;
using System.IO;
using LastRide.Audio;
using LastRide.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LastRide.UI
{
    /// <summary>
    /// Видеозаставка при запуске игры — до главного меню (один раз за запуск, не при возврате из игры в меню).
    /// Файл кладётся в <c>Assets/StreamingAssets/</c> под именем <c>Intro.mp4</c> (также <c>.mov</c>, <c>.webm</c>); нет файла — заставки нет,
    /// сразу меню. Видео на весь экран с сохранением пропорций (чёрные поля), звук идёт через микшер (громкость «Эффекты»), музыка меню
    /// на это время замолкает. Пропуск — любая клавиша или клик после <see cref="SkipAfterSeconds"/>. Любая ошибка воспроизведения — просто в меню.
    /// В плеере с аргументом <c>-lr-perf</c> заставка выключена.
    /// </summary>
    public static class IntroSplash
    {
        private static readonly string[] FileNames = { "Intro.mp4", "intro.mp4", "Intro.mov", "intro.mov", "Intro.webm", "intro.webm" };
        private const float SkipAfterSeconds = 0.5f;
        private const float FadeSeconds = 0.5f;
        private const float PrepareTimeout = 20f;

        /// <summary>Заставка уже показана (или пропущена как отсутствующая) в этом запуске.</summary>
        public static bool Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Played = false;

        /// <summary>Путь к файлу заставки или null.</summary>
        public static string FindFile()
        {
            string dir = Application.streamingAssetsPath;
            foreach (var name in FileNames)
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>
        /// Проиграть заставку. Любая ошибка внутри (видео, звук, интерфейс) не должна оставить чёрный экран — логируем, убираем
        /// созданное и возвращаемся к меню.
        /// </summary>
        public static IEnumerator Play(GameManager gm)
        {
            var inner = PlayInner(gm);
            while (true)
            {
                object step;
                try
                {
                    if (!inner.MoveNext()) break;
                    step = inner.Current;
                }
                catch (Exception e)
                {
                    Debug.LogError("[Intro] Сбой заставки, переходим в меню: " + e);
                    Cleanup(gm);
                    break;
                }
                yield return step;
            }
        }

        private static void Cleanup(GameManager gm)
        {
            var canvas = gm.transform.Find("IntroCanvas");
            if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
            var host = gm.transform.Find("IntroVideo");
            if (host != null) UnityEngine.Object.Destroy(host.gameObject);
            var am = AudioManager.Instance;
            if (am != null) { am.SetDuck(AudioBus.Music, 0f); am.SetDuck(AudioBus.Ambience, 0f); }
        }

        private static IEnumerator PlayInner(GameManager gm)
        {
            Played = true;
            Debug.Log("[Intro] Старт, файл: " + (FindFile() ?? "нет"));
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lr-perf") >= 0) yield break;
            string file = FindFile();
            if (file == null) yield break;

            // Чёрная подложка и экран для видео — под затемнением сцен (канвас 400 < 500).
            var canvas = UiKit.CreateCanvas(gm.transform, "IntroCanvas", 400);
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            var black = UiKit.Panel(canvas.transform, "Black", Color.black);
            Stretch(black.rectTransform);
            var rawGo = UiKit.Rect(canvas.transform, "Video");
            Stretch(rawGo);
            var raw = rawGo.gameObject.AddComponent<RawImage>();
            raw.color = Color.black;
            raw.raycastTarget = false;
            var fit = rawGo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9f;

            var hint = UiKit.Label(canvas.transform, "Esc — пропустить", 26f, new Color(1f, 1f, 1f, 0.5f), TMPro.TextAlignmentOptions.BottomRight);
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(1f, 0f);
            hrt.anchoredPosition = new Vector2(-40f, 30f);
            hrt.sizeDelta = new Vector2(500f, 50f);
            hint.alpha = 0f;

            var host = new GameObject("IntroVideo");
            host.transform.SetParent(gm.transform, false);
            var src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            var am = AudioManager.Instance;
            if (am != null) src.outputAudioMixerGroup = am.Group(AudioBus.SFX);
            var vp = host.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.source = VideoSource.Url;
            vp.url = new Uri(file).AbsoluteUri;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.SetTargetAudioSource(0, src);

            bool failed = false, finished = false;
            vp.errorReceived += (_, msg) => { failed = true; Debug.LogWarning("[Intro] Ошибка видео: " + msg); };
            vp.loopPointReached += _ => finished = true;

            if (am != null) { am.SetDuck(AudioBus.Music, -60f); am.SetDuck(AudioBus.Ambience, -60f); }

            RenderTexture rt = null;
            vp.Prepare();
            float wait = 0f;
            while (!vp.isPrepared && !failed && wait < PrepareTimeout) { wait += Time.unscaledDeltaTime; yield return null; }

            if (vp.isPrepared && !failed)
            {
                int w = Mathf.Max(16, (int)vp.width), h = Mathf.Max(16, (int)vp.height);
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
                vp.targetTexture = rt;
                raw.texture = rt;
                raw.color = Color.white;
                fit.aspectRatio = (float)w / h;
                vp.Play();

                var fader = gm.Fader;
                if (fader != null) yield return fader.FadeTo(0f, FadeSeconds);

                float t = 0f;
                while (!finished && !failed)
                {
                    t += Time.unscaledDeltaTime;
                    hint.alpha = t > 1f ? 0.5f : 0f;
                    if (t > SkipAfterSeconds && SkipPressed()) break;
                    // Подстраховка: проигрывание остановилось само (конец файла без события).
                    if (t > 1f && !vp.isPlaying && !vp.isPaused) break;
                    yield return null;
                }
                if (fader != null) yield return fader.FadeTo(1f, FadeSeconds);
            }
            else Debug.LogWarning("[Intro] Видео не подготовилось (failed=" + failed + ", prepared=" + vp.isPrepared + ") — пропускаем заставку.");

            vp.Stop();
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(canvas.gameObject);
            if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            if (am != null) { am.SetDuck(AudioBus.Music, 0f); am.SetDuck(AudioBus.Ambience, 0f); }
            Debug.Log("[Intro] Конец заставки.");
        }

        private static bool SkipPressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
