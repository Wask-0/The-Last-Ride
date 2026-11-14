using UnityEngine;
using static LastRide.Audio.ProcAudio;

namespace LastRide.Audio
{
    /// <summary>
    /// Короткие процедурные звуки (ТЗ 8.3), рендерятся один раз при старте через <see cref="ProcAudio.Render"/>.
    /// Повторяющиеся (шаги) — по нескольку вариантов; высота тона дополнительно «дрожит» при проигрывании.
    /// </summary>
    public sealed class SfxBank
    {
        public AudioClip Pickup, FlapOpen, FlapClose, Throw, Rekindle, EmbersAlarm, Extinguish, DoorSlam, Clang,
                         WindKey, TapeClick, StaticBurst, StaticLoop, GustThump, WindDown, Clink;
        public AudioClip[] StepSnow, StepDeep, StepIce;

        private static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        public static SfxBank Build()
        {
            var b = new SfxBank();
            b.Pickup = MakePickup();
            b.FlapOpen = MakeFlap(true);
            b.FlapClose = MakeFlap(false);
            b.Throw = MakeThrow();
            b.Rekindle = MakeRekindle();
            b.EmbersAlarm = MakeAlarm();
            b.Extinguish = MakeExtinguish();
            b.DoorSlam = MakeDoor();
            b.Clang = MakeClang();
            b.WindKey = MakeWindKey();
            b.TapeClick = MakeTapeClick();
            b.StaticBurst = MakeStaticBurst();
            b.StaticLoop = MakeStaticLoop();
            b.GustThump = MakeGust();
            b.WindDown = MakeWindDown();
            b.Clink = MakeClink();
            b.StepSnow = new AudioClip[4]; b.StepDeep = new AudioClip[3]; b.StepIce = new AudioClip[3];
            for (int i = 0; i < b.StepSnow.Length; i++) b.StepSnow[i] = MakeStep(0, 100u + (uint)i * 7919u);
            for (int i = 0; i < b.StepDeep.Length; i++) b.StepDeep[i] = MakeStep(1, 300u + (uint)i * 6007u);
            for (int i = 0; i < b.StepIce.Length; i++) b.StepIce[i] = MakeStep(2, 500u + (uint)i * 4409u);
            return b;
        }

        // ------------------------------------------------------------------ подбор / печка / огонь

        private static AudioClip MakePickup()
        {
            var rng = new Rng(11);
            return Render("sfx_pickup", 0.22f, (i, sr) =>
            {
                float t = (float)i / sr;
                float hz = Mathf.Lerp(520f, 820f, Mathf.Clamp01(t / 0.08f));
                float tone = Sine(hz, t) * AttackDecay(t, 0.003f, 0.07f) + 0.35f * Sine(hz * 2f, t) * AttackDecay(t, 0.002f, 0.04f);
                float tick = rng.Next() * AttackDecay(t, 0.0005f, 0.004f) * 0.4f;
                return tone + tick;
            }, 0.7f);
        }

