using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Маленький заводной робот (ТЗ 8.1): корпус-капсула, голова-фонарь, ключ завода на спине,
    /// руки и ноги отдельными частями (для поз замёрзших роботов), руки для вида от 1-го лица.
    /// Все части «снежные» (UV0.x = 1): у робота игрока снег выключает материал M_Robot,
    /// у замёрзших — M_World_Frosted.
    /// Ось: вперёд +Z. У каждой части начало координат — в её шарнире.
    /// </summary>
    public static class RobotModel
    {
        // ---- Шарниры относительно корня робота (корень — на земле между ступнями) ----
        public const float HipHeight = 0.36f;
        public static readonly Vector3 HipLeft = new Vector3(-0.13f, HipHeight, 0f);
        public static readonly Vector3 HipRight = new Vector3(0.13f, HipHeight, 0f);
        /// <summary>Корпус крепится в точке таза (по центру).</summary>
        public static readonly Vector3 TorsoPivot = new Vector3(0f, HipHeight, 0f);
        public static readonly Vector3 ShoulderLeft = new Vector3(-0.33f, HipHeight + 0.52f, 0f);
        public static readonly Vector3 ShoulderRight = new Vector3(0.33f, HipHeight + 0.52f, 0f);
        public static readonly Vector3 Neck = new Vector3(0f, HipHeight + 0.74f, 0f);
        public static readonly Vector3 KeyMount = new Vector3(0f, HipHeight + 0.42f, -0.29f);
        /// <summary>Высота глаз (камера 1-го лица) — центр стекла головы.</summary>
        public static readonly Vector3 Eye = new Vector3(0f, HipHeight + 0.74f + 0.22f, 0.05f);
        /// <summary>Полная высота до ушка на голове.</summary>
        public const float TotalHeight = HipHeight + 0.74f + 0.62f;

        /// <summary>Поза робота: повороты частей в их шарнирах (градусы).</summary>
        public struct Pose
        {
            /// <summary>Поворот всего робота вокруг точки между ступнями (например, лёг на бок).</summary>
            public Vector3 rootEuler;
            /// <summary>Высота таза над землёй (стоя — <see cref="HipHeight"/>, сидя — меньше).</summary>
            public float hipHeight;
            public Vector3 torsoEuler, headEuler;
            public Vector3 armLeftEuler, armRightEuler;
            public Vector3 legLeftEuler, legRightEuler;

            public static Pose Standing => new Pose { hipHeight = HipHeight };
        }

        /// <summary>
        /// Собрать робота в позе в ОДИН меш (для неподвижных замёрзших роботов).
        /// Модель опускается так, чтобы низ касался земли (чуть утоплен).
        /// </summary>
        public static MeshBuilder BuildPosed(Pose p, float sink = 0.04f)
        {
            var torso = BuildTorso();
            var head = BuildHead();
            var glass = BuildHeadGlass();
            var key = BuildKey();
            var arm = BuildArm();
            var leg = BuildLeg();

            var root = Matrix4x4.Rotate(Quaternion.Euler(p.rootEuler));
            var hips = new Vector3(0f, p.hipHeight, 0f);
            var torsoM = root * Matrix4x4.TRS(hips, Quaternion.Euler(p.torsoEuler), Vector3.one);
            var headM = torsoM * Matrix4x4.TRS(Neck - TorsoPivot, Quaternion.Euler(p.headEuler), Vector3.one);

            var b = new MeshBuilder();
            b.Add(torso, torsoM);
            b.Add(head, headM);
            b.Add(glass, headM);
            b.Add(key, torsoM * Matrix4x4.Translate(KeyMount - TorsoPivot));
            b.Add(arm, torsoM * Matrix4x4.TRS(ShoulderLeft - TorsoPivot, Quaternion.Euler(p.armLeftEuler), Vector3.one));
            b.Add(arm, torsoM * Matrix4x4.TRS(ShoulderRight - TorsoPivot, Quaternion.Euler(p.armRightEuler), Vector3.one));
            b.Add(leg, root * Matrix4x4.TRS(HipLeft - TorsoPivot + hips, Quaternion.Euler(p.legLeftEuler), Vector3.one));
            b.Add(leg, root * Matrix4x4.TRS(HipRight - TorsoPivot + hips, Quaternion.Euler(p.legRightEuler), Vector3.one));

            var bounds = b.ComputeBounds();
            b.Offset(new Vector3(0f, -bounds.min.y - sink, 0f));
            return b;
        }

        public enum FrozenPose
        {
            /// <summary>Сидит, обхватив колени, голова опущена.</summary>
            Sitting,
            /// <summary>Лежит на боку, поджав ноги.</summary>
            Lying,
            /// <summary>Упал лицом вперёд, одна рука тянется дальше по дороге.</summary>
            Reaching
        }

        public static Pose GetFrozenPose(FrozenPose kind)
        {
            switch (kind)
            {
                case FrozenPose.Lying:
                    return new Pose
                    {
                        rootEuler = new Vector3(0f, 0f, 86f),
                        hipHeight = HipHeight,
                        torsoEuler = new Vector3(18f, 0f, 0f),
                        headEuler = new Vector3(20f, 0f, -10f),
                        armLeftEuler = new Vector3(-70f, 0f, 10f),
                        armRightEuler = new Vector3(-55f, 0f, -5f),
                        legLeftEuler = new Vector3(-60f, 0f, 0f),
                        legRightEuler = new Vector3(-45f, 0f, 0f),
                    };
                case FrozenPose.Reaching:
                    return new Pose
                    {
                        rootEuler = new Vector3(78f, 0f, 0f),
                        hipHeight = HipHeight,
                        torsoEuler = new Vector3(6f, 0f, 0f),
                        headEuler = new Vector3(-35f, 15f, 0f),
                        armLeftEuler = new Vector3(-165f, 0f, -6f),  // вытянута вперёд, за голову
                        armRightEuler = new Vector3(-20f, 0f, 15f),
                        legLeftEuler = new Vector3(5f, 0f, 3f),
                        legRightEuler = new Vector3(-12f, 0f, -3f),
                    };
                default:
                    return new Pose
                    {
                        hipHeight = 0.13f,
                        torsoEuler = new Vector3(16f, 0f, 0f),
                        headEuler = new Vector3(30f, 0f, 8f),
                        armLeftEuler = new Vector3(-52f, 0f, 12f),   // руки на коленях
                        armRightEuler = new Vector3(-48f, 0f, -12f),
                        legLeftEuler = new Vector3(-86f, -8f, 0f),
                        legRightEuler = new Vector3(-82f, 8f, 0f),
                    };
            }
        }

        /// <summary>Замёрзший робот: поза + сугроб у основания. Рисуется материалом M_World_Frosted.</summary>
        public static MeshBuilder BuildFrozen(FrozenPose kind, int seed)
        {
            var b = BuildPosed(GetFrozenPose(kind));
            var bounds = b.ComputeBounds();
            var mound = Primitives.Icosphere(0.5f, 1, PaletteAsset.Get(PaletteColor.Snow));
            Deform.NoiseAlongNormal(mound, new Noise3(seed), 0.12f, 2.5f, 2);
            mound.Scale(new Vector3(Mathf.Max(0.9f, bounds.size.x + 0.5f), 0.45f, Mathf.Max(0.9f, bounds.size.z + 0.5f)));
            b.Add(mound.Translate(new Vector3(bounds.center.x, -0.06f, bounds.center.z)));
            return b;
        }

        /// <summary>Корпус. Начало координат — таз (низ корпуса).</summary>
        public static MeshBuilder BuildTorso()
        {
            Color body = PaletteAsset.Get(PaletteColor.RobotBody);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            var b = new MeshBuilder();

            b.Add(Primitives.Capsule(0.28f, 0.78f, 8, 3, body).Translate(new Vector3(0f, 0.39f, 0f)));
            // Нагрудная пластина и пояс.
            b.Add(Primitives.BeveledBox(new Vector3(0.3f, 0.26f, 0.06f), 0.02f, dark).Translate(new Vector3(0f, 0.47f, 0.26f)));
            b.Add(Primitives.Cylinder(0.29f, 0.29f, 0.05f, 8, dark).Translate(new Vector3(0f, 0.2f, 0f)));
            // Гнездо ключа на спине.
            b.Add(Primitives.Cylinder(0.06f, 0.06f, 0.04f, 6, dark).Rotate(new Vector3(-90f, 0f, 0f)).Translate(new Vector3(0f, 0.42f, -0.27f)));
            // Шарниры плеч.
            foreach (float sx in new[] { -1f, 1f })
                b.Add(Primitives.Icosphere(0.07f, 0, dark).Translate(new Vector3(sx * 0.3f, 0.52f, 0f)));
            return b;
        }

        /// <summary>Голова-фонарь без стекла. Начало координат — основание шеи.</summary>
        public static MeshBuilder BuildHead()
        {
            Color body = PaletteAsset.Get(PaletteColor.RobotBody);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            var b = new MeshBuilder();

            b.Add(Primitives.Cylinder(0.06f, 0.06f, 0.09f, 6, dark));                                   // шея
            b.Add(Primitives.Cylinder(0.21f, 0.2f, 0.06f, 8, dark).Translate(new Vector3(0f, 0.09f, 0f)));  // нижнее кольцо
            // Каркас фонаря — 4 стойки вокруг стекла.
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 0.185f, 0.25f, Mathf.Sin(a) * 0.185f);
                b.Add(Primitives.Box(new Vector3(0.035f, 0.22f, 0.035f), body).Translate(p));
            }
            b.Add(Primitives.Cylinder(0.22f, 0.21f, 0.04f, 8, body).Translate(new Vector3(0f, 0.36f, 0f)));   // верхнее кольцо
            b.Add(Primitives.Cylinder(0.2f, 0.03f, 0.12f, 8, dark).Translate(new Vector3(0f, 0.4f, 0f)));     // крышка
            var ring = Primitives.Torus(0.05f, 0.012f, 10, 4, dark).Rotate(new Vector3(90f, 0f, 0f)).Translate(new Vector3(0f, 0.56f, 0f));
            b.Add(ring);                                                                                       // ушко
            return b;
        }

        /// <summary>Светящееся стекло головы (отдельно — чтобы гасить у замёрзших и менять яркость).</summary>
        public static MeshBuilder BuildHeadGlass()
        {
            Color glow = PaletteAsset.Get(PaletteColor.WarmGlow);
            return new MeshBuilder().Add(Primitives.Cylinder(0.17f, 0.17f, 0.21f, 8, glow, Surface.Glow(1f)).Translate(new Vector3(0f, 0.14f, 0f)));
        }

        /// <summary>Заводной ключ-«бабочка». Начало — точка крепления, ось вращения — Z, ключ торчит в −Z.</summary>
        public static MeshBuilder BuildKey()
        {
            Color brass = PaletteAsset.Get(PaletteColor.WoodLight);
            var b = new MeshBuilder();
            b.Add(Primitives.Cylinder(0.02f, 0.02f, 0.14f, 5, brass).Rotate(new Vector3(-90f, 0f, 0f)));
            var wing = new[] { new Vector2(0f, -0.03f), new Vector2(0.12f, -0.07f), new Vector2(0.15f, 0f), new Vector2(0.12f, 0.07f), new Vector2(0f, 0.03f) };
            foreach (float sx in new[] { -1f, 1f })
            {
                var w = Primitives.ExtrudeConvex(wing, 0.025f, brass);
                if (sx < 0) w.Scale(new Vector3(-1f, 1f, 1f));
                b.Add(w.Translate(new Vector3(0f, 0f, -0.15f)));
            }
            return b;
        }

        /// <summary>Рука целиком (плечо, локоть, предплечье, кисть). Начало — плечо, висит вдоль −Y.</summary>
        public static MeshBuilder BuildArm()
        {
            Color body = PaletteAsset.Get(PaletteColor.RobotBody);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            var b = new MeshBuilder();
            b.Add(Primitives.Cylinder(0.05f, 0.045f, 0.24f, 6, dark).Translate(new Vector3(0f, -0.26f, 0f)));
            b.Add(Primitives.Icosphere(0.06f, 0, body).Translate(new Vector3(0f, -0.28f, 0f)));
            b.Add(Primitives.Cylinder(0.055f, 0.05f, 0.22f, 6, body).Translate(new Vector3(0f, -0.52f, 0f)));
            b.Add(Primitives.BeveledBox(new Vector3(0.09f, 0.11f, 0.07f), 0.015f, dark).Translate(new Vector3(0f, -0.58f, 0f)));
            return b;
        }

        /// <summary>Нога. Начало — тазобедренный шарнир, вниз по −Y, ступня вперёд (+Z).</summary>
        public static MeshBuilder BuildLeg()
        {
            Color body = PaletteAsset.Get(PaletteColor.RobotBody);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            var b = new MeshBuilder();
            b.Add(Primitives.Icosphere(0.065f, 0, dark));
            b.Add(Primitives.Cylinder(0.065f, 0.06f, 0.27f, 6, dark).Translate(new Vector3(0f, -0.3f, 0f)));
            b.Add(Primitives.BeveledBox(new Vector3(0.14f, 0.07f, 0.24f), 0.02f, body).Translate(new Vector3(0f, -HipHeight + 0.035f, 0.04f)));
            return b;
        }

        /// <summary>
        /// Правая рука для вида от 1-го лица: предплечье + кисть с пальцами, вытянута вдоль +Z.
        /// Начало — локоть. Левая — тот же меш с масштабом (−1, 1, 1).
        /// </summary>
        public static MeshBuilder BuildFirstPersonArm()
        {
            Color body = PaletteAsset.Get(PaletteColor.RobotBody);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            var b = new MeshBuilder();
            b.Add(Primitives.Cylinder(0.05f, 0.045f, 0.3f, 7, body).Rotate(new Vector3(90f, 0f, 0f)));
            b.Add(Primitives.Cylinder(0.052f, 0.052f, 0.03f, 7, dark).Rotate(new Vector3(90f, 0f, 0f)).Translate(new Vector3(0f, 0f, 0.22f)));
            b.Add(Primitives.BeveledBox(new Vector3(0.1f, 0.045f, 0.09f), 0.012f, dark).Translate(new Vector3(0f, 0f, 0.34f)));
            for (int i = 0; i < 3; i++)
                b.Add(Primitives.Box(new Vector3(0.022f, 0.022f, 0.07f), body).Translate(new Vector3(-0.03f + i * 0.03f, -0.005f, 0.415f)));
            b.Add(Primitives.Box(new Vector3(0.022f, 0.022f, 0.05f), body).Rotate(new Vector3(0f, -40f, 0f)).Translate(new Vector3(-0.06f, 0f, 0.35f)));
            return b;
        }
    }
}
