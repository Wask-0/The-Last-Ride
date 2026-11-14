using System;
using System.Reflection;
using LastRide.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace LastRide.Editor
{
    /// <summary>
    /// 10.1: собирает <c>Assets/_Project/Audio/GameMixer.mixer</c> (ТЗ 8.3): группы Master → Music / SFX → Ambience / Voice,
    /// выведенные параметры громкости (<see cref="AudioBusInfo.VolumeParams"/>) и «радио»-фильтр на Voice (ФНЧ + ФВЧ + дисторшн).
    /// Публичного API для создания микшера нет — используется внутренний <c>UnityEditor.Audio.*</c> через рефлексию.
    /// Меню: <c>The Last Ride/Setup/Create Audio Mixer</c>. Повторный запуск пересоздаёт ассет.
    /// </summary>
    internal static class AudioMixerBuilder
    {
        public const string Path = "Assets/_Project/Audio/GameMixer.mixer";
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly Assembly EditorAsm = typeof(UnityEditor.Editor).Assembly;
        private static Type T(string n) => EditorAsm.GetType("UnityEditor.Audio." + n, true);

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Create Audio Mixer")]
        public static void Build()
        {
            try { BuildInternal(); }
            catch (Exception e) { Debug.LogError("[AudioMixerBuilder] " + e); }
        }

        private static object Call(object target, Type type, string method, params object[] args)
        {
            var m = type.GetMethod(method, All);
            if (m == null) throw new MissingMethodException(type.Name + "." + method);
            return m.Invoke(target, args);
        }

        private static void BuildInternal()
        {
            GeneratedAssets.EnsureFolder("Assets/_Project/Audio");
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(Path) != null) AssetDatabase.DeleteAsset(Path);

            var tCtrl = T("AudioMixerController");
            var tGroup = T("AudioMixerGroupController");
            var tEffect = T("AudioMixerEffectController");
            var tPath = T("AudioParameterPath");

            var ctrl = Call(null, tCtrl, "CreateMixerControllerAtPath", Path);
            var master = tCtrl.GetProperty("masterGroup", All).GetValue(ctrl);
            ((UnityEngine.Object)master).name = "Master";

            object NewGroup(string name, object parent)
            {
                var g = Call(ctrl, tCtrl, "CreateNewGroup", name, false);
                Call(ctrl, tCtrl, "AddChildToParent", g, parent);
                return g;
            }

            var music = NewGroup("Music", master);
            var sfx = NewGroup("SFX", master);
            var voice = NewGroup("Voice", master);
            var ambience = NewGroup("Ambience", sfx);

            // Порядок = AudioBus: Master, Music, SFX, Ambience, Voice.
            object[] groups = { master, music, sfx, ambience, voice };
            for (int i = 0; i < groups.Length; i++) Expose(ctrl, tCtrl, tGroup, tPath, groups[i], AudioBusInfo.VolumeParams[i]);

            // Радио-фильтр на Voice (ТЗ 7.1).
            AddEffect(ctrl, tGroup, tEffect, voice, "Highpass Simple", ("Cutoff freq", 380f));
            AddEffect(ctrl, tGroup, tEffect, voice, "Lowpass Simple", ("Cutoff freq", 3200f));
            AddEffect(ctrl, tGroup, tEffect, voice, "Distortion", ("Level", 0.18f));

            EditorUtility.SetDirty((UnityEngine.Object)ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
            int found = 0;
            for (int i = 0; i < AudioBusInfo.Count; i++)
                if (mixer.FindMatchingGroups(AudioBusInfo.GroupPaths[i]).Length > 0 && mixer.GetFloat(AudioBusInfo.VolumeParams[i], out _)) found++;
            var config = AssetDatabase.LoadAssetAtPath<LastRide.Core.GameConfig>("Assets/_Project/Data/Resources/GameConfig.asset");
            if (config != null)
            {
                config.mixer = mixer;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            else Debug.LogWarning("[AudioMixerBuilder] GameConfig.asset не найден — свяжи GameConfig.mixer вручную.");
            Debug.Log($"[The Last Ride] GameMixer создан: {Path}, групп с параметрами {found}/{AudioBusInfo.Count}.");
        }

        private static void Expose(object ctrl, Type tCtrl, Type tGroup, Type tPath, object group, string paramName)
        {
            var guid = Call(group, tGroup, "GetGUIDForVolume");
            var tGroupPath = T("AudioGroupParameterPath");
            var path = Activator.CreateInstance(tGroupPath, All | BindingFlags.CreateInstance, null, new object[] { group, guid }, null);
            Call(ctrl, tCtrl, "AddExposedParameter", path);

            // Новый параметр получает имя по умолчанию — переименовываем по GUID.
            var prop = tCtrl.GetProperty("exposedParameters", All);
            var arr = (Array)prop.GetValue(ctrl);
            for (int i = 0; i < arr.Length; i++)
            {
                var el = arr.GetValue(i);
                var tEl = el.GetType();
                var g = tEl.GetField("guid", All).GetValue(el);
                if (g.Equals(guid))
                {
                    tEl.GetField("name", All).SetValue(el, paramName);
                    arr.SetValue(el, i); // структура — записываем копию обратно
                }
            }
            prop.SetValue(ctrl, arr);
        }

        private static void AddEffect(object ctrl, Type tGroup, Type tEffect, object group, string effectName, (string param, float value) setting)
        {
            var effect = Activator.CreateInstance(tEffect, All | BindingFlags.CreateInstance, null, new object[] { effectName }, null);
            var eo = (UnityEngine.Object)effect;
            eo.name = effectName;
            AssetDatabase.AddObjectToAsset(eo, Path);

            var tCtrl = ctrl.GetType();
            var snapshot = tCtrl.GetProperty("TargetSnapshot", All).GetValue(ctrl);
            Call(effect, tEffect, "SetValueForParameter", ctrl, snapshot, setting.param, setting.value);

            var effects = (Array)tGroup.GetProperty("effects", All).GetValue(group);
            Call(group, tGroup, "InsertEffect", effect, effects.Length);
        }
    }
}
