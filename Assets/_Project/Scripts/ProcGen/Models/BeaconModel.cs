using UnityEngine;

namespace LastRide.ProcGen.Models
{
    /// <summary>
    /// Великий Маяк с электростанцией и лифтом-гнездом (ТЗ 3, 5, 8.1).
    /// Ступенчатая восьмигранная башня ~46 м (видна издалека тонким силуэтом), фонарь наверху.
    /// Огни — отдельные меши по рядам (<see cref="LightRows"/>), чтобы в финале зажигать их снизу вверх.
    /// Лифт: гнездо у подножия (+Z), рельсы по фасаду, кабинка — отдельный меш, едет вверх.
    /// Начало координат — на земле в центре башни; фасад с лифтом смотрит в +Z.
    /// </summary>
    public static class BeaconModel
    {
        public const int Sides = 8;
        public const int LightRows = 8;

        private static readonly float[] TierRadius = { 6.0f, 4.8f, 3.8f, 2.9f };
        private static readonly float[] TierHeight = { 2.0f, 13f, 11f, 9f };
        private static float TierBase(int i)
        {
            float y = 0f;
            for (int k = 0; k < i; k++) y += TierHeight[k];
            return y;
        }

        public static float TowerTop => TierBase(TierRadius.Length);
        public static readonly float LanternHeight = 4f;
        public static float TotalHeight => TowerTop + LanternHeight + 4f;

        /// <summary>Куда игрок вставляет Искру (центр чаши гнезда).</summary>
        public static readonly Vector3 SocketPoint = new Vector3(0f, 1.25f, 6.9f);
        /// <summary>Нижняя и верхняя точки хода кабинки лифта (по фасаду).</summary>
        public static readonly Vector3 LiftBottom = new Vector3(0f, 1.0f, 6.9f);
        public static Vector3 LiftTop => new Vector3(0f, TowerTop - 1f, TierRadius[3] + 0.9f);
        /// <summary>Центр фонаря наверху (куда в итоге «приезжает» Искра).</summary>
        public static Vector3 LanternCenter => new Vector3(0f, TowerTop + LanternHeight * 0.5f, 0f);

        /// <summary>Высота ряда огней i (0 — нижний).</summary>
        public static float LightRowHeight(int i) => Mathf.Lerp(3.5f, TowerTop - 2.5f, i / (float)(LightRows - 1));

        /// <summary>Радиус (до вершин восьмигранника) башни на высоте y — с учётом сужения ступени к верху.</summary>
        public static float RadiusAt(float y)
        {
            for (int i = TierRadius.Length - 1; i >= 0; i--)
            {
                if (y < TierBase(i)) continue;
                float t = Mathf.Clamp01((y - TierBase(i)) / TierHeight[i]);
                return Mathf.Lerp(TierRadius[i], TierRadius[i] * 0.94f, t);
            }
            return TierRadius[0];
        }

