using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The game's front end in the Nainital scene (user, 2026-11-17: "main menu - resume, save, volume ya jo bhi settings,
    /// credits, exit; black fade out hoke game start ho; GTA 5 ki tarah pehle map upar se, phir player tak aata hai, then
    /// align to player camera, everything in 3 seconds"):
    ///  - the main menu over the live world, the camera drifting over Naini Lake towards Mallital;
    ///  - Continue (the saved journey), New Game, Settings, Credits (empty for now), Quit;
    ///  - the pause menu: Resume, Save, Missions, Settings, Abandon Mission, Main Menu, Quit, and a card of where you are;
    ///  - Settings: sound, controls (look speed, invert Y, vibration), graphics (quality, 30/60 fps), language;
    ///  - starting play: to black, then the town seen from high above the player like a map, two drops down to them and a
    ///    swing into the third-person camera, three seconds in all, the HUD coming in at its end.
    /// Built in code like the HUD, in the Aipan look (Aipan.cs). Runs after the third-person camera (execution order 200)
    /// so it can take the camera over in the menu and the intro and hand it back exactly where that camera would be.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class FrontEnd : MonoBehaviour
    {
        public static FrontEnd Instance { get; private set; }
        public enum State { Menu, Intro, Playing, Paused }
        public State Current { get; private set; } = State.Menu;
        /// <summary>A menu or the intro has the screen: no HUD prompts, no gameplay keys.</summary>
        public static bool MenuOpen => Instance && Instance.Current != State.Playing;

        Canvas canvas;
        RectTransform root;
        Image fade;
        float fadeTarget = 1f, fadeSpeed = 3f;
        readonly UiKit.LocTexts loc = new UiKit.LocTexts();
        readonly List<System.Action> onLanguage = new List<System.Action>();
        Camera cam;
        AudioSource sfx;
        bool sessionStarted, busy;
        Vector3 defaultSpawn;
        float defaultYaw, defaultHour = 17.5f;

        // ---- screens
        class Pane
        {
            public RectTransform rt;
            public CanvasGroup g;
            public Vector2 basePos, slide;
            public bool on;
            public float shownAt;
            public readonly List<MenuItemFx> items = new List<MenuItemFx>();
        }
        Pane mainMenu, pauseMenu, settings, credits, confirm, missions, status, introMarker;
        readonly List<Pane> screens = new List<Pane>();
        Text continueSub, statsChip, versionText, missionList, statusPlace, statusLines, confirmTitle, confirmBody;
        MenuItemFx continueItem, abandonItem;
        System.Action confirmYes;
        Text confirmYesLabel;

        void Awake()
        {
            Instance = this;
            cam = Camera.main;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            canvas = UiKit.Canvas("FrontEndCanvas", 50, transform);
            root = (RectTransform)canvas.transform;
            Build();
            fade = UiKit.FullImg("Fade", root, Color.black);
            fade.raycastTarget = true;
            gameObject.AddComponent<SaveTracker>();
            Loc.Changed += OnLanguage;
        }

        void OnDestroy() { Loc.Changed -= OnLanguage; if (Instance == this) Instance = null; }

        void OnLanguage()
        {
            loc.Refresh();
            foreach (var a in onLanguage) a();
            RefreshMenuInfo();
        }

        IEnumerator Start()
        {
            var p = PlayerController.Instance;
            if (p) { defaultSpawn = p.transform.position; defaultYaw = p.transform.eulerAngles.y; }
            if (DayNightCycle.Instance) defaultHour = DayNightCycle.Instance.timeOfDay;
            SetupMenuShot();
            GameSettings.ApplyAll();

            // the editor's play tests go straight into the game
            if (Application.isEditor && PlayerPrefs.GetInt("nt_skip_menu", 0) == 1)
            {
                SetFade(0f, true);
                sessionStarted = true;
                EnterPlaying(false);
                Boot.WorldReady = true;
                yield break;
            }

            Current = State.Menu;
            GameInput.UiBlocking = true;
            SetHud(0f);
            // let the world settle (first frames compile shaders and stream in what the camera sees)
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            Boot.WorldReady = true;
            while (Boot.Instance) yield return null;   // the loading screen fades out over us
            ShowMainMenu();
            SetFade(0f);
            fadeSpeed = 1.6f;
        }

        // ------------------------------------------------------------------ building
        void Build()
        {
            BuildMainMenu();
            BuildPauseMenu();
            BuildStatus();
            BuildMissions();
            BuildSettings();
            BuildCredits();
            BuildConfirm();
            BuildIntroMarker();
        }

        Pane NewScreen(string name, Vector2 slide)
        {
            var rt = UiKit.Full(name, root);
            var s = new Pane { rt = rt, g = rt.gameObject.AddComponent<CanvasGroup>(), slide = slide };
            s.g.alpha = 0f;
            s.g.blocksRaycasts = false;
            s.g.interactable = false;
            rt.gameObject.SetActive(false);
            screens.Add(s);
            return s;
        }

        Pane NewPanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Vector2 slide)
        {
            var rt = UiKit.Rect(name, parent, anchor, pivot, pos, size);
            var s = new Pane { rt = rt, g = rt.gameObject.AddComponent<CanvasGroup>(), basePos = pos, slide = slide };
            s.g.alpha = 0f;
            s.g.blocksRaycasts = false;
            rt.gameObject.SetActive(false);
            screens.Add(s);
            return s;
        }

        void Show(Pane s, bool on)
        {
            if (s == null || s.on == on) return;
            s.on = on;
            if (on)
            {
                s.rt.gameObject.SetActive(true);
                s.shownAt = Time.unscaledTime;
                foreach (var it in s.items) it.shownAt = s.shownAt;
            }
        }

        /// <summary>The left half of a menu screen: the dark fade, the Aipan at the top and bottom, the mark.</summary>
        RectTransform MenuBackdrop(Pane s, string hiTitle, string enTitle, bool logo)
        {
            var left = UiKit.Img("Shade", s.rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1250, 4000), new Color(Aipan.Night.r, Aipan.Night.g, Aipan.Night.b, 0.93f), HudStyle.FadeRight);
            left.raycastTarget = false;
            UiKit.Strip("Vasudhara", s.rt, Aipan.Vasudhara, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -6), new Vector2(900, 64), new Color(Aipan.Rice.r, Aipan.Rice.g, Aipan.Rice.b, 0.35f));
            UiKit.Strip("Border", s.rt, Aipan.Border, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(5000, 24), Color.white);
            var head = UiKit.Rect("Head", s.rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -84), new Vector2(900, 170));
            if (logo)
            {
                var m = UiKit.Img("Mandala", head, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70, 0), new Vector2(150, 150), Aipan.Saffron, Aipan.Mandala);
                m.gameObject.AddComponent<Spin>().speed = 6f;
                UiKit.Txt("Title", head, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170, 24), new Vector2(800, 110), 104, TextAnchor.MiddleLeft, Aipan.Rice, true).text = UiKit.Spaced("NAINITA");
                UiKit.Txt("Sub", head, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(176, -50), new Vector2(800, 40), 32, TextAnchor.MiddleLeft, Aipan.Saffron, true).text = UiKit.Spaced("THE SUKOON");
            }
            else
            {
                loc.AddSpaced(UiKit.Txt("Title", head, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 20), new Vector2(900, 110), 92, TextAnchor.MiddleLeft, Aipan.Rice, true), hiTitle, enTitle);
                UiKit.Strip("Dots", head, Aipan.DotRow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, -46), new Vector2(300, 20), Aipan.Saffron);
            }
            return UiKit.Rect("List", s.rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -310), new Vector2(700, 700));
        }

        MenuItemFx Item(Pane s, RectTransform list, ref float y, string hi, string en, System.Action act, bool withSub = false)
        {
            float h = withSub ? 110f : 84f;
            var rt = UiKit.Rect("Item_" + en, list, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(640, h));
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            var bar = UiKit.Panel("Bar", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(640, h), Aipan.Geru, true);
            var dots = UiKit.Strip("Dots", bar.transform, Aipan.DotRow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(600, 14), new Color(Aipan.Rice.r, Aipan.Rice.g, Aipan.Rice.b, 0.55f));
            var bullet = UiKit.Img("Bullet", rt, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, withSub ? 16 : 0), new Vector2(18, 18), new Color(1, 1, 1, 0.45f), HudStyle.Diamond);
            var label = UiKit.Txt("Label", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(62, withSub ? 16 : 0), new Vector2(560, 70), 46, TextAnchor.MiddleLeft, Aipan.Rice, true);
            loc.AddSpaced(label, hi, en);
            Text sub = null;
            if (withSub) sub = UiKit.Txt("Sub", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, -28), new Vector2(560, 34), 25, TextAnchor.MiddleLeft, HudStyle.InkDim);
            var tb = rt.gameObject.AddComponent<TapButton>();
            tb.onClick = () => { if (!busy) act(); };
            var fx = rt.gameObject.AddComponent<MenuItemFx>();
            fx.Setup(tb, bar, dots, bullet, label, sub, s.items.Count * 0.06f);
            s.items.Add(fx);
            y -= h + 12f;
            return fx;
        }

        void BuildMainMenu()
        {
            mainMenu = NewScreen("MainMenu", new Vector2(-40, 0));
            var list = MenuBackdrop(mainMenu, "", "", true);
            float y = 0f;
            continueItem = Item(mainMenu, list, ref y, "JAARI RAKHO", "CONTINUE", () => StartCoroutine(StartPlaying(false)), true);
            continueSub = continueItem.sub;
            Item(mainMenu, list, ref y, "NAYA SAFAR", "NEW GAME", () =>
            {
                if (SaveGame.Exists || sessionStarted)
                    Ask("NAYA SAFAR?", "NEW GAME?",
                        "Purana save, paise aur missions - sab mit jayega. Pakka?",
                        "Your save, money and missions will all be wiped. Sure?",
                        "HAAN, SHURU KARO", "YES, START", () => StartCoroutine(StartPlaying(true)));
                else StartCoroutine(StartPlaying(true));
            });
            Item(mainMenu, list, ref y, "SETTINGS", "SETTINGS", () => OpenRight(settings));
            Item(mainMenu, list, ref y, "CREDITS", "CREDITS", () => OpenRight(credits));
            Item(mainMenu, list, ref y, "BAHAR NIKLO", "QUIT", AskQuit);

            versionText = UiKit.Txt("Version", mainMenu.rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(84, 44), new Vector2(900, 30), 22, TextAnchor.MiddleLeft, HudStyle.InkFaint);
            versionText.text = "v" + Application.version + "   ◆   DEVBHOOMI UTTARAKHAND";

            // top right: money and missions; bottom right: the language (both step aside for settings and credits)
            var corners = UiKit.Full("Corners", mainMenu.rt);
            cornerGroup = corners.gameObject.AddComponent<CanvasGroup>();
            var chip = UiKit.Panel("Stats", corners, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -48), new Vector2(470, 64), HudStyle.Glass, true);
            statsChip = UiKit.Txt("Text", chip.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(450, 60), 30, TextAnchor.MiddleCenter, Aipan.Rice, true);
            Segmented(corners, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 54), 170, new[] { "ENGLISH", "HINDI_NAME" }, new[] { "ENGLISH", "HINDI_NAME" },
                () => Loc.Hindi ? 1 : 0, i => Loc.Set(i == 1 ? Loc.Lang.Hindi : Loc.Lang.English));
        }

        CanvasGroup cornerGroup;

        void BuildPauseMenu()
        {
            pauseMenu = NewScreen("PauseMenu", new Vector2(-40, 0));
            var dim = UiKit.FullImg("Dim", pauseMenu.rt, new Color(0, 0, 0, 0.35f));
            dim.raycastTarget = true;
            dim.transform.SetAsFirstSibling();
            var list = MenuBackdrop(pauseMenu, "THODA RUKO", "PAUSED", false);
            float y = 0f;
            Item(pauseMenu, list, ref y, "WAPAS KHELO", "RESUME", TogglePause);
            Item(pauseMenu, list, ref y, "SAVE KARO", "SAVE GAME", () =>
            {
                SaveGame.Save();
                Haptics.Success();
                GameEvents.ShowToast(UiKit.L("Safar save ho gaya", "Journey saved"), 2f);
                RefreshMenuInfo();
                OpenRight(status);
            });
            // the bus ride, bus stand -> Khurpatal, him aboard (owner, 23 Nov 2026: "bus journey chlaane wala menu option ...
            // add kro..jo ye play kre"): the game resumes and the ride starts
            Item(pauseMenu, list, ref y, "BUS SAWARI", "BUS RIDE", () => { TogglePause(); BusTour.Begin(); });
            Item(pauseMenu, list, ref y, "MISSIONS", "MISSIONS", () => { RefreshMissionList(); OpenRight(missions); });
            Item(pauseMenu, list, ref y, "SETTINGS", "SETTINGS", () => OpenRight(settings));
            abandonItem = Item(pauseMenu, list, ref y, "MISSION CHHODO", "ABANDON MISSION", () =>
                Ask("MISSION CHHODEIN?", "ABANDON MISSION?", "Is mission ka inaam nahi milega.", "You won't get this mission's reward.",
                    "HAAN, CHHODO", "YES, ABANDON", () => { if (MissionManager.Instance) MissionManager.Instance.Abort(); TogglePause(); }));
            Item(pauseMenu, list, ref y, "MAIN MENU", "MAIN MENU", () => StartCoroutine(BackToMenu()));
            Item(pauseMenu, list, ref y, "BAHAR NIKLO", "QUIT GAME", AskQuit);
        }

        /// <summary>The right-hand panel shape shared by the status card, missions, settings and credits.</summary>
        Pane RightPanel(string name, Vector2 size, string hi, string en, out RectTransform body)
        {
            var s = NewPanel(name, root, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-70, 6), size, new Vector2(40, 0));
            var bg = UiKit.Panel("Bg", s.rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(Aipan.Night.r, Aipan.Night.g, Aipan.Night.b, 0.94f));
            bg.raycastTarget = true;
            UiKit.Img("Mandala", s.rt, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-40, 40), new Vector2(520, 520), new Color(1, 1, 1, 0.035f), Aipan.Mandala);
            UiKit.Strip("Border", s.rt, Aipan.Border, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(size.x - 40, 26), Color.white);
            loc.AddSpaced(UiKit.Txt("Title", s.rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -54), new Vector2(size.x - 100, 80), 56, TextAnchor.MiddleLeft, Aipan.Rice, true), hi, en);
            UiKit.Strip("Dots", s.rt, Aipan.DotRow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(54, -134), new Vector2(220, 18), Aipan.Saffron);
            body = UiKit.Rect("Body", s.rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -170), new Vector2(size.x - 104, size.y - 200));
            // a close button for a phone held one-handed
            var close = UiKit.Panel("Close", s.rt, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-62, -84), new Vector2(68, 68), new Color(1, 1, 1, 0.08f), true);
            close.raycastTarget = true;
            UiKit.Txt("X", close.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(68, 68), 40, TextAnchor.MiddleCenter, Aipan.Rice, true, false).text = "×";
            var tb = close.gameObject.AddComponent<TapButton>();
            tb.onClick = () => { Show(s, false); if (Current == State.Paused) Show(status, true); };
            return s;
        }

        void BuildStatus()
        {
            status = RightPanel("Status", new Vector2(820, 620), "AAP YAHAN HO", "WHERE YOU ARE", out var body);
            statusPlace = UiKit.Txt("Place", body, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -10), new Vector2(720, 80), 64, TextAnchor.MiddleLeft, Aipan.Saffron, true);
            statusLines = UiKit.Txt("Lines", body, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -110), new Vector2(720, 320), 34, TextAnchor.UpperLeft, Aipan.Rice);
            statusLines.lineSpacing = 1.25f;
        }

        void BuildMissions()
        {
            missions = RightPanel("Missions", new Vector2(820, 860), "MISSIONS", "MISSIONS", out var body);
            missionList = UiKit.Txt("List", body, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(720, 640), 30, TextAnchor.UpperLeft, Aipan.Rice);
            missionList.lineSpacing = 1.15f;
        }

        void BuildCredits()
        {
            credits = RightPanel("Credits", new Vector2(820, 640), "CREDITS", "CREDITS", out var body);
            // (user, 2026-11-17: "credit me abhi khaali rakho")
        }

        // ---- settings
        RectTransform[] tabPages;
        Image[] tabBg;
        Text[] tabText;
        int tab;

        void BuildSettings()
        {
            settings = RightPanel("Settings", new Vector2(960, 880), "SETTINGS", "SETTINGS", out var body);
            string[] th = { "AWAAZ", "CONTROLS", "GRAPHICS", "BHASHA" }, te = { "SOUND", "CONTROLS", "GRAPHICS", "LANGUAGE" };
            tabPages = new RectTransform[4];
            tabBg = new Image[4];
            tabText = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = UiKit.Panel("Tab" + i, body, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 212, 0), new Vector2(200, 64), new Color(1, 1, 1, 0.07f), true);
                b.raycastTarget = true;
                tabBg[i] = b;
                tabText[i] = loc.Add(UiKit.Txt("T", b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(200, 64), 26, TextAnchor.MiddleCenter, Aipan.Rice, true, false), th[i], te[i]);
                b.gameObject.AddComponent<TapButton>().onClick = () => SetTab(k);
                tabPages[i] = UiKit.Rect("Page" + i, body, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -110), new Vector2(856, 560));
            }

            // sound
            float y = -10f;
            SliderRow(tabPages[0], ref y, "Sab kuch", "Everything", () => GameSettings.Master, v => GameSettings.Master = v);
            SliderRow(tabPages[0], ref y, "Mahaul (pakshi, hawa, jheel)", "Ambience (birds, wind, lake)", () => GameSettings.Ambience, v => GameSettings.Ambience = v);
            SliderRow(tabPages[0], ref y, "Effects (gaadi, kadam, ghoonse)", "Effects (cars, steps, punches)", () => GameSettings.Effects, v => GameSettings.Effects = v);
            // controls
            y = -10f;
            SliderRow(tabPages[1], ref y, "Camera ki speed", "Camera speed", () => Mathf.InverseLerp(0.4f, 2f, GameSettings.LookSpeed), v => GameSettings.LookSpeed = Mathf.Lerp(0.4f, 2f, v), true);
            SegRow(tabPages[1], ref y, "Camera ulta (upar-neeche)", "Invert camera (up-down)", new[] { "NAHI", "HAAN" }, new[] { "OFF", "ON" }, () => GameSettings.InvertY ? 1 : 0, i => GameSettings.InvertY = i == 1);
            SegRow(tabPages[1], ref y, "Vibration", "Vibration", new[] { "BAND", "CHALU" }, new[] { "OFF", "ON" }, () => GameSettings.HapticsOn ? 1 : 0, i => { GameSettings.HapticsOn = i == 1; if (i == 1) Haptics.Medium(); });
            // graphics
            y = -10f;
            SegRow(tabPages[2], ref y, "Quality", "Quality", new[] { "KAM", "THEEK", "ZABARDAST" }, new[] { "LOW", "MEDIUM", "HIGH" }, () => GameSettings.Quality, i => GameSettings.Quality = i, 170);
            SegRow(tabPages[2], ref y, "Frame rate", "Frame rate", new[] { "30 FPS", "60 FPS" }, new[] { "30 FPS", "60 FPS" }, () => GameSettings.Fps60 ? 1 : 0, i => GameSettings.Fps60 = i == 1);
            loc.Add(UiKit.Txt("Note", tabPages[2], new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y - 10), new Vector2(820, 90), 25, TextAnchor.UpperLeft, HudStyle.InkDim),
                "Phone garam ho ya battery bachani ho to Quality KAM aur 30 FPS rakho.",
                "If the phone runs warm or you want to save battery, use LOW quality and 30 FPS.");
            // language
            y = -10f;
            SegRow(tabPages[3], ref y, "Bhasha", "Language", new[] { "ENGLISH", "HINDI_NAME" }, new[] { "ENGLISH", "HINDI_NAME" }, () => Loc.Hindi ? 1 : 0, i => Loc.Set(i == 1 ? Loc.Lang.Hindi : Loc.Lang.English));
            loc.Add(UiKit.Txt("Note", tabPages[3], new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y - 10), new Vector2(820, 90), 25, TextAnchor.UpperLeft, HudStyle.InkDim),
                "LANG_NOTE_HI",
                "Everything in the game - menus, phone, signs on screen, people's lines - in English or in Hindi.");
            SetTab(0);
        }

        void SetTab(int k)
        {
            tab = k;
            for (int i = 0; i < 4; i++)
            {
                tabPages[i].gameObject.SetActive(i == k);
                tabBg[i].color = i == k ? Aipan.Saffron : new Color(1, 1, 1, 0.07f);
                tabText[i].color = i == k ? new Color(0.12f, 0.06f, 0.02f) : Aipan.Rice;
            }
        }

        void RowLabel(RectTransform page, float y, string hi, string en) =>
            loc.Add(UiKit.Txt("Label", page, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(400, 90), 31, TextAnchor.MiddleLeft, Aipan.Rice), hi, en);

        void SliderRow(RectTransform page, ref float y, string hi, string en, System.Func<float> get, System.Action<float> set, bool speed = false)
        {
            RowLabel(page, y, hi, en);
            var rt = UiKit.Rect("Slider", page, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(420, y - 45), new Vector2(330, 60));
            var bg = UiKit.Panel("Bg", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 12), new Color(1, 1, 1, 0.16f), true);
            bg.raycastTarget = true;
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f); bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.offsetMin = new Vector2(0, -6); bg.rectTransform.offsetMax = new Vector2(0, 6);
            var fillArea = UiKit.Rect("FillArea", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(0, -6); fillArea.offsetMax = new Vector2(0, 6);
            var fill = UiKit.Panel("Fill", fillArea, new Vector2(0, 0), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, Aipan.Saffron, true);
            UiKit.Stretch(fill.rectTransform);
            var handleArea = UiKit.Rect("HandleArea", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Stretch(handleArea);
            var handle = UiKit.Img("Handle", handleArea, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), Aipan.Rice, HudStyle.Disc);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(46, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0.5f); handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(46, 46);
            UiKit.Img("Dot", handle.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16), Aipan.Geru, HudStyle.Disc);
            var val = UiKit.Txt("Value", page, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(770, y - 45), new Vector2(90, 60), 30, TextAnchor.MiddleRight, Aipan.Saffron, true);
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            handle.rectTransform.sizeDelta = new Vector2(46, -14);   // the slider stretches the handle to its height (60): a round 46
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;
            float v0 = get();
            s.SetValueWithoutNotify(v0);
            string Fmt(float v) => speed ? (Mathf.Lerp(0.4f, 2f, v)).ToString("0.0") + "x" : Mathf.RoundToInt(v * 100f) + "%";
            val.text = Fmt(v0);
            int lastStep = Mathf.RoundToInt(v0 * 10f);
            s.onValueChanged.AddListener(v =>
            {
                set(v);
                val.text = Fmt(v);
                int st = Mathf.RoundToInt(v * 10f);
                if (st != lastStep) { lastStep = st; Haptics.Selection(); }
            });
            y -= 100f;
        }

        void SegRow(RectTransform page, ref float y, string hi, string en, string[] optHi, string[] optEn, System.Func<int> get, System.Action<int> set, float segW = 200f)
        {
            RowLabel(page, y, hi, en);
            Segmented(page, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(420, y - 45), segW, optHi, optEn, get, set);
            y -= 100f;
        }

        /// <summary>A row of choices, the chosen one in saffron.</summary>
        void Segmented(RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float segW, string[] hi, string[] en, System.Func<int> get, System.Action<int> set)
        {
            int n = hi.Length;
            var rt = UiKit.Rect("Seg", parent, anchor, pivot, pos, new Vector2(n * segW + (n - 1) * 8, 60));
            var bgs = new Image[n];
            var txts = new Text[n];
            void Refresh()
            {
                if (!rt) return;   // (a static event outlives the scene)
                int cur = get();
                for (int i = 0; i < n; i++)
                {
                    bgs[i].color = i == cur ? Aipan.Saffron : new Color(1, 1, 1, 0.08f);
                    txts[i].color = i == cur ? new Color(0.12f, 0.06f, 0.02f) : Aipan.Rice;
                }
            }
            for (int i = 0; i < n; i++)
            {
                int k = i;
                var b = UiKit.Panel("Opt" + i, rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * (segW + 8), 0), new Vector2(segW, 60), new Color(1, 1, 1, 0.08f), true);
                b.raycastTarget = true;
                bgs[i] = b;
                txts[i] = loc.Add(UiKit.Txt("T", b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(segW, 60), 25, TextAnchor.MiddleCenter, Aipan.Rice, true, false), hi[i], en[i]);
                b.gameObject.AddComponent<TapButton>().onClick = () => { set(k); Refresh(); };
            }
            Refresh();
            onLanguage.Add(Refresh);
            GameSettings.Changed += Refresh;
        }

        void BuildConfirm()
        {
            confirm = NewScreen("Confirm", Vector2.zero);
            var dim = UiKit.FullImg("Dim", confirm.rt, new Color(0, 0, 0, 0.6f));
            dim.raycastTarget = true;
            var box = UiKit.Panel("Box", confirm.rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 420), new Color(Aipan.GeruDeep.r, Aipan.GeruDeep.g, Aipan.GeruDeep.b, 0.97f));
            UiKit.Img("Mandala", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 420), new Color(1, 1, 1, 0.05f), Aipan.Mandala);
            UiKit.Strip("Border", box.transform, Aipan.Border, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(820, 26), Color.white);
            confirmTitle = UiKit.Txt("Title", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(800, 80), 54, TextAnchor.MiddleCenter, Aipan.Rice, true);
            confirmBody = UiKit.Txt("Body", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(760, 100), 31, TextAnchor.UpperCenter, new Color(1, 1, 1, 0.85f));
            var yes = UiKit.Panel("Yes", box.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-195, 44), new Vector2(360, 80), Aipan.Saffron, true);
            yes.raycastTarget = true;
            confirmYesLabel = UiKit.Txt("T", yes.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(360, 80), 30, TextAnchor.MiddleCenter, new Color(0.12f, 0.06f, 0.02f), true, false);
            yes.gameObject.AddComponent<TapButton>().onClick = () => { Show(confirm, false); Haptics.Medium(); confirmYes?.Invoke(); };
            var no = UiKit.Panel("No", box.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(195, 44), new Vector2(360, 80), new Color(1, 1, 1, 0.1f), true);
            no.raycastTarget = true;
            loc.Add(UiKit.Txt("T", no.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(360, 80), 30, TextAnchor.MiddleCenter, Aipan.Rice, true, false), "NAHI", "NO");
            no.gameObject.AddComponent<TapButton>().onClick = () => Show(confirm, false);
        }

        void Ask(string titleHi, string titleEn, string bodyHi, string bodyEn, string yesHi, string yesEn, System.Action yes)
        {
            confirmTitle.text = UiKit.L(titleHi, titleEn);
            confirmBody.text = UiKit.L(bodyHi, bodyEn);
            confirmYesLabel.text = UiKit.L(yesHi, yesEn);
            confirmYes = yes;
            confirm.rt.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
            Show(confirm, true);
            Haptics.Warning();
        }

        void AskQuit() => Ask("KHEL BAND KAREIN?", "QUIT THE GAME?", "Aapka safar save ho jayega.", "Your journey will be saved.", "HAAN, BAND KARO", "YES, QUIT", () => StartCoroutine(Quit()));

        // the "you are here" pin over the player while the camera is high up
        RectTransform markerRt;
        Image markerRing;
        Text markerText;

        void BuildIntroMarker()
        {
            introMarker = NewScreen("IntroMarker", Vector2.zero);
            markerRt = UiKit.Rect("Pin", introMarker.rt, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
            markerRing = UiKit.Img("Ring", markerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120), Aipan.Saffron, HudStyle.RingSprite);
            UiKit.Img("Dot", markerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), Aipan.Saffron, HudStyle.Disc);
            UiKit.Img("Core", markerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12), Color.white, HudStyle.Disc);
            markerText = UiKit.Txt("Label", markerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 92), new Vector2(600, 50), 34, TextAnchor.MiddleCenter, Aipan.Rice, true);
            loc.AddSpaced(markerText, "AAP YAHAN HO", "YOU ARE HERE");
        }

        // ------------------------------------------------------------------ menus
        void ShowMainMenu()
        {
            Current = State.Menu;
            RefreshMenuInfo();
            foreach (var s in screens) Show(s, false);
            Show(mainMenu, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OpenRight(Pane s)
        {
            foreach (var r in new[] { status, missions, settings, credits }) if (r != s) Show(r, false);
            s.rt.SetAsLastSibling();
            confirm.rt.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
            Show(s, true);
        }

        void RefreshMenuInfo()
        {
            bool canContinue = SaveGame.Exists || sessionStarted;
            if (continueItem) continueItem.gameObject.SetActive(canContinue);
            if (continueItem && continueSub)
            {
                if (sessionStarted && Current != State.Menu) continueSub.text = "";
                else if (SaveGame.Exists)
                {
                    var d = SaveGame.Read();
                    string place = string.IsNullOrEmpty(d.place) ? "Nainital" : Loc.T(d.place);
                    continueSub.text = $"{place}   ◆   {SaveGame.Clock(d.hour)}   ◆   {SaveGame.Ago(d.savedAt)}";
                }
                else continueSub.text = UiKit.L("Wahin se jahan chhoda tha", "Right where you left off");
            }
            // the menu list closes up when Continue is hidden
            if (mainMenu != null)
            {
                float y = 0f;
                foreach (var it in mainMenu.items)
                {
                    if (!it.gameObject.activeSelf) continue;
                    var rt = (RectTransform)it.transform;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
                    y -= rt.sizeDelta.y + 12f;
                }
            }
            int done = 0, total = 0;
            if (MissionManager.Instance) { total = MissionManager.Instance.missions.Count; foreach (var m in MissionManager.Instance.missions) if (GameState.IsDone(m.id)) done++; }
            if (statsChip) statsChip.text = $"<color=#FFA91F>₹</color> {HudStyle.Rupees(GameState.Money).Substring(1)}     <color=#FFA91F>◆</color>     {done}/{total} {UiKit.L("missions", "missions")}";
            if (abandonItem) abandonItem.gameObject.SetActive(MissionManager.Instance && MissionManager.Instance.Active != null);
            if (pauseMenu != null)
            {
                float y = 0f;
                foreach (var it in pauseMenu.items)
                {
                    if (!it.gameObject.activeSelf) continue;
                    var rt = (RectTransform)it.transform;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
                    y -= rt.sizeDelta.y + 12f;
                }
            }
            if (statusPlace)
            {
                var tr = SaveTracker.Instance;
                statusPlace.text = tr && !string.IsNullOrEmpty(tr.Place) ? Loc.T(tr.Place) : "Nainital";
                var dn = DayNightCycle.Instance;
                var ws = WeatherSystem.Instance;
                string weather = ws ? Loc.T(new[] { "Saaf", "Baadal", "Kohra", "Baarish", "Baarish + Kohra" }[(int)ws.current]) : "";
                int mins = Mathf.FloorToInt(SaveGame.PlaySeconds / 60f);
                string saved = SaveGame.Exists ? SaveGame.Ago(SaveGame.Read().savedAt) : UiKit.L("abhi nahi", "not yet");
                string c = "<color=#FFA91F>◆</color>  ";
                statusLines.text =
                    c + (dn ? SaveGame.Clock(dn.timeOfDay) : "") + (weather.Length > 0 ? "   ·   " + weather : "") + "\n" +
                    c + UiKit.L("Paise", "Money") + "   " + HudStyle.Rupees(GameState.Money) + "\n" +
                    c + UiKit.L("Missions", "Missions") + "   " + done + " / " + total + "\n" +
                    c + UiKit.L("Khela", "Played") + "   " + (mins >= 60 ? $"{mins / 60}h {mins % 60}m" : Loc.T($"{mins} min")) + "\n" +
                    c + UiKit.L("Aakhri save", "Last saved") + "   " + saved;
            }
        }

        void RefreshMissionList()
        {
            var mm = MissionManager.Instance;
            if (!mm || !missionList) return;
            var sb = new System.Text.StringBuilder();
            int done = 0;
            foreach (var m in mm.missions)
            {
                bool isDone = GameState.IsDone(m.id);
                if (isDone) done++;
                sb.Append(isDone ? "<color=#74EB8C>✓   " : "<color=#FFA91F>◆   ").Append(Loc.T(m.title)).Append("</color>")
                  .Append("   <color=#B8C2C9>").Append(HudStyle.Rupees(m.reward)).Append("</color>");
                if (m.nightOnly) sb.Append("  <color=#33D1C2>").Append(Loc.L("(raat)", "(night)")).Append("</color>");
                sb.Append('\n');
            }
            sb.Append("\n<color=#B8C2C9>").Append(done).Append(" / ").Append(mm.missions.Count).Append(" ").Append(Loc.L("poore", "done")).Append("</color>\n");
            sb.Append("<color=#B8C2C9>").Append(Loc.L("Radar pe sunehre 'M' marker se mission shuru hota hai.", "Gold 'M' markers on the radar start missions.")).Append("</color>");
            missionList.text = sb.ToString();
        }

        public void TogglePause()
        {
            if (busy) return;
            if (Current == State.Playing)
            {
                Current = State.Paused;
                Time.timeScale = 0f;
                GameInput.Paused = true;
                GameInput.UiBlocking = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshMenuInfo();
                Show(pauseMenu, true);
                OpenRight(status);
                SetHud(0f);   // the menu alone over the stilled world
                Haptics.Light();
            }
            else if (Current == State.Paused)
            {
                foreach (var s in screens) Show(s, false);
                Current = State.Playing;
                Time.timeScale = CheatCodes.GameSpeed;   // the pace the TEZ / DHEEMA cheat set (1 otherwise)
                GameInput.Paused = false;
                GameInput.UiBlocking = HUD.Instance && HUD.Instance.DialogOpen;
                SetHud(1f);
            }
        }

        /// <summary>Esc / Android back: closes the top panel, else the pause menu.</summary>
        void Back()
        {
            if (confirm.on) { Show(confirm, false); return; }
            foreach (var r in new[] { settings, credits, missions })
                if (r.on) { Show(r, false); if (Current == State.Paused) Show(status, true); return; }
            if (Current == State.Paused) TogglePause();
        }

        // ------------------------------------------------------------------ into the game
        float introT;
        float fogStart0, fogEnd0;
        Vector3 introPlayer, introFwd;
        bool intro;

        IEnumerator StartPlaying(bool newGame)
        {
            busy = true;
            Haptics.Medium();
            fadeSpeed = 2.4f;
            SetFade(1f);
            while (fade.color.a < 0.999f) yield return null;
            foreach (var s in screens) Show(s, false);
            var p = PlayerController.Instance;
            if (MissionManager.Instance && MissionManager.Instance.Active != null) MissionManager.Instance.Abort();
            if (newGame)
            {
                SaveGame.Clear();
                if (p)
                {
                    if (p.CurrentMode == PlayerController.Mode.Vehicle) p.ExitVehicle(defaultSpawn);
                    else if (p.CurrentMode == PlayerController.Mode.Boat) p.ExitBoat(defaultSpawn);
                    else if (p.CurrentMode == PlayerController.Mode.Mount) p.ExitMount(defaultSpawn);
                    SaveGame.Place(p, defaultSpawn, defaultYaw);
                }
                if (DayNightCycle.Instance) { DayNightCycle.Instance.timeOfDay = defaultHour; DayNightCycle.Instance.Apply(); }
                if (WeatherSystem.Instance) WeatherSystem.Instance.current = WeatherSystem.Kind.Clear;
            }
            else if (!sessionStarted && SaveGame.Exists) SaveGame.Apply();
            sessionStarted = true;
            if (ThirdPersonCamera.Instance) { ThirdPersonCamera.Instance.SnapBehind(); ThirdPersonCamera.Instance.pitch = 12f; }
            Current = State.Intro;
            yield return null;
            yield return null;

            // the intro (LateUpdate moves the camera)
            p = PlayerController.Instance;
            introPlayer = p ? p.transform.position : Vector3.zero;
            introFwd = p ? Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized : Vector3.forward;
            if (introFwd.sqrMagnitude < 0.5f) introFwd = Vector3.forward;
            fogStart0 = RenderSettings.fogStartDistance;
            fogEnd0 = RenderSettings.fogEndDistance;
            introT = 0f;
            intro = true;
            fadeSpeed = 1f / 0.4f;
            SetFade(0f);
            Show(introMarker, true);
            introMarker.rt.SetAsLastSibling();
            fade.transform.SetAsLastSibling();
            Whoosh(0.55f, 0.5f);
            bool d1 = false, d2 = false, d3 = false;
            while (introT < IntroLength)
            {
                introT += Time.unscaledDeltaTime;
                if (!d1 && introT > 0.40f) { d1 = true; Whoosh(0.7f, 0.8f); Haptics.Light(); }
                if (!d2 && introT > 1.10f) { d2 = true; Whoosh(0.85f, 0.8f); Haptics.Light(); }
                if (!d3 && introT > 1.60f) { d3 = true; Whoosh(1.0f, 0.55f); }
                SetHud(Mathf.InverseLerp(2.45f, IntroLength, introT));
                yield return null;
            }
            intro = false;
            RenderSettings.fogStartDistance = fogStart0;
            RenderSettings.fogEndDistance = fogEnd0;
            Show(introMarker, false);
            introMarker.g.alpha = 0f;
            introMarker.rt.gameObject.SetActive(false);
            EnterPlaying(true);
            busy = false;
        }

        const float IntroLength = 3f;

        void EnterPlaying(bool greet)
        {
            Current = State.Playing;
            Time.timeScale = 1f;
            GameInput.Paused = false;
            GameInput.UiBlocking = false;
            SetHud(1f);
            if (SaveTracker.Instance) SaveTracker.Instance.Playing = true;
            if (greet)
            {
                Haptics.Medium();
                GameEvents.ShowToast(Application.isMobilePlatform ? "Nainital mein swagat hai!" : "Nainital mein swagat hai!  (H = controls, M = map)", 4f);
            }
        }

        IEnumerator BackToMenu()
        {
            busy = true;
            fadeSpeed = 2.4f;
            SetFade(1f);
            while (fade.color.a < 0.999f) yield return null;
            SaveGame.Save();
            if (SaveTracker.Instance) SaveTracker.Instance.Playing = false;
            if (MissionManager.Instance && MissionManager.Instance.Active != null) MissionManager.Instance.Abort();
            foreach (var s in screens) Show(s, false);
            Time.timeScale = 1f;
            GameInput.Paused = false;
            GameInput.UiBlocking = true;
            SetHud(0f);
            Current = State.Menu;
            menuT = 0f;
            yield return null;
            ShowMainMenu();
            fadeSpeed = 1.6f;
            SetFade(0f);
            busy = false;
        }

        IEnumerator Quit()
        {
            busy = true;
            if (sessionStarted && SaveTracker.Instance && SaveTracker.Instance.HasSafe) SaveGame.Save();
            fadeSpeed = 2.4f;
            SetFade(1f);
            while (fade.color.a < 0.999f) yield return null;
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Whoosh(float pitch, float vol)
        {
            var c = Resources.Load<AudioClip>(Random.value < 0.5f ? "Audio/whoosh_1" : "Audio/whoosh_2");
            if (!c) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(c, vol * GameSettings.Effects);
        }

        void SetHud(float a)
        {
            if (HUD.Instance) HUD.Instance.SetAlpha(a);
        }

        void SetFade(float a, bool now = false)
        {
            fadeTarget = a;
            if (now && fade) fade.color = new Color(0, 0, 0, a);
        }

        // ------------------------------------------------------------------ the menu's camera: over Naini Lake
        Vector3 shotFrom, shotTo, shotLook;
        float menuT;

        void SetupMenuShot()
        {
            var lake = LakeArea.Instance;
            if (!lake || lake.outline == null || lake.outline.Length < 3)
            {
                var p = PlayerController.Instance;
                Vector3 c = p ? p.transform.position : Vector3.zero;
                shotFrom = c + new Vector3(-40, 30, -40);
                shotTo = c + new Vector3(-20, 25, -20);
                shotLook = c;
                return;
            }
            // the lake's long axis: its two farthest points; the camera floats up it from the south (Tallital) end,
            // looking at the north end (Mallital, the Flats, Naina Peak behind)
            var o = lake.outline;
            int ia = 0, ib = 0;
            float best = 0f;
            int step = Mathf.Max(1, o.Length / 300);
            for (int i = 0; i < o.Length; i += step)
                for (int j = i + 1; j < o.Length; j += step)
                {
                    float d = (o[i] - o[j]).sqrMagnitude;
                    if (d > best) { best = d; ia = i; ib = j; }
                }
            Vector2 a = o[ia], b = o[ib];
            Vector2 north = a.y > b.y ? a : b, south = a.y > b.y ? b : a;
            float wy = lake.waterY;
            Vector3 S = new Vector3(south.x, wy, south.y), N = new Vector3(north.x, wy, north.y);
            shotFrom = Vector3.Lerp(S, N, 0.16f) + Vector3.up * 34f;
            shotTo = Vector3.Lerp(S, N, 0.42f) + Vector3.up * 24f;
            shotLook = N + Vector3.up * 70f + (N - S).normalized * 250f;
        }

        void LateUpdate()
        {
            // the black: eased, in real time
            if (fade)
            {
                float a = Mathf.MoveTowards(fade.color.a, fadeTarget, Time.unscaledDeltaTime * fadeSpeed);
                fade.color = new Color(0, 0, 0, a);
                fade.raycastTarget = a > 0.05f;
            }
            AnimateScreens();
            if (cornerGroup)
            {
                bool covered = (settings != null && settings.on) || (credits != null && credits.on);   // null for a frame around a reload
                cornerGroup.alpha = Mathf.MoveTowards(cornerGroup.alpha, covered ? 0f : 1f, Time.unscaledDeltaTime * 6f);
                cornerGroup.blocksRaycasts = !covered;
            }

            if (!cam) cam = Camera.main;
            if (!cam) return;
            if (Current == State.Menu)
            {
                menuT += Time.unscaledDeltaTime;
                float u = 0.5f - 0.5f * Mathf.Cos(menuT * Mathf.PI * 2f / 150f);   // there and back in two and a half minutes
                Vector3 pos = Vector3.Lerp(shotFrom, shotTo, u);
                Vector3 look = shotLook + new Vector3(Mathf.Sin(menuT * 0.045f) * 60f, Mathf.Sin(menuT * 0.07f) * 12f, 0f);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                cam.fieldOfView = 52f;
            }
            else if (Current == State.Intro && intro) IntroCamera();
            // (the two frames in the black before the intro: the third-person camera's own pose)
        }

        void IntroCamera()
        {
            // the third-person camera has just put the camera where it wants it (it runs first): that is where we land
            Vector3 endPos = cam.transform.position;
            Quaternion endRot = cam.transform.rotation;
            float endFov = cam.fieldOfView;
            Vector3 pp = PlayerController.Instance ? PlayerController.Instance.transform.position : introPlayer;
            const float H0 = 380f, H1 = 140f, H2 = 42f;
            float t = introT, h;
            if (t < 0.40f) h = H0;
            else if (t < 0.95f) h = Mathf.Lerp(H0, H1, UiKit.EaseInOut((t - 0.40f) / 0.55f));
            else if (t < 1.10f) h = H1;
            else if (t < 1.60f) h = Mathf.Lerp(H1, H2, UiKit.EaseInOut((t - 1.10f) / 0.50f));
            else h = H2;
            // far fog while high up, back to normal by the second drop
            float hiK = Mathf.InverseLerp(H2, H0, h);
            RenderSettings.fogStartDistance = Mathf.Lerp(fogStart0, Mathf.Max(fogStart0, 700f), hiK);
            RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd0, Mathf.Max(fogEnd0, 2800f), hiK);

            Vector3 top = new Vector3(pp.x, pp.y + h, pp.z);
            if (t < 1.60f)
            {
                cam.transform.SetPositionAndRotation(top, Quaternion.LookRotation(Vector3.down, introFwd));
                cam.fieldOfView = 55f;
            }
            else
            {
                float e = UiKit.EaseInOut((t - 1.60f) / (IntroLength - 1.60f));
                // across first, down late: the camera stays above the roofs and comes down just behind the player
                float ey = e * e * e;
                Vector3 pos = new Vector3(Mathf.Lerp(top.x, endPos.x, e), Mathf.Lerp(top.y, endPos.y, ey), Mathf.Lerp(top.z, endPos.z, e));
                // keep the player in frame all the way: look from the top straight down at them, ending on the camera's own aim
                Vector3 pivot = pp + Vector3.up * 1.55f;
                Vector3 endLook = endPos + endRot * Vector3.forward * Mathf.Max(1f, Vector3.Distance(endPos, pivot));
                Vector3 look = Vector3.Lerp(pp, endLook, e);
                Vector3 dir = look - pos;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.down;
                Vector3 up = Vector3.Slerp(introFwd, Vector3.up, e);
                Quaternion rot = e >= 0.999f ? endRot : Quaternion.LookRotation(dir, up);
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = Mathf.Lerp(55f, endFov, e);
            }

            // the pin over the player while high up
            float pinA = Mathf.Clamp01(t / 0.25f) * (1f - Mathf.InverseLerp(1.15f, 1.55f, t));
            introMarker.g.alpha = pinA;
            if (pinA > 0f)
            {
                Vector3 sp = cam.WorldToScreenPoint(pp);
                float scale = root.localScale.x;
                markerRt.anchoredPosition = new Vector2(sp.x, sp.y) / Mathf.Max(0.0001f, scale);
                float pulse = Mathf.Repeat(t * 1.6f, 1f);
                markerRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, pulse);
                markerRing.color = new Color(Aipan.Saffron.r, Aipan.Saffron.g, Aipan.Saffron.b, 1f - pulse);
            }
        }

        void AnimateScreens()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var s in screens)
            {
                if (s == introMarker) continue;
                float target = s.on ? 1f : 0f;
                if (!s.on && s.g.alpha <= 0f) { if (s.rt.gameObject.activeSelf) s.rt.gameObject.SetActive(false); continue; }
                s.g.alpha = Mathf.MoveTowards(s.g.alpha, target, dt * (s.on ? 4.5f : 6f));
                s.g.blocksRaycasts = s.on;
                s.g.interactable = s.on;
                float k = 1f - UiKit.EaseOut(s.g.alpha);
                s.rt.anchoredPosition = s.basePos + s.slide * k;
            }
        }

        void Update()
        {
            // Esc / P / the pad's Start / Android's back: the pause menu in play, else back out of the top panel
            if (GameInput.PausePressed && !busy)
            {
                if (Current == State.Playing) { if (!(HUD.Instance && HUD.Instance.DialogOpen)) TogglePause(); }
                else if (Current == State.Menu || Current == State.Paused) Back();
            }
            if (Current == State.Paused && Time.timeScale != 0f) Time.timeScale = 0f;
        }
    }

    /// <summary>A main / pause menu entry: a geru bar with a row of Aipan dots slides in behind it when touched or
    /// hovered, its diamond turns saffron; entries come in one after another when their menu opens.</summary>
    public class MenuItemFx : MonoBehaviour
    {
        TapButton tb;
        Image bar, dots, bullet;
        Text label;
        public Text sub;
        float delay, k, appear;
        public float shownAt;
        CanvasGroup g;
        RectTransform rt;
        float baseX;

        public void Setup(TapButton tb, Image bar, Image dots, Image bullet, Text label, Text sub, float delay)
        {
            this.tb = tb; this.bar = bar; this.dots = dots; this.bullet = bullet; this.label = label; this.sub = sub; this.delay = delay;
            g = gameObject.AddComponent<CanvasGroup>();
            rt = (RectTransform)transform;
            baseX = rt.anchoredPosition.x;
            bar.rectTransform.pivot = new Vector2(0, 0.5f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool hot = tb.Held || tb.Hovered;
            k = Mathf.MoveTowards(k, hot ? 1f : 0f, dt * (hot ? 7f : 4f));
            float e = UiKit.EaseOut(k);
            bar.color = new Color(Aipan.Geru.r, Aipan.Geru.g, Aipan.Geru.b, 0.92f * e);
            bar.rectTransform.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, e), 1f, 1f);
            dots.color = new Color(Aipan.Rice.r, Aipan.Rice.g, Aipan.Rice.b, 0.55f * e);
            bullet.color = Color.Lerp(new Color(1, 1, 1, 0.45f), Aipan.Saffron, e);
            bullet.rectTransform.localEulerAngles = new Vector3(0, 0, e * 45f);
            label.rectTransform.anchoredPosition = new Vector2(62 + e * 14f, label.rectTransform.anchoredPosition.y);
            if (sub) sub.rectTransform.anchoredPosition = new Vector2(64 + e * 14f, sub.rectTransform.anchoredPosition.y);
            appear = UiKit.EaseOut((Time.unscaledTime - shownAt - delay) / 0.35f);
            g.alpha = appear;
            rt.anchoredPosition = new Vector2(baseX - 30f * (1f - appear), rt.anchoredPosition.y);
        }
    }

    /// <summary>Turns a UI element slowly (the menu's mandala).</summary>
    public class Spin : MonoBehaviour
    {
        public float speed = 10f;
        void Update() => transform.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * speed);
    }
}
