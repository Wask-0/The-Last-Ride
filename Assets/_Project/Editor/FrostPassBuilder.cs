using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastRide.Editor
{
    /// <summary>
    /// 9.4: материал <c>M_Frost</c> (шейдер «The Last Ride/Frost») и <c>FullScreenPassRendererFeature</c> «FrostPass»
    /// во всех рендерерах URP проекта (после пост-обработки, Render Graph). Повторный запуск ничего не дублирует.
    /// Меню: <c>The Last Ride/Setup/Add Frost Pass</c>.
    /// </summary>
    internal static class FrostPassBuilder
    {
        public const string MaterialPath = MaterialsBuilder.Folder + "M_Frost.mat";
        public const string FeatureName = "FrostPass";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Add Frost Pass")]
        public static void Build()
        {
            var shader = Shader.Find("The Last Ride/Frost");
            if (shader == null) { Debug.LogError("[FrostPassBuilder] Нет шейдера The Last Ride/Frost."); return; }

            GeneratedAssets.EnsureFolder(MaterialsBuilder.Folder.TrimEnd('/'));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_Frost" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            int added = 0, total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue; // рендереры из пакетов (immutable) не трогаем
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;
                total++;
                if (Ensure(data, mat)) added++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[The Last Ride] Frost pass: рендереров {total}, добавлено {added}.");
        }

        private static bool Ensure(UniversalRendererData data, Material mat)
        {
            foreach (var f in data.rendererFeatures)
            {
                if (f is FullScreenPassRendererFeature existing && existing.name == FeatureName)
                {
                    existing.passMaterial = mat;
                    EditorUtility.SetDirty(existing);
                    return false;
                }
            }

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.passMaterial = mat;
            feature.passIndex = 0;
            feature.requirements = ScriptableRenderPassInput.None;

            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            int n = list.arraySize;
            list.arraySize = n + 1;
            list.GetArrayElementAtIndex(n).objectReferenceValue = feature;
            map.arraySize = n + 1;
            map.GetArrayElementAtIndex(n).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return true;
        }
    }
}