        public static MeshBuilder BuildTower()
        {
            Color concrete = PaletteAsset.Get(PaletteColor.Concrete);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color rock = PaletteAsset.Get(PaletteColor.RockDark);
            var s = new Shape();

            // Ступени башни (восьмигранные). Небольшой карниз-выступ на каждой ступени.
            for (int i = 0; i < TierRadius.Length; i++)
            {
                float y = TierBase(i);
                var tier = Primitives.Cylinder(TierRadius[i], TierRadius[i] * 0.94f, TierHeight[i], Sides, i == 0 ? rock : concrete);
                tier.Rotate(new Vector3(0f, 22.5f, 0f));
                s.Append(tier, Matrix4x4.Translate(new Vector3(0f, y, 0f)));
                if (i > 0)
                {
                    var ledge = Primitives.Cylinder(TierRadius[i] + 0.35f, TierRadius[i] + 0.35f, 0.4f, Sides, dark).Rotate(new Vector3(0f, 22.5f, 0f));
                    s.Append(ledge, Matrix4x4.Translate(new Vector3(0f, y + TierHeight[i] - 0.4f, 0f)));
                }
            }

            // Галерея и фонарь наверху: площадка, стойки, крыша-шпиль (стекло фонаря — отдельный меш).
            float top = TowerTop;
            s.Append(Primitives.Cylinder(3.6f, 3.6f, 0.4f, Sides, dark).Rotate(new Vector3(0f, 22.5f, 0f)), Matrix4x4.Translate(new Vector3(0f, top, 0f)));
            for (int i = 0; i < Sides; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                s.Append(Primitives.Box(new Vector3(0.25f, LanternHeight, 0.25f), dark), Matrix4x4.Translate(new Vector3(Mathf.Cos(a) * 2.2f, top + 0.4f + LanternHeight * 0.5f, Mathf.Sin(a) * 2.2f)));
            }
            s.Append(Primitives.Cylinder(2.8f, 0.3f, 3.6f, Sides, dark).Rotate(new Vector3(0f, 22.5f, 0f)), Matrix4x4.Translate(new Vector3(0f, top + 0.4f + LanternHeight, 0f)));

            // Рельсы лифта по фасаду (+Z): две полосы от земли до верха, по ступеням.
            for (int i = 0; i < TierRadius.Length; i++)
            {
                float y = TierBase(i);
                float z = TierRadius[i] + 0.25f;
                foreach (float x in new[] { -0.6f, 0.6f })
                    s.Append(Primitives.Box(new Vector3(0.12f, TierHeight[i], 0.18f), dark), Matrix4x4.Translate(new Vector3(x, y + TierHeight[i] * 0.5f, z)));
                // Перемычка на переходе ступеней.
                if (i > 0)
                    s.Append(Primitives.Box(new Vector3(1.4f, 0.2f, TierRadius[i - 1] - TierRadius[i] + 0.4f), dark),
                        Matrix4x4.Translate(new Vector3(0f, y, (TierRadius[i - 1] + TierRadius[i]) * 0.5f + 0.25f)));
            }

            // Гнездо-лифт у подножия: постамент, чаша (Искра кладётся сюда), рамка.
            var pedestal = Primitives.BeveledBox(new Vector3(1.4f, 1.0f, 1.0f), 0.08f, concrete);
            s.Append(pedestal, Matrix4x4.Translate(new Vector3(0f, 0.5f, SocketPoint.z)));
            s.Append(Primitives.Cylinder(0.32f, 0.42f, 0.22f, Sides, dark, Surface.NoSnow), Matrix4x4.Translate(new Vector3(0f, 1.0f, SocketPoint.z)));
            foreach (float x in new[] { -0.85f, 0.85f })
                s.Append(Primitives.Box(new Vector3(0.14f, 3.2f, 0.14f), dark), Matrix4x4.Translate(new Vector3(x, 1.6f, SocketPoint.z)));
            s.Append(Primitives.Box(new Vector3(1.85f, 0.16f, 0.3f), dark), Matrix4x4.Translate(new Vector3(0f, 3.25f, SocketPoint.z)));

            return new MeshBuilder().Add(s);
        }

        /// <summary>Ряд огней (окошки по кругу башни) на высоте ряда <paramref name="row"/>.</summary>
        public static MeshBuilder BuildLightRow(int row)
        {
            Color light = PaletteAsset.Get(PaletteColor.BeaconLight);
            float y = LightRowHeight(row);
            // Расстояние до центра грани восьмигранника + чуть наружу, чтобы окошко не тонуло в стене.
            float faceDistance = RadiusAt(y) * Mathf.Cos(22.5f * Mathf.Deg2Rad) + 0.03f;
            var b = new MeshBuilder();
            for (int i = 0; i < Sides; i++)
            {
                // Башня повёрнута на 22.5°, поэтому центры граней — на 0°, 45°, 90°, …
                float a = i * 45f;
                var rot = Quaternion.Euler(0f, -a, 0f);
                var pos = rot * new Vector3(faceDistance, 0f, 0f) + Vector3.up * y;
                b.Add(Primitives.Box(new Vector3(0.12f, 0.7f, 0.9f), light, Surface.Glow(1f)), Matrix4x4.TRS(pos, rot, Vector3.one));
            }
            return b;
        }

        /// <summary>Стекло фонаря наверху (светится в финале).</summary>
        public static MeshBuilder BuildLantern()
        {
            Color light = PaletteAsset.Get(PaletteColor.BeaconLight);
            var glass = Primitives.Cylinder(2.1f, 2.1f, LanternHeight, Sides, light, Surface.Glow(1f)).Rotate(new Vector3(0f, 22.5f, 0f));
            return new MeshBuilder().Add(glass.Translate(new Vector3(0f, TowerTop + 0.4f, 0f)));
        }

