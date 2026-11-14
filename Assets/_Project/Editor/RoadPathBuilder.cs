using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LastRide.Level;
using UnityEditor;
using UnityEngine;

namespace LastRide.Editor
{
    /// <summary>
    /// Собирает ось пути уровня (ТЗ 5) детерминированно из таблицы сегментов: прямые и дуги (радиус, угол) с уклоном и шириной.
    /// Кривизна, уклон и ширина между сегментами плавно перетекают (smoothstep), поэтому руль и «клевание» фургона мягкие.
    /// Результат — <c>Data/Resources/RoadPath.asset</c> (точки с шагом 1 м по длине дуги, ширины, маркеры).
    /// Меню: <b>Build Road Path</b> (сборка + проверка + PNG-превью), <b>Validate Road Path</b> (только проверка).
    /// Требования: уклон ≤ 14° везде, ровно один короткий участок ≈ 20° с ровным разгоном перед ним (середина акта 2),
    /// радиус не меньше 18 м, соседние витки серпантина не ближе 40 м друг от друга.
    /// </summary>
    internal static class RoadPathBuilder
    {
        private const string AssetPath = "Assets/_Project/Data/Resources/RoadPath.asset";
        private const string PreviewDir = "Assets/Screenshots/";

        private const float H = 0.25f;            // шаг интегрирования по горизонтали, м
        private const float EaseCurvature = 20f;   // длина перетекания кривизны между сегментами, м
        private const float EaseSlope = 14f;       // уклона
        private const float EaseWidth = 20f;       // ширины

        // Критерии проверки (ТЗ 4.5, 5 и требования к уклонам).
        private const float MaxRoadSlope = 14.5f;      // выше — считается «крутым» участком
        private const float RampPeakMin = 19f, RampPeakMax = 21f;
        private const float MinRoadRadius = 18f;
        private const float MinTrailRadius = 14f;
        private const float MinLegSeparation = 40f;

        private struct Seg
        {
            public float len;       // горизонтальная длина, м
            public float curvature; // 1/м, + вправо
            public float slope;     // градусов
            public float width;     // м
            public string marker;
            public bool markerAtCenter;
        }

        // ------------------------------------------------------------------ таблица сегментов (дизайн пути)

        private static Seg S(float len, float slope, float width = 7f, string marker = null, bool center = false) =>
            new Seg { len = len, curvature = 0f, slope = slope, width = width, marker = marker, markerAtCenter = center };

        /// <summary>Дуга: радиус, угол (+ вправо, − влево), длина = R·|угол|.</summary>
        private static Seg A(float radius, float angleDeg, float slope, float width = 7f, string marker = null, bool center = false) =>
            new Seg { len = radius * Mathf.Abs(angleDeg) * Mathf.Deg2Rad, curvature = Mathf.Sign(angleDeg) / radius, slope = slope, width = width, marker = marker, markerAtCenter = center };

