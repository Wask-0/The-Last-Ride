using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Материалы мира на шейдере «The Last Ride/World» (подзадача 2.2).
    /// Существующие материалы НЕ перезаписываются — подкрученные в инспекторе значения сохраняются.
    /// </summary>
    internal static class MaterialsBuilder
    {
        public const string ShaderName = "The Last Ride/World";
        public const string Folder = "Assets/_Project/Materials/";
        public const string WorldPath = Folder + "M_World.mat";
        public const string FrostedPath = Folder + "M_World_Frosted.mat";
        public const string GlowPath = Folder + "M_Glow.mat";
        public const string RobotPath = Folder + "M_Robot.mat";
        public const string BeaconPath = Folder + "M_Beacon.mat";
        public const string BeaconLightPath = Folder + "M_BeaconLight.mat";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Create Materials")]
        public static void CreateAll()
        {
            GetOrCreate(WorldPath, "M_World", _ => { });

            // Замёрзшие роботы: снег налипает сильнее и ниже по склону граней.
            GetOrCreate(FrostedPath, "M_World_Frosted", m =>
            {
                m.SetFloat("_SnowThreshold", 0.1f);
                m.SetFloat("_SnowBlend", 0.25f);
                m.SetFloat("_SnowAmount", 1.6f);
                m.SetFloat("_EmissionIntensity", 0f); // у замёрзших роботов фонари погасли
            });

            // Робот игрока: меши робота «снежные» (для замёрзших), а у живого снег выключен.
            GetOrCreate(RobotPath, "M_Robot", m =>
            {
                m.SetFloat("_SnowAmount", 0f);
                m.SetFloat("_SparkleStrength", 0f);
            });

            // Отдельный материал для огней, которые включаются/гаснут независимо (Маяк, отражатели).
            GetOrCreate(GlowPath, "M_Glow", m =>
            {
                m.SetFloat("_EmissionIntensity", 3f);
                m.SetFloat("_SparkleStrength", 0f);
            });

            // Маяк: почти не тонет в тумане — тонкий силуэт виден издалека в акте 1.
            // В акте 2 погода поднимает _FogAmount экземпляра до 1, и Маяк пропадает в метели.
            GetOrCreate(BeaconPath, "M_Beacon", m =>
            {
                m.SetFloat("_FogAmount", 0.15f);
                m.SetFloat("_SparkleStrength", 0f);
            });

            // Огни Маяка: по умолчанию выключены (финал зажигает ряды экземплярами материала).
            GetOrCreate(BeaconLightPath, "M_BeaconLight", m =>
            {
                m.SetColor("_BaseTint", new Color(0.22f, 0.24f, 0.28f)); // погасшее окошко — тёмное стекло
                m.SetFloat("_EmissionIntensity", 0f);
                m.SetFloat("_SparkleStrength", 0f);
                m.SetFloat("_FogAmount", 0.15f);
            });

            // Искры из трубы и лампы: частицы на URP Particles/Unlit, цвет HDR — ловится Bloom.
            if (AssetDatabase.LoadAssetAtPath<Material>(SparkPath) == null)
            {
                var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader != null)
                {
                    GeneratedAssets.EnsureFolder(Folder.TrimEnd('/'));
                    var spark = new Material(particleShader) { name = "M_Spark" };
                    spark.SetColor("_BaseColor", new Color(4f, 2.1f, 0.6f, 1f));
                    AssetDatabase.CreateAsset(spark, SparkPath);
                }
                else Debug.LogError("[MaterialsBuilder] Шейдер URP Particles/Unlit не найден.");
            }

            CreateSnowflake();

            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Materials ready: " + Folder);
        }

        public const string SnowflakePath = Folder + "M_Snowflake.mat";
        private const string SnowTexPath = Folder + "T_SnowSoft.png";

        /// <summary>Материал снежинок метели (9.1) + текстура мягкого круга; привязывает к GameBalance.snowfall.flakeMaterial.</summary>
        private static void CreateSnowflake()
        {
            GeneratedAssets.EnsureFolder(Folder.TrimEnd('/'));
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SnowTexPath) == null)
            {
                var tex = LastRide.Weather.SnowfallView.MakeSoftCircle(32);
                System.IO.File.WriteAllBytes(SnowTexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SnowTexPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(SnowTexPath);
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SnowflakePath);
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) { Debug.LogError("[MaterialsBuilder] Нет шейдера Particles/Unlit."); return; }
                mat = new Material(sh) { name = "M_Snowflake" };
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SnowTexPath));
                mat.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(mat, SnowflakePath);
            }

            var cfg = LastRide.Core.GameConfig.Load();
            if (cfg != null && cfg.balance != null && cfg.balance.snowfall.flakeMaterial == null)
            {
                cfg.balance.snowfall.flakeMaterial = mat;
                EditorUtility.SetDirty(cfg.balance);
            }

            // Небо (9.2): свой градиентный шейдер, сливается с туманом.
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (skyMat == null)
            {
                var skySh = Shader.Find("The Last Ride/Sky");
                if (skySh == null) Debug.LogError("[MaterialsBuilder] Нет шейдера The Last Ride/Sky.");
                else { skyMat = new Material(skySh) { name = "M_Sky" }; AssetDatabase.CreateAsset(skyMat, SkyPath); }
            }
            if (skyMat != null && cfg != null && cfg.balance != null && cfg.balance.atmosphere.skyMaterial == null)
            {
                cfg.balance.atmosphere.skyMaterial = skyMat;
                EditorUtility.SetDirty(cfg.balance);
            }
        }

        public const string SkyPath = Folder + "M_Sky.mat";

        public const string SparkPath = Folder + "M_Spark.mat";
        public static Material Spark => LoadOrCreate(SparkPath);

        public static Material World => LoadOrCreate(WorldPath);
        public static Material Frosted => LoadOrCreate(FrostedPath);
        public static Material Glow => LoadOrCreate(GlowPath);
        public static Material Robot => LoadOrCreate(RobotPath);
        public static Material Beacon => LoadOrCreate(BeaconPath);
        public static Material BeaconLight => LoadOrCreate(BeaconLightPath);

        private static Material LoadOrCreate(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                CreateAll();
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return m;
        }

        private static Material GetOrCreate(string path, string name, System.Action<Material> fill)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[MaterialsBuilder] Шейдер '" + ShaderName + "' не найден (ошибка компиляции?).");
                return null;
            }
            GeneratedAssets.EnsureFolder(Folder.TrimEnd('/'));
            var m = new Material(shader) { name = name };
            fill(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
