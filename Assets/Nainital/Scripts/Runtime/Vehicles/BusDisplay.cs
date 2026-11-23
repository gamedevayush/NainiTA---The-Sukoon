using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The scrolling LED destination board on a government bus (owner, 24 Nov 2026: "har sarkari bus ke upar scrolling
    /// display hogi..kahan se kahan jayegi"; the safari's: "bhaar display pe nainital safari type"). Amber dot-matrix
    /// text (a 5x7 font drawn here, no font asset) on black, over the windscreen and high on both sides, running right to
    /// left. The texture is made once per text and shared; the scroll is the material's offset (no per-frame texture work).
    /// </summary>
    public class BusDisplay : MonoBehaviour
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static Shader unlit;
        const int Cell = 4, Dot = 3;   // texels per dot cell, lit texels per dot

        Material mat;
        float speed;     // texture widths per second
        float off;

        /// <summary>Put the boards on `bus` showing `text`.</summary>
        public static BusDisplay Fit(GameObject bus, string text)
        {
            var d = bus.GetComponent<BusDisplay>() ?? bus.AddComponent<BusDisplay>();
            d.Build(text);
            return d;
        }

        void Build(string text)
        {
            var tex = TextureFor(text);
            if (!unlit) unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (!unlit) return;
            mat = new Material(unlit) { name = "M_BusDisplay" };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f));   // a little over 1: reads as lit by day too
            var vi = GetComponent<VehicleInfo>();
            float L = vi ? vi.length : 10f, W = vi ? (vi.bodyWidth > 0f ? vi.bodyWidth : vi.width) : 2.5f, H = vi ? vi.height : 3.1f;
            float boardH = 0.24f;
            Board("LED front", new Vector3(0f, H - 0.42f, L * 0.5f + 0.03f), Quaternion.Euler(0f, 180f, 0f), W * 0.72f, boardH, tex);
            Board("LED left", new Vector3(-W * 0.5f - 0.02f, H - 0.38f, L * 0.18f), Quaternion.Euler(0f, 90f, 0f), L * 0.42f, boardH, tex);
            Board("LED right", new Vector3(W * 0.5f + 0.02f, H - 0.38f, L * 0.18f), Quaternion.Euler(0f, -90f, 0f), L * 0.42f, boardH, tex);
            speed = 2.4f / Mathf.Max(1f, text.Length);   // ~2.4 characters a second, right to left
        }

        void Board(string n, Vector3 pos, Quaternion outward, float w, float h, Texture2D tex)
        {
            // Unity's quad: its face looks down -z, uv (0,0) at its bottom-left as seen from the front. `outward` turns that
            // face to where people look from (front: +z of the bus, sides: -x / +x), so the text reads left to right.
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = outward;
            go.transform.localScale = new Vector3(w, h, 1f);
            var mf = go.GetComponent<MeshFilter>();
            var m = Instantiate(mf.sharedMesh);
            float uSpan = (w / h) / ((float)tex.width / tex.height);   // what fits of the text at its own aspect
            var uv = m.uv;
            for (int k = 0; k < uv.Length; k++) uv[k].x *= uSpan;
            m.uv = uv;
            mf.sharedMesh = m;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = mat;
        }

        void Update()
        {
            if (!mat) return;
            off = Mathf.Repeat(off + speed * Time.deltaTime, 1f);
            mat.SetTextureOffset("_BaseMap", new Vector2(-off, 0f));
        }

        void OnDestroy() { if (mat) Destroy(mat); }

        // ------------------------------------------------------------------ the dot-matrix text
        static Texture2D TextureFor(string text)
        {
            if (cache.TryGetValue(text, out var t) && t) return t;
            text = text.ToUpperInvariant();
            int cols = text.Length * 6;
            int w = cols * Cell, h = 9 * Cell;   // 7 rows of dots and a row of black above and below
            var px = new Color32[w * h];
            var off = new Color32(28, 14, 4, 255);      // an unlit dot: dark amber
            var on = new Color32(255, 150, 20, 255);    // a lit dot
            var black = new Color32(6, 6, 6, 255);
            for (int i = 0; i < px.Length; i++) px[i] = black;
            for (int c = 0; c < text.Length; c++)
            {
                var g = Glyph(text[c]);
                for (int gx = 0; gx < 6; gx++)
                    for (int gy = 0; gy < 7; gy++)
                    {
                        bool lit = gx < 5 && (g[gy] >> (4 - gx) & 1) == 1;
                        int cx = (c * 6 + gx) * Cell, cy = (7 - gy) * Cell;   // row 0 at the top
                        for (int dx = 0; dx < Dot; dx++)
                            for (int dy = 0; dy < Dot; dy++)
                                px[(cy + dy) * w + cx + dx] = lit ? on : off;
                    }
            }
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "LED " + text, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            cache[text] = t;
            return t;
        }

        /// <summary>5x7 glyph rows, top first, the left column the 0x10 bit.</summary>
        static int[] Glyph(char ch)
        {
            switch (ch)
            {
                case 'A': return new[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
                case 'B': return new[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
                case 'C': return new[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
                case 'D': return new[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C };
                case 'E': return new[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
                case 'F': return new[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
                case 'G': return new[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F };
                case 'H': return new[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
                case 'I': return new[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
                case 'J': return new[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C };
                case 'K': return new[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
                case 'L': return new[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
                case 'M': return new[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 };
                case 'N': return new[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 };
                case 'O': return new[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
                case 'P': return new[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
                case 'Q': return new[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
                case 'R': return new[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
                case 'S': return new[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E };
                case 'T': return new[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
                case 'U': return new[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
                case 'V': return new[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
                case 'W': return new[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A };
                case 'X': return new[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
                case 'Y': return new[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
                case 'Z': return new[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };
                case '0': return new[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
                case '1': return new[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
                case '2': return new[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F };
                case '3': return new[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E };
                case '4': return new[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
                case '5': return new[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
                case '6': return new[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
                case '7': return new[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
                case '8': return new[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
                case '9': return new[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };
                case '>': return new[] { 0x10, 0x08, 0x04, 0x02, 0x04, 0x08, 0x10 };
                case '-': return new[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
                case '/': return new[] { 0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10 };
                case '.': return new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
                case ':': return new[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
                default: return new[] { 0, 0, 0, 0, 0, 0, 0 };
            }
        }
    }
}
