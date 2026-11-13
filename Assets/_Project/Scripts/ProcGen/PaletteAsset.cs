using System;
using UnityEngine;

namespace LastRide.ProcGen
{
    /// <summary>Именованные цвета палитры (ТЗ 8.1: 10–15 цветов).</summary>
    public enum PaletteColor
    {
        Snow,
        SnowShadow,
        Ice,
        RockLight,
        RockDark,
        WoodLight,
        WoodDark,
        Rust,
        MetalDark,
        RobotBody,
        Pine,
        Fabric,
        WarmGlow,
        Concrete,
        BeaconLight
    }

    /// <summary>
    /// Палитра low-poly мира. Цвета запекаются в вершины при генерации —
    /// после правки нужно перегенерировать модели.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Ride/Palette", fileName = "Palette")]
    public sealed class PaletteAsset : ScriptableObject
    {
        public const string ResourcePath = "Palette";

        [Serializable]
        public struct Entry
        {
            public PaletteColor key;
            public Color color;
        }

        public Entry[] colors = Defaults();

        private static PaletteAsset _cached;

        public static PaletteAsset Load()
        {
            if (_cached == null) _cached = Resources.Load<PaletteAsset>(ResourcePath);
            return _cached;
        }

        public Color this[PaletteColor key]
        {
            get
            {
                if (colors != null)
                    for (int i = 0; i < colors.Length; i++)
                        if (colors[i].key == key) return colors[i].color;
                return DefaultColor(key);
            }
        }

        /// <summary>Цвет из загруженной палитры, а без неё — значение по умолчанию.</summary>
        public static Color Get(PaletteColor key)
        {
            var p = Load();
            return p != null ? p[key] : DefaultColor(key);
        }

        public static Entry[] Defaults()
        {
            var keys = (PaletteColor[])Enum.GetValues(typeof(PaletteColor));
            var result = new Entry[keys.Length];
            for (int i = 0; i < keys.Length; i++) result[i] = new Entry { key = keys[i], color = DefaultColor(keys[i]) };
            return result;
        }

        public static Color DefaultColor(PaletteColor key)
        {
            switch (key)
            {
                case PaletteColor.Snow: return Hex(0xEEF3FA);
                case PaletteColor.SnowShadow: return Hex(0xB9C6DC);
                case PaletteColor.Ice: return Hex(0x9FC3D9);
                case PaletteColor.RockLight: return Hex(0x7D7F8A);
                case PaletteColor.RockDark: return Hex(0x4A4C57);
                case PaletteColor.WoodLight: return Hex(0x9A6B45);
                case PaletteColor.WoodDark: return Hex(0x5C3D2A);
                case PaletteColor.Rust: return Hex(0xA4532E);
                case PaletteColor.MetalDark: return Hex(0x3A3F47);
                case PaletteColor.RobotBody: return Hex(0xC9A45C);
                case PaletteColor.Pine: return Hex(0x2F4F43);
                case PaletteColor.Fabric: return Hex(0xB23A3A);
                case PaletteColor.WarmGlow: return Hex(0xFFB35C);
                case PaletteColor.Concrete: return Hex(0x8E9196);
                case PaletteColor.BeaconLight: return Hex(0xFFF1C9);
                default: return Color.magenta;
            }
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
