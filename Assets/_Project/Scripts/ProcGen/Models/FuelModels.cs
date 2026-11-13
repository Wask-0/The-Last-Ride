using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Подбираемое топливо (ТЗ 4.1): щепки/ветки, доски, сжатые брикеты.
    /// Без снега и с лёгким собственным тёплым свечением — чтобы читались в пурге.
    /// Начало координат — на земле по центру.
    /// </summary>
    public static class FuelModels
    {
        /// <summary>Слабое свечение «тёплого блика» у подбираемых предметов.</summary>
        public static readonly Surface PickupSurface = new Surface(0f, 0.12f);

        /// <summary>Вязанка щепок и веток (~0.5 м).</summary>
        public static MeshBuilder BuildKindling(int seed)
        {
            var rng = new Rng(seed);
            Color light = PaletteAsset.Get(PaletteColor.WoodLight);
            Color dark = PaletteAsset.Get(PaletteColor.WoodDark);
            var s = new Shape();
            int sticks = rng.Range(5, 8);
            for (int i = 0; i < sticks; i++)
            {
                float len = rng.Range(0.4f, 0.6f);
                var stick = Primitives.Cylinder(rng.Range(0.018f, 0.03f), 0.015f, len, 4, i % 2 == 0 ? light : dark, PickupSurface)
                    .Translate(new Vector3(0f, -len * 0.5f, 0f));
                var rot = Quaternion.Euler(90f + rng.Range(-12f, 12f), rng.Range(-25f, 25f), 0f);
                var pos = new Vector3(rng.Range(-0.06f, 0.06f), 0.04f + rng.Range(0f, 0.06f), rng.Range(-0.03f, 0.03f));
                s.Append(stick, Matrix4x4.TRS(pos, rot, Vector3.one));
            }
            // Перевязка.
            s.Append(Primitives.Torus(0.07f, 0.008f, 8, 3, PaletteAsset.Get(PaletteColor.Fabric), PickupSurface),
                Matrix4x4.TRS(new Vector3(0f, 0.07f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, 0.8f)));
            return new MeshBuilder().Add(s);
        }

        /// <summary>Две-три доски, сложенные стопкой (~0.9 м).</summary>
        public static MeshBuilder BuildPlanks(int seed)
        {
            var rng = new Rng(seed);
            Color light = PaletteAsset.Get(PaletteColor.WoodLight);
            Color dark = PaletteAsset.Get(PaletteColor.WoodDark);
            var s = new Shape();
            int count = rng.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                var plank = Primitives.Box(new Vector3(0.16f, 0.035f, rng.Range(0.75f, 0.95f)), i % 2 == 0 ? light : dark, PickupSurface);
                Deform.Jitter(plank, rng.Fork(i), 0.008f);
                s.Append(plank, Matrix4x4.TRS(new Vector3(rng.Range(-0.03f, 0.03f), 0.02f + i * 0.037f, 0f), Quaternion.Euler(0f, rng.Range(-10f, 10f), 0f), Vector3.one));
            }
            return new MeshBuilder().Add(s);
        }

        /// <summary>Сжатый брикет топлива — плотный тёмный блок с тёплыми прожилками (~0.25 м).</summary>
        public static MeshBuilder BuildBriquette()
        {
            Color dark = PaletteAsset.Get(PaletteColor.RockDark);
            Color warm = PaletteAsset.Get(PaletteColor.WarmGlow);
            var s = new Shape();
            var block = Primitives.BeveledBox(new Vector3(0.26f, 0.11f, 0.16f), 0.02f, dark, PickupSurface);
            s.Append(block, Matrix4x4.Translate(new Vector3(0f, 0.055f, 0f)));
            // Полоски-прожилки, которые чуть светятся — брикет должен выглядеть «ценным».
            foreach (float x in new[] { -0.07f, 0f, 0.07f })
                s.Append(Primitives.Box(new Vector3(0.012f, 0.115f, 0.165f), warm, new Surface(0f, 0.35f)), Matrix4x4.Translate(new Vector3(x, 0.055f, 0f)));
            return new MeshBuilder().Add(s);
        }
    }
}
