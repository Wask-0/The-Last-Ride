using System.IO;
using LastRide.Core;
using LastRide.Fire;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Создаёт ScriptableObject-ы баланса со стартовыми значениями из ТЗ.
    /// Существующие ассеты НЕ перезаписываются (чтобы не потерять ручной тюнинг) —
    /// только связываются в GameConfig.
    /// </summary>
    internal static class ConfigAssetsBuilder
    {
        private const string Root = "Assets/_Project/Data/";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Create Config Assets")]
        public static void CreateAll()
        {
            var balance = GetOrCreate<GameBalance>(Root + "Balance/GameBalance.asset", _ => { });

            var acts = new[]
            {
                GetOrCreate<ActSettings>(Root + "Acts/Act1.asset", FillAct1),
                GetOrCreate<ActSettings>(Root + "Acts/Act2.asset", FillAct2),
                GetOrCreate<ActSettings>(Root + "Acts/Act3.asset", FillAct3),
            };

            var fuels = new[]
            {
                GetOrCreate<FuelType>(Root + "Fuel/Kindling.asset", f => Fuel(f, "kindling", "Щепки", "щепки", 20f, 1, new Color(1f, 0.78f, 0.45f))),
                GetOrCreate<FuelType>(Root + "Fuel/Planks.asset", f => Fuel(f, "planks", "Доски", "доски", 60f, 2, new Color(0.95f, 0.6f, 0.3f))),
                GetOrCreate<FuelType>(Root + "Fuel/Briquette.asset", f => Fuel(f, "briquette", "Брикеты", "брикет", 120f, 3, new Color(1f, 0.45f, 0.2f))),
            };

            var easy = GetOrCreate<DifficultyProfile>(Root + "Difficulty/Easy.asset", d =>
            {
                d.id = "easy";
                d.displayName = "Лёгкая";
                d.description = "Акцент на истории. Топлива хватает.";
                d.optionalPickupMultiplier = 1f;
                d.guaranteedPickupsPerSegment = 1;
            });

            // «Напряжённая» (этап 14): меньше необязательного топлива на пути, на каждом отрезке остаётся минимум.
            var hard = GetOrCreate<DifficultyProfile>(Root + "Difficulty/Hard.asset", d =>
            {
                d.id = "hard";
                d.displayName = "Напряжённая";
                d.description = "Ресурсов впритык: меньше необязательного топлива.";
                d.optionalPickupMultiplier = 0.4f; // ниже ~0.4 почти ничего не меняется: на каждом отрезке остаётся минимум 1 предмет
                d.guaranteedPickupsPerSegment = 1;
            });

            GetOrCreate<LastRide.ProcGen.PaletteAsset>(Root + "Resources/Palette.asset", p => p.colors = LastRide.ProcGen.PaletteAsset.Defaults());

            var config = GetOrCreate<GameConfig>(Root + "Resources/GameConfig.asset", _ => { });
            config.balance = balance;
            config.acts = acts;
            config.fuelTypes = fuels;
            config.difficulties = new[] { easy, hard };
            if (config.defaultDifficulty == null) config.defaultDifficulty = easy;
            if (config.audioLogs == null) config.audioLogs = new LastRide.Narrative.AudioLogData[0];
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            Debug.Log("[The Last Ride] Config assets ready: " + Root);
        }

        private static T GetOrCreate<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            var asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Fuel(FuelType f, string id, string name, string prompt, float burn, int key, Color tint)
        {
            f.id = id;
            f.displayName = name;
            f.promptName = prompt;
            f.burnSeconds = burn;
            f.hotkey = key;
            f.tint = tint;
        }

        // ---- Стартовые значения актов (ТЗ 3, 4.3, 4.6) ----

        private static void FillAct1(ActSettings a)
        {
            a.act = ActId.Act1;
            a.transitionSeconds = 15f;
            a.calm = new WeatherPreset
            {
                snowDensity = 0.25f, snowSpeed = 1f, windStrength = 2f, windDirectionDeg = 70f,
                fogDensity = 0.004f, fogColor = new Color(0.80f, 0.74f, 0.78f), frost = 0f, freezeSeconds = 90f
            };
            a.stormCycle = false;
            a.gusts = false;
            a.lighting = new ActLighting
            {
                sunColor = new Color(1f, 0.74f, 0.5f), sunIntensity = 1.1f, sunEuler = new Vector3(12f, -40f, 0f),
                ambientSky = new Color(0.78f, 0.64f, 0.62f), ambientEquator = new Color(0.58f, 0.52f, 0.56f),
                ambientGround = new Color(0.36f, 0.40f, 0.55f),
                colorTemperature = 25f, saturation = 5f, contrast = 5f, vignette = 0.2f
            };
        }

        private static void FillAct2(ActSettings a)
        {
            a.act = ActId.Act2;
            a.transitionSeconds = 20f;
            a.calm = new WeatherPreset
            {
                snowDensity = 0.5f, snowSpeed = 3f, windStrength = 6f, windDirectionDeg = 100f,
                fogDensity = 0.018f, fogColor = new Color(0.55f, 0.60f, 0.68f), frost = 0.15f, freezeSeconds = 40f
            };
            a.stormCycle = true;
            a.storm = new WeatherPreset
            {
                snowDensity = 1f, snowSpeed = 8f, windStrength = 16f, windDirectionDeg = 110f,
                fogDensity = 0.05f, fogColor = new Color(0.60f, 0.64f, 0.70f), frost = 0.5f, freezeSeconds = 12f
            };
            a.stormSeconds = new Vector2(40f, 60f);
            a.calmSeconds = new Vector2(30f, 40f);
            a.gusts = true;
            a.lighting = new ActLighting
            {
                sunColor = new Color(0.65f, 0.72f, 0.85f), sunIntensity = 0.6f, sunEuler = new Vector3(35f, -60f, 0f),
                ambientSky = new Color(0.50f, 0.56f, 0.66f), ambientEquator = new Color(0.45f, 0.48f, 0.55f),
                ambientGround = new Color(0.30f, 0.33f, 0.40f),
                colorTemperature = -20f, saturation = -25f, contrast = 0f, vignette = 0.3f
            };
        }

        private static void FillAct3(ActSettings a)
        {
            a.act = ActId.Act3;
            a.transitionSeconds = 15f;
            a.calm = new WeatherPreset
            {
                snowDensity = 1f, snowSpeed = 9f, windStrength = 18f, windDirectionDeg = 120f,
                fogDensity = 0.08f, fogColor = new Color(0.85f, 0.87f, 0.90f), frost = 0.4f, freezeSeconds = 120f
            };
            a.stormCycle = false;
            a.blendToSummit = true;
            a.summit = new WeatherPreset
            {
                snowDensity = 0.15f, snowSpeed = 1.5f, windStrength = 3f, windDirectionDeg = 120f,
                fogDensity = 0.01f, fogColor = new Color(0.95f, 0.93f, 0.90f), frost = 0f, freezeSeconds = 120f
            };
            a.gusts = true;
            a.freezeSecondsLampOut = 20f;
            a.lighting = new ActLighting
            {
                sunColor = new Color(0.95f, 0.95f, 1f), sunIntensity = 0.8f, sunEuler = new Vector3(25f, -20f, 0f),
                ambientSky = new Color(0.85f, 0.86f, 0.90f), ambientEquator = new Color(0.75f, 0.76f, 0.80f),
                ambientGround = new Color(0.60f, 0.62f, 0.68f),
                colorTemperature = -5f, saturation = -40f, contrast = -5f, vignette = 0.35f
            };
        }
    }
}
