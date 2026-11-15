using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>The round radar in the corner (turns with the camera), ringed by the player's health with a compass
    /// inside its edge, and the full map on M: landmark, mission and enemy blips (HudStyle).</summary>
    public class Minimap : MonoBehaviour
    {
        public static Texture2D MapTexture;
        public static float WorldSize = 4000f;
        public static float WorldCx, WorldCz;   // the map square's centre (local metres)
        /// <summary>The radar's outer size on the 1920 x 1080 canvas (the health ring's outside).</summary>
        public const float Footprint = 330f;
        static Minimap instance;
        /// <summary>True while the full map is open.</summary>
        public static bool BigMapOpen => instance && instance.bigMap && instance.bigMap.activeSelf;

        RectTransform rotator, compass, mapRect, arrow, bigMarker;
        GameObject bigMap;
        Image healthFill;
        readonly Text[] compassLetters = new Text[4];
        float viewRadius = 230f, sizePx = 280f, mapPx, shownHealth = 1f;
        readonly List<(Vector3 pos, RectTransform rt)> landmarks = new List<(Vector3, RectTransform)>();
        readonly List<(EnemyAI e, RectTransform rt)> enemies = new List<(EnemyAI, RectTransform)>();
        HUD hud;
        Sprite tri;
        RectTransform targetBlip, targetGlow;
        readonly List<(Mission m, RectTransform rt)> giverBlips = new List<(Mission, RectTransform)>();

        Vector2 RadarPos(Vector3 world, Vector3 pp, float rpx, bool pin)
        {
            Vector2 d = new Vector2(world.x - pp.x, world.z - pp.z) / viewRadius * (sizePx * 0.5f);
            if (pin && d.magnitude > rpx) d = d.normalized * rpx;
            return d;
        }

        void UpdateMissionBlips(Vector3 pp, float rpx)
        {
            var mm = MissionManager.Instance;
            if (!mm) return;
            if (!targetBlip)
            {
                var gl = hud.Img("TargetGlow", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54), new Color(HudStyle.Teal.r, HudStyle.Teal.g, HudStyle.Teal.b, 0.55f));
                gl.sprite = HudStyle.Glow;
                gl.raycastTarget = false;
                targetGlow = gl.rectTransform;
                var b = hud.Img("TargetBlip", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18), HudStyle.Teal);
                b.sprite = HudStyle.Disc;
                b.raycastTarget = false;
                var o = hud.Img("Ring", b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24), Color.white);
                o.sprite = HudStyle.RingSprite;
                o.raycastTarget = false;
                targetBlip = b.rectTransform;
            }
            var t = mm.CurrentTarget;
            targetBlip.gameObject.SetActive(t.HasValue);
            targetGlow.gameObject.SetActive(t.HasValue);
            if (t.HasValue)
            {
                targetBlip.anchoredPosition = targetGlow.anchoredPosition = RadarPos(t.Value, pp, rpx, true);
                targetGlow.localScale = Vector3.one * (0.8f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f));
            }
            foreach (var (m, mk) in mm.Givers)
            {
                if (giverBlips.Exists(g => g.m == m)) continue;
                var bg = hud.Panel("GiverBlip", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26), HudStyle.Marigold, true);
                bg.raycastTarget = false;
                var tx = hud.TxtB("M", bg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(26, 26), 20, TextAnchor.MiddleCenter, new Color(0.08f, 0.06f, 0.02f));
                tx.GetComponent<Shadow>().enabled = false;
                tx.text = "M";
                giverBlips.Add((m, bg.rectTransform));
            }
            foreach (var (m, rt) in giverBlips)
            {
                bool show = mm.Active == null;
                if (rt.gameObject.activeSelf != show) rt.gameObject.SetActive(show);
                if (show) rt.anchoredPosition = RadarPos(m.giver, pp, rpx, true);
                rt.localRotation = Quaternion.Inverse(rotator.localRotation);
            }
        }

        static Sprite TriangleSprite(int n)
        {
            // an arrowhead with a notch at its back, anti-aliased
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n - 0.5f, fy = (y + 0.5f) / n;
                    float side = (1f - fy) * 0.5f - Mathf.Abs(fx);          // inside the tall triangle when > 0
                    float notch = fy - 0.28f * (1f - Mathf.Abs(fx) * 2f);   // the cut-out at the back
                    float a = Mathf.Clamp01(side * n * 0.9f) * Mathf.Clamp01(notch * n * 0.9f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        Image Gauge(Transform parent, string name, float size, Color c)
        {
            var a = hud.Img(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), c);
            a.sprite = HudStyle.RingSprite;
            a.type = Image.Type.Filled;
            a.fillMethod = Image.FillMethod.Radial360;
            a.fillOrigin = (int)Image.Origin360.Bottom;
            a.fillClockwise = true;
            a.fillAmount = 0.75f;
            a.rectTransform.localEulerAngles = new Vector3(0, 0, -45f);   // three quarters round, the gap at the bottom
            a.raycastTarget = false;
            return a;
        }

        public void Build(HUD h, Transform root, string[] names, Vector3[] positions)
        {
            hud = h;
            instance = this;
            tri = TriangleSprite(64);
            var frame = h.Rect("Radar", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40), new Vector2(Footprint, Footprint));

            // health: three quarters of a ring round the radar
            Gauge(frame, "HealthTrack", Footprint, new Color(1, 1, 1, 0.14f));
            healthFill = Gauge(frame, "Health", Footprint, HudStyle.Good);

            var disc = h.Img("Disc", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizePx + 14, sizePx + 14), new Color(0.02f, 0.03f, 0.04f, 0.82f));
            disc.sprite = HudStyle.Disc;
            disc.raycastTarget = false;
            var maskImg = h.Img("RadarMask", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizePx, sizePx), Color.white);
            maskImg.sprite = HudStyle.Disc;
            maskImg.raycastTarget = false;
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            rotator = h.Rect("Rotator", maskImg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            mapPx = sizePx * WorldSize / (viewRadius * 2f);
            var mapGo = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            mapGo.transform.SetParent(rotator, false);
            mapRect = (RectTransform)mapGo.transform;
            mapRect.sizeDelta = new Vector2(mapPx, mapPx);
            var raw = mapGo.GetComponent<RawImage>();
            raw.texture = MapTexture;
            raw.raycastTarget = false;
            raw.color = MapTexture ? new Color(0.82f, 0.86f, 0.88f) : new Color(0.2f, 0.3f, 0.2f);   // a touch darker, so the blips stand out
            var shade = h.Img("Shade", maskImg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizePx, sizePx), new Color(0, 0, 0, 0.55f));
            shade.sprite = HudStyle.Vignette;
            shade.raycastTarget = false;

            if (names != null)
                for (int i = 0; i < names.Length; i++)
                {
                    if (positions[i] == Vector3.zero) continue;
                    var b = h.Img("Blip " + names[i], rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11, 11), HudStyle.Marigold);
                    b.sprite = HudStyle.Diamond;
                    b.raycastTarget = false;
                    landmarks.Add((positions[i], b.rectTransform));
                }

            var edge = h.Img("Edge", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizePx + 4, sizePx + 4), new Color(1, 1, 1, 0.22f));
            edge.sprite = HudStyle.RingThin;
            edge.raycastTarget = false;

            // compass: N E S W just inside the edge, turning with the map, the letters kept upright
            compass = h.Rect("Compass", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            string[] lt = { "N", "E", "S", "W" };
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f, r = sizePx * 0.5f - 18f;
                var pos = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                var tx = i == 0
                    ? h.TxtB("N", compass, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(30, 30), 26, TextAnchor.MiddleCenter, HudStyle.Marigold)
                    : h.Txt(lt[i], compass, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(30, 30), 20, TextAnchor.MiddleCenter, HudStyle.InkDim);
                tx.text = lt[i];
                compassLetters[i] = tx;
            }

            var glow = h.Img("PlayerGlow", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), new Color(0, 0, 0, 0.55f));
            glow.sprite = HudStyle.Glow;
            glow.raycastTarget = false;
            var arrowImg = h.Img("PlayerArrow", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 28), Color.white);
            arrowImg.sprite = tri;
            arrowImg.raycastTarget = false;
            arrow = arrowImg.rectTransform;

            // full map
            var big = h.Panel("BigMap", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 1040), HudStyle.GlassDeep);
            bigMap = big.gameObject;
            var title = h.TxtB("Title", big.transform, new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(34, -14), new Vector2(600, 50), 40, TextAnchor.MiddleLeft, HudStyle.Ink);
            title.text = "NAINITAL";
            var close = h.Txt("Close", big.transform, new Vector2(1, 1f), new Vector2(1, 1f), new Vector2(-34, -14), new Vector2(400, 50), 24, TextAnchor.MiddleRight, HudStyle.InkDim);
            close.text = "<color=#FFB829>M</color>  band karo / close";
            var bigRaw = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            bigRaw.transform.SetParent(big.transform, false);
            var brt = (RectTransform)bigRaw.transform;
            brt.anchoredPosition = new Vector2(0, -6);
            brt.sizeDelta = new Vector2(910, 910);
            bigRaw.GetComponent<RawImage>().texture = MapTexture;
            var mk = h.Img("Marker", bigRaw.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 34), HudStyle.Coral);
            mk.sprite = tri;
            bigMarker = mk.rectTransform;
            // legend
            var leg = h.Txt("Legend", big.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(900, 34), 24, TextAnchor.MiddleCenter, HudStyle.InkDim);
            leg.text = "<color=#FA5C52>▲</color> Aap / You      <color=#FFB829>◆</color> Jagah / Places      <color=#FFB829>M</color> Mission      <color=#33D1C2>●</color> Target";
            bigMap.SetActive(false);
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!p || mapRect == null) return;
            if (GameInput.MapPressed)
            {
                bool open = !bigMap.activeSelf;
                bigMap.SetActive(open);
                if (open)
                {
                    // over everything (the speedometer and the touch buttons were drawn on top of it), and small enough
                    // for the canvas: a phone's is only about 1000 high, and the 1040 map lost its title off the top
                    bigMap.transform.SetAsLastSibling();
                    float h = ((RectTransform)bigMap.transform.parent).rect.height;
                    bigMap.transform.localScale = Vector3.one * Mathf.Min(1f, (h - 24f) / 1040f);
                }
            }
            Vector3 pp = p.transform.position;
            float camYaw = ThirdPersonCamera.Instance ? ThirdPersonCamera.Instance.yaw : p.transform.eulerAngles.y;
            Vector2 uv = new Vector2((pp.x - WorldCx + WorldSize / 2f) / WorldSize, (pp.z - WorldCz + WorldSize / 2f) / WorldSize);
            mapRect.anchoredPosition = -(uv - new Vector2(0.5f, 0.5f)) * mapPx;
            rotator.localRotation = Quaternion.Euler(0, 0, camYaw);
            compass.localRotation = rotator.localRotation;
            foreach (var l in compassLetters) l.rectTransform.localRotation = Quaternion.Inverse(compass.localRotation);
            float heading = p.CurrentMode == PlayerController.Mode.Vehicle && p.Car ? p.Car.transform.eulerAngles.y : p.transform.eulerAngles.y;
            arrow.localRotation = Quaternion.Euler(0, 0, camYaw - heading);

            // health round the radar: green, gold under 60 %, coral under 30 %
            float hf = p.Health.Fraction;
            shownHealth = Mathf.MoveTowards(shownHealth, hf, Time.unscaledDeltaTime * 1.5f);
            healthFill.fillAmount = 0.75f * shownHealth;
            healthFill.color = hf > 0.6f ? HudStyle.Good : hf > 0.3f ? HudStyle.Marigold : HudStyle.Coral;

            float rpx = sizePx * 0.5f - 8f;
            foreach (var (pos, rt) in landmarks)
            {
                // only the ones in range: pinned to the edge, dozens of them read as a dotted ring (missions stay pinned)
                Vector2 d = new Vector2(pos.x - pp.x, pos.z - pp.z) / viewRadius * (sizePx * 0.5f);
                bool inside = d.magnitude < rpx;
                if (rt.gameObject.activeSelf != inside) rt.gameObject.SetActive(inside);
                if (inside) rt.anchoredPosition = d;
            }
            foreach (var e in EnemyAI.All)
                if (e && !enemies.Exists(x => x.e == e))
                {
                    var b = hud.Img("EnemyBlip", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12), HudStyle.Coral);
                    b.sprite = HudStyle.Disc;
                    b.raycastTarget = false;
                    enemies.Add((e, b.rectTransform));
                }
            foreach (var (e, rt) in enemies)
            {
                bool show = e && !e.Dead && e.isActiveAndEnabled;
                if (show)
                {
                    Vector2 d = new Vector2(e.transform.position.x - pp.x, e.transform.position.z - pp.z) / viewRadius * (sizePx * 0.5f);
                    show = d.magnitude < rpx;
                    rt.anchoredPosition = d;
                }
                if (rt.gameObject.activeSelf != show) rt.gameObject.SetActive(show);
            }
            UpdateMissionBlips(pp, rpx);
            if (bigMap.activeSelf)
            {
                bigMarker.anchoredPosition = (uv - new Vector2(0.5f, 0.5f)) * 910f;
                bigMarker.localRotation = Quaternion.Euler(0, 0, -heading);
            }
        }
    }
}
