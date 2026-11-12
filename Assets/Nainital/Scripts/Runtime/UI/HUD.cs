using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// Whole HUD built in code: health, money, time, Hindi/English toggle, mission objective + timer, banners,
    /// prompts, dialogs, toasts, enemy bars, radar, speedometer, pause menu (missions, graphics, weather), touch controls.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        Font font;
        Canvas canvas;
        Image healthFill, damageFlash;
        Text locationText, timeText, promptText, toastText, dialogTitle, dialogBody, helpText, speedText, moneyText;
        Text objTitle, objText, timerText, bannerTitle, bannerSub, missionList, qualityLabel, langHindi, langEnglish;
        GameObject dialogPanel, promptPanel, helpPanel, objPanel, pausePanel, missionsPanel;
        CanvasGroup locationGroup, bannerGroup;
        float toastTimer, locationTimer, flash, bannerTimer;
        string toastSrc = "", locationSrc = "", dialogTitleSrc = "", dialogBodySrc = "", bannerTitleSrc = "", bannerSubSrc = "";
        readonly List<(EnemyAI e, Image bg, Image fill)> enemyBars = new List<(EnemyAI, Image, Image)>();
        readonly List<(Text t, string src)> locTexts = new List<(Text, string)>();
        bool showTouch;
        int quality = 2;

        void Awake()
        {
            Instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameAssets.Font = font;
            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            quality = PlayerPrefs.GetInt("nt_quality", Application.isMobilePlatform ? 1 : 2);
            Build();
            RefreshLanguage();
        }

        void Start() => ApplyQuality(quality);

        void OnEnable()
        {
            GameEvents.Toast += OnToast;
            GameEvents.Dialog += OnDialog;
            GameEvents.LocationChanged += OnLocation;
            GameEvents.Banner += OnBanner;
            Loc.Changed += RefreshLanguage;
        }

        void OnDisable()
        {
            GameEvents.Toast -= OnToast;
            GameEvents.Dialog -= OnDialog;
            GameEvents.LocationChanged -= OnLocation;
            GameEvents.Banner -= OnBanner;
            Loc.Changed -= RefreshLanguage;
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ helpers
        public RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
        {
            var img = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c)
        {
            var t = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.75f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        Text LocTxt(Text t, string src) { locTexts.Add((t, src)); t.text = Loc.T(src); return t; }

        public Button Btn(string label, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, System.Action onClick, int fontSize = 26)
        {
            var img = Img("Btn_" + label, parent, anchor, new Vector2(0.5f, 0.5f), pos, size, new Color(0.05f, 0.08f, 0.1f, 0.72f));
            var b = img.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => onClick());
            var t = Txt("Label", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
            LocTxt(t, label);
            return b;
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            Transform root = canvas.transform;
            damageFlash = Img("DamageFlash", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0.8f, 0, 0, 0));
            damageFlash.raycastTarget = false;

            // health
            var hb = Img("HealthBg", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(420, 26), new Color(0, 0, 0, 0.55f));
            hb.raycastTarget = false;
            healthFill = Img("HealthFill", hb.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(3, 0), new Vector2(414, 20), new Color(0.85f, 0.2f, 0.2f));
            healthFill.raycastTarget = false;
            Txt("HealthLabel", hb.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 26), new Vector2(300, 30), 22, TextAnchor.MiddleLeft, Color.white).text = "Health";

            // mission objective panel
            var op = Img("Objective", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -78), new Vector2(560, 120), new Color(0, 0, 0, 0.5f));
            op.raycastTarget = false;
            objPanel = op.gameObject;
            objTitle = Txt("ObjTitle", op.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(530, 36), 28, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.3f));
            objText = Txt("ObjText", op.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -44), new Vector2(530, 70), 25, TextAnchor.UpperLeft, Color.white);
            timerText = Txt("Timer", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(300, 60), 48, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
            objPanel.SetActive(false);

            // time, money, buttons (top right)
            timeText = Txt("Time", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -22), new Vector2(380, 40), 30, TextAnchor.UpperRight, Color.white);
            moneyText = Txt("Money", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -60), new Vector2(380, 44), 36, TextAnchor.UpperRight, new Color(0.45f, 1f, 0.45f));
            Btn("Din / Raat", root, new Vector2(1, 1), new Vector2(-110, -140), new Vector2(180, 54), () => { if (DayNightCycle.Instance) DayNightCycle.Instance.ToggleDayNight(); });
            Btn("Help", root, new Vector2(1, 1), new Vector2(-110, -202), new Vector2(180, 54), () => helpPanel.SetActive(!helpPanel.activeSelf));
            Btn("Menu", root, new Vector2(1, 1), new Vector2(-110, -264), new Vector2(180, 54), TogglePause);

            // language toggle (top centre)
            var lang = Img("Language", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(300, 46), new Color(0, 0, 0, 0.55f));
            var bh = Btn("HINDI", lang.transform, new Vector2(0.25f, 0.5f), Vector2.zero, new Vector2(140, 40), () => Loc.SetEnglish(false), 24);
            var be = Btn("ENGLISH", lang.transform, new Vector2(0.75f, 0.5f), Vector2.zero, new Vector2(140, 40), () => Loc.SetEnglish(true), 24);
            langHindi = bh.GetComponentInChildren<Text>();
            langEnglish = be.GetComponentInChildren<Text>();

            // location banner
            locationText = Txt("Location", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(1000, 64), 44, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.75f));
            locationGroup = locationText.gameObject.AddComponent<CanvasGroup>();
            locationGroup.alpha = 0;

            // big mission banner
            var bannerRt = Rect("Banner", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 220));
            bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerTitle = Txt("BannerTitle", bannerRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1400, 100), 78, TextAnchor.MiddleCenter, Color.yellow);
            bannerSub = Txt("BannerSub", bannerRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(1200, 110), 32, TextAnchor.UpperCenter, Color.white);
            var outline = bannerTitle.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3, -3);

            // prompt
            var pp = Img("Prompt", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(760, 60), new Color(0, 0, 0, 0.55f));
            pp.raycastTarget = false;
            promptPanel = pp.gameObject;
            promptText = Txt("PromptText", pp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740, 60), 28, TextAnchor.MiddleCenter, Color.white);

            toastText = Txt("Toast", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1200, 120), 34, TextAnchor.MiddleCenter, Color.white);

            // dialog
            var dp = Img("Dialog", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1200, 300), new Color(0.03f, 0.05f, 0.08f, 0.9f));
            dialogPanel = dp.gameObject;
            dialogTitle = Txt("Title", dp.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(1140, 50), 36, TextAnchor.UpperLeft, new Color(1f, 0.8f, 0.4f));
            dialogBody = Txt("Body", dp.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(1140, 180), 28, TextAnchor.UpperLeft, Color.white);
            Btn("Theek hai", dp.transform, new Vector2(1, 0), new Vector2(-120, 45), new Vector2(200, 60), CloseDialog);
            dialogPanel.SetActive(false);

            // help
            var help = Img("Help", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 700), new Color(0.03f, 0.05f, 0.08f, 0.92f));
            helpPanel = help.gameObject;
            helpText = Txt("HelpText", help.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 660), 25, TextAnchor.UpperLeft, Color.white);
            helpPanel.SetActive(false);

            // radar + speedometer
            var gm = FindAnyObjectByType<GameManager>();
            gameObject.AddComponent<Minimap>().Build(this, root, gm ? gm.blipNames : null, gm ? gm.blipPositions : null);
            speedText = Txt("Speed", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(340, 40), new Vector2(300, 70), 52, TextAnchor.LowerLeft, new Color(1f, 0.9f, 0.6f));

            BuildPause(root);

            showTouch = Application.isMobilePlatform || PlayerPrefs.GetInt("nt_touch", 0) == 1;   // nt_touch: the editor's phone play test
            if (showTouch) gameObject.AddComponent<TouchControls>().Build(this, root);
            dialogPanel.transform.SetAsLastSibling();
            helpPanel.transform.SetAsLastSibling();
            pausePanel.transform.SetAsLastSibling();
        }

        void BuildPause(Transform root)
        {
            var pp = Img("Pause", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0, 0, 0, 0.6f));
            pausePanel = pp.gameObject;
            var box = Img("PauseBox", pp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, 0), new Vector2(460, 640), new Color(0.04f, 0.06f, 0.09f, 0.95f));
            LocTxt(Txt("PauseTitle", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(420, 60), 46, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f)), "PAUSE");
            float y = -110;
            void B(string label, System.Action a) { Btn(label, box.transform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(380, 58), a); y -= 70; }
            B("Wapas khelo", TogglePause);
            B("Missions", () => missionsPanel.SetActive(!missionsPanel.activeSelf));
            B("Graphics", () => { quality = (quality + 1) % 3; PlayerPrefs.SetInt("nt_quality", quality); ApplyQuality(quality); });
            qualityLabel = Txt("QualityLabel", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y + 30), new Vector2(380, 30), 22, TextAnchor.MiddleCenter, new Color(0.7f, 0.9f, 1f));
            y -= 20;
            B("Mausam badlo", () => { if (WeatherSystem.Instance) WeatherSystem.Instance.Next(); });
            B("Din / Raat badlo", () => { if (DayNightCycle.Instance) DayNightCycle.Instance.ToggleDayNight(); });
            B("Mission chhodo", () => { if (MissionManager.Instance) MissionManager.Instance.Abort(); });
            B("Game band karo", () => { Application.Quit(); });

            var mp = Img("MissionsBox", pp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330, 0), new Vector2(640, 640), new Color(0.04f, 0.06f, 0.09f, 0.95f));
            missionsPanel = mp.gameObject;
            missionList = Txt("MissionList", mp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 600), 26, TextAnchor.UpperLeft, Color.white);
            missionsPanel.SetActive(false);
            pausePanel.SetActive(false);
        }

        // ------------------------------------------------------------------ pause / quality / language
        public void TogglePause()
        {
            bool on = !pausePanel.activeSelf;
            pausePanel.SetActive(on);
            GameInput.Paused = on;
            GameInput.UiBlocking = on || dialogPanel.activeSelf;
            Time.timeScale = on ? 0f : 1f;
            if (on) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; RefreshMissionList(); }
        }

        void ApplyQuality(int q)
        {
            string[] names = { "Graphics: LOW", "Graphics: MEDIUM", "Graphics: HIGH" };
            if (qualityLabel) qualityLabel.text = names[q];
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = new[] { 0.7f, 0.85f, 1f }[q];
                urp.shadowDistance = new[] { 50f, 100f, 160f }[q];
                foreach (var rd in urp.rendererDataList)
                {
                    if (!rd) continue;
                    foreach (var f in rd.rendererFeatures)
                        if (f && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion") && !Application.isMobilePlatform) f.SetActive(q == 2);
                }
            }
            foreach (var g in FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None))
            {
                g.radius = new[] { 0.1f, 32f, 55f }[q];
                g.enabled = q > 0;
            }
            foreach (var t in FindObjectsByType<TreeRenderer>(FindObjectsSortMode.None))
            {
                t.maxDistance = new[] { 700f, 1100f, 1500f }[q];
                t.lod0Distance = new[] { 55f, 85f, 110f }[q];
            }
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.sharedProfile && v.sharedProfile.TryGet<DepthOfField>(out var dof)) dof.active = q == 2;
        }

        void RefreshLanguage()
        {
            foreach (var (t, src) in locTexts) if (t) t.text = Loc.T(src);
            bool en = Loc.English;
            if (langHindi) langHindi.color = en ? new Color(1, 1, 1, 0.45f) : new Color(1f, 0.85f, 0.3f);
            if (langEnglish) langEnglish.color = en ? new Color(1f, 0.85f, 0.3f) : new Color(1, 1, 1, 0.45f);
            helpText.text = en ? HelpEn : HelpHi;
            toastText.text = Loc.T(toastSrc);
            locationText.text = Loc.T(locationSrc);
            dialogTitle.text = Loc.T(dialogTitleSrc);
            dialogBody.text = Loc.T(dialogBodySrc);
            bannerTitle.text = Loc.T(bannerTitleSrc);
            bannerSub.text = Loc.T(bannerSubSrc);
            if (pausePanel && pausePanel.activeSelf) RefreshMissionList();
        }

        void RefreshMissionList()
        {
            var mm = MissionManager.Instance;
            if (!mm || !missionList) return;
            var sb = new System.Text.StringBuilder();
            sb.Append(Loc.English ? "<b>MISSIONS</b>\n\n" : "<b>MISSIONS</b>\n\n");
            foreach (var m in mm.missions)
            {
                bool done = GameState.IsDone(m.id);
                sb.Append(done ? "<color=#7CFF7C>[x] " : "<color=#FFD24D>[ ] ").Append(Loc.T(m.title)).Append("</color>  Rs ").Append(m.reward);
                if (m.nightOnly) sb.Append(Loc.English ? "  (night)" : "  (raat)");
                sb.Append('\n');
            }
            sb.Append(Loc.English ? "\nYellow 'M' markers on the radar start missions." : "\nRadar pe peele 'M' marker se mission shuru hota hai.");
            missionList.text = sb.ToString();
        }

        const string HelpHi =
            "<b>NAINITAL - Controls</b>\n\n" +
            "WASD / Joystick : chalo   |   Shift : daudo   |   Alt : dheere chalo\n" +
            "Mouse / right side drag : camera ghumao   |   Scroll : zoom\n" +
            "Space : jump   |   Ctrl / C : roll (dodge)\n" +
            "Left click : punch combo (3rd hit kick)   |   F : kick   |   Right click / Q : block\n" +
            "E : baat karo / padho / khao / naav / bench / gaadi (chalti traffic wali bhi) / mission shuru\n" +
            "Gaadi: W/S race-brake, A/D steering, Space handbrake, G horn, E utro\n" +
            "N : din <-> raat   |   M : poora map   |   Esc / P : menu\n" +
            "` ya F1 (phone: upar-daaye kone pe 3 tap) : cheat - JEEVAN, AMAR, GAADI, TAXI, JEEP, BUS, UDAAN (jetpack), SAFAR (teleport)\nF2 : kisi bhi jagah teleport\n\n" +
            "Peele 'M' marker = mission. Hara roshni ka khamba = agla target.\n" +
            "Jheel mein gir gaye? Tairo, kinare pe Space dabake chadho.\n\n" +
            "Map: (c) OpenStreetMap contributors (ODbL). Elevation: SRTM via AWS Terrain Tiles.";

        const string HelpEn =
            "<b>NAINITAL - Controls</b>\n\n" +
            "WASD / Joystick : move   |   Shift : sprint   |   Alt : walk\n" +
            "Mouse / drag right side : camera   |   Scroll : zoom\n" +
            "Space : jump   |   Ctrl / C : dodge roll\n" +
            "Left click : punch combo (3rd hit kicks)   |   F : kick   |   Right click / Q : block\n" +
            "E : talk / read / eat / boat / bench / car / start mission\n" +
            "Car: W/S throttle-brake, A/D steer, Space handbrake, G horn, E get out\n" +
            "N : day <-> night   |   M : full map   |   Esc / P : menu\n" +
            "` or F1 (phone: 3 taps top-right) : cheats - JEEVAN, AMAR, GAADI, TAXI, JEEP, BUS, UDAAN (jetpack), SAFAR (teleport)\nF2 : teleport to a place\n\n" +
            "Yellow 'M' marker = mission. Green light beam = next objective.\n" +
            "Fell in the lake? Swim to the bank and press Space to climb out.\n\n" +
            "Map: (c) OpenStreetMap contributors (ODbL). Elevation: SRTM via AWS Terrain Tiles.";

        // ------------------------------------------------------------------ events
        void OnToast(string msg, float seconds) { toastSrc = msg; toastText.text = Loc.T(msg); toastTimer = seconds; }

        void OnDialog(string title, string body)
        {
            dialogTitleSrc = title;
            dialogBodySrc = body;
            dialogTitle.text = Loc.T(title);
            dialogBody.text = Loc.T(body);
            dialogPanel.SetActive(true);
            GameInput.UiBlocking = true;
        }

        public void CloseDialog()
        {
            dialogPanel.SetActive(false);
            GameInput.UiBlocking = pausePanel.activeSelf;
        }

        void OnLocation(string name) { locationSrc = name; locationText.text = Loc.T(name); locationTimer = 4f; }

        void OnBanner(string title, string sub, Color c)
        {
            bannerTitleSrc = title;
            bannerSubSrc = sub;
            bannerTitle.text = Loc.T(title);
            bannerSub.text = Loc.T(sub);
            bannerTitle.color = c;
            bannerTimer = 5f;
        }

        void Update()
        {
            if (GameInput.PausePressed && !dialogPanel.activeSelf) TogglePause();
            float dt = Time.unscaledDeltaTime;
            var p = PlayerController.Instance;
            if (p)
            {
                var h = p.Health;
                healthFill.rectTransform.sizeDelta = new Vector2(414f * h.Fraction, 20);
                if (h.LastDamageTime > Time.time - 0.05f) flash = 0.35f;
            }
            flash = Mathf.MoveTowards(flash, 0f, dt);
            damageFlash.color = new Color(0.8f, 0f, 0f, flash);
            moneyText.text = "Rs " + GameState.Money.ToString("N0");

            var dn = DayNightCycle.Instance;
            if (dn)
            {
                int hh = Mathf.FloorToInt(dn.timeOfDay), mm = Mathf.FloorToInt((dn.timeOfDay - hh) * 60f);
                string ampm = hh < 12 ? "AM" : "PM";
                int h12 = hh % 12 == 0 ? 12 : hh % 12;
                timeText.text = $"Nainital  {h12}:{mm:00} {ampm}";
            }

            if (dialogPanel.activeSelf)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) CloseDialog();
            }
            var kb2 = UnityEngine.InputSystem.Keyboard.current;
            if (kb2 != null && kb2.hKey.wasPressedThisFrame) helpPanel.SetActive(!helpPanel.activeSelf);

            var cur = PlayerInteractor.Current;
            bool showPrompt = cur && !dialogPanel.activeSelf && !pausePanel.activeSelf;
            promptPanel.SetActive(showPrompt);
            if (showPrompt) promptText.text = (showTouch ? "" : "[E] ") + Loc.T(cur.Prompt);
            if (TouchControls.Instance) TouchControls.Instance.SetInteractVisible(showPrompt);

            var car = CarController.Driving;
            speedText.text = car ? Mathf.RoundToInt(car.SpeedKmh) + " km/h" : "";

            // mission objective
            var mmgr = MissionManager.Instance;
            bool active = mmgr && mmgr.Active != null && mmgr.Step != null;
            if (objPanel.activeSelf != active) objPanel.SetActive(active);
            if (active)
            {
                objTitle.text = Loc.T(mmgr.Active.title);
                objText.text = Loc.T(mmgr.Step.text) + "  " + Loc.T(mmgr.Step.Progress);
                timerText.text = mmgr.Timed ? $"{Mathf.FloorToInt(mmgr.TimeLeft / 60f)}:{Mathf.FloorToInt(mmgr.TimeLeft % 60f):00}" : "";
                timerText.color = mmgr.Timed && mmgr.TimeLeft < 20f ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.9f, 0.4f);
            }
            else timerText.text = "";

            toastTimer -= dt;
            toastText.color = new Color(1, 1, 1, Mathf.Clamp01(toastTimer));
            locationTimer -= dt;
            locationGroup.alpha = Mathf.Clamp01(Mathf.Min(locationTimer, 4f - locationTimer) * 2f);
            bannerTimer -= dt;
            bannerGroup.alpha = Mathf.Clamp01(Mathf.Min(bannerTimer, 5f - bannerTimer) * 3f);

            UpdateEnemyBars();
        }

        void UpdateEnemyBars()
        {
            var cam = Camera.main;
            foreach (var e in EnemyAI.All)
            {
                if (!e || enemyBars.Exists(b => b.e == e)) continue;
                var bg = Img("EnemyBar", canvas.transform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 10), new Color(0, 0, 0, 0.6f));
                bg.raycastTarget = false;
                var fill = Img("Fill", bg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(1, 0), new Vector2(88, 8), new Color(0.95f, 0.75f, 0.1f));
                fill.raycastTarget = false;
                enemyBars.Add((e, bg, fill));
            }
            var scale = canvas.GetComponent<RectTransform>().localScale.x;
            foreach (var (e, bg, fill) in enemyBars)
            {
                bool show = e && cam && !e.Dead && Time.time - e.Health.LastDamageTime < 6f;
                Vector3 sp = show ? cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.1f) : Vector3.zero;
                show &= sp.z > 0;
                bg.gameObject.SetActive(show);
                if (!show) continue;
                bg.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                fill.rectTransform.sizeDelta = new Vector2(88f * e.Health.Fraction, 8);
            }
        }
    }
}
