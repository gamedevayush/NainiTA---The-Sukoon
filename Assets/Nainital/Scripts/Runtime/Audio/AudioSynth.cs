using UnityEngine;

namespace Nainital
{
    /// <summary>Procedurally synthesised sound effects and ambience loops (no audio files needed).</summary>
    public static class AudioSynth
    {
        public const int SR = 22050;
        static System.Random rng = new System.Random(5);
        static float N() => (float)rng.NextDouble() * 2f - 1f;

        static AudioClip Make(string name, float[] d, bool loop)
        {
            if (loop)
            {
                int fade = Mathf.Min(d.Length / 4, SR / 4);
                for (int i = 0; i < fade; i++)
                {
                    float t = i / (float)fade;
                    d[i] = d[i] * t + d[d.Length - fade + i] * (1f - t);
                }
                System.Array.Resize(ref d, d.Length - fade);
            }
            float peak = 0.0001f;
            foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.9f;
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        public static AudioClip Wind()
        {
            var d = new float[SR * 10];
            float b = 0, lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                b = b * 0.997f + N() * 0.03f;
                lp += (b - lp) * 0.08f;
                float gust = 0.55f + 0.3f * Mathf.Sin(t * 0.6f) + 0.15f * Mathf.Sin(t * 1.7f + 1f);
                d[i] = lp * gust;
            }
            return Make("Wind", d, true);
        }

        public static AudioClip Water()
        {
            var d = new float[SR * 8];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += (N() - lp) * 0.12f;
                lp2 += (lp - lp2) * 0.2f;
                float lap = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI * 0.45f)), 3f) * 0.8f + 0.2f;
                d[i] = lp2 * lap;
                if (rng.NextDouble() < 0.0004)   // little plops
                {
                    float f = 500f + (float)rng.NextDouble() * 700f;
                    for (int k = 0; k < SR / 20 && i + k < d.Length; k++)
                        d[i + k] += Mathf.Sin(k * f * 2f * Mathf.PI / SR * (1f + k / (float)SR * 8f)) * Mathf.Exp(-k / (float)SR * 60f) * 0.3f;
                }
            }
            return Make("Water", d, true);
        }

        public static AudioClip Crickets()
        {
            var d = new float[SR * 4];
            foreach (var (freq, rate, offset) in new[] { (4300f, 0.9f, 0f), (3800f, 1.3f, 0.37f), (4700f, 0.7f, 0.8f) })
            {
                for (float start = offset; start < 4f; start += rate)
                    for (int p = 0; p < 3; p++)
                    {
                        int s0 = (int)((start + p * 0.05f) * SR);
                        for (int k = 0; k < SR * 0.03f && s0 + k < d.Length; k++)
                            d[s0 + k] += Mathf.Sin(2f * Mathf.PI * freq * k / SR) * Mathf.Sin(Mathf.PI * k / (SR * 0.03f)) * 0.3f;
                    }
            }
            return Make("Crickets", d, true);
        }

        public static AudioClip Bird()
        {
            var d = new float[(int)(SR * 0.7f)];
            int notes = 2 + rng.Next(3);
            float baseF = 2200f + (float)rng.NextDouble() * 1500f;
            for (int n = 0; n < notes; n++)
            {
                int s0 = (int)(n * 0.16f * SR);
                float f0 = baseF * (1f + 0.15f * n), f1 = f0 * (1.3f + (float)rng.NextDouble() * 0.4f);
                float ph = 0;
                for (int k = 0; k < SR * 0.12f && s0 + k < d.Length; k++)
                {
                    float t = k / (SR * 0.12f);
                    float f = Mathf.Lerp(f0, f1, t) * (1f + 0.03f * Mathf.Sin(k * 0.02f));
                    ph += 2f * Mathf.PI * f / SR;
                    d[s0 + k] += Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * t);
                }
            }
            return Make("Bird", d, false);
        }

        public static AudioClip Engine()
        {
            var d = new float[SR];   // exactly 1 s: 38 Hz fits whole cycles, loops cleanly
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float saw = 0;
                for (int h = 1; h <= 6; h++) saw += Mathf.Sin(2f * Mathf.PI * 38f * h * t) / h;
                lp += (N() - lp) * 0.3f;
                d[i] = saw * 0.7f + lp * 0.25f + Mathf.Sin(2f * Mathf.PI * 76f * t) * 0.3f;
            }
            return Make("Engine", d, false);
        }

        public static AudioClip Horn()
        {
            var d = new float[(int)(SR * 0.7f)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((0.7f - t) * 12f);
                float a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 420f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * 840f * t) * 0.3f;
                float b = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 525f * t)) * 0.5f;
                d[i] = (a + b) * env * 0.5f;
            }
            return Make("Horn", d, false);
        }

        public static AudioClip Punch()
        {
            var d = new float[(int)(SR * 0.25f)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = Mathf.Sin(2f * Mathf.PI * (75f - t * 120f) * t) * Mathf.Exp(-t * 22f) + N() * Mathf.Exp(-t * 70f) * 0.6f;
            }
            return Make("Punch", d, false);
        }

        public static AudioClip Splash()
        {
            var d = new float[SR];
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += (N() - lp) * Mathf.Lerp(0.6f, 0.08f, t);
                d[i] = lp * Mathf.Exp(-t * 3.5f) * Mathf.Clamp01(t * 60f);
            }
            return Make("Splash", d, false);
        }

        public static AudioClip Step()
        {
            var d = new float[(int)(SR * 0.09f)];
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += (N() - lp) * 0.25f;
                d[i] = lp * Mathf.Exp(-t * 55f);
            }
            return Make("Step", d, false);
        }

        public static AudioClip Bell()
        {
            var d = new float[(int)(SR * 2.8f)];
            float[] partials = { 820f, 1312f, 2050f, 2870f };
            float[] amps = { 1f, 0.5f, 0.35f, 0.2f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float v = 0;
                for (int p = 0; p < partials.Length; p++) v += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * amps[p] * Mathf.Exp(-t * (1.2f + p * 0.8f));
                d[i] = v * Mathf.Clamp01(t * 400f);
            }
            return Make("Bell", d, false);
        }

        public static AudioClip Rain()
        {
            var d = new float[SR * 4];
            float hp = 0, prev = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float x = N();
                hp = 0.85f * (hp + x - prev);
                prev = x;
                d[i] = hp * 0.5f;
                if (rng.NextDouble() < 0.002) d[i] += N() * 0.8f;
            }
            return Make("Rain", d, true);
        }

        public static AudioClip Thunder()
        {
            var d = new float[SR * 4];
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += (N() - lp) * 0.02f;
                float env = Mathf.Exp(-t * 0.9f) * (0.6f + 0.4f * Mathf.Sin(t * 7f));
                d[i] = lp * env + (t < 0.15f ? N() * (0.15f - t) * 3f : 0f);
            }
            return Make("Thunder", d, false);
        }

        public static AudioClip Jingle()
        {
            var d = new float[(int)(SR * 1.1f)];
            float[] notes = { 523f, 659f, 784f, 1046f };
            for (int n = 0; n < notes.Length; n++)
            {
                int s0 = (int)(n * 0.14f * SR);
                for (int k = 0; s0 + k < d.Length; k++)
                {
                    float t = k / (float)SR;
                    d[s0 + k] += (Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * t)) * Mathf.Exp(-t * 3f) * 0.5f;
                }
            }
            return Make("Jingle", d, false);
        }
    }
}
