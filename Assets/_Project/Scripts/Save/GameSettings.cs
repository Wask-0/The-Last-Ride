using System.Collections.Generic;
using UnityEngine;

namespace LastRide.Save
{
    /// <summary>
    /// Пользовательские настройки (ТЗ 6.2) в PlayerPrefs: качество графики, разрешение и режим экрана, чувствительность мыши,
    /// инверсия Y. Громкости хранит <c>AudioManager</c> (ключи <c>AudioBusInfo.PrefKeys</c>), субтитры — <c>SubtitleView.Enabled</c>.
    /// Разрешение, режим экрана и качество применяются при старте игры (<see cref="ApplyOnStartup"/>) только если игрок их менял,
    /// чтобы не трогать окно редактора и систему по умолчанию.
    /// </summary>
    public static class GameSettings
    {
        public const string KeyQuality = "opt_quality";
        public const string KeyFullscreen = "opt_fullscreen";
        public const string KeyResWidth = "opt_res_w";
        public const string KeyResHeight = "opt_res_h";
        public const string KeyMouse = "opt_mouse";
        public const string KeyInvertY = "opt_invert_y";
        public const string KeyDeveloper = "opt_dev_mode";

        public const float MouseMin = 0.2f, MouseMax = 3f;
        public const int MinResolutionWidth = 1024;

        /// <summary>Множитель чувствительности мыши (1 — значение из GameBalance).</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyMouse, 1f), MouseMin, MouseMax);
            set => PlayerPrefs.SetFloat(KeyMouse, Mathf.Clamp(value, MouseMin, MouseMax));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(KeyInvertY, 0) != 0;
            set => PlayerPrefs.SetInt(KeyInvertY, value ? 1 : 0);
        }

        /// <summary>
        /// Режим разработчика (отладочные клавиши F1–F5: оверлей, чекпоинт, топливо, бессмертие, время). По умолчанию выключен;
        /// включается в настройках и работает в любой сборке.
        /// </summary>
        public static bool DeveloperMode
        {
            get => PlayerPrefs.GetInt(KeyDeveloper, 0) != 0;
            set => PlayerPrefs.SetInt(KeyDeveloper, value ? 1 : 0);
        }

        // ---- Графика ----
        public static string[] QualityNames => QualitySettings.names;

        public static int QualityLevel
        {
            get => QualitySettings.GetQualityLevel();
            set
            {
                int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, true);
                PlayerPrefs.SetInt(KeyQuality, level);
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.HasKey(KeyFullscreen) ? PlayerPrefs.GetInt(KeyFullscreen) != 0 : Screen.fullScreen;
            set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); ApplyScreen(); }
        }

        public static Vector2Int Resolution
        {
            get => PlayerPrefs.HasKey(KeyResWidth) && PlayerPrefs.HasKey(KeyResHeight)
                ? new Vector2Int(PlayerPrefs.GetInt(KeyResWidth), PlayerPrefs.GetInt(KeyResHeight))
                : new Vector2Int(Screen.width, Screen.height);
            set
            {
                PlayerPrefs.SetInt(KeyResWidth, value.x);
                PlayerPrefs.SetInt(KeyResHeight, value.y);
                ApplyScreen();
            }
        }

        /// <summary>Доступные разрешения (уникальные ширина×высота, от <see cref="MinResolutionWidth"/>), по возрастанию.</summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= MinResolutionWidth && !list.Contains(v)) list.Add(v);
            }
            var cur = Resolution;
            if (!list.Contains(cur)) list.Add(cur);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static void ApplyScreen()
        {
            var res = Resolution;
            Screen.SetResolution(res.x, res.y, Fullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
        }

        /// <summary>Применить сохранённые настройки экрана и качества (старт игры). Не трогает то, что игрок не менял.</summary>
        public static void ApplyOnStartup()
        {
            if (PlayerPrefs.HasKey(KeyQuality)) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality), 0, QualitySettings.names.Length - 1), true);
#if !UNITY_EDITOR
            if (PlayerPrefs.HasKey(KeyResWidth) || PlayerPrefs.HasKey(KeyFullscreen)) ApplyScreen();
#endif
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
