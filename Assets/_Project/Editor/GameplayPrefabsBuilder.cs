using LastRide.Core;
using LastRide.Vehicle;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Игровые префабы — Prefab Variant-ы от сгенерированных моделей (Generated/Prefabs).
    /// Вариант хранит игровые компоненты и их настройки; при перегенерации модели
    /// вариант подхватывает новый меш и сохраняет свои компоненты.
    /// Меню: «The Last Ride/Setup/Build Gameplay Prefabs». Существующие варианты не пересоздаются —
    /// только добавляются недостающие компоненты.
    /// </summary>
    internal static class GameplayPrefabsBuilder
    {
        public const string Root = "Assets/_Project/Prefabs/";
        public const string VanPath = Root + "Van.prefab";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Build Gameplay Prefabs")]
        public static void BuildAll()
        {
            if (PrefabBuilder.Load("Vehicles/Van") == null) PrefabBuilder.BuildAll();
            GeneratedAssets.EnsureFolder(Root.TrimEnd('/'));
            BuildVan();
            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Gameplay prefabs ready: " + Root);
        }

        public static GameObject Van => AssetDatabase.LoadAssetAtPath<GameObject>(VanPath);

        private static void BuildVan()
        {
            if (Van == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(PrefabBuilder.Load("Vehicles/Van"));
                instance.name = "Van";
                PrefabUtility.SaveAsPrefabAsset(instance, VanPath); // экземпляр префаба → сохраняется как Variant
                Object.DestroyImmediate(instance);
            }

            var root = PrefabUtility.LoadPrefabContents(VanPath);
            SetLayerRecursive(root, LayerMask.NameToLayer(Layers.Vehicle));
            if (root.GetComponent<Rigidbody>() == null) root.AddComponent<Rigidbody>();
            if (root.GetComponent<VanController>() == null) root.AddComponent<VanController>();
            PrefabUtility.SaveAsPrefabAsset(root, VanPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }
    }
}
