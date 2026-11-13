using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LastRide.Editor
{
    /// <summary>
    /// Кириллический шрифт интерфейса: динамический SDF-ассет TMP из LiberationSans (лицензия OFL, идёт с TMP Essentials),
    /// с предзаполненным русским алфавитом, и назначение его шрифтом TMP по умолчанию.
    /// Меню: «The Last Ride/Setup/Create UI Font». Требует импортированных TMP Essential Resources.
    /// </summary>
    internal static class UiFontBuilder
    {
        public const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        public const string FontAssetPath = "Assets/_Project/Data/Fonts/LR_Font SDF.asset";
        public const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string Glyphs =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "«»—–…№°·•←→↑↓×";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Create UI Font")]
        public static void Create()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError("[The Last Ride] Нет " + SourceFontPath + ": импортируй TMP Essential Resources (Window → TextMeshPro).");
                return;
            }

            GeneratedAssets.EnsureFolder("Assets/_Project/Data/Fonts");
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) AssetDatabase.DeleteAsset(FontAssetPath);

            var fontAsset = TMP_FontAsset.CreateFontAsset(source, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fontAsset.name = "LR_Font SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (fontAsset.atlasTexture != null) { fontAsset.atlasTexture.name = "LR_Font Atlas"; AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset); }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "LR_Font Material";
                // Полная версия шейдера (с обводкой и подложкой): мобильная версия обводку не поддерживает.
                var full = Shader.Find("TextMeshPro/Distance Field");
                if (full != null) fontAsset.material.shader = full;
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            fontAsset.TryAddCharacters(Glyphs, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[The Last Ride] В шрифте нет символов: " + missing);
            EditorUtility.SetDirty(fontAsset);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] UI font ready: " + FontAssetPath + " (кириллица, TMP по умолчанию)");
        }
    }
}
