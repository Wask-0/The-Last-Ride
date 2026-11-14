using System.Collections.Generic;
using System.IO;
using LastRide.Core;
using LastRide.Narrative;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Создаёт 10 ассетов <see cref="AudioLogData"/> с черновиками текстов из ТЗ 7.2 и вносит их в GameConfig.
    /// Существующие ассеты НЕ перезаписываются (пользователь правит тексты и тайминги) — только докрепляется
    /// клип из <c>Audio/VoiceLogs/&lt;id&gt;.wav</c>, если поле клипа пустое.
    /// </summary>
    internal static class AudioLogsBuilder
    {
        private const string LogsDir = "Assets/_Project/Data/AudioLogs/";
        private const string VoiceDir = "Assets/_Project/Audio/VoiceLogs";
        private const string ConfigPath = "Assets/_Project/Data/Resources/GameConfig.asset";

        private struct Def
        {
            public string Id;
            public ActId Act;
            public bool Required;
            public string[] Lines;
        }

        // Строки — смысловые куски текста: субтитры показываются по одной.
        private static readonly Def[] Defs =
        {
            new Def { Id = "A1_01", Act = ActId.Act1, Required = true, Lines = new[] { "Мой маленький друг... Мне холодно.", "Отвези Искру на Вершину.", "Не дай ей погаснуть." } },
            new Def { Id = "A1_02", Act = ActId.Act1, Lines = new[] { "Щепки горят быстро, доски — дольше.", "А брикеты... брикеты я берёг для тебя." } },
            new Def { Id = "A1_03", Act = ActId.Act1, Lines = new[] { "Видишь тучи над перевалом?", "Держись ближе к огню." } },
            new Def { Id = "A2_01", Act = ActId.Act2, Lines = new[] { "Помнишь, как мы собирали этот фургон?", "Ты подавал мне гайки." } },
            new Def { Id = "A2_02", Act = ActId.Act2, Lines = new[] { "Они тоже несли свои Искры.", "Не смотри на них долго, малыш.", "Просто едь дальше." } },
            new Def { Id = "A2_03", Act = ActId.Act2, Lines = new[] { "Я всё боялся, что ты так и не поймёшь, что такое тепло.", "Что это не цифра на шкале." } },
            new Def { Id = "A2_04", Act = ActId.Act2, Lines = new[] { "Если станет совсем тяжело — вспомни мастерскую.", "Лампу, чайник, скрип пола." } },
            new Def { Id = "A3_01", Act = ActId.Act3, Required = true, Lines = new[] { "Прости, малыш. Дальше тебе придётся идти одному.", "Но ты уже знаешь, что такое тепло." } },
            new Def { Id = "A3_02", Act = ActId.Act3, Lines = new[] { "Иди по вехам.", "Я ставил их, когда был моложе." } },
            new Def { Id = "A3_FINAL", Act = ActId.Act3, Required = true, Lines = new[] { "Ты познал настоящее тепло." } },
        };

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Create Audio Logs")]
        public static void CreateAll()
        {
            EnsureFolder("Assets/_Project/Data", "AudioLogs");
            EnsureFolder("Assets/_Project/Audio", "VoiceLogs");

            var logs = new List<AudioLogData>();
            int created = 0, clipsLinked = 0;
            foreach (var d in Defs)
            {
                string path = LogsDir + d.Id + ".asset";
                var log = AssetDatabase.LoadAssetAtPath<AudioLogData>(path);
                if (log == null)
                {
                    log = ScriptableObject.CreateInstance<AudioLogData>();
                    log.id = d.Id;
                    log.act = d.Act;
                    log.required = d.Required;
                    log.lines = new AudioLogData.SubtitleLine[d.Lines.Length];
                    for (int i = 0; i < d.Lines.Length; i++)
                        log.lines[i] = new AudioLogData.SubtitleLine { text = d.Lines[i], duration = 0f };
                    AssetDatabase.CreateAsset(log, path);
                    created++;
                }

                if (log.clip == null)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoiceDir}/{d.Id}.wav");
                    if (clip != null)
                    {
                        log.clip = clip;
                        EditorUtility.SetDirty(log);
                        clipsLinked++;
                    }
                }
                logs.Add(log);
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[The Last Ride] GameConfig не найден — сначала «Setup/Create Config Assets».");
            }
            else
            {
                config.audioLogs = logs.ToArray();
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[The Last Ride] Audio logs: {logs.Count} в GameConfig, создано новых {created}, клипов подключено {clipsLinked}. Голос кладём в {VoiceDir}/<id>.wav.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
