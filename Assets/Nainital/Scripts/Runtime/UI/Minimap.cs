using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>Round radar in the corner (rotates with the camera) + full map on M, with landmark blips and enemies.</summary>
    public class Minimap : MonoBehaviour
    {
        public static Texture2D MapTexture;
        public static float WorldSize = 4000f;
        public static float WorldCx, WorldCz;   // the map square's centre (local metres)

        RectTransform rotator, mapRect, arrow, bigMarker;
        GameObject bigMap;
        float viewRadius = 230f, sizePx = 290f, mapPx;
        readonly List<(Vector3 pos, RectTransform rt)> landmarks = new List<(Vector3, RectTransform)>();
        readonly List<(EnemyAI e, RectTransform rt)> enemies = new List<(EnemyAI, RectTransform)>();
        HUD hud;
        Sprite circle, tri;
        RectTransform targetBlip;
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
                var b = hud.Img("TargetBlip", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20), new Color(0.3f, 1f, 0.4f));
                b.sprite = circle;
                b.raycastTarget = false;
                targetBlip = b.rectTransform;
            }
            var t = mm.CurrentTarget;
            targetBlip.gameObject.SetActive(t.HasValue);
            if (t.HasValue)
            {
                targetBlip.anchoredPosition = RadarPos(t.Value, pp, rpx, true);
                targetBlip.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
            }
            foreach (var (m, mk) in mm.Givers)
            {
                if (giverBlips.Exists(g => g.m == m)) continue;
                var bg = hud.Img("GiverBlip", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22), new Color(0.1f, 0.1f, 0.1f, 0.9f));
                bg.sprite = circle;
                bg.raycastTarget = false;
                var tx = hud.Txt("M", bg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22), 17, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
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

        static Sprite CircleSprite(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite TriangleSprite(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fy = y / (float)n, fx = Mathf.Abs(x / (float)n - 0.5f) * 2f;
                    t.SetPixel(x, y, new Color(1, 1, 1, fx < (1f - fy) ? 1f : 0f));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        public void Build(HUD h, Transform root, string[] names, Vector3[] positions)
        {
            hud = h;
            circle = CircleSprite(128);
            tri = TriangleSprite(64);
            var frame = h.Img("Radar", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(sizePx + 14, sizePx + 14), new Color(0, 0, 0, 0.75f));
            frame.sprite = circle;
            frame.raycastTarget = false;
            var maskImg = h.Img("RadarMask", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizePx, sizePx), Color.white);
            maskImg.sprite = circle;
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
            if (!MapTexture) raw.color = new Color(0.2f, 0.3f, 0.2f);

            if (names != null)
                for (int i = 0; i < names.Length; i++)
                {
                    if (positions[i] == Vector3.zero) continue;
                    var b = h.Img("Blip " + names[i], rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12), new Color(1f, 0.85f, 0.2f));
                    b.sprite = circle;
                    b.raycastTarget = false;
                    landmarks.Add((positions[i], b.rectTransform));
                }

            var arrowImg = h.Img("PlayerArrow", maskImg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 26), Color.white);
            arrowImg.sprite = tri;
            arrowImg.raycastTarget = false;
            arrow = arrowImg.rectTransform;
            var n = h.Txt("N", frame.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(40, 30), 22, TextAnchor.MiddleCenter, Color.white);
            n.text = "N";

            // full map
            var big = h.Img("BigMap", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 960), new Color(0, 0, 0, 0.85f));
            bigMap = big.gameObject;
            var bigRaw = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            bigRaw.transform.SetParent(big.transform, false);
            ((RectTransform)bigRaw.transform).sizeDelta = new Vector2(930, 930);
            bigRaw.GetComponent<RawImage>().texture = MapTexture;
            var mk = h.Img("Marker", bigRaw.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 30), new Color(1f, 0.25f, 0.2f));
            mk.sprite = tri;
            bigMarker = mk.rectTransform;
            var title = h.Txt("Title", big.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(900, 40), 28, TextAnchor.UpperCenter, Color.white);
            title.text = "NAINITAL  (M = band karo)";
            bigMap.SetActive(false);
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!p || mapRect == null) return;
            if (GameInput.MapPressed) bigMap.SetActive(!bigMap.activeSelf);
            Vector3 pp = p.transform.position;
            float camYaw = ThirdPersonCamera.Instance ? ThirdPersonCamera.Instance.yaw : p.transform.eulerAngles.y;
            Vector2 uv = new Vector2((pp.x - WorldCx + WorldSize / 2f) / WorldSize, (pp.z - WorldCz + WorldSize / 2f) / WorldSize);
            mapRect.anchoredPosition = -(uv - new Vector2(0.5f, 0.5f)) * mapPx;
            rotator.localRotation = Quaternion.Euler(0, 0, camYaw);
            float heading = p.CurrentMode == PlayerController.Mode.Vehicle && p.Car ? p.Car.transform.eulerAngles.y : p.transform.eulerAngles.y;
            arrow.localRotation = Quaternion.Euler(0, 0, camYaw - heading);

            float rpx = sizePx * 0.5f - 8f;
            foreach (var (pos, rt) in landmarks)
            {
                Vector2 d = new Vector2(pos.x - pp.x, pos.z - pp.z) / viewRadius * (sizePx * 0.5f);
                if (d.magnitude > rpx) d = d.normalized * rpx;   // pin to the edge like a GPS
                rt.anchoredPosition = d;
            }
            foreach (var e in EnemyAI.All)
                if (e && !enemies.Exists(x => x.e == e))
                {
                    var b = hud.Img("EnemyBlip", rotator, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11, 11), new Color(0.95f, 0.15f, 0.15f));
                    b.sprite = circle;
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
                bigMarker.anchoredPosition = (uv - new Vector2(0.5f, 0.5f)) * 930f;
                bigMarker.localRotation = Quaternion.Euler(0, 0, -heading);
            }
        }
    }
}
