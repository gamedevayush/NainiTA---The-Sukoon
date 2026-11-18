using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The wanted level at the top of the screen: three stars on a glass pill, filled in gold for each level
    /// (WantedSystem), shown only while the police are after the player. They pulse when a level is added and blink
    /// while the police have lost sight of the player (keep out of sight and they give up).
    /// </summary>
    public class WantedHud : MonoBehaviour
    {
        HUD hud;
        RectTransform pill;
        CanvasGroup group;
        readonly Image[] stars = new Image[3];
        float pulse, alpha;
        int shown;
        static Sprite star;

        public void Build(HUD h, Transform root)
        {
            hud = h;
            var p = h.Panel("Wanted", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(236f, 70f), HudStyle.Glass);
            p.raycastTarget = false;
            pill = p.rectTransform;
            group = pill.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (!star) star = MakeStar(96);
            for (int i = 0; i < 3; i++)
            {
                var s = h.Img("Star" + i, pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 68f, 0f), new Vector2(54f, 54f), Color.white);
                s.sprite = star;
                s.raycastTarget = false;
                stars[i] = s;
            }
            pill.gameObject.SetActive(false);
        }

        void Update()
        {
            var ws = WantedSystem.Instance;
            int n = ws ? ws.Stars : 0;
            if (n > shown) pulse = 1f;
            shown = n;
            alpha = Mathf.MoveTowards(alpha, n > 0 ? 1f : 0f, Time.unscaledDeltaTime * (n > 0 ? 6f : 1.5f));
            bool on = alpha > 0.001f;
            if (pill.gameObject.activeSelf != on) pill.gameObject.SetActive(on);
            if (!on) return;
            group.alpha = alpha;
            pill.anchoredPosition = new Vector2(0f, hud && hud.TimerShown ? -106f : -30f);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 1.6f);
            pill.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(pulse * Mathf.PI));
            bool blink = ws && ws.Searching && Mathf.Repeat(Time.unscaledTime * 1.6f, 1f) < 0.45f;
            for (int i = 0; i < 3; i++)
            {
                bool filled = i < n;
                stars[i].color = filled ? (blink ? new Color(1f, 1f, 1f, 0.55f) : HudStyle.Marigold) : new Color(1f, 1f, 1f, 0.18f);
            }
        }

        /// <summary>A five-pointed star, anti-aliased.</summary>
        static Sprite MakeStar(int n)
        {
            var pts = new Vector2[10];
            for (int k = 0; k < 10; k++)
            {
                float a = Mathf.PI * 0.5f + k * Mathf.PI / 5f;
                float r = k % 2 == 0 ? 0.47f : 0.2f;
                pts[k] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float aa = 1.2f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 q = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                    bool inside = false;
                    float dmin = float.MaxValue;
                    for (int i = 0, j = 9; i < 10; j = i++)
                    {
                        Vector2 a = pts[i], b = pts[j];
                        if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                        Vector2 ab = b - a;
                        float h = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                        dmin = Mathf.Min(dmin, (q - a - ab * h).magnitude);
                    }
                    float sd = inside ? dmin : -dmin;
                    float al = Mathf.Clamp01(sd / aa + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(al * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
