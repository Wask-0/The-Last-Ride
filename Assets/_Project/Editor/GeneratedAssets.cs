using System.IO;
using LastRide.ProcGen;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Сохранение сгенерированных мешей в <c>Assets/_Project/Generated/</c>.
    /// Если ассет уже существует, данные пишутся в него же — GUID и все ссылки (префабы, сцены) сохраняются.
    /// </summary>
    internal static class GeneratedAssets
    {
        public const string Root = "Assets/_Project/Generated/";

        /// <param name="relativePath">Путь внутри Generated без расширения, например "Meshes/Van".</param>
        public static Mesh SaveMesh(MeshBuilder builder, string relativePath)
        {
            string path = Root + relativePath + ".asset";
            string name = Path.GetFileNameWithoutExtension(path);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                builder.WriteTo(mesh);
                mesh.name = name;
                EditorUtility.SetDirty(mesh);
                return mesh;
            }

            EnsureFolder(Path.GetDirectoryName(path));
            mesh = builder.ToMesh(name);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
