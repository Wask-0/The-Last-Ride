using System;
using System.IO;
using UnityEngine;

namespace LastRide.Save
{
    /// <summary>
    /// Файл сохранения (ТЗ 6.3): JSON в <c>Application.persistentDataPath/save.json</c>.
    /// Запись атомарная (во временный файл, затем замена), битый или чужой версии файл читается как «сохранения нет».
    /// </summary>
    public static class SaveSystem
    {
        public const string FileName = "save.json";

        /// <summary>Переопределение пути (для автотестов); null — стандартный.</summary>
        public static string PathOverride { get; set; }

        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Есть читаемое сохранение с незавершённой игрой — доступно «Продолжить».</summary>
        public static bool HasContinue => TryLoad(out var data) && !data.completed;

        public static bool Exists => File.Exists(FilePath);

        public static bool Save(SaveData data)
        {
            if (data == null) return false;
            string path = FilePath;
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Не удалось записать {path}: {e.Message}");
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* временный файл — не критично */ }
                return false;
            }
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            string path = FilePath;
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Сохранение {path} не читается: {e.Message}");
                data = null;
                return false;
            }
            if (data == null || data.version != SaveData.CurrentVersion || string.IsNullOrEmpty(data.checkpointId))
            {
                Debug.LogWarning($"[Save] Сохранение {path} пустое или неподходящей версии — игнорируем.");
                data = null;
                return false;
            }
            return true;
        }

        /// <summary>Удалить сохранение (после титров, «Новая игра» с подтверждением).</summary>
        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"[Save] Не удалось удалить сохранение: {e.Message}"); }
        }
    }
}
