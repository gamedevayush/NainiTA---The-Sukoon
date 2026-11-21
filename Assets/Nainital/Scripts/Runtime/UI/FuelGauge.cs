using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The fuel gauge (owner, 21 Nov 2026, FREEROAM #4: "a gauge on the HUD"): a slim glass pill with a pump glyph, a
    /// level bar filling from the bottom and the percentage, shown while the player drives or rides anything with a tank
    /// (VehicleFuel; not the electric ones). It follows the speedometer's box wherever the HUD or the touch layout puts
    /// it: in a car it stands just left of the dial, on a bike (no speedometer) in the dial's place. Teal when there is
    /// plenty, marigold under a third, coral and pulsing under 15 %; while a pump fills it the bar glows marigold.
    /// Built from one line in HUD.Build; no per-frame allocations (the number is redrawn only when it changes).
    /// </summary>
    public class FuelGauge : MonoBehaviour
    {
        HUD hud;
        RectTransform speed, box, pill;
        CanvasGroup group;
        Image fill, icon, glow;
        Text pct;
        float alpha, shownFill = -1f;
        int shownPct = -1;
        bool carLayout = true;
        static Sprite pumpIcon;

        const float PillW = 60f, PillH = 200f, BarW = 14f, BarH = 116f;

        public void Build(HUD h, Transform root)
        {
            hud = h;
            speed = root.Find("Speed") as RectTransform;
            box = h.Rect("Fuel", root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 40), new Vector2(280, 250));
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var p = h.Panel("Pill", box, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(12, 10), new Vector2(PillW, PillH), HudStyle.Glass);
            p.raycastTarget = false;
            pill = p.rectTransform;
            glow = h.Img("Glow", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(PillW * 2.2f, PillH * 1.1f), new Color(HudStyle.Marigold.r, HudStyle.Marigold.g, HudStyle.Marigold.b, 0f));
            glow.sprite = HudStyle.Glow;
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();

            if (!pumpIcon) pumpIcon = MakePumpIcon(96);
            icon = h.Img("Icon", pill, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(34, 34), HudStyle.Ink);
            icon.sprite = pumpIcon;
            icon.raycastTarget = false;

            var track = h.Panel("Track", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(BarW, BarH), new Color(1, 1, 1, 0.13f), true);
            track.raycastTarget = false;
            fill = h.Img("Fill", track.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarW - 4f, BarH - 4f), HudStyle.Teal);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            // quarter ticks beside the bar
            for (int i = 1; i < 4; i++)
            {
                var tick = h.Img("Tick", track.transform, new Vector2(1, 0), new Vector2(0, 0.5f), new Vector2(3, BarH * i * 0.25f), new Vector2(i == 2 ? 9 : 6, 2), new Color(1, 1, 1, 0.35f));
                tick.raycastTarget = false;
            }

            pct = h.TxtB("Pct", pill, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(PillW, 30), 22, TextAnchor.MiddleCenter, HudStyle.Ink);
            pct.horizontalOverflow = HorizontalWrapMode.Overflow;
            box.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!box) return;
            var f = VehicleFuel.Player;
            bool want = f && !f.electric && !(hud && hud.DialogOpen) && !Minimap.BigMapOpen;
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            bool on = alpha > 0.001f && f;
            if (box.gameObject.activeSelf != on) box.gameObject.SetActive(on);
            if (!on) { shownFill = -1f; return; }
            group.alpha = alpha;
            Follow(CarController.Driving != null);

            float frac = f.Fraction;
            shownFill = shownFill < 0f ? frac : Mathf.Lerp(shownFill, frac, Mathf.Clamp01(Time.unscaledDeltaTime * 8f));
            fill.fillAmount = shownFill;
            float t = Time.unscaledTime;
            bool low = frac < 0.15f;
            Color c = low ? HudStyle.Coral : frac < 0.34f ? HudStyle.Marigold : HudStyle.Teal;
            if (low) c.a = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(t * 4f));
            fill.color = c;
            icon.color = low ? c : HudStyle.Ink;
            int n = Mathf.Clamp(Mathf.RoundToInt(frac * 100f), 0, 100);
            if (n != shownPct) { shownPct = n; pct.text = NumText(n); }
            pct.color = low ? HudStyle.Coral : HudStyle.Ink;
            var gc = glow.color;
            gc.a = f.Filling ? 0.35f + 0.2f * Mathf.Sin(t * 6f) : Mathf.MoveTowards(gc.a, 0f, Time.unscaledDeltaTime * 2f);
            glow.color = gc;
        }

        /// <summary>Takes the speedometer's place and size (the touch layout moves and scales it); the pill beside the dial
        /// in a car, in its middle on a bike.</summary>
        void Follow(bool car)
        {
            if (speed)
            {
                if (box.anchorMin != speed.anchorMin) box.anchorMin = speed.anchorMin;
                if (box.anchorMax != speed.anchorMax) box.anchorMax = speed.anchorMax;
                if (box.pivot != speed.pivot) box.pivot = speed.pivot;
                if (box.anchoredPosition != speed.anchoredPosition) box.anchoredPosition = speed.anchoredPosition;
                if (box.sizeDelta != speed.sizeDelta) box.sizeDelta = speed.sizeDelta;
                if (box.localScale != speed.localScale) box.localScale = speed.localScale;
            }
            if (car == carLayout) return;
            carLayout = car;
            if (car)
            {
                pill.anchorMin = pill.anchorMax = new Vector2(0, 0.5f);
                pill.pivot = new Vector2(1, 0.5f);
                pill.anchoredPosition = new Vector2(12, 10);
            }
            else
            {
                pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0.5f);
                pill.pivot = new Vector2(0.5f, 0.5f);
                pill.anchoredPosition = new Vector2(0, 10);
            }
        }

        static readonly string[] nums = new string[101];
        static string NumText(int n) => nums[n] ?? (nums[n] = n + "%");

        // ------------------------------------------------------------------ the glyph
        const float AA = 1f / 96f;
        static float Box(float x, float y, float cx, float cy, float hx, float hy, float round)
        {
            float qx = Mathf.Abs(x - cx) - hx + round, qy = Mathf.Abs(y - cy) - hy + round;
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
            return Mathf.Clamp01(-d / AA + 0.5f);
        }
        static float Seg(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            Vector2 p = new Vector2(x - ax, y - ay), ba = new Vector2(bx - ax, by - ay);
            float h = Mathf.Clamp01(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba));
            return Mathf.Clamp01((w - (p - ba * h).magnitude) / AA + 0.5f);
        }

        /// <summary>A petrol pump: the body with its window cut out, a base, the hose looping out to the nozzle.</summary>
        static Sprite MakePumpIcon(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float body = Box(u, v, 0.40f, 0.50f, 0.20f, 0.36f, 0.05f) - Box(u, v, 0.40f, 0.67f, 0.12f, 0.10f, 0.02f);
                    float a = Mathf.Max(Mathf.Clamp01(body), Box(u, v, 0.40f, 0.11f, 0.27f, 0.04f, 0.015f));
                    a = Mathf.Max(a, Seg(u, v, 0.60f, 0.70f, 0.76f, 0.70f, 0.035f));    // the hose out of the top
                    a = Mathf.Max(a, Seg(u, v, 0.76f, 0.70f, 0.84f, 0.60f, 0.035f));
                    a = Mathf.Max(a, Seg(u, v, 0.84f, 0.60f, 0.84f, 0.32f, 0.035f));    // down the side
                    a = Mathf.Max(a, Seg(u, v, 0.84f, 0.32f, 0.70f, 0.24f, 0.035f));    // back to the body
                    a = Mathf.Max(a, Seg(u, v, 0.84f, 0.80f, 0.84f, 0.66f, 0.045f));    // the nozzle on its hook
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