        private static List<Seg> Table()
        {
            var t = new List<Seg>();

            // ======== АКТ 1: начало пути (~1200 м). Мастерская, широкие пологие повороты, один крутой поворот, две стоянки. ========
            t.Add(S(120, 0f, 9f, "Start"));                    // гараж / мастерская: ровная площадка
            t.Add(S(90, 2f));
            t.Add(A(90, +35, 3f));
            t.Add(S(110, 4f));
            t.Add(A(80, -40, 4f));
            t.Add(S(70, 5f));
            t.Add(S(60, 0.8f, 9f, "Stop_A1_1", true));         // стоянка 1: сарай с дровами
            t.Add(S(90, 5f));
            t.Add(A(28, +105, 6f, 8f, "SharpTurn_A1", true));  // единственный крутой поворот акта 1
            t.Add(S(90, 6f));
            t.Add(A(70, -30, 6f));
            t.Add(S(80, 6f));
            t.Add(S(60, 0.8f, 9f, "Stop_A1_2", true));         // стоянка 2
            t.Add(S(80, 5f));
            t.Add(A(100, +25, 5f));
            t.Add(S(70, 5f));
            t.Add(S(40, 5f));

            // ======== АКТ 2: метель (~2000 м). Серпантин, развалины на ровных площадках, один участок 20°. ========
            t.Add(A(60, -50, 7f, 7f, "Act2Start"));
            t.Add(S(200, 8f));
            t.Add(A(30, +170, 7f, 9f, "Hairpin_1", true));
            t.Add(S(90, 8f));
            t.Add(S(60, 1f, 9f, "Stop_A2_1", true));
            t.Add(S(110, 8f));
            t.Add(A(30, -175, 7f, 9f, "Hairpin_2", true));
            t.Add(S(120, 9f));
            t.Add(S(60, 1f, 9f, "Stop_A2_2", true));
            t.Add(S(90, 9f));
            t.Add(A(70, +40, 8f));
            t.Add(S(130, 3f, 7f, "RampRunUp", true));         // ровный разгонный участок перед рампой
            t.Add(S(40, 20f, 7f, "Ramp20", true));            // единственный участок 20° — только с разгона
            t.Add(S(120, 6f));
            t.Add(S(60, 1f, 9f, "Stop_A2_3", true));
            t.Add(A(30, -170, 8f, 9f, "Hairpin_3", true));   // влево: серпантин поднимается к северу, а не разворачивается на тропу
            t.Add(S(130, 10f));
            t.Add(S(60, 1f, 9f, "Stop_A2_4", true));
            t.Add(A(60, -20, 9f));
            t.Add(S(100, 10f));
            t.Add(A(30, +170, 9f, 9f, "Hairpin_4", true));
            t.Add(S(60, 10f));
            t.Add(S(90, 10f, 7f, "Breakdown", true));        // подъём, на котором ломается ходовая (акт 3)

            // ======== АКТ 3: пешая тропа (~450 м). Завал на месте, где кончается дорога; тропа узкая; 3 укрытия. ========
            t.Add(S(50, 9f, 3f, "Act3Start"));                // скалы-завал: дальше фургон не едет
            t.Add(A(40, -50, 10f, 3f));
            t.Add(S(30, 11f, 3f));
            t.Add(S(40, 3f, 4f, "Shelter_1", true));
            t.Add(A(35, +60, 10f, 3f));
            t.Add(S(30, 12f, 3f));
            t.Add(A(40, -45, 11f, 3f));
            t.Add(S(40, 3f, 4f, "Shelter_2", true));
            t.Add(S(30, 10f, 3f));
            t.Add(A(30, +50, 10f, 3f));
            t.Add(S(40, 3f, 4f, "Shelter_3", true));
            t.Add(S(30, 9f, 3f));
            t.Add(S(50, 0f, 12f, "Summit", true));            // вершина: площадка Маяка, лифт-гнездо
            return t;
        }

        // ------------------------------------------------------------------ сборка

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Build Road Path")]
        public static void Build()
        {
            var table = Table();
            var starts = new float[table.Count];
            float total = 0f;
            for (int i = 0; i < table.Count; i++) { starts[i] = total; total += table[i].len; }

            int n = Mathf.CeilToInt(total / H);
            var fp = new Vector3[n + 1];
            var farc = new float[n + 1];
            var fw = new float[n + 1];
            float psi = 0f, x = 0f, y = 0f, z = 0f, arc = 0f;
            for (int k = 0; k <= n; k++)
            {
                float s = k * H;
                float kappa = Blend(table, starts, s, g => g.curvature, EaseCurvature);
                float slopeDeg = Blend(table, starts, s, g => g.slope, EaseSlope);
                float width = Blend(table, starts, s, g => g.width, EaseWidth);
                fp[k] = new Vector3(x, y, z);
                farc[k] = arc;
                fw[k] = width;

                float mid = psi + 0.5f * kappa * H;
                x += Mathf.Sin(mid) * H;
                z += Mathf.Cos(mid) * H;
                psi += kappa * H;
                float tan = Mathf.Tan(slopeDeg * Mathf.Deg2Rad);
                y += tan * H;
                arc += H / Mathf.Cos(slopeDeg * Mathf.Deg2Rad);
            }

            // Перепробуем по длине дуги с шагом 1 м.
            float totalArc = farc[n];
            int count = Mathf.FloorToInt(totalArc) + 1;
            var pts = new Vector3[count];
            var widths = new float[count];
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float d = i;
                while (j < n - 1 && farc[j + 1] < d) j++;
                float seg = Mathf.Max(1e-5f, farc[j + 1] - farc[j]);
                float u = Mathf.Clamp01((d - farc[j]) / seg);
                pts[i] = Vector3.Lerp(fp[j], fp[j + 1], u);
                widths[i] = Mathf.Lerp(fw[j], fw[j + 1], u);
            }

