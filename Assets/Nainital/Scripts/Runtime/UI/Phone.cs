using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The player's phone (owner, 21-22 Nov 2026: "phone UI tgda bnaao .. curved sides .. iphone ki tarah but not iphone ..
    /// crazy glassmorphism apps with smooth animations .. keypad .. phone me nainital ka wallpaper"). T on a PC, the teal
    /// phone button under the money, or "Phone" in the tray.
    ///
    /// A rounded titanium body with a punch-hole camera; the screen shows one of the game's own Nainital renders
    /// (Resources/Phone/wall_*.jpg, Tools/phone/make_phone_art.py) and every panel on it is frosted glass
    /// (Resources/Phone/UIGlass.shader samples a blurred copy of the wallpaper behind it). Home: clock, date, a weather
    /// glass, the apps, a dock. Apps: Phone (keypad; 112 / 100 / 108 / 101 go to EmergencyCalls' operator), SOS, Camera and
    /// Selfie (PhoneCamera), Gallery, Map, Weather, Wallet, Settings (wallpaper, language). Everything slides, springs and
    /// fades on unscaled time; every word goes through Loc.
    /// </summary>
    public class Phone : MonoBehaviour
    {
        public static Phone Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.open;

        public static void Toggle()
        {
            if (!Instance) return;
            if (Instance.open) Instance.Close(); else Instance.Open();
        }

        // ------------------------------------------------------------------ art
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Spr(string name, float border = 0f)
        {
            string key = name + border;
            if (sprites.TryGetValue(key, out var s) && s) return s;
            var tex = Resources.Load<Texture2D>("Phone/" + name);
            if (!tex) { Debug.LogWarning("[Nainital] phone art missing: " + name); return null; }
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprites[key] = s;
            return s;
        }
        static readonly string[] Walls = { "lake", "dawn", "mall_night" };
        static Texture2D Wall(int i, bool blur) => Resources.Load<Texture2D>("Phone/wall_" + Walls[Mathf.Clamp(i, 0, Walls.Length - 1)] + (blur ? "_blur" : ""));
        static int WallIndex { get => Mathf.Clamp(PlayerPrefs.GetInt("nt_phone_wall", 0), 0, Walls.Length - 1); set { PlayerPrefs.SetInt("nt_phone_wall", value); PlayerPrefs.Save(); } }

        Material glassLight, glassDark, glassClear;

        // ------------------------------------------------------------------ build
        HUD hud;
        Canvas rootCanvas;
        RectTransform phone, screen, pages, statusBar;
        CanvasGroup phoneGroup;
        RawImage wallpaper;
        Text statusClock;
        bool open, mobile;
        float W, H, SW, SH;
        Vector2 openPos, shutPos;
        const float Bezel = 11f;

        public void Build(HUD h, Transform root)
        {
            Instance = this;
            hud = h;
            rootCanvas = root.GetComponentInParent<Canvas>().rootCanvas;
            mobile = Application.isMobilePlatform || PlayerPrefs.GetInt("nt_touch", 0) == 1;
            H = mobile ? 960f : 900f;
            W = Mathf.Round(H * 0.478f);
            SW = W - Bezel * 2f;
            SH = H - Bezel * 2f;
            openPos = new Vector2(-60f, 44f);
            shutPos = new Vector2(-60f, -H - 120f);

            var sh = Shader.Find("Nainital/UIGlass");
            if (!sh) sh = Resources.Load<Shader>("Phone/UIGlass");
            Material M(float tint, float lift) { var m = new Material(sh); m.SetFloat("_Tint", tint); m.SetFloat("_Lift", lift); return m; }
            if (sh)
            {
                glassLight = M(0.20f, 0.10f);
                glassDark = M(0.55f, 0.02f);
                glassClear = M(0.08f, 0.16f);
            }

            phone = h.Rect("Phone", root, new Vector2(1, 0), new Vector2(1, 0), shutPos, new Vector2(W, H));
            phoneGroup = phone.gameObject.AddComponent<CanvasGroup>();
            var shadow = h.Img("Shadow", phone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, -10), new Vector2(W + 70, H + 70), new Color(0, 0, 0, 0.4f));
            shadow.sprite = Spr("shadow", 70); shadow.type = Image.Type.Sliced; shadow.raycastTarget = false;
            // side buttons (power on the right, volume on the left)
            Side(new Vector2(1, 0.5f), new Vector2(2.5f, 120f), new Vector2(5, 96));
            Side(new Vector2(0, 0.5f), new Vector2(-2.5f, 170f), new Vector2(5, 70));
            Side(new Vector2(0, 0.5f), new Vector2(-2.5f, 90f), new Vector2(5, 70));

            // the screen: a rounded mask holding the wallpaper and the pages
            var scr = h.Img("Screen", phone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H), Color.white);
            scr.sprite = Spr("screen", 120); scr.type = Image.Type.Sliced; scr.pixelsPerUnitMultiplier = 120f / 64f;
            scr.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            screen = h.Rect("ScreenArea", scr.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW, SH));
            wallpaper = new GameObject("Wallpaper", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            wallpaper.transform.SetParent(scr.transform, false);
            wallpaper.transform.SetSiblingIndex(0);
            Stretch(wallpaper.rectTransform);
            wallpaper.raycastTarget = false;
            pages = h.Rect("Pages", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW, SH));
            BuildStatusBar();
            var ind = h.Img("HomeBar", screen, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(130, 6), new Color(1, 1, 1, 0.85f));
            ind.sprite = Spr("pill", 18); ind.type = Image.Type.Sliced; ind.raycastTarget = false;
            var homeHit = h.Img("HomeHit", screen, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(220, 30), new Color(0, 0, 0, 0.001f));
            Tap(homeHit, () => { if (page != "home") { if (page == "call") HangUp(); else Home(); } });

            // the titanium body on top, and the punch-hole camera
            var frame = h.Img("Frame", phone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H), Color.white);
            frame.sprite = Spr("frame", 120); frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 120f / 64f; frame.raycastTarget = false;
            var cam = h.Img("PunchHole", phone, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -Bezel - 17f), new Vector2(15, 15), new Color(0.01f, 0.01f, 0.015f, 1f));
            cam.sprite = Spr("circle"); cam.raycastTarget = false;
            var lens = h.Img("Lens", cam.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1.5f, 1.5f), new Vector2(5, 5), new Color(0.25f, 0.3f, 0.5f, 0.8f));
            lens.sprite = Spr("circle"); lens.raycastTarget = false;

            phone.gameObject.SetActive(false);
            Loc.Changed += OnLanguage;
        }

        void OnDestroy() { Loc.Changed -= OnLanguage; }
        void OnLanguage() { if (open) Show(page); }

        void Side(Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var b = hud.Img("SideButton", phone, anchor, new Vector2(0.5f, 0.5f), pos, size, new Color(0.55f, 0.57f, 0.62f, 1f));
            b.sprite = Spr("pill", 18); b.type = Image.Type.Sliced; b.pixelsPerUnitMultiplier = 6f; b.raycastTarget = false;
        }

        void BuildStatusBar()
        {
            statusBar = hud.Rect("StatusBar", screen, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(SW, 40));
            statusClock = Label(statusBar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, -2), new Vector2(120, 34), 23, TextAnchor.MiddleLeft, Color.white, true);
            var ic = new[] { "battery", "wifi", "signal" };
            for (int i = 0; i < ic.Length; i++)
                Icon(statusBar, "ic_" + ic[i], new Vector2(1, 0.5f), new Vector2(-36 - i * 28, -2), i == 0 ? 28f : 20f, Color.white);
        }

        // ------------------------------------------------------------------ helpers
        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        Text Label(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false, bool shadow = true)
        {
            var rt = hud.Rect("Text", parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? HudStyle.Bold : HudStyle.Regular;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.35f);
                s.effectDistance = new Vector2(0, -1.5f);
            }
            return t;
        }

        Image Icon(Transform parent, string sprite, Vector2 anchor, Vector2 pos, float size, Color c)
        {
            var img = hud.Img("Icon", parent, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size), c);
            img.sprite = Spr(sprite);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A frosted glass panel (radius in screen pixels), with its light edge.</summary>
        Image Glass(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float radius, Color tint, Material mat = null)
        {
            var img = hud.Img("Glass", parent, anchor, pivot, pos, size, tint);
            img.sprite = Spr("glass", 46);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 46f / Mathf.Max(4f, radius);
            img.material = mat ? mat : glassLight;
            var edge = hud.Img("Edge", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(1, 1, 1, 0.55f));
            Stretch(edge.rectTransform);
            edge.sprite = Spr("glass_edge", 46);
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
            edge.raycastTarget = false;
            return img;
        }

        /// <summary>Glass in a round shape (keys, the avatar, round buttons).</summary>
        Image RoundGlass(Transform parent, Vector2 anchor, Vector2 pos, float d, Color tint, Material mat)
        {
            var img = Glass(parent, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(d, d), d * 0.5f, tint, mat);
            img.sprite = Spr("circle");
            img.type = Image.Type.Simple;
            var e = img.transform.Find("Edge").GetComponent<Image>();
            e.sprite = Spr("ring");
            e.type = Image.Type.Simple;
            e.color = new Color(1, 1, 1, 0.4f);
            return img;
        }

        Image Circle(Transform parent, Vector2 anchor, Vector2 pos, float d, Color c)
        {
            var img = hud.Img("Circle", parent, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(d, d), c);
            img.sprite = Spr("circle");
            return img;
        }

        /// <summary>Makes g a button: springs in under the finger, a ripple, the click and a tick.</summary>
        Button Tap(Graphic g, Action onClick, bool ripple = false)
        {
            var b = g.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var fx = g.gameObject.AddComponent<PhonePress>();
            fx.owner = this;
            fx.ripple = ripple;
            b.onClick.AddListener(() => { Haptics.Selection(); if (AudioManager.Instance) AudioManager.Instance.Click(); onClick(); });
            return b;
        }

        // ------------------------------------------------------------------ tweens (unscaled time)
        class Tw { public float t, dur, delay; public Action<float> f; public Func<float, float> ease; public Action done; public object tag; }
        readonly List<Tw> tws = new List<Tw>();
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InCubic(float t) => t * t * t;
        public static float OutBack(float t) { const float c1 = 1.55f, c3 = c1 + 1f; t -= 1f; return 1f + c3 * t * t * t + c1 * t * t; }
        public void Tween(float dur, Action<float> f, Func<float, float> ease = null, float delay = 0f, Action done = null, object tag = null)
        {
            if (tag != null) tws.RemoveAll(x => x.tag == tag);
            var tw = new Tw { dur = Mathf.Max(0.01f, dur), f = f, ease = ease ?? OutCubic, delay = delay, done = done, tag = tag };
            tws.Add(tw);
            if (delay <= 0f) { try { f(tw.ease(0f)); } catch { } }
        }

        void RunTweens()
        {
            float dt = Time.unscaledDeltaTime;
            var snapshot = tws.ToArray();
            foreach (var tw in snapshot)
            {
                if (!tws.Contains(tw)) continue;
                if (tw.delay > 0f) { tw.delay -= dt; continue; }
                tw.t += dt / tw.dur;
                float k = Mathf.Clamp01(tw.t);
                try { tw.f(tw.ease(k)); } catch { tw.t = 1f; }
                if (tw.t >= 1f)
                {
                    tws.Remove(tw);
                    try { tw.done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        // ------------------------------------------------------------------ open / close
        public void Open()
        {
            if (open || PhoneCamera.IsOpen || !phone) return;
            open = true;
            phone.gameObject.SetActive(true);
            phone.SetAsLastSibling();
            wallpaper.texture = Wall(WallIndex, false);
            GameInput.UiBlocking = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (call == null) Home(); else Show("call");
            Vector2 from = shutPos;
            Tween(0.5f, k =>
            {
                phone.anchoredPosition = Vector2.LerpUnclamped(from, openPos, k);
                phone.localRotation = Quaternion.Euler(0, 0, (1f - k) * 7f);
            }, OutBack, 0f, null, "phone");
            Tween(0.18f, k => phoneGroup.alpha = k, null, 0f, null, "phoneA");
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            GameInput.UiBlocking = FrontEnd.MenuOpen || (HUD.Instance && HUD.Instance.DialogOpen);
            Vector2 from = phone.anchoredPosition;
            Tween(0.28f, k =>
            {
                phone.anchoredPosition = Vector2.LerpUnclamped(from, shutPos, k);
                phone.localRotation = Quaternion.Euler(0, 0, k * 6f);
            }, InCubic, 0f, () => { if (!open) phone.gameObject.SetActive(false); }, "phone");
            if (call != null && call.node != null && call.node.End) call = null;
        }

        void Update()
        {
            RunTweens();
            var kb = Keyboard.current;
            if (kb != null && !PhoneCamera.IsOpen && !FrontEnd.MenuOpen)
            {
                if (kb.tKey.wasPressedThisFrame && (open || !GameInput.UiBlocking)) Toggle();
                else if (open && kb.escapeKey.wasPressedThisFrame) { if (page != "home" && page != "call") Back(); else Close(); }
                else if (open && page == "dialer") KeyboardDial(kb);
            }
            if (!open) return;
            string now = Clock();
            if (statusClock.text != now) statusClock.text = now;
            if (homeClock && homeClock.text != now) homeClock.text = now;
            UpdateCall();
        }

        void LateUpdate()
        {
            if (!phone || !phone.gameObject.activeSelf || !screen) return;
            // the glass samples the blurred wallpaper at its place on the screen (UIGlass.shader)
            var c = new Vector3[4];
            screen.GetWorldCorners(c);
            var ct = rootCanvas.transform;
            Vector3 a = ct.InverseTransformPoint(c[0]), b = ct.InverseTransformPoint(c[2]);
            Shader.SetGlobalVector("_NtPhoneRect", new Vector4(a.x, a.y, b.x, b.y));
            Shader.SetGlobalTexture("_NtPhoneBlur", Wall(WallIndex, true));
        }

        static string Clock()
        {
            var dn = DayNightCycle.Instance;
            if (!dn) return DateTime.Now.ToString("HH:mm");
            int hh = Mathf.FloorToInt(dn.timeOfDay) % 24, mm = Mathf.FloorToInt((dn.timeOfDay - Mathf.Floor(dn.timeOfDay)) * 60f);
            return $"{hh:00}:{mm:00}";
        }

        // ------------------------------------------------------------------ pages
        string page = "home";
        RectTransform current;
        readonly Stack<string> history = new Stack<string>();
        Text homeClock;
        RectTransform fromIcon;   // the app icon a page grows out of

        RectTransform NewPage(string name)
        {
            var p = hud.Rect("Page_" + name, pages, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW, SH));
            p.gameObject.AddComponent<CanvasGroup>();
            return p;
        }

        void Show(string name, RectTransform icon = null)
        {
            var old = current;
            string oldName = page;
            var oldIcon = fromIcon;
            page = name;
            homeClock = null;
            var p = NewPage(name);
            current = p;
            switch (name)
            {
                case "home": BuildHome(p); break;
                case "dialer": BuildDialer(p); break;
                case "call": BuildCall(p); break;
                case "gallery": BuildGallery(p); break;
                case "photo": BuildPhoto(p); break;
                case "settings": BuildSettings(p); break;
                case "weather": BuildWeather(p); break;
                case "wallet": BuildWallet(p); break;
                case "sos": BuildSos(p); break;
                case "stats": BuildStats(p); break;
                case "radio": BuildRadio(p); break;
            }
            if (current != p) return;   // the build moved on (a call that had ended went home)
            var g = p.GetComponent<CanvasGroup>();
            bool shrinkBack = old && name == "home" && oldName != "home" && oldName != "call" && oldIcon;
            if (icon && name != "home")
            {
                // the app grows out of its icon
                Vector2 at = pages.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
                float s0 = Mathf.Max(0.12f, icon.rect.width / SW);
                Tween(0.38f, k =>
                {
                    if (!p) return;
                    p.anchoredPosition = Vector2.LerpUnclamped(at, Vector2.zero, k);
                    p.localScale = Vector3.one * Mathf.LerpUnclamped(s0, 1f, k);
                    g.alpha = Mathf.Clamp01(k * 2.2f);
                }, OutCubic, 0f, null, p);
            }
            else if (!shrinkBack)
            {
                Tween(0.28f, k => { if (!p) return; g.alpha = k; p.anchoredPosition = new Vector2(0f, (1f - k) * -26f); }, OutCubic, 0f, null, p);
            }
            if (name == "home") StaggerIcons(p, shrinkBack ? 0.08f : 0.12f);
            if (old)
            {
                var og = old.GetComponent<CanvasGroup>();
                og.blocksRaycasts = false;
                Vector2 target = shrinkBack ? (Vector2)pages.InverseTransformPoint(oldIcon.TransformPoint(oldIcon.rect.center)) : Vector2.zero;
                float s1 = shrinkBack ? Mathf.Max(0.12f, oldIcon.rect.width / SW) : 0.94f;
                var oldRt = old;
                if (shrinkBack) old.SetAsLastSibling();   // the closing app shrinks back into its icon, over the home screen
                Tween(shrinkBack ? 0.32f : 0.22f, k =>
                {
                    if (!oldRt) return;
                    og.alpha = shrinkBack ? 1f - k * k : 1f - k;
                    oldRt.localScale = Vector3.one * Mathf.Lerp(1f, s1, k);
                    if (shrinkBack) oldRt.anchoredPosition = Vector2.Lerp(Vector2.zero, target, k);
                }, OutCubic, 0f, () => { if (oldRt) Destroy(oldRt.gameObject); }, old);
            }
            statusBar.SetAsLastSibling();
        }

        void StaggerIcons(RectTransform p, float delay0)
        {
            int i = 0;
            foreach (Transform t in p)
            {
                if (!t.name.StartsWith("App")) continue;
                var rt = (RectTransform)t;
                var cg = t.gameObject.GetComponent<CanvasGroup>();
                if (!cg) cg = t.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                rt.localScale = Vector3.one * 0.6f;
                Tween(0.45f, k => { if (!rt) return; rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, k); cg.alpha = Mathf.Clamp01(k * 1.6f); }, OutBack, delay0 + i * 0.03f);
                i++;
            }
        }

        void Go(string name, RectTransform icon)
        {
            history.Push(page);
            fromIcon = icon;
            Show(name, icon);
        }

        void Back()
        {
            string to = history.Count > 0 ? history.Pop() : "home";
            if (to == "call" || to == page) to = "home";
            if (to == "home") { Show("home"); fromIcon = null; return; }
            Show(to);
        }

        void Home()
        {
            history.Clear();
            Show("home");
            fromIcon = null;
        }

        /// <summary>A title row with a back chevron.</summary>
        void TopBar(RectTransform p, string title, Action back = null)
        {
            var hit = hud.Img("Back", p, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(40, -74), new Vector2(54, 54), new Color(1, 1, 1, 0.001f));
            Icon(hit.transform, "ic_back", new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white);
            Tap(hit, back ?? Back);
            Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -74), new Vector2(SW - 140, 50), 34, TextAnchor.MiddleCenter, Color.white, true).text = Loc.T(title);
        }

        // ------------------------------------------------------------------ home
        static readonly (string id, string label)[] GridApps =
        {
            ("selfie", "Selfie"), ("gallery", "Gallery"), ("weather", "Weather"), ("wallet", "Wallet"),
            ("settings", "Settings"), ("stats", "Stats"), ("radio", "Radio"),
        };
        static readonly string[] DockApps = { "phone", "sos", "camera", "map" };

        void BuildHome(RectTransform p)
        {
            homeClock = Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -128), new Vector2(SW, 120), 112, TextAnchor.MiddleCenter, Color.white, true);
            homeClock.text = Clock();
            Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -196), new Vector2(SW, 36), 26, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.92f), true).text = DateLine();
            // the weather glass
            var wg = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -238), new Vector2(SW - 44, 112), 30, Color.white);
            wg.name = "AppWeatherCard";
            Icon(wg.transform, "ic_weather_g", new Vector2(0, 0.5f), new Vector2(58, 0), 70, Color.white);
            Label(wg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(108, 18), new Vector2(SW - 200, 34), 25, TextAnchor.MiddleLeft, new Color(0.08f, 0.1f, 0.14f), true, false).text = Loc.T(Place());
            Label(wg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(108, -18), new Vector2(SW - 200, 32), 23, TextAnchor.MiddleLeft, new Color(0.12f, 0.15f, 0.2f), false, false).text = Loc.T(WeatherName()) + "   " + Temp() + "°C";
            var wrt = wg.rectTransform;
            Tap(wg, () => Go("weather", wrt));
            // apps
            float tile = 82f, gx = (SW - 40f) / 4f, y0 = -440f;
            for (int i = 0; i < GridApps.Length; i++)
            {
                var (id, label) = GridApps[i];
                float x = -((SW - 40f) * 0.5f) + gx * (i % 4) + gx * 0.5f;
                AppIcon(p, id, label, new Vector2(x, y0 - (i / 4) * 132f), tile, new Vector2(0.5f, 1));
            }
            // the dock
            var dock = Glass(p, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(SW - 30, 112), 40, Color.white, glassClear);
            dock.name = "Dock";
            dock.raycastTarget = false;
            for (int i = 0; i < DockApps.Length; i++)
            {
                float x = -((SW - 60f) * 0.5f) + (SW - 60f) / 4f * (i + 0.5f);
                AppIcon(p, DockApps[i], null, new Vector2(x, 86f), 78f, new Vector2(0.5f, 0));
            }
        }

        static string Place()
        {
            var tr = SaveTracker.Instance;   // the place the HUD names (the same as the pause menu)
            return tr && !string.IsNullOrEmpty(tr.Place) ? tr.Place : "Nainital";
        }

        void AppIcon(RectTransform p, string id, string label, Vector2 pos, float size, Vector2 anchor)
        {
            var holder = hud.Rect("App_" + id, p, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size + (label != null ? 34 : 0)));
            var tile = Glass(holder, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(size, size), size * 0.28f, new Color(0.06f, 0.08f, 0.12f, 1f), glassDark);
            Icon(tile.transform, "ic_" + id + "_g", new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.62f, Color.white);
            if (label != null)
                Label(holder, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -4), new Vector2(size + 40, 30), 20, TextAnchor.MiddleCenter, Color.white, true).text = Loc.T(label);
            var rt = tile.rectTransform;
            Tap(tile, () => Launch(id, rt));
        }

        void Launch(string id, RectTransform icon)
        {
            switch (id)
            {
                case "phone": Go("dialer", icon); break;
                case "sos": Go("sos", icon); break;
                case "gallery": Go("gallery", icon); break;
                case "settings": Go("settings", icon); break;
                case "weather": Go("weather", icon); break;
                case "wallet": Go("wallet", icon); break;
                case "stats": Go("stats", icon); break;
                case "radio": Go("radio", icon); break;
                case "map": Close(); GameInput.TouchMap = true; break;
                case "camera": Close(); PhoneCamera.Ensure().Open(false); break;
                case "selfie": Close(); PhoneCamera.Ensure().Open(true); break;
            }
        }

        static readonly string[] Days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        static readonly string[] Months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        static string DateLine()
        {
            var d = DateTime.Now;
            return Loc.T(Days[(int)d.DayOfWeek]) + ", " + d.Day + " " + Loc.T(Months[d.Month - 1]);
        }

        static string WeatherName()
        {
            var ws = WeatherSystem.Instance;
            return ws ? new[] { "Saaf", "Baadal", "Kohra", "Baarish" }[(int)ws.current] : "Saaf";
        }

        /// <summary>A believable Nainital temperature for the hour and the weather (about 1-17 degrees in this season).</summary>
        static int Temp(float? hourOverride = null)
        {
            var dn = DayNightCycle.Instance;
            float h = hourOverride ?? (dn ? dn.timeOfDay : 14f);
            float t = 9f + 7f * Mathf.Sin((h - 9f) / 24f * Mathf.PI * 2f);
            var ws = WeatherSystem.Instance;
            if (ws) t -= new[] { 0f, 1.5f, 2.5f, 4f }[(int)ws.current];
            return Mathf.RoundToInt(t);
        }

        // ------------------------------------------------------------------ dialer (keypad)
        string number = "";
        Text numberText, numberSub;
        Image backspace;

        void BuildDialer(RectTransform p)
        {
            TopBar(p, "Phone");
            numberText = Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(SW - 40, 64), 56, TextAnchor.MiddleCenter, Color.white, true);
            numberSub = Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -194), new Vector2(SW - 40, 30), 22, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.9f), true);
            // quick emergency chips
            string[] quick = { "112", "100", "108", "101" };
            float cw = (SW - 70f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                string n = quick[i];
                var chip = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-((SW - 70f) * 0.5f) + cw * (i + 0.5f), -244), new Vector2(cw - 10, 44), 22, new Color(0.7f, 0.12f, 0.12f), glassDark);
                Label(chip.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cw, 40), 24, TextAnchor.MiddleCenter, Color.white, true, false).text = n;
                Tap(chip, () => { number = n; ShowNumber(); });
            }
            // the keys
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" };
            string[] letters = { "", "ABC", "DEF", "GHI", "JKL", "MNO", "PQRS", "TUV", "WXYZ", "", "+", "" };
            float kd = 84f, gx = 108f, gy = 100f, ky0 = -330f;
            for (int i = 0; i < 12; i++)
            {
                string k = keys[i];
                var key = RoundGlass(p, new Vector2(0.5f, 1), new Vector2((i % 3 - 1) * gx, ky0 - (i / 3) * gy), kd, Color.white, glassClear);
                Label(key.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, letters[i].Length > 0 ? 8 : 0), new Vector2(kd, 50), 40, TextAnchor.MiddleCenter, Color.white, true).text = k;
                if (letters[i].Length > 0)
                    Label(key.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(kd, 20), 15, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f), true).text = letters[i];
                Tap(key, () => Digit(k), true);
            }
            // call + backspace
            var callBtn = Circle(p, new Vector2(0.5f, 0), new Vector2(0, 98), 84, new Color(0.18f, 0.82f, 0.45f));
            Icon(callBtn.transform, "ic_call", new Vector2(0.5f, 0.5f), Vector2.zero, 46, Color.white);
            Tap(callBtn, () => { if (number.Length > 0) StartCall(number); }, true);
            backspace = hud.Img("Backspace", p, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(gx, 98), new Vector2(64, 64), new Color(1, 1, 1, 0.001f));
            Icon(backspace.transform, "ic_backspace", new Vector2(0.5f, 0.5f), Vector2.zero, 40, Color.white);
            Tap(backspace, () => { if (number.Length > 0) { number = number.Substring(0, number.Length - 1); ShowNumber(); } });
            ShowNumber();
        }

        void Digit(string k)
        {
            if (number.Length >= 14) return;
            number += k;
            ShowNumber();
        }

        void KeyboardDial(Keyboard kb)
        {
            for (int d = 0; d <= 9; d++)
            {
                var key = kb[(Key)((int)Key.Digit0 + d)];
                var pad = kb[(Key)((int)Key.Numpad0 + d)];
                if (key.wasPressedThisFrame || pad.wasPressedThisFrame) { Digit(d.ToString()); if (AudioManager.Instance) AudioManager.Instance.Click(); }
            }
            if (kb.backspaceKey.wasPressedThisFrame && number.Length > 0) { number = number.Substring(0, number.Length - 1); ShowNumber(); }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && number.Length > 0) StartCall(number);
        }

        void ShowNumber()
        {
            if (!numberText) return;
            numberText.text = number;
            numberText.fontSize = number.Length > 10 ? 40 : 56;
            numberSub.text = number switch { "112" => Loc.T("112 Emergency"), "100" => Loc.T("Police"), "108" => Loc.T("Ambulance"), "101" => Loc.T("Fire brigade"), _ => "" };
            if (backspace) backspace.gameObject.SetActive(number.Length > 0);
        }

        // ------------------------------------------------------------------ calls
        class Call { public string number, who; public CallNode node; public float t0, ringUntil, typed; public bool ended; public float endedAt; }
        Call call;
        Text callStatus, callLine, callWho;
        RectTransform repliesRoot, ringA, ringB;
        CanvasGroup repliesGroup;
        string lineFull = "";

        void StartCall(string n)
        {
            number = "";
            call = new Call { number = n, node = EmergencyCalls.Dial(n), t0 = Time.unscaledTime, ringUntil = Time.unscaledTime + 1.6f };
            call.who = call.node.who;
            history.Clear();
            fromIcon = null;
            Show("call");
        }

        void BuildCall(RectTransform p)
        {
            if (call == null) { Home(); return; }
            // a darker glass over the whole screen, so the call reads
            var shade = hud.Img("Shade", p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW, SH), new Color(0.03f, 0.05f, 0.08f));
            shade.material = glassDark;
            shade.raycastTarget = false;
            Stretch(shade.rectTransform);
            callWho = Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -98), new Vector2(SW - 40, 76), 32, TextAnchor.MiddleCenter, Color.white, true);
            callWho.text = string.IsNullOrEmpty(call.who) ? call.number : Loc.T(call.who);
            callStatus = Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -146), new Vector2(SW - 40, 30), 23, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f), true);
            // avatar with pulsing rings while it rings
            Vector2 av = new Vector2(0, -262);
            var ra = Circle(p, new Vector2(0.5f, 1), av, 132, new Color(0.35f, 1f, 0.75f, 0f)); ra.sprite = Spr("ring"); ra.raycastTarget = false; ringA = ra.rectTransform;
            var rb = Circle(p, new Vector2(0.5f, 1), av, 132, new Color(0.35f, 1f, 0.75f, 0f)); rb.sprite = Spr("ring"); rb.raycastTarget = false; ringB = rb.rectTransform;
            var avatar = RoundGlass(p, new Vector2(0.5f, 1), av, 132, Color.white, glassLight);
            avatar.raycastTarget = false;
            bool police = call.number == "100";
            string glyph = police ? "ic_user" : call.number == "108" ? "ic_sos_g" : call.number == "101" ? "ic_weather_g" : "ic_phone_g";
            Icon(avatar.transform, glyph, new Vector2(0.5f, 0.5f), Vector2.zero, 70, police ? new Color(0.2f, 0.3f, 0.55f) : Color.white);
            // the operator's words in a glass bubble, the replies under it
            var bubble = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(SW - 40, 196), 26, Color.white, glassLight);
            bubble.raycastTarget = false;
            callLine = Label(bubble.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW - 76, 176), 25, TextAnchor.MiddleLeft, new Color(0.06f, 0.08f, 0.12f), false, false);
            repliesRoot = hud.Rect("Replies", p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -560), new Vector2(SW - 40, 260));
            repliesGroup = repliesRoot.gameObject.AddComponent<CanvasGroup>();
            // end call
            var end = Circle(p, new Vector2(0.5f, 0), new Vector2(0, 92), 84, new Color(0.95f, 0.22f, 0.22f));
            Icon(end.transform, "ic_endcall", new Vector2(0.5f, 0.5f), Vector2.zero, 46, Color.white);
            Tap(end, HangUp, true);
            lineFull = "";
            ShowLine();
        }

        /// <summary>The current line: typed out once the ringing stops, its replies fading in after it.</summary>
        void ShowLine()
        {
            if (call == null || !callLine) return;
            bool ringing = Time.unscaledTime < call.ringUntil;
            lineFull = ringing ? "" : Loc.T(call.node.line);
            call.typed = 0f;
            callLine.text = "";
            foreach (Transform c in repliesRoot) Destroy(c.gameObject);
            repliesGroup.alpha = 0f;
            repliesGroup.blocksRaycasts = false;
            if (ringing) return;
            float y = 0f;
            var replies = call.node.replies;
            for (int i = 0; i < replies.Count; i++)
            {
                var next = replies[i].next;
                var b = Glass(repliesRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(SW - 40, 64), 32, new Color(0.10f, 0.55f, 0.95f), glassDark);
                Label(b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW - 76, 60), 22, TextAnchor.MiddleCenter, Color.white, true, false).text = Loc.T(replies[i].label);
                Tap(b, () =>
                {
                    if (call == null) return;
                    call.node = next();
                    if (!string.IsNullOrEmpty(call.node.who)) call.who = call.node.who;
                    if (callWho) callWho.text = Loc.T(call.who);
                    ShowLine();
                });
                y += 74f;
            }
            if (call.node.End)
            {
                var ok = Glass(repliesRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(SW - 40, 64), 32, Color.white, glassLight);
                Label(ok.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW - 76, 60), 24, TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.14f), true, false).text = Loc.T("Theek hai");
                Tap(ok, HangUp);
            }
        }

        void UpdateCall()
        {
            if (page != "call" || call == null || !callStatus) return;
            float now = Time.unscaledTime;
            bool ringing = now < call.ringUntil;
            if (call.ended) callStatus.text = Loc.T("Call khatam");
            else if (ringing) callStatus.text = Loc.T("Call lag rahi hai...");
            else
            {
                int s = Mathf.FloorToInt(now - call.ringUntil);
                callStatus.text = $"{s / 60:00}:{s % 60:00}";
            }
            // the rings pulse while it rings
            float ph = (now - call.t0) / 1.3f;
            for (int i = 0; i < 2; i++)
            {
                var r = i == 0 ? ringA : ringB;
                if (!r) continue;
                float k = Mathf.Repeat(ph + i * 0.5f, 1f);
                r.localScale = Vector3.one * (1f + k * 0.75f);
                r.GetComponent<Image>().color = new Color(0.35f, 1f, 0.75f, ringing ? (1f - k) * 0.7f : 0f);
            }
            if (!ringing && lineFull.Length == 0 && call.node != null && !string.IsNullOrEmpty(call.node.line)) ShowLine();
            // the operator speaks: the words type themselves out, then the replies fade in
            if (!ringing && callLine && lineFull.Length > 0 && callLine.text.Length < lineFull.Length)
            {
                call.typed += Time.unscaledDeltaTime * 60f;
                int n = Mathf.Min(lineFull.Length, Mathf.FloorToInt(call.typed));
                callLine.text = lineFull.Substring(0, n);
                if (n >= lineFull.Length)
                {
                    var rg = repliesGroup;
                    Tween(0.3f, k => { if (rg) { rg.alpha = k; rg.blocksRaycasts = k > 0.5f; } }, OutCubic, 0.05f);
                }
            }
            if (call.node != null && call.node.End && !call.ended && lineFull.Length > 0 && callLine && callLine.text.Length >= lineFull.Length)
            {
                call.ended = true;
                call.endedAt = now;
            }
            if (call.ended && now - call.endedAt > 6f) HangUp();
        }

        void HangUp()
        {
            call = null;
            if (open) Home();
        }

        // ------------------------------------------------------------------ for play tests (Editor/PhoneTest)
        public void TestApp(string id) { if (!open) Open(); Launch(id, null); }
        public void TestType(string digits) { if (!open) Open(); if (page != "dialer") Go("dialer", null); number = digits; ShowNumber(); }
        public void TestCall() { if (number.Length > 0) StartCall(number); }
        public void TestReply(int i) { if (call != null && call.node != null && i < call.node.replies.Count) { call.node = call.node.replies[i].next(); ShowLine(); } }
        public string Page => page;

        // ------------------------------------------------------------------ SOS
        void BuildSos(RectTransform p)
        {
            TopBar(p, "SOS");
            (string n, string label)[] rows = { ("112", "112 Emergency"), ("100", "100 Police Control Room"), ("108", "108 Ambulance"), ("101", "101 Fire Control") };
            for (int i = 0; i < rows.Length; i++)
            {
                var (n, label) = rows[i];
                bool red = i == 0;
                var r = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130 - i * 98), new Vector2(SW - 40, 84), 26, red ? new Color(0.75f, 0.1f, 0.12f) : Color.white, red ? glassDark : glassLight);
                var dot = Circle(r.transform, new Vector2(0, 0.5f), new Vector2(46, 0), 54, new Color(0.95f, 0.25f, 0.25f));
                dot.raycastTarget = false;
                Icon(dot.transform, "ic_call", new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white);
                Color ink = red ? Color.white : new Color(0.06f, 0.08f, 0.12f);
                Label(r.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(88, 13), new Vector2(SW - 150, 34), 27, TextAnchor.MiddleLeft, ink, true, false).text = n;
                Label(r.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(88, -16), new Vector2(SW - 150, 28), 20, TextAnchor.MiddleLeft, ink, true, false).text = Loc.T(label);
                Tap(r, () => StartCall(n));
            }
            Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -540), new Vector2(SW - 60, 80), 21, TextAnchor.UpperCenter, new Color(1, 1, 1, 0.9f), true).text = Loc.T("Jhoothi call pe police aayegi aur challan katega.");
        }

        // ------------------------------------------------------------------ gallery
        readonly List<Texture2D> thumbs = new List<Texture2D>();
        string photoPath;

        void FreeThumbs() { foreach (var t in thumbs) if (t) Destroy(t); thumbs.Clear(); }

        Image RoundedPicture(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float radius, Texture tex)
        {
            var frame = hud.Img("Picture", parent, anchor, pivot, pos, size, Color.white);
            frame.sprite = Spr("glass", 46); frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 46f / radius;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var raw = new GameObject("Img", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(frame.transform, false);
            Stretch(raw.rectTransform);
            raw.texture = tex;
            if (tex) raw.uvRect = Cover(tex, size.x, size.y);
            raw.raycastTarget = false;
            return frame;
        }

        void BuildGallery(RectTransform p)
        {
            FreeThumbs();
            TopBar(p, "Gallery");
            var files = PhoneCamera.Photos();
            if (files.Count == 0)
            {
                var g = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(SW - 40, 120), 26, Color.white);
                Label(g.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW - 80, 100), 24, TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.14f), true, false).text = Loc.T("Abhi koi photo nahi. Camera se kheencho!");
                return;
            }
            float cell = (SW - 48f) / 3f;
            int max = Mathf.Min(files.Count, 15);
            for (int i = 0; i < max; i++)
            {
                string f = files[i];
                var tex = LoadThumb(f, 220);
                if (!tex) continue;
                thumbs.Add(tex);
                var pic = RoundedPicture(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18 + (i % 3) * (cell + 6), -122 - (i / 3) * (cell * 1.25f + 6)), new Vector2(cell, cell * 1.25f), 18f, tex);
                var rt = pic.rectTransform;
                Tap(pic, () => { photoPath = f; Go("photo", rt); });
            }
        }

        void BuildPhoto(RectTransform p)
        {
            TopBar(p, "Photo");
            if (string.IsNullOrEmpty(photoPath) || !File.Exists(photoPath)) return;
            var tex = LoadThumb(photoPath, 900);
            if (!tex) return;
            thumbs.Add(tex);
            float w = SW - 24f, h = w * tex.height / Mathf.Max(1f, tex.width);
            RoundedPicture(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(w, h), 20f, tex).raycastTarget = false;
            Label(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, 20 - h * 0.5f - 12), new Vector2(SW - 40, 28), 19, TextAnchor.UpperCenter, new Color(1, 1, 1, 0.8f)).text = Path.GetFileNameWithoutExtension(photoPath);
            var del = RoundGlass(p, new Vector2(0.5f, 0), new Vector2(0, 98), 76, new Color(0.9f, 0.2f, 0.2f), glassDark);
            Icon(del.transform, "ic_trash", new Vector2(0.5f, 0.5f), Vector2.zero, 38, Color.white);
            string f = photoPath;
            Tap(del, () => { try { File.Delete(f); } catch { } history.Clear(); history.Push("home"); fromIcon = null; Show("gallery"); });
        }

        static Rect Cover(Texture t, float w, float h)
        {
            float ta = t.width / (float)t.height, ra = w / h;
            if (ta > ra) { float u = ra / ta; return new Rect((1f - u) * 0.5f, 0f, u, 1f); }
            float v = ta / ra;
            return new Rect(0f, (1f - v) * 0.5f, 1f, v);
        }

        /// <summary>A photo read and shrunk on the GPU to about maxW wide (full-size shots would eat a phone's memory).</summary>
        static Texture2D LoadThumb(string path, int maxW)
        {
            try
            {
                var full = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!full.LoadImage(File.ReadAllBytes(path))) { Destroy(full); return null; }
                if (full.width <= maxW) return full;
                int w = maxW, hh = Mathf.Max(1, full.height * maxW / full.width);
                var rtex = RenderTexture.GetTemporary(w, hh, 0);
                Graphics.Blit(full, rtex);
                var prev = RenderTexture.active;
                RenderTexture.active = rtex;
                var small = new Texture2D(w, hh, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, hh), 0, 0);
                small.Apply(false, true);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rtex);
                Destroy(full);
                return small;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ settings
        void BuildSettings(RectTransform p)
        {
            TopBar(p, "Settings");
            Label(p, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(26, -132), new Vector2(SW - 52, 32), 24, TextAnchor.MiddleLeft, Color.white, true).text = Loc.T("Wallpaper");
            float tw = (SW - 72f) / 3f, th = tw * 2.05f;
            for (int i = 0; i < Walls.Length; i++)
            {
                int k = i;
                bool on = WallIndex == i;
                if (on)
                {
                    var ring = hud.Img("Selected", p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(19 + i * (tw + 12), -153), new Vector2(tw + 10, th + 10), new Color(0.3f, 1f, 0.8f, 1f));
                    ring.sprite = Spr("glass", 46); ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 46f / 20f; ring.raycastTarget = false;
                }
                var pic = RoundedPicture(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + i * (tw + 12), -158), new Vector2(tw, th), 16f, Wall(i, false));
                Tap(pic, () => { WallIndex = k; wallpaper.texture = Wall(k, false); Show("settings"); });
            }
            float y = -158 - th - 46;
            Label(p, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(26, y), new Vector2(SW - 52, 32), 24, TextAnchor.MiddleLeft, Color.white, true).text = Loc.T("Bhasha");
            string[] opts = { "ENGLISH", "HINDI_NAME" };
            float ow = (SW - 60f) / 2f;
            for (int i = 0; i < 2; i++)
            {
                bool on = (i == 1) == Loc.Hindi;
                var b = Glass(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + i * (ow + 12), y - 28), new Vector2(ow, 62), 31, on ? new Color(0.15f, 0.75f, 0.55f) : Color.white, on ? glassDark : glassLight);
                Label(b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ow, 58), 24, TextAnchor.MiddleCenter, on ? Color.white : new Color(0.08f, 0.1f, 0.14f), true, false).text = Loc.T(opts[i]);
                int k = i;
                Tap(b, () => Loc.Set(k == 1 ? Loc.Lang.Hindi : Loc.Lang.English));
            }
        }

        // ------------------------------------------------------------------ weather
        void BuildWeather(RectTransform p)
        {
            TopBar(p, "Weather");
            Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -142), new Vector2(SW - 40, 40), 30, TextAnchor.MiddleCenter, Color.white, true).text = Loc.T(Place());
            Icon(p, "ic_weather_g", new Vector2(0.5f, 1), new Vector2(0, -250), 150, Color.white);
            Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -378), new Vector2(SW, 110), 104, TextAnchor.MiddleCenter, Color.white, true).text = Temp() + "°";
            Label(p, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -446), new Vector2(SW, 36), 28, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.9f), true).text = Loc.T(WeatherName());
            // the next hours
            var row = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -490), new Vector2(SW - 40, 130), 28, Color.white);
            row.raycastTarget = false;
            var dn = DayNightCycle.Instance;
            float h0 = dn ? Mathf.Floor(dn.timeOfDay) : 12f;
            float cw = (SW - 40f) / 5f;
            for (int i = 0; i < 5; i++)
            {
                float hr = (h0 + i * 2f) % 24f;
                float x = -((SW - 40f) * 0.5f) + cw * (i + 0.5f);
                Label(row.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x, -30), new Vector2(cw, 28), 20, TextAnchor.MiddleCenter, new Color(0.1f, 0.12f, 0.18f), true, false).text = i == 0 ? Loc.T("Abhi") : $"{(int)hr:00}:00";
                Label(row.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x, -86), new Vector2(cw, 40), 32, TextAnchor.MiddleCenter, new Color(0.06f, 0.08f, 0.12f), true, false).text = Temp(hr) + "°";
            }
        }

        // ------------------------------------------------------------------ stats (PlayerStats)
        /// <summary>The tray's "Stats" button: the phone opens straight on the stats.</summary>
        public void OpenStats() { if (!open) Open(); history.Clear(); history.Push("home"); fromIcon = null; Show("stats"); }

        void BuildStats(RectTransform p)
        {
            TopBar(p, "Stats");
            (string label, float v01, Color c)[] rows =
            {
                ("Bhookh", PlayerStats.Hunger01, new Color(1f, 0.62f, 0.25f)),
                ("Saans / taakat", PlayerStats.Energy01, new Color(0.35f, 0.9f, 1f)),
                ("Stamina", PlayerStats.Get01(PlayerStats.Stat.Stamina), new Color(0.4f, 1f, 0.6f)),
                ("Muscle", PlayerStats.Get01(PlayerStats.Stat.Muscle), new Color(1f, 0.4f, 0.45f)),
                ("Fat", PlayerStats.Get01(PlayerStats.Stat.Fat), new Color(1f, 0.85f, 0.35f)),
                ("Driving", PlayerStats.Get01(PlayerStats.Stat.Driving), new Color(0.6f, 0.7f, 1f)),
                ("Bike", PlayerStats.Get01(PlayerStats.Stat.Bike), new Color(0.85f, 0.6f, 1f)),
                ("Swimming", PlayerStats.Get01(PlayerStats.Stat.Swimming), new Color(0.3f, 0.75f, 1f)),
            };
            var card = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(SW - 36, rows.Length * 72f + 26f), 28, Color.white, glassLight);
            card.raycastTarget = false;
            for (int i = 0; i < rows.Length; i++)
            {
                var (label, v, c) = rows[i];
                float y = -22f - i * 72f;
                Label(card.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, y), new Vector2(SW - 140, 30), 23, TextAnchor.MiddleLeft, new Color(0.07f, 0.09f, 0.13f), true, false).text = Loc.T(label);
                Label(card.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, y), new Vector2(90, 30), 23, TextAnchor.MiddleRight, new Color(0.07f, 0.09f, 0.13f), true, false).text = Mathf.RoundToInt(v * 100f) + (i < 2 ? "%" : "");
                var track = hud.Img("Track", card.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, y - 34), new Vector2(SW - 80, 12), new Color(0, 0, 0, 0.12f));
                track.sprite = Spr("pill", 18); track.type = Image.Type.Sliced; track.pixelsPerUnitMultiplier = 3f; track.raycastTarget = false;
                var fill = hud.Img("Fill", track.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0f, 12), c);
                fill.sprite = Spr("pill", 18); fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 3f; fill.raycastTarget = false;
                var frt = fill.rectTransform;
                float target = Mathf.Max(12f, (SW - 80f) * Mathf.Clamp01(v));
                Tween(0.6f, k => { if (frt) frt.sizeDelta = new Vector2(target * k, 12f); }, OutCubic, 0.15f + i * 0.04f);
            }
            if (PlayerStats.Hungry)
                Label(p, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(SW - 50, 60), 21, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.6f), true).text = Loc.T("Bhookh lagi hai - kisi stall ya restaurant mein kuch khao");
        }

        // ------------------------------------------------------------------ radio (Audio/Radio.cs)
        void BuildRadio(RectTransform p)
        {
            TopBar(p, "Radio");
            var r = Radio.Instance;
            if (!r) return;
            var keys = r.StationKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                bool on = i == r.CurrentIndex;
                var row = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -124 - i * 84), new Vector2(SW - 40, 72), 26, on ? new Color(1f, 0.45f, 0.3f) : Color.white, on ? glassDark : glassLight);
                Label(row.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(SW - 100, 60), 26, TextAnchor.MiddleLeft, on ? Color.white : new Color(0.07f, 0.09f, 0.13f), true, false).text =
                    Loc.T(keys[i]) + (i == 0 && r.UserCount > 0 ? "  (" + r.UserCount + ")" : "");
                int k = i;
                Tap(row, () => { r.Select(k); Show("radio"); });
            }
            Label(p, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(SW - 50, 90), 20, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.9f), true).text =
                Loc.T(Application.isMobilePlatform ? "Apne gaane: Files app > NainiTA > Music. Gaadi mein radio bajta hai." : "Apne gaane: Documents\\NainiTA\\Music. Gaadi mein radio bajta hai (R se station badlo).");
        }

        // ------------------------------------------------------------------ wallet
        void BuildWallet(RectTransform p)
        {
            TopBar(p, "Wallet");
            var card = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(SW - 40, 220), 30, new Color(0.95f, 0.55f, 0.2f), glassDark);
            card.raycastTarget = false;
            Label(card.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -20), new Vector2(SW - 90, 30), 22, TextAnchor.MiddleLeft, Color.white, true, false).text = Loc.T("Nainital Sukoon Bank");
            Label(card.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -60), new Vector2(SW - 90, 26), 20, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.85f), true, false).text = Loc.T("Balance");
            Label(card.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -92), new Vector2(SW - 90, 80), 72, TextAnchor.MiddleLeft, Color.white, true, false).text = HudStyle.Rupees(GameState.Money);
            Label(card.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-26, 18), new Vector2(200, 26), 20, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.8f), true, false).text = "**** 2207";
            var tip = Glass(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -372), new Vector2(SW - 40, 100), 26, Color.white);
            tip.raycastTarget = false;
            Label(tip.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SW - 80, 90), 22, TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.14f), true, false).text = Loc.T("Paise missions se kamao. Jurmana, ilaaj aur petrol isi se katte hain.");
        }
    }

    /// <summary>The phone's buttons spring under the finger (and the keypad's keys ripple).</summary>
    public class PhonePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Phone owner;
        public bool ripple;
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            var rt = (RectTransform)transform;
            owner.Tween(0.12f, k => { if (rt) rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, k); }, Phone.OutCubic, 0f, null, this);
            if (ripple) Ripple();
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) { if (down) Release(); }

        void Release()
        {
            down = false;
            var rt = (RectTransform)transform;
            if (!rt) return;
            float from = rt.localScale.x;
            owner.Tween(0.35f, k => { if (rt) rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, k); }, Phone.OutBack, 0f, null, this);
        }

        void Ripple()
        {
            var rt = (RectTransform)transform;
            var go = new GameObject("Ripple", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = rt.rect.size;
            var img = go.GetComponent<Image>();
            img.sprite = Phone.Spr("circle");
            img.raycastTarget = false;
            owner.Tween(0.45f, k =>
            {
                if (!r) return;
                r.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.25f, k);
                img.color = new Color(1, 1, 1, (1f - k) * 0.4f);
            }, Phone.OutCubic, 0f, () => { if (go) Destroy(go); });
        }
    }
}
