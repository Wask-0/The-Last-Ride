using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Природа и мелкие объекты вдоль дороги (ТЗ 8.1): ёлки, скалы, столбы ЛЭП, вехи с флажками.
    /// Вариативность — через seed (детерминированно). Начало координат — у основания, по центру.
    /// </summary>
    public static class NatureModels
    {
        /// <summary>Ёлка: стопка конусов с шумом, лёгкий наклон. Высота ~3–7 м в зависимости от seed.</summary>
        public static MeshBuilder BuildTree(int seed)
        {
            var rng = new Rng(seed);
            var noise = new Noise3(seed * 13 + 1);
            Color pine = PaletteAsset.Get(PaletteColor.Pine);
            Color pineDark = pine * 0.8f;
            pineDark.a = 1f;
            Color trunk = PaletteAsset.Get(PaletteColor.WoodDark);

            // Ярусы довольно крутые: снег ложится пятнами у кромок, а не покрывает конус целиком.
            int tiers = rng.Range(3, 6);
            float baseRadius = rng.Range(1.0f, 1.4f);
            float tierHeight = rng.Range(1.5f, 2.0f);
            float step = tierHeight * 0.55f;
            float trunkHeight = rng.Range(0.6f, 1.0f);

            var tree = new Shape();
            tree.Append(Primitives.Cylinder(0.2f, 0.15f, trunkHeight + 0.3f, 5, trunk));
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)Mathf.Max(1, tiers - 1);
                float r = Mathf.Lerp(baseRadius, baseRadius * 0.35f, t);
                var tier = Primitives.Cylinder(r, 0f, tierHeight, rng.Range(6, 9), i % 2 == 0 ? pine : pineDark);
                tier.Rotate(new Vector3(0f, rng.Range(0f, 360f), 0f));
                Deform.NoisePerAxis(tier, noise, new Vector3(0.14f, 0.1f, 0.14f) * r, 1.4f, new Vector3(i * 5.3f, 0f, 0f));
                tree.Append(tier, Matrix4x4.Translate(Vector3.up * (trunkHeight + i * step)));
            }
            Deform.Bend(tree, rng.Range(-6f, 6f), trunkHeight + tiers * step + tierHeight);
            return new MeshBuilder().Add(tree);
        }

        /// <summary>Скала: деформированная икосфера, тёмный низ. <paramref name="size"/> — примерный диаметр, м.</summary>
        public static MeshBuilder BuildRock(int seed, float size)
        {
            var rng = new Rng(seed);
            Color light = PaletteAsset.Get(PaletteColor.RockLight);
            Color dark = PaletteAsset.Get(PaletteColor.RockDark);
            // «Снежность» 0.8: на верхушке снег, но камень местами просвечивает.
            var rock = Primitives.Icosphere(0.5f, size > 2.5f ? 2 : 1, light, new Surface(0.8f, 0f));
            Deform.NoiseAlongNormal(rock, new Noise3(seed * 7 + 3), 0.22f, rng.Range(1.4f, 2.4f), 2, new Vector3(seed, 0f, 0f));
            Deform.Jitter(rock, rng.Fork(1), 0.04f);
            rock.Scale(new Vector3(rng.Range(0.9f, 1.3f), rng.Range(0.8f, 1.15f), rng.Range(0.85f, 1.2f)) * size);
            rock.Rotate(new Vector3(0f, rng.Range(0f, 360f), rng.Range(-8f, 8f)));
            Deform.PaintWhere(rock, n => n.y < -0.2f, dark);
            // Немного утопить в землю, чтобы не «висела».
            var b = rock.ComputeBounds();
            rock.Translate(new Vector3(0f, -b.min.y - b.size.y * 0.15f, 0f));
            return new MeshBuilder().Add(rock);
        }

        /// <summary>Деревянный столб ЛЭП с траверсой и изоляторами, высота ~7 м.</summary>
        public static MeshBuilder BuildPowerPole()
        {
            Color wood = PaletteAsset.Get(PaletteColor.WoodDark);
            Color ceramic = PaletteAsset.Get(PaletteColor.SnowShadow);
            var s = new Shape();
            s.Append(Primitives.Cylinder(0.14f, 0.11f, 7f, 6, wood));
            s.Append(Primitives.Box(new Vector3(1.8f, 0.14f, 0.14f), wood), Matrix4x4.Translate(new Vector3(0f, 6.4f, 0f)));
            s.Append(Primitives.Box(new Vector3(0.08f, 0.9f, 0.08f), wood), Matrix4x4.TRS(new Vector3(0.45f, 6.0f, 0f), Quaternion.Euler(0f, 0f, 40f), Vector3.one));
            foreach (float x in new[] { -0.8f, -0.3f, 0.8f })
                s.Append(Primitives.Cylinder(0.05f, 0.04f, 0.16f, 5, ceramic, Surface.NoSnow), Matrix4x4.Translate(new Vector3(x, 6.47f, 0f)));
            Deform.Bend(s, 2.5f, 7f);
            return new MeshBuilder().Add(s);
        }

        /// <summary>
        /// Веха (акт 3): тонкий столбик с тряпичным флажком и тусклым отражателем.
        /// Отражатель — свечение (UV0.y), чтобы проступать сквозь пургу.
        /// </summary>
        public static MeshBuilder BuildMarker(int seed)
        {
            var rng = new Rng(seed);
            Color wood = PaletteAsset.Get(PaletteColor.WoodDark);
            Color fabric = PaletteAsset.Get(PaletteColor.Fabric);
            Color reflector = PaletteAsset.Get(PaletteColor.BeaconLight);
            var s = new Shape();
            float h = rng.Range(1.5f, 1.8f);
            s.Append(Primitives.Cylinder(0.035f, 0.03f, h, 5, wood));

            // Флажок: тонкая пластина с волнистым шумом (рваная тряпка).
            var flag = Primitives.Grid(0.45f, 0.28f, 4, 2, fabric, Surface.NoSnow);
            flag.Rotate(new Vector3(-90f, 0f, 0f)).Translate(new Vector3(0.24f, 0f, 0f));
            Deform.NoisePerAxis(flag, new Noise3(seed + 5), new Vector3(0.03f, 0.04f, 0.08f), 5f);
            flag.MakeDoubleSided();
            s.Append(flag, Matrix4x4.Translate(new Vector3(0.02f, h - 0.18f, 0f)));

            // Отражатели с двух сторон столбика.
            foreach (float z in new[] { -1f, 1f })
                s.Append(Primitives.Box(new Vector3(0.06f, 0.1f, 0.012f), reflector, Surface.Glow(0.5f)), Matrix4x4.Translate(new Vector3(0f, h - 0.5f, z * 0.035f)));
            return new MeshBuilder().Add(s);
        }
    }
}