            // Маркеры: горизонтальное положение → расстояние по дуге.
            var markers = new List<RoadMarker>();
            for (int i = 0; i < table.Count; i++)
            {
                if (string.IsNullOrEmpty(table[i].marker)) continue;
                float sh = starts[i] + (table[i].markerAtCenter ? table[i].len * 0.5f : 0f);
                int idx = Mathf.Clamp(Mathf.RoundToInt(sh / H), 0, n);
                markers.Add(new RoadMarker { id = table[i].marker, distance = farc[idx] });
            }
            markers.Add(new RoadMarker { id = "End", distance = count - 1 });

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var asset = AssetDatabase.LoadAssetAtPath<RoadPathData>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RoadPathData>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.step = 1f;
            asset.points = pts;
            asset.widths = widths;
            asset.markers = markers.ToArray();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            RoadPath.ResetCache();

            var report = Validate(asset, out bool ok);
            Directory.CreateDirectory(PreviewDir);
            File.WriteAllText(PreviewDir + "road_report.txt", report);
            Debug.Log("[The Last Ride] Road path built: " + AssetPath + "\n" + report);
            ExportPreview(asset);
            if (!ok) Debug.LogWarning("[The Last Ride] Road path: есть нарушения критериев — см. отчёт выше.");
        }

        [MenuItem(EditorAssemblyMarker.MenuRoot + "Level/Validate Road Path")]
        public static void ValidateMenu()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoadPathData>(AssetPath);
            if (asset == null) { Debug.LogError("RoadPath.asset не собран: The Last Ride/Level/Build Road Path"); return; }
            var report = Validate(asset, out bool ok);
            Directory.CreateDirectory(PreviewDir);
            File.WriteAllText(PreviewDir + "road_report.txt", report);
            if (ok) Debug.Log("[The Last Ride] Road path OK\n" + report);
            else Debug.LogWarning("[The Last Ride] Road path: НАРУШЕНИЯ\n" + report);
        }

        /// <summary>Значение параметра сегмента с плавным перетеканием на границах.</summary>
        private static float Blend(List<Seg> table, float[] starts, float s, Func<Seg, float> get, float ease)
        {
            int e = table.Count - 1;
            for (int i = 0; i < table.Count; i++) if (starts[i] > s) { e = i - 1; break; }
            e = Mathf.Max(0, e);

            float half = ease * 0.5f;
            // Граница с предыдущим сегментом.
            if (e > 0)
            {
                float b = starts[e];
                if (s < b + half)
                {
                    float u = Mathf.Clamp01((s - (b - half)) / ease);
                    return Mathf.Lerp(get(table[e - 1]), get(table[e]), u * u * (3f - 2f * u));
                }
            }
            // Граница со следующим.
            if (e + 1 < table.Count)
            {
                float b = starts[e + 1];
                if (s > b - half)
                {
                    float u = Mathf.Clamp01((s - (b - half)) / ease);
                    return Mathf.Lerp(get(table[e]), get(table[e + 1]), u * u * (3f - 2f * u));
                }
            }
            return get(table[e]);
        }

        // ------------------------------------------------------------------ проверка

        private static string Validate(RoadPathData d, out bool ok)
        {
            ok = true;
            var p = d.points;
            int n = p.Length;
            var sb = new StringBuilder();

            float M(string id) { foreach (var m in d.markers) if (m.id == id) return m.distance; return -1f; }
            float a2 = M("Act2Start"), a3 = M("Act3Start"), end = n - 1;
            sb.AppendLine($"Длина оси {end:0} м; высота {p[0].y:0.0} → {p[n - 1].y:0.0} (набор {p[n - 1].y - p[0].y:0} м)");
            sb.AppendLine($"Акт 1: {a2:0} м (цель ≈1200), акт 2: {a3 - a2:0} м (≈2000), акт 3 (тропа): {end - a3:0} м (≈450–500)");

            // Уклоны.
            var slope = new float[n - 1];
            float maxOther = 0f;
            var spans = new List<(float from, float to, float peak)>();
            bool inSpan = false; float spanFrom = 0f, spanPeak = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                var dv = p[i + 1] - p[i];
                slope[i] = Mathf.Asin(Mathf.Clamp(dv.y / dv.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
                if (slope[i] > MaxRoadSlope)
                {
                    if (!inSpan) { inSpan = true; spanFrom = i; spanPeak = slope[i]; }
                    spanPeak = Mathf.Max(spanPeak, slope[i]);
                }
                else
                {
                    if (inSpan) { spans.Add((spanFrom, i, spanPeak)); inSpan = false; }
                    maxOther = Mathf.Max(maxOther, slope[i]);
                }
            }
            if (inSpan) spans.Add((spanFrom, n - 1, spanPeak));

            sb.AppendLine($"Макс. уклон вне крутого участка: {maxOther:0.0}° (допуск {MaxRoadSlope}°)");
            sb.AppendLine($"Крутых участков (> {MaxRoadSlope}°): {spans.Count}");
            foreach (var sp in spans) sb.AppendLine($"  {sp.from:0}–{sp.to:0} м (длина {sp.to - sp.from:0} м), пик {sp.peak:0.0}°");
            if (spans.Count != 1) { ok = false; sb.AppendLine("  НАРУШЕНИЕ: должен быть ровно один участок ≈20°"); }
            else if (spans[0].peak < RampPeakMin || spans[0].peak > RampPeakMax) { ok = false; sb.AppendLine("  НАРУШЕНИЕ: пик рампы вне 19–21°"); }
            else
            {
                float rampStart = spans[0].from;
                // Разгон: пропускаем зону плавного перехода к рампе, дальше считаем ровный участок (уклон < 4°).
                int ri = (int)rampStart - 1;
                while (ri > 0 && slope[ri] >= 4f) ri--;
                float runUp = 0f;
                for (; ri >= 0 && slope[ri] < 4f; ri--) runUp += 1f;
                sb.AppendLine($"  Разгонный участок перед рампой (уклон < 4°): {runUp:0} м (нужно ≥ 60 м)");
                if (runUp < 60f) { ok = false; sb.AppendLine("  НАРУШЕНИЕ: разгон короче 60 м"); }
                float ramp = M("Ramp20");
                if (ramp < a2 + 0.5f * (a3 - a2) - 600f || ramp > a2 + (a3 - a2) * 0.8f)
                    sb.AppendLine($"  Заметка: рампа на {ramp:0} м — вне диапазона 'середина акта 2'");
            }

            // Радиусы (по повороту направления на окне ±3 м).
            float minR = float.MaxValue, minRTrail = float.MaxValue;
            float atR = 0f, atRTrail = 0f;
            for (int i = 3; i < n - 3; i++)
            {
                float h0 = Mathf.Atan2(p[i].x - p[i - 3].x, p[i].z - p[i - 3].z);
                float h1 = Mathf.Atan2(p[i + 3].x - p[i].x, p[i + 3].z - p[i].z);
                float dh = Mathf.Abs(Mathf.DeltaAngle(h0 * Mathf.Rad2Deg, h1 * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                // Хорды по 3 м: поворот между серединами хорд на расстоянии 3 м, поэтому R = 3 / Δугол.
                float r = dh < 1e-4f ? float.MaxValue : 3f / dh;
                if (i < a3) { if (r < minR) { minR = r; atR = i; } }
                else if (r < minRTrail) { minRTrail = r; atRTrail = i; }
            }
            sb.AppendLine($"Мин. радиус дороги: {minR:0.0} м на {atR:0} м (норма ≥ {MinRoadRadius}); тропы: {minRTrail:0.0} м на {atRTrail:0} м (≥ {MinTrailRadius})");
            if (minR < MinRoadRadius - 0.5f || minRTrail < MinTrailRadius - 0.5f) { ok = false; sb.AppendLine("  НАРУШЕНИЕ: радиус"); }

            // Расстояние между витками (горизонталь), только для точек, далёких по длине пути.
            float minSep = float.MaxValue; int sepI = 0, sepJ = 0;
            for (int i = 0; i < n; i += 2)
            for (int j = i + 150; j < n; j += 2)
            {
                float dx = p[i].x - p[j].x, dz = p[i].z - p[j].z;
                float sq = dx * dx + dz * dz;
                if (sq < minSep) { minSep = sq; sepI = i; sepJ = j; }
            }
            minSep = Mathf.Sqrt(minSep);
            sb.AppendLine($"Мин. расстояние между далёкими участками пути: {minSep:0.0} м (между {sepI} и {sepJ} м; норма ≥ {MinLegSeparation})");
            if (minSep < MinLegSeparation) { ok = false; sb.AppendLine("  НАРУШЕНИЕ: витки слишком близко"); }

            // Равномерность шага и ширины.
            float stepErr = 0f;
            for (int i = 0; i < n - 1; i++) stepErr = Mathf.Max(stepErr, Mathf.Abs((p[i + 1] - p[i]).magnitude - 1f));
            float wMin = float.MaxValue, wMax = 0f;
            foreach (var w in d.widths) { wMin = Mathf.Min(wMin, w); wMax = Mathf.Max(wMax, w); }
            sb.AppendLine($"Шаг точек: ошибка до {stepErr * 1000f:0} мм; ширина {wMin:0.0}–{wMax:0.0} м");

            sb.Append("Маркеры: ");
            foreach (var m in d.markers) sb.Append($"{m.id}={m.distance:0}  ");
            sb.AppendLine();
            sb.Append(ok ? "РЕЗУЛЬТАТ: все критерии выполнены" : "РЕЗУЛЬТАТ: есть нарушения");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ PNG-превью (вид сверху и профиль высоты)

        private static void ExportPreview(RoadPathData d)
        {
            Directory.CreateDirectory(PreviewDir);
            var p = d.points;
            int n = p.Length;
            float a2 = 0f, a3 = 0f;
            foreach (var m in d.markers) { if (m.id == "Act2Start") a2 = m.distance; if (m.id == "Act3Start") a3 = m.distance; }

            Color Col(int i, float slope)
            {
                if (slope > MaxRoadSlope) return new Color(1f, 0.15f, 0.15f);
                if (i < a2) return new Color(0.35f, 0.85f, 0.45f);
                if (i < a3) return new Color(1f, 0.65f, 0.2f);
                return new Color(0.3f, 0.8f, 1f);
            }
            float SlopeAt(int i) { var dv = p[Mathf.Min(n - 1, i + 1)] - p[Mathf.Max(0, i - 1)]; return Mathf.Asin(Mathf.Clamp(dv.y / dv.magnitude, -1f, 1f)) * Mathf.Rad2Deg; }

            // Вид сверху.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in p) { minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z); }
            const int size = 1400, margin = 60;
            float scale = Mathf.Min((size - 2 * margin) / (maxX - minX), (size - 2 * margin) / (maxZ - minZ));
            var top = NewTexture(size, size, new Color(0.08f, 0.09f, 0.12f));
            Vector2Int Px(Vector3 v) => new Vector2Int(margin + Mathf.RoundToInt((v.x - minX) * scale), margin + Mathf.RoundToInt((v.z - minZ) * scale));
            for (int i = 0; i < n; i++) Disc(top, Px(p[i]), 3, Col(i, SlopeAt(i)));
            foreach (var m in d.markers)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt(m.distance), 0, n - 1);
                Color c; int r;
                if (m.id.StartsWith("Stop")) { c = Color.yellow; r = 7; }
                else if (m.id.StartsWith("Hairpin")) { c = new Color(1f, 0.3f, 1f); r = 7; }
                else if (m.id.StartsWith("Shelter")) { c = Color.cyan; r = 6; }
                else if (m.id == "Ramp20") { c = Color.red; r = 9; }
                else if (m.id == "SharpTurn_A1") { c = new Color(0.4f, 0.5f, 1f); r = 8; }
                else { c = Color.white; r = 9; }
                Disc(top, Px(p[idx]), r, c);
            }
            top.Apply();
            File.WriteAllBytes(PreviewDir + "road_top.png", top.EncodeToPNG());

            // Профиль высоты.
            const int pw = 1800, ph = 520;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in p) { minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }
            var prof = NewTexture(pw, ph, new Color(0.08f, 0.09f, 0.12f));
            for (int i = 0; i < n; i++)
            {
                int px = 40 + Mathf.RoundToInt(i / (float)(n - 1) * (pw - 80));
                int py = 30 + Mathf.RoundToInt((p[i].y - minY) / Mathf.Max(1f, maxY - minY) * (ph - 60));
                Disc(prof, new Vector2Int(px, py), 2, Col(i, SlopeAt(i)));
            }
            foreach (var m in d.markers)
            {
                if (!(m.id == "Ramp20" || m.id == "Act2Start" || m.id == "Act3Start" || m.id == "Summit")) continue;
                int idx = Mathf.Clamp(Mathf.RoundToInt(m.distance), 0, n - 1);
                int px = 40 + Mathf.RoundToInt(idx / (float)(n - 1) * (pw - 80));
                for (int y = 0; y < ph; y += 4) prof.SetPixel(px, y, Color.white);
            }
            prof.Apply();
            File.WriteAllBytes(PreviewDir + "road_profile.png", prof.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(top);
            UnityEngine.Object.DestroyImmediate(prof);
            AssetDatabase.Refresh();
            Debug.Log("[The Last Ride] Road preview: " + PreviewDir + "road_top.png, road_profile.png");
        }

        private static Texture2D NewTexture(int w, int h, Color fill)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            t.SetPixels(px);
            return t;
        }

        private static void Disc(Texture2D t, Vector2Int c, int r, Color col)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r * r) continue;
                int x = c.x + dx, y = c.y + dy;
                if (x < 0 || y < 0 || x >= t.width || y >= t.height) continue;
                t.SetPixel(x, y, col);
            }
        }
    }
}