        private static AudioClip MakeFlap(bool open)
        {
            var rng = new Rng(open ? 21u : 22u);
            var bp = new Biquad(); var bp2 = new Biquad();
            float len = open ? 0.55f : 0.35f;
            float clackAt = open ? 0.38f : 0.02f;
            int c = 0;
            return Render(open ? "sfx_flap_open" : "sfx_flap_close", len, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 31) == 0)
                {
                    float f = open ? Mathf.Lerp(260f, 720f, Mathf.Clamp01(t / 0.38f)) : Mathf.Lerp(600f, 300f, Mathf.Clamp01(t / 0.3f));
                    bp.Set(BiquadType.BandPass, f, 6f, sr);
                }
                float creak = 0f;
                float creakLen = open ? 0.38f : 0.0f;
                if (t < creakLen) creak = bp.Process(rng.Next()) * 1.4f * Mathf.Sin(Mathf.PI * t / creakLen);
                float dt = t - clackAt;
                float clack = 0f;
                if (dt >= 0f)
                {
                    if ((c & 31) == 1) bp2.Set(BiquadType.BandPass, 1700f, 1.5f, sr);
                    clack = bp2.Process(rng.Next()) * AttackDecay(dt, 0.0003f, 0.012f) * 1.6f
                          + Sine(open ? 330f : 150f, dt) * AttackDecay(dt, 0.001f, open ? 0.05f : 0.09f) * 0.7f;
                }
                return creak * 0.5f + clack;
            }, 0.75f);
        }

        private static AudioClip MakeThrow()
        {
            var rng = new Rng(31);
            var lp = new Biquad(); var cr = new Biquad();
            float[] clicks = { 0.18f, 0.23f, 0.27f, 0.33f, 0.36f, 0.44f };
            int c = 0;
            return Render("sfx_throw", 0.7f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 15) == 0)
                {
                    float f = 500f + 2200f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f));
                    lp.Set(BiquadType.LowPass, f, 0.8f, sr);
                    cr.Set(BiquadType.BandPass, 2600f, 1.2f, sr);
                }
                float whoosh = lp.Process(rng.Next()) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f)) * 1.2f;
                float crackle = 0f;
                for (int k = 0; k < clicks.Length; k++)
                    if (t >= clicks[k]) crackle += AttackDecay(t - clicks[k], 0.0002f, 0.01f);
                return whoosh + cr.Process(rng.Next()) * crackle * 1.8f;
            }, 0.75f);
        }

        private static AudioClip MakeRekindle()
        {
            var rng = new Rng(41);
            var bp = new Biquad();
            int c = 0;
            return Render("sfx_rekindle", 0.9f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 15) == 0) bp.Set(BiquadType.BandPass, Mathf.Lerp(280f, 2300f, Mathf.Clamp01(t / 0.5f)), 0.9f, sr);
                float env = AttackDecay(t, 0.28f, 0.22f);
                return bp.Process(rng.Next()) * env * 2f + Sine(88f, t) * env * 0.5f;
            }, 0.75f);
        }

        private static AudioClip MakeAlarm()
        {
            return Render("sfx_embers_alarm", 0.55f, (i, sr) =>
            {
                float t = (float)i / sr;
                float p1 = Sine(196f, t) * 0.8f + Sine(392f, t) * 0.12f;
                float p2 = (Sine(147f, t - 0.25f) * 0.8f + Sine(294f, t - 0.25f) * 0.12f) * (t >= 0.25f ? 1f : 0f);
                return p1 * AttackDecay(t, 0.01f, 0.09f) + p2 * AttackDecay(t - 0.25f, 0.01f, 0.1f);
            }, 0.6f, 8f);
        }

        private static AudioClip MakeExtinguish()
        {
            var rng = new Rng(51);
            var bp = new Biquad();
            int c = 0;
            return Render("sfx_extinguish", 1.5f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 15) == 0) bp.Set(BiquadType.BandPass, Mathf.Lerp(2600f, 380f, Mathf.Clamp01(t / 1.2f)), 0.8f, sr);
                return bp.Process(rng.Next()) * AttackDecay(t, 0.02f, 0.45f) * 2f
                     + Sine(Mathf.Lerp(90f, 38f, Mathf.Clamp01(t / 1f)), t) * AttackDecay(t, 0.01f, 0.5f) * 0.7f;
            }, 0.75f);
        }

        private static AudioClip MakeClink()
        {
            return Render("sfx_clink", 0.35f, (i, sr) =>
            {
                float t = (float)i / sr;
                return Sine(2100f, t) * AttackDecay(t, 0.0005f, 0.09f) + 0.6f * Sine(3170f, t) * AttackDecay(t, 0.0005f, 0.06f)
                     + 0.3f * Sine(4650f, t) * AttackDecay(t, 0.0005f, 0.03f);
            }, 0.6f);
        }

        // ------------------------------------------------------------------ фургон

        private static AudioClip MakeDoor()
        {
            var rng = new Rng(61);
            var lp = new Biquad();
            int c = 0;
            return Render("sfx_door", 0.7f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 31) == 0) lp.Set(BiquadType.LowPass, 900f, 0.8f, sr);
                float thud = Sine(Mathf.Lerp(110f, 42f, Mathf.Clamp01(t / 0.12f)), t) * AttackDecay(t, 0.001f, 0.13f);
                float slap = lp.Process(rng.Next()) * AttackDecay(t, 0.0005f, 0.04f) * 2f;
                float rattle = (Sine(412f, t) + 0.7f * Sine(587f, t)) * AttackDecay(t - 0.01f, 0.002f, 0.16f) * 0.22f;
                return thud * 1.2f + slap + rattle;
            }, 0.85f);
        }

        private static AudioClip MakeClang()
        {
            var rng = new Rng(71);
            float[] f = { 158f, 244f, 391f, 563f, 811f, 1187f };
            float[] a = { 1f, 0.8f, 0.65f, 0.45f, 0.3f, 0.2f };
            float[] d = { 1.5f, 1.2f, 0.9f, 0.7f, 0.5f, 0.35f };
            return Render("sfx_clang", 2.4f, (i, sr) =>
            {
                float t = (float)i / sr;
                float s = 0f;
                for (int k = 0; k < f.Length; k++)
                {
                    // Пара слегка расстроенных синусов даёт биения «железа».
                    s += a[k] * (Sine(f[k], t) + 0.6f * Sine(f[k] * 1.0035f, t)) * AttackDecay(t, 0.0008f, d[k]);
                }
                float thud = Sine(Mathf.Lerp(90f, 48f, Mathf.Clamp01(t / 0.2f)), t) * AttackDecay(t, 0.001f, 0.3f) * 2.5f;
                float hit = rng.Next() * AttackDecay(t, 0.0002f, 0.012f) * 3f;
                return s * 0.5f + thud + hit;
            }, 0.9f, 6f);
        }

        private static AudioClip MakeGust()
        {
            var rng = new Rng(81);
            var lp = new Biquad();
            int c = 0;
            return Render("sfx_gust", 1.0f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 31) == 0) lp.Set(BiquadType.LowPass, Mathf.Lerp(180f, 420f, Mathf.Clamp01(t / 0.3f)), 0.7f, sr);
                return lp.Process(rng.Next()) * AttackDecay(t, 0.05f, 0.3f) * 3f + Sine(54f, t) * AttackDecay(t, 0.01f, 0.35f) * 0.8f;
            }, 0.8f);
        }

        // ------------------------------------------------------------------ робот

        /// <summary>kind: 0 — снег, 1 — глубокий снег, 2 — лёд.</summary>
        private static AudioClip MakeStep(int kind, uint seed)
        {
            var rng = new Rng(seed);
            var hp = new Biquad(); var lp = new Biquad();
            int c = 0;
            float len = kind == 1 ? 0.34f : kind == 2 ? 0.16f : 0.2f;
            return Render("sfx_step_" + kind + "_" + seed, len, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 63) == 0)
                {
                    hp.Set(BiquadType.HighPass, kind == 2 ? 3000f : kind == 1 ? 700f : 1400f, 0.7f, sr);
                    lp.Set(BiquadType.LowPass, kind == 1 ? 260f : 220f, 0.8f, sr);
                }
                float grain = rng.Next01() > 0.35f ? 1f : 0.25f; // хруст — «зёрна» снега
                float dec = kind == 1 ? 0.11f : kind == 2 ? 0.012f : 0.045f;
                float crunch = hp.Process(rng.Next()) * grain * AttackDecay(t, kind == 1 ? 0.012f : 0.002f, dec) * (kind == 1 ? 0.9f : 1.6f);
                float thump = lp.Process(rng.Next()) * AttackDecay(t, 0.003f, kind == 1 ? 0.1f : 0.06f) * 3f;
                float tick = kind == 2 ? Sine(2200f, t) * AttackDecay(t, 0.0003f, 0.005f) * 0.6f : 0f;
                return crunch + thump * (kind == 1 ? 1.2f : 0.8f) + tick;
            }, 0.7f, 2f);
        }

        private static AudioClip MakeWindKey()
        {
            var rng = new Rng(91);
            var bp = new Biquad();
            int n = 14;
            float[] times = new float[n];
            float tt = 0.02f;
            for (int k = 0; k < n; k++) { times[k] = tt; tt += Mathf.Lerp(0.045f, 0.095f, (float)k / n); }
            int c = 0;
            return Render("sfx_windkey", 1.4f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 31) == 0) bp.Set(BiquadType.BandPass, 2100f, 2.5f, sr);
                float clicks = 0f;
                for (int k = 0; k < n; k++)
                {
                    float dt = t - times[k];
                    if (dt < 0f) break;
                    if (dt > 0.05f) continue;
                    clicks += AttackDecay(dt, 0.0002f, 0.007f) * (0.7f + 0.3f * Mathf.Sin(k * 1.7f)) + Sine(680f + k * 25f, dt) * AttackDecay(dt, 0.0005f, 0.012f) * 0.4f;
                }
                float whirr = 0f;
                if (t < times[n - 1]) whirr = (Mathf.Repeat(t * (150f + 80f * t), 1f) * 2f - 1f) * 0.08f * (0.6f + 0.4f * rng.Next01());
                return bp.Process(rng.Next()) * clicks * 1.5f + clicks * 0.5f + whirr;
            }, 0.7f);
        }

        private static AudioClip MakeWindDown()
        {
            var rng = new Rng(101);
            var lp = new Biquad();
            float phase = 0f;
            int c = 0;
            return Render("sfx_winddown", 2.6f, (i, sr) =>
            {
                float t = (float)i / sr;
                float k = Mathf.Clamp01(t / 2.4f);
                float hz = 240f * Mathf.Pow(0.12f, k);
                phase += hz / sr; if (phase >= 1f) phase -= 1f;
                if ((c++ & 63) == 0) lp.Set(BiquadType.LowPass, Mathf.Lerp(1800f, 200f, k), 0.8f, sr);
                float saw = phase * 2f - 1f;
                float tickIntensity = Mathf.Sin(t * Mathf.Lerp(40f, 6f, k)) > 0.9f ? 1f : 0f;
                float env = (1f - k) * (1f - k);
                return lp.Process(saw * 0.6f + rng.Next() * 0.15f) * env + tickIntensity * 0.15f * env;
            }, 0.7f, 20f);
        }

        // ------------------------------------------------------------------ радио

        private static AudioClip MakeTapeClick()
        {
            var rng = new Rng(111);
            var bp = new Biquad();
            int c = 0;
            return Render("sfx_tapeclick", 0.14f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 31) == 0) bp.Set(BiquadType.BandPass, 2800f, 1.1f, sr);
                return bp.Process(rng.Next()) * AttackDecay(t, 0.0002f, 0.004f) * 3f + Sine(170f, t) * AttackDecay(t, 0.0005f, 0.03f) * 0.9f;
            }, 0.8f, 1.5f);
        }

        private static AudioClip MakeStaticBurst()
        {
            var rng = new Rng(121);
            var bp = new Biquad(); var lp = new Biquad();
            int c = 0;
            float gate = 1f;
            return Render("sfx_staticburst", 0.45f, (i, sr) =>
            {
                float t = (float)i / sr;
                if ((c++ & 31) == 0) { bp.Set(BiquadType.HighPass, 500f, 0.7f, sr); lp.Set(BiquadType.LowPass, 4200f, 0.7f, sr); }
                gate += ((rng.Next01() > 0.5f ? 1f : 0.3f) - gate) * 0.003f;
                return lp.Process(bp.Process(rng.Next())) * AttackDecay(t, 0.004f, 0.16f) * gate * 2f;
            }, 0.7f, 3f);
        }

        private static AudioClip MakeStaticLoop()
        {
            var rng = new Rng(131);
            var hp = new Biquad(); var lp = new Biquad();
            float gate = 1f, pop = 0f;
            int c = 0;
            return Render("sfx_staticloop", 3f, (i, sr) =>
            {
                if ((c++ & 31) == 0) { hp.Set(BiquadType.HighPass, 420f, 0.7f, sr); lp.Set(BiquadType.LowPass, 3600f, 0.7f, sr); }
                gate += ((0.45f + 0.55f * rng.Next01()) - gate) * 0.0006f;
                if (rng.Next01() > 0.9996f) pop = 1f;
                pop *= 0.995f;
                float n = lp.Process(hp.Process(rng.Next())) * gate;
                return n * 1.6f + rng.Next() * pop * 0.8f;
            }, 0.6f, 0f);
        }
    }
}
