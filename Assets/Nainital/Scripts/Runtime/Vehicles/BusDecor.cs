using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The inside of the bus he rides, dressed the way hill buses are (owner, 23 Nov 2026: "bus ke andr thoda decoration nhi
    /// krskte...raat me strip kind lights..waise thoda bhdiya look"): LED strips along both edges of the ceiling, front to
    /// back, their colours running down the bus (soft by day, bright at night), two coloured lights that wash the inside
    /// at night, a marigold garland sagging across the top of the windscreen and a red-and-gold fringe under it. Placed in
    /// the bus's body box (its BoxCollider: the roof, the sides, the windscreen). Added with the bus radio when he boards.
    /// </summary>
    public class BusDecor : MonoBehaviour
    {
        const int PerSide = 22;
        Renderer[] leds;
        Light[] glow;
        MaterialPropertyBlock mpb;
        static Material ledMat, marigold, marigold2, fringeRed, fringeGold, leaf;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseId = Shader.PropertyToID("_BaseColor");

        public static void On(GameObject bus)
        {
            if (!bus || bus.GetComponent<BusDecor>()) return;
            bus.AddComponent<BusDecor>();
        }

        static Material M(Color c, bool glowing = false)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor(BaseId, c);
            m.SetFloat("_Smoothness", 0.35f);
            if (glowing) { m.EnableKeyword("_EMISSION"); m.SetColor(EmissionId, c); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            return m;
        }

        static GameObject Part(PrimitiveType t, Transform parent, Vector3 local, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.layer = VehicleFactory.VehicleLayer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void Start()
        {
            var box = GetComponent<BoxCollider>();
            if (!box) { enabled = false; return; }
            if (!ledMat)
            {
                ledMat = M(Color.white, true);
                marigold = M(new Color(1f, 0.55f, 0.05f));
                marigold2 = M(new Color(1f, 0.8f, 0.1f));
                fringeRed = M(new Color(0.75f, 0.05f, 0.08f));
                fringeGold = M(new Color(0.95f, 0.72f, 0.2f));
                leaf = M(new Color(0.15f, 0.45f, 0.12f));
            }
            mpb = new MaterialPropertyBlock();
            var root = new GameObject("Bus decor").transform;
            root.SetParent(transform, false);
            Vector3 c = box.center, s = box.size;
            float top = c.y + s.y * 0.5f - 0.38f;           // under the roof's inside
            float half = s.x * 0.5f - 0.2f;                  // against the side walls
            float back = c.z - s.z * 0.5f + 0.55f, front = c.z + s.z * 0.5f - 0.9f;

            // the LED strips: a row of short glowing pieces each side
            leds = new Renderer[PerSide * 2];
            float seg = (front - back) / PerSide;
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < PerSide; i++)
                {
                    float x = side == 0 ? -half : half;
                    var g = Part(PrimitiveType.Cube, root, new Vector3(x, top, back + seg * (i + 0.5f)), new Vector3(0.035f, 0.035f, seg * 0.82f), ledMat);
                    leds[side * PerSide + i] = g.GetComponent<Renderer>();
                }
            // two lights for the colour inside at night
            glow = new Light[2];
            for (int k = 0; k < 2; k++)
            {
                var lg = new GameObject("Decor light").AddComponent<Light>();
                lg.transform.SetParent(root, false);
                lg.transform.localPosition = new Vector3(0f, top - 0.25f, Mathf.Lerp(back, front, k == 0 ? 0.28f : 0.72f));
                lg.type = LightType.Point;
                lg.range = 4.5f;
                lg.shadows = LightShadows.None;
                lg.intensity = 0f;
                glow[k] = lg;
            }
            // the marigold garland across the top of the windscreen, sagging, a mango leaf every few flowers
            float wz = c.z + s.z * 0.5f - 0.35f, wy = top - 0.05f;
            const int beads = 34;
            for (int i = 0; i <= beads; i++)
            {
                float u = i / (float)beads * 2f - 1f;           // -1 .. 1 across
                float x = u * half * 0.95f;
                float y = wy - 0.16f * (1f - u * u);              // the sag
                var m = i % 5 == 2 ? leaf : i % 2 == 0 ? marigold : marigold2;
                float r = i % 5 == 2 ? 0.05f : 0.075f;
                Part(PrimitiveType.Sphere, root, new Vector3(x, y, wz), new Vector3(r, r, r), m);
            }
            // the fringe under it: red and gold tassels
            for (int i = 0; i < 26; i++)
            {
                float x = Mathf.Lerp(-half * 0.95f, half * 0.95f, i / 25f);
                Part(PrimitiveType.Cube, root, new Vector3(x, wy - 0.2f, wz + 0.02f), new Vector3(0.05f, 0.14f, 0.01f), i % 2 == 0 ? fringeRed : fringeGold);
            }
        }

        void LateUpdate()
        {
            if (leds == null) return;
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.NightFactor : 0f;
            float strength = Mathf.Lerp(0.8f, 4f, night);
            float t = Time.time;
            for (int i = 0; i < leds.Length; i++)
            {
                if (!leds[i]) continue;
                int k = i % PerSide;
                // the colours run from the front to the back on both sides
                Color col = Color.HSVToRGB(Mathf.Repeat(t * 0.12f - k / (float)PerSide, 1f), 0.85f, 1f);
                mpb.SetColor(BaseId, col);
                mpb.SetColor(EmissionId, col * strength);
                leds[i].SetPropertyBlock(mpb);
            }
            for (int k = 0; k < glow.Length; k++)
            {
                if (!glow[k]) continue;
                glow[k].color = Color.HSVToRGB(Mathf.Repeat(t * 0.12f - (k == 0 ? 0.7f : 0.25f), 1f), 0.6f, 1f);
                glow[k].intensity = Mathf.Lerp(0.05f, 1.4f, night);
            }
        }
    }
}