        /// <summary>Кабинка лифта: открытая клетка с полом. Начало — пол кабинки по центру.</summary>
        public static MeshBuilder BuildLiftCage()
        {
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color brass = PaletteAsset.Get(PaletteColor.RobotBody);
            var s = new Shape();
            s.Append(Primitives.Box(new Vector3(1.1f, 0.08f, 0.8f), dark, Surface.NoSnow));
            foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { -0.35f, 0.35f })
                s.Append(Primitives.Box(new Vector3(0.05f, 0.9f, 0.05f), brass, Surface.NoSnow), Matrix4x4.Translate(new Vector3(x, 0.45f, z)));
            s.Append(Primitives.Box(new Vector3(1.1f, 0.06f, 0.8f), dark, Surface.NoSnow), Matrix4x4.Translate(new Vector3(0f, 0.9f, 0f)));
            return new MeshBuilder().Add(s);
        }

        // ---- Электростанция ----
        public static readonly Vector3 StationOffset = new Vector3(-16f, 0f, -2f);

        /// <summary>
        /// Электростанция рядом с Маяком (смещение <see cref="StationOffset"/> от центра башни задаётся при расстановке).
        /// Зал с плоской крышей, три цистерны-генератора, труба, кабели к башне. Окна — отдельный меш.
        /// Начало координат — центр зала на земле.
        /// </summary>
        public static MeshBuilder BuildStation()
        {
            Color concrete = PaletteAsset.Get(PaletteColor.Concrete);
            Color dark = PaletteAsset.Get(PaletteColor.MetalDark);
            Color rust = PaletteAsset.Get(PaletteColor.Rust);
            var s = new Shape();

            s.Append(Primitives.BeveledBox(new Vector3(12f, 6f, 8f), 0.15f, concrete), Matrix4x4.Translate(new Vector3(0f, 3f, 0f)));
            s.Append(Primitives.Box(new Vector3(12.6f, 0.4f, 8.6f), dark), Matrix4x4.Translate(new Vector3(0f, 6.2f, 0f)));
            // Ворота.
            s.Append(Primitives.Box(new Vector3(3f, 3.6f, 0.1f), dark, Surface.NoSnow), Matrix4x4.Translate(new Vector3(-2.5f, 1.8f, 4.02f)));
            // Цистерны-генераторы снаружи (вдоль задней стены).
            for (int i = 0; i < 3; i++)
            {
                var tank = Primitives.Cylinder(1.4f, 1.4f, 5f, 10, i == 1 ? rust : dark);
                s.Append(tank, Matrix4x4.Translate(new Vector3(-4f + i * 4f, 0f, -6f)));
                s.Append(Primitives.Cylinder(1.45f, 0.4f, 0.8f, 10, dark), Matrix4x4.Translate(new Vector3(-4f + i * 4f, 5f, -6f)));
                s.Append(Primitives.Box(new Vector3(0.4f, 0.4f, 1.6f), dark), Matrix4x4.Translate(new Vector3(-4f + i * 4f, 3.5f, -4.4f)));
            }
            // Труба.
            s.Append(Primitives.Cylinder(0.8f, 0.6f, 14f, 8, concrete), Matrix4x4.Translate(new Vector3(4.5f, 0f, 1.5f)));
            s.Append(Primitives.Cylinder(0.75f, 0.75f, 0.4f, 8, dark), Matrix4x4.Translate(new Vector3(4.5f, 13.6f, 1.5f)));
            // Кабельный короб к башне (в сторону +X, к Маяку), на опорах.
            float len = -StationOffset.x - 6f - 5f;
            s.Append(Primitives.Box(new Vector3(len, 0.5f, 0.5f), dark), Matrix4x4.Translate(new Vector3(6f + len * 0.5f, 4.5f, -StationOffset.z)));
            for (int i = 0; i <= 2; i++)
                s.Append(Primitives.Box(new Vector3(0.3f, 4.5f, 0.3f), dark), Matrix4x4.Translate(new Vector3(6f + len * i / 2f, 2.25f, -StationOffset.z)));
            return new MeshBuilder().Add(s);
        }

        /// <summary>Окна электростанции (загораются, когда станция начинает гудеть).</summary>
        public static MeshBuilder BuildStationWindows()
        {
            Color light = PaletteAsset.Get(PaletteColor.WarmGlow);
            var b = new MeshBuilder();
            for (int i = 0; i < 5; i++)
                b.Add(Primitives.Box(new Vector3(1.2f, 1.0f, 0.06f), light, Surface.Glow(1f)), Matrix4x4.Translate(new Vector3(-4.8f + i * 2.4f, 4.4f, 4.04f)));
            for (int i = 0; i < 3; i++)
                b.Add(Primitives.Box(new Vector3(0.06f, 1.0f, 1.2f), light, Surface.Glow(1f)), Matrix4x4.Translate(new Vector3(6.04f, 4.4f, -2.4f + i * 2.4f)));
            return b;
        }
    }
}
