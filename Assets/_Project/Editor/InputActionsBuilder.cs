using System.IO;
using LastRide.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastRide.Editor
{
    /// <summary>
    /// Собирает <c>Data/LastRideInput.inputactions</c> (ТЗ 2.2) и назначает его project-wide.
    /// Карты: Global, Van, OnFoot, UI, Debug. Имена действий — в <see cref="InputNames"/>.
    /// Ассет полностью пересобирается — руками его не правим.
    /// </summary>
    internal static class InputActionsBuilder
    {
        public const string AssetPath = "Assets/_Project/Data/LastRideInput.inputactions";
        private const string ProjectWideKey = "com.unity.input.settings.actions";
        private const string Scheme = "KeyboardMouse";

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Setup/Create Input Actions")]
        public static void Build()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "LastRideInput";
            asset.AddControlScheme(Scheme).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");

            // ---- Global: всегда включена, кроме главного меню ----
            var global = asset.AddActionMap(InputNames.MapGlobal);
            Button(global, InputNames.Pause, "<Keyboard>/escape");

            // ---- Van ----
            var van = asset.AddActionMap(InputNames.MapVan);
            Wasd(van.AddAction(InputNames.Drive, InputActionType.Value, expectedControlLayout: "Vector2"));
            Look(van);
            Button(van, InputNames.Handbrake, "<Keyboard>/space");
            Button(van, InputNames.ExitVan, "<Keyboard>/e");
            Button(van, InputNames.Stove, "<Keyboard>/tab");
            FuelSlots(van);

            // ---- OnFoot ----
            var foot = asset.AddActionMap(InputNames.MapOnFoot);
            Wasd(foot.AddAction(InputNames.Move, InputActionType.Value, expectedControlLayout: "Vector2"));
            Look(foot);
            Button(foot, InputNames.Interact, "<Keyboard>/e");
            Button(foot, InputNames.Lamp, "<Keyboard>/r");
            FuelSlots(foot);

            // ---- UI (для InputSystemUIInputModule) ----
            var ui = asset.AddActionMap(InputNames.MapUI);
            var nav = ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            Wasd(nav);
            Button(ui, "Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space");
            Button(ui, "Cancel", "<Keyboard>/escape");
            ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
            ui.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton", expectedControlLayout: "Button");
            ui.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton", expectedControlLayout: "Button");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");

            // ---- Debug (только Editor / Development build) ----
            var dbg = asset.AddActionMap(InputNames.MapDebug);
            Button(dbg, InputNames.DebugOverlay, "<Keyboard>/f1");
            Button(dbg, InputNames.DebugNextCheckpoint, "<Keyboard>/f2");
            Button(dbg, InputNames.DebugAddFuel, "<Keyboard>/f3");
            Button(dbg, InputNames.DebugGodMode, "<Keyboard>/f4");
            Button(dbg, InputNames.DebugTimeScale, "<Keyboard>/f5");

            foreach (var map in asset.actionMaps)
                for (int i = 0; i < map.bindings.Count; i++)
                    map.ChangeBinding(i).WithGroup(Scheme);

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.WriteAllText(AssetPath, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            var imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            EditorBuildSettings.AddConfigObject(ProjectWideKey, imported, true);
            InputSystem.actions = imported;
            Debug.Log("[The Last Ride] Input actions built and set as project-wide: " + AssetPath);
        }

        private static void Button(InputActionMap map, string name, params string[] paths)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
        }

        private static void Look(InputActionMap map)
        {
            map.AddAction(InputNames.Look, InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
        }

        private static void Wasd(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        }

        private static void FuelSlots(InputActionMap map)
        {
            Button(map, InputNames.Fuel1, "<Keyboard>/1", "<Keyboard>/numpad1");
            Button(map, InputNames.Fuel2, "<Keyboard>/2", "<Keyboard>/numpad2");
            Button(map, InputNames.Fuel3, "<Keyboard>/3", "<Keyboard>/numpad3");
        }
    }
}
