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
    /// The whole HUD, built in code (user, 2026-11-15: a full open-world HUD; the game's own look, HudStyle). Bottom
    /// left: the radar ringed by health (Minimap) and the place, time and weather beside it. Top right: money, with each
    /// change popping up under it. Top left: the mission card; top centre: its timer, and a place's name as you enter it.
    /// Left, above the radar: notifications sliding in. Bottom centre: the prompt with its key. Bottom right: the
    /// speedometer while driving. Centre: mission banners. Red at the screen's edges when hurt, pulsing when low.
    /// Also the dialog, help, pause menu (missions, graphics, weather, language), enemy health bars and, on a phone,
    /// the touch controls.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }

        Font font, fontBold;
        Canvas canvas;
        Image vignette;
        Text placeText, clockText, promptText, promptKey, dialogTitle, dialogBody, helpText, moneyText, moneyDelta;
        Text objTitle, objText, timerText, bannerTitle, bannerSub, missionList, qualityLabel, langHindi, langEnglish, revealText;
        RectTransform promptPanel, objPanel, timerPill, bannerRt, revealLine, toastRoot, moneyDeltaRt, sideButtons;
        GameObject dialogPanel, helpPanel, pausePanel, missionsPanel, promptKeyChip, areaInfo;
        CanvasGroup revealGroup, bannerGroup, moneyDeltaGroup;
        Image bannerLine;
        float revealTimer, flash, bannerTimer, moneyDeltaTimer;
        string revealSrc = "", placeSrc = "", dialogTitleSrc = "", dialogBodySrc = "", bannerTitleSrc = "", bannerSubSrc = "";
        int lastMoney = int.MinValue;
        readonly List<(EnemyAI e, Image bg, Image fill)> enemyBars = new List<(EnemyAI, Image, Image)>();
        readonly List<(Text t, string src)> locTexts = new List<(Text, string)>();
        readonly List<Toast> toasts = new List<Toast>();
        bool showTouch;
        int quality = 2;

        class Toast { public RectTransform rt; public CanvasGroup g; public Text t; public string src; public float life, age; }

        void Awake()
        {
            Instance = this;
            font = HudStyle.Regular;
            fontBold = HudStyle.Bold;
            GameAssets.Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");   // world-space text keeps its font
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

        /// <summary>A rounded glass panel (9-sliced).</summary>
        public Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, bool small = false)
        {
            var img = Img(name, parent, anchor, pivot, pos, size, c);
            img.sprite = small ? HudStyle.RoundedSmall : HudStyle.Rounded;
            img.type = Image.Type.Sliced;
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
            t.lineSpacing = 0.92f;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>The same in the bold face.</summary>
        public Text TxtB(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c)
        {
            var t = Txt(name, parent, anchor, pivot, pos, size, fontSize, align, c);
            t.font = fontBold;
            return t;
        }

        Text LocTxt(Text t, string src) { locTexts.Add((t, src)); t.text = Loc.T(src); return t; }

        public Button Btn(string label, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, System.Action onClick, int fontSize = 26)
        {
            var img = Panel("Btn_" + label, parent, anchor, new Vector2(0.5f, 0.5f), pos, size, new Color(0.05f, 0.07f, 0.10f, 0.80f), true);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
            var t = Txt("Label", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), size, fontSize, TextAnchor.MiddleCenter, HudStyle.Ink);
            LocTxt(t, label);
            return b;
        }

        /// <summary>A thin coloured bar on a panel's left edge.</summary>
        Image Accent(Transform panel, Color c, float inset = 14f)
        {
            var a = Panel("Accent", panel, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(6, 10), c, true);
            var rt = a.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
            rt.offsetMin = new Vector2(0, inset); rt.offsetMax = new Vector2(6, -inset);
            a.raycastTarget = false;
            return a;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        // ------------------------------------------------------------------ build
        void Build()
        {
            Transform root = canvas.transform;
            showTouch = Application.isMobilePlatform || PlayerPrefs.GetInt("nt_touch", 0) == 1;   // nt_touch: the editor's phone play test

            // hurt: red at the screen's edges
            vignette = Img("Vignette", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(HudStyle.Coral.r, 0.05f, 0.04f, 0f));
            vignette.sprite = HudStyle.Vignette;
            vignette.raycastTarget = false;
            Stretch(vignette.rectTransform);

            BuildMoney(root);
            BuildMission(root);
            BuildReveal(root);
            BuildBanner(root);
            BuildPrompt(root);
            BuildToasts(root);

            // radar (bottom left) and the place / time card beside it
            var gm = FindAnyObjectByType<GameManager>();
            gameObject.AddComponent<Minimap>().Build(this, root, gm ? gm.blipNames : null, gm ? gm.blipPositions : null);
            var info = Panel("AreaInfo", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40 + Minimap.Footprint + 16, 64), new Vector2(380, 92), HudStyle.Glass);
            info.raycastTarget = false;
            areaInfo = info.gameObject;
            Accent(info.transform, HudStyle.Teal);
            placeText = TxtB("Place", info.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -8), new Vector2(350, 44), 34, TextAnchor.MiddleLeft, HudStyle.Ink);
            clockText = Txt("Clock", info.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 8), new Vector2(350, 34), 25, TextAnchor.MiddleLeft, HudStyle.InkDim);

            BuildSpeedometer(root);
            BuildDialog(root);
            BuildHelp(root);
            BuildPause(root);

            if (showTouch) gameObject.AddComponent<TouchControls>().Build(this, root);
            dialogPanel.transform.SetAsLastSibling();
            helpPanel.transform.SetAsLastSibling();
            pausePanel.transform.SetAsLastSibling();
        }

        void BuildMoney(Transform root)
        {
            moneyText = TxtB("Money", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -18), new Vector2(460, 64), 54, TextAnchor.MiddleRight, HudStyle.Ink);
            moneyDeltaRt = Rect("MoneyDelta", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -80), new Vector2(460, 40));
            moneyDeltaGroup = moneyDeltaRt.gameObject.AddComponent<CanvasGroup>();
            moneyDeltaGroup.alpha = 0f;
            moneyDelta = TxtB("Text", moneyDeltaRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(460, 40), 34, TextAnchor.MiddleRight, HudStyle.Good);

            // Din/Raat, Help, Menu: shown on a phone, or on a PC while the mouse is free (the keys do the same)
            sideButtons = Rect("SideButtons", root, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            Stretch(sideButtons);
            Btn("Din / Raat", sideButtons, new Vector2(1, 1), new Vector2(-110, SideButtonY(0)), SideButtonSize, () => { if (DayNightCycle.Instance) DayNightCycle.Instance.ToggleDayNight(); }, SideButtonFont);
            Btn("Help", sideButtons, new Vector2(1, 1), new Vector2(-110, SideButtonY(1)), SideButtonSize, () => helpPanel.SetActive(!helpPanel.activeSelf), SideButtonFont);
            Btn("Menu", sideButtons, new Vector2(1, 1), new Vector2(-110, SideButtonY(2)), SideButtonSize, TogglePause, SideButtonFont);
        }

        // the right-hand column of buttons (TouchControls adds Map, Cheat, Mausam and Bug under these three). On a phone
        // the canvas is only about 1000 high, so the column is tighter and ends clear of the Use button.
        public float SideButtonY(int i) => showTouch ? -120f - i * 50f : -140f - i * 62f;
        public Vector2 SideButtonSize => showTouch ? new Vector2(170, 44) : new Vector2(180, 54);
        public int SideButtonFont => showTouch ? 24 : 26;

        void BuildMission(Transform root)
        {
            var op = Panel("Objective", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -36), new Vector2(560, 150), HudStyle.Glass);
            op.raycastTarget = false;
            objPanel = op.rectTransform;
            Accent(op.transform, HudStyle.Marigold);
            var tag = Panel("Tag", op.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(112, 28), new Color(HudStyle.Marigold.r, HudStyle.Marigold.g, HudStyle.Marigold.b, 0.95f), true);
            tag.raycastTarget = false;
            LocTxt(TxtB("Label", tag.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(112, 28), 20, TextAnchor.MiddleCenter, new Color(0.08f, 0.06f, 0.02f)), "MISSION")
                .GetComponent<Shadow>().enabled = false;
            objTitle = TxtB("ObjTitle", op.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -46), new Vector2(516, 42), 34, TextAnchor.MiddleLeft, HudStyle.Ink);
            objText = Txt("ObjText", op.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -90), new Vector2(516, 60), 27, TextAnchor.UpperLeft, HudStyle.InkDim);
            objPanel.gameObject.SetActive(false);

            var tp = Panel("Timer", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(200, 66), HudStyle.Glass);
            tp.raycastTarget = false;
            timerPill = tp.rectTransform;
            timerText = TxtB("Text", tp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(200, 66), 46, TextAnchor.MiddleCenter, HudStyle.Ink);
            timerPill.gameObject.SetActive(false);
        }

        void BuildReveal(Transform root)
        {
            // a place's name as you walk into it: top centre, a teal line drawing out under it
            // on a phone a little lower, clear of the mission card beside the radar
            var r = Rect("LocationReveal", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, showTouch ? -210 : -116), new Vector2(1100, 110));
            revealGroup = r.gameObject.AddComponent<CanvasGroup>();
            revealGroup.alpha = 0f;
            revealGroup.blocksRaycasts = false;
            var glow = Img("Glow", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(900, 150), new Color(0, 0, 0, 0.35f));
            glow.sprite = HudStyle.Glow;
            glow.raycastTarget = false;
            revealText = TxtB("Name", r, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1100, 70), 62, TextAnchor.MiddleCenter, HudStyle.Ink);
            revealLine = Img("Line", r, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(10, 3), HudStyle.Teal).rectTransform;
            foreach (float s in new[] { -1f, 1f })
            {
                var d = Img("Dot", revealLine, new Vector2(s < 0 ? 0f : 1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12), HudStyle.Teal);
                d.sprite = HudStyle.Diamond;
                d.raycastTarget = false;
            }
        }

        void BuildBanner(Transform root)
        {
            bannerRt = Rect("Banner", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1500, 240));
            bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerGroup.blocksRaycasts = false;
            var glow = Img("Glow", bannerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1500, 300), new Color(0, 0, 0, 0.5f));
            glow.sprite = HudStyle.Glow;
            glow.raycastTarget = false;
            bannerTitle = TxtB("BannerTitle", bannerRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1500, 110), 96, TextAnchor.MiddleCenter, HudStyle.Marigold);
            var ol = bannerTitle.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.6f);
            ol.effectDistance = new Vector2(2, -2);
            bannerLine = Img("Line", bannerRt, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(560, 4), HudStyle.Marigold);
            bannerLine.raycastTarget = false;
            bannerSub = Txt("BannerSub", bannerRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(1200, 100), 34, TextAnchor.UpperCenter, HudStyle.Ink);
        }

        void BuildPrompt(Transform root)
        {
            // on a phone it sits higher: the speedometer takes the bottom centre there
            var pp = Panel("Prompt", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, showTouch ? 250 : 168), new Vector2(520, 70), HudStyle.Glass);
            pp.raycastTarget = false;
            promptPanel = pp.rectTransform;
            var chip = Panel("Key", pp.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(46, 46), HudStyle.Marigold, true);
            chip.raycastTarget = false;
            promptKeyChip = chip.gameObject;
            promptKey = TxtB("K", chip.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(46, 46), 30, TextAnchor.MiddleCenter, new Color(0.08f, 0.06f, 0.02f));
            promptKey.GetComponent<Shadow>().enabled = false;
            promptKey.text = "E";
            promptText = Txt("PromptText", pp.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(74, 0), new Vector2(900, 60), 30, TextAnchor.MiddleLeft, HudStyle.Ink);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void BuildToasts(Transform root)
        {
            // notifications stack above the radar on a PC; on a phone the radar is up top, so they go under it
            toastRoot = showTouch
                ? Rect("Toasts", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -400), new Vector2(560, 400))
                : Rect("Toasts", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40 + Minimap.Footprint + 26), new Vector2(560, 400));
        }

        Image speedArc, speedTrack;
        Text speedNum, speedGear;
        Image[] dmgPips;
        RectTransform speedRt;

        void BuildSpeedometer(Transform root)
        {
            speedRt = Rect("Speed", root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 40), new Vector2(280, 250));
            var g = Img("Disc", speedRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(222, 222), new Color(0.02f, 0.03f, 0.04f, 0.72f));
            g.sprite = HudStyle.Disc;
            g.raycastTarget = false;
            Image Arc(string n, Color c, float size)
            {
                var a = Img(n, speedRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(size, size), c);
                a.sprite = HudStyle.RingSprite;
                a.type = Image.Type.Filled;
                a.fillMethod = Image.FillMethod.Radial360;
                a.fillOrigin = (int)Image.Origin360.Bottom;
                a.fillClockwise = true;
                a.fillAmount = 0.75f;
                a.rectTransform.localEulerAngles = new Vector3(0, 0, -45f);   // the gap at the bottom
                a.raycastTarget = false;
                return a;
            }
            speedTrack = Arc("Track", new Color(1, 1, 1, 0.13f), 250);
            speedArc = Arc("Arc", HudStyle.Teal, 250);
            speedNum = TxtB("Num", speedRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(240, 90), 82, TextAnchor.MiddleCenter, HudStyle.Ink);
            Txt("Unit", speedRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(200, 30), 24, TextAnchor.MiddleCenter, HudStyle.InkDim).text = "KM/H";
            var gear = Panel("Gear", speedRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(46, 38), new Color(1, 1, 1, 0.12f), true);
            gear.raycastTarget = false;
            speedGear = TxtB("G", gear.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(46, 38), 28, TextAnchor.MiddleCenter, HudStyle.Ink);
            // damage: eight pips under the dial, filling coral as the car takes knocks (VehicleDamage: smoke at 4-5, fire at 7-8)
            dmgPips = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                dmgPips[i] = Panel("Pip", speedRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2((i - 3.5f) * 26f, 6), new Vector2(20, 8), new Color(1, 1, 1, 0.16f), true);
                dmgPips[i].raycastTarget = false;
            }
            speedRt.gameObject.SetActive(false);
        }

        void BuildDialog(Transform root)
        {
            var dp = Panel("Dialog", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1180, 300), HudStyle.GlassDeep);
            dialogPanel = dp.gameObject;
            Accent(dp.transform, HudStyle.Marigold, 22f);
            dialogTitle = TxtB("Title", dp.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -20), new Vector2(1110, 50), 40, TextAnchor.MiddleLeft, HudStyle.Marigold);
            dialogBody = Txt("Body", dp.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -80), new Vector2(1110, 170), 30, TextAnchor.UpperLeft, HudStyle.Ink);
            var ok = Btn("Theek hai", dp.transform, new Vector2(1, 0), new Vector2(-130, 48), new Vector2(210, 58), CloseDialog, 28);
            ok.GetComponent<Image>().color = new Color(HudStyle.Marigold.r * 0.85f, HudStyle.Marigold.g * 0.85f, HudStyle.Marigold.b * 0.85f, 0.95f);
            ok.GetComponentInChildren<Text>().color = new Color(0.08f, 0.06f, 0.02f);
            ok.GetComponentInChildren<Text>().GetComponent<Shadow>().enabled = false;
            dialogPanel.SetActive(false);
        }

        void BuildHelp(Transform root)
        {
            var help = Panel("Help", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 740), HudStyle.GlassDeep);
            helpPanel = help.gameObject;
            Img("Line", help.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(980, 2), HudStyle.GlassLine).raycastTarget = false;
            helpText = Txt("HelpText", help.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(980, 690), 27, TextAnchor.UpperLeft, HudStyle.Ink);
            helpPanel.SetActive(false);
        }

        void BuildPause(Transform root)
        {
            var pp = Img("Pause", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.01f, 0.015f, 0.02f, 0.62f));
            Stretch(pp.rectTransform);
            pausePanel = pp.gameObject;
            var box = Panel("PauseBox", pp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, 0), new Vector2(480, 760), HudStyle.GlassDeep);
            LocTxt(TxtB("PauseTitle", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(420, 64), 58, TextAnchor.MiddleCenter, HudStyle.Marigold), "PAUSE");
            Img("Line", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -96), new Vector2(120, 3), HudStyle.Teal).raycastTarget = false;
            float y = -150;
            void B(string label, System.Action a) { Btn(label, box.transform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(400, 58), a, 28); y -= 70; }
            B("Wapas khelo", TogglePause);
            B("Missions", () => { missionsPanel.SetActive(!missionsPanel.activeSelf); RefreshMissionList(); });
            B("Graphics", () => { quality = (quality + 1) % 3; PlayerPrefs.SetInt("nt_quality", quality); ApplyQuality(quality); });
            qualityLabel = Txt("QualityLabel", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y + 32), new Vector2(400, 28), 22, TextAnchor.MiddleCenter, HudStyle.Teal);
            y -= 18;
            B("Mausam badlo", () => { if (WeatherSystem.Instance) WeatherSystem.Instance.Next(); });
            B("Din / Raat badlo", () => { if (DayNightCycle.Instance) DayNightCycle.Instance.ToggleDayNight(); });
            B("Mission chhodo", () => { if (MissionManager.Instance) MissionManager.Instance.Abort(); });
            B("Game band karo", () => { Application.Quit(); });
            // language: a two-way switch at the foot of the menu
            var lang = Panel("Language", box.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(400, 56), new Color(1, 1, 1, 0.06f), true);
            var bh = Btn("HINDI", lang.transform, new Vector2(0.25f, 0.5f), Vector2.zero, new Vector2(190, 46), () => Loc.SetEnglish(false), 24);
            var be = Btn("ENGLISH", lang.transform, new Vector2(0.75f, 0.5f), Vector2.zero, new Vector2(190, 46), () => Loc.SetEnglish(true), 24);
            langHindi = bh.GetComponentInChildren<Text>();
            langEnglish = be.GetComponentInChildren<Text>();

            var mp = Panel("MissionsBox", pp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300, 0), new Vector2(660, 760), HudStyle.GlassDeep);
            missionsPanel = mp.gameObject;
            missionList = Txt("MissionList", mp.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(600, 700), 28, TextAnchor.UpperLeft, HudStyle.Ink);
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
            if (langHindi) langHindi.color = en ? HudStyle.InkFaint : HudStyle.Marigold;
            if (langEnglish) langEnglish.color = en ? HudStyle.Marigold : HudStyle.InkFaint;
            helpText.text = en ? HelpEn : HelpHi;
            revealText.text = Loc.T(revealSrc).ToUpperInvariant();
            placeText.text = Loc.T(placeSrc);
            dialogTitle.text = Loc.T(dialogTitleSrc);
            dialogBody.text = Loc.T(dialogBodySrc);
            bannerTitle.text = Loc.T(bannerTitleSrc).ToUpperInvariant();
            bannerSub.text = Loc.T(bannerSubSrc);
            foreach (var t in toasts) if (t.t) t.t.text = Loc.T(t.src);
            if (pausePanel && pausePanel.activeSelf) RefreshMissionList();
        }

        void RefreshMissionList()
        {
            var mm = MissionManager.Instance;
            if (!mm || !missionList) return;
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=40><b>MISSIONS</b></size>\n\n");
            int done = 0;
            foreach (var m in mm.missions)
            {
                bool isDone = GameState.IsDone(m.id);
                if (isDone) done++;
                sb.Append(isDone ? "<color=#74EB8C>✓  " : "<color=#FFB829>◆  ").Append(Loc.T(m.title)).Append("</color>")
                  .Append("   <color=#B8C2C9>").Append(HudStyle.Rupees(m.reward)).Append("</color>");
                if (m.nightOnly) sb.Append(Loc.English ? "  <color=#33D1C2>(night)</color>" : "  <color=#33D1C2>(raat)</color>");
                sb.Append('\n');
            }
            sb.Append("\n<color=#B8C2C9>").Append(done).Append(" / ").Append(mm.missions.Count).Append(Loc.English ? " done" : " poore").Append("</color>\n");
            sb.Append(Loc.English ? "<color=#B8C2C9>Gold 'M' markers on the radar start missions.</color>" : "<color=#B8C2C9>Radar pe sunehre 'M' marker se mission shuru hota hai.</color>");
            missionList.text = sb.ToString();
        }

        const string HelpHi =
            "<size=40><b>NAINITAL  -  Controls</b></size>\n\n" +
            "<color=#FFB829>WASD / Joystick</color>  chalo     <color=#FFB829>Shift</color>  daudo     <color=#FFB829>Alt</color>  dheere chalo\n" +
            "<color=#FFB829>Mouse / right side drag</color>  camera ghumao     <color=#FFB829>Scroll</color>  zoom\n" +
            "<color=#FFB829>Space</color>  jump     <color=#FFB829>Ctrl / C</color>  roll (dodge)\n" +
            "<color=#FFB829>Left click</color>  punch combo (3rd hit kick)     <color=#FFB829>F</color>  kick     <color=#FFB829>Right click / Q</color>  block\n" +
            "<color=#FFB829>E</color>  baat karo / padho / khao / naav / bench / gaadi (chalti traffic wali bhi) / mission shuru\n" +
            "<color=#FFB829>Gaadi</color>  W/S race-brake, A/D steering, Space handbrake, G horn, E utro\n" +
            "<color=#FFB829>N</color>  din <-> raat     <color=#FFB829>M</color>  poora map     <color=#FFB829>Esc / P</color>  menu     <color=#FFB829>H</color>  help\n" +
            "<color=#FFB829>` ya F1</color> (phone: upar-daaye kone pe 3 tap)  cheat - JEEVAN, AMAR, GAADI, TAXI, JEEP, BUS, UDAAN (jetpack), SAFAR (teleport)\n<color=#FFB829>F2</color>  kisi bhi jagah teleport\n\n" +
            "Sunehra 'M' marker = mission. Hara roshni ka khamba = agla target.\n" +
            "Jheel mein gir gaye? Tairo, kinare pe Space dabake chadho.\n\n" +
            "<size=22><color=#B8C2C9>Map: (c) OpenStreetMap contributors (ODbL). Elevation: SRTM via AWS Terrain Tiles. Font: Rajdhani (Indian Type Foundry, SIL OFL).</color></size>";

        const string HelpEn =
            "<size=40><b>NAINITAL  -  Controls</b></size>\n\n" +
            "<color=#FFB829>WASD / Joystick</color>  move     <color=#FFB829>Shift</color>  sprint     <color=#FFB829>Alt</color>  walk\n" +
            "<color=#FFB829>Mouse / drag right side</color>  camera     <color=#FFB829>Scroll</color>  zoom\n" +
            "<color=#FFB829>Space</color>  jump     <color=#FFB829>Ctrl / C</color>  dodge roll\n" +
            "<color=#FFB829>Left click</color>  punch combo (3rd hit kicks)     <color=#FFB829>F</color>  kick     <color=#FFB829>Right click / Q</color>  block\n" +
            "<color=#FFB829>E</color>  talk / read / eat / boat / bench / car / start mission\n" +
            "<color=#FFB829>Car</color>  W/S throttle-brake, A/D steer, Space handbrake, G horn, E get out\n" +
            "<color=#FFB829>N</color>  day <-> night     <color=#FFB829>M</color>  full map     <color=#FFB829>Esc / P</color>  menu     <color=#FFB829>H</color>  help\n" +
            "<color=#FFB829>` or F1</color> (phone: 3 taps top-right)  cheats - JEEVAN, AMAR, GAADI, TAXI, JEEP, BUS, UDAAN (jetpack), SAFAR (teleport)\n<color=#FFB829>F2</color>  teleport to a place\n\n" +
            "Gold 'M' marker = mission. Green light beam = next objective.\n" +
            "Fell in the lake? Swim to the bank and press Space to climb out.\n\n" +
            "<size=22><color=#B8C2C9>Map: (c) OpenStreetMap contributors (ODbL). Elevation: SRTM via AWS Terrain Tiles. Font: Rajdhani (Indian Type Foundry, SIL OFL).</color></size>";

        // ------------------------------------------------------------------ events
        void OnToast(string msg, float seconds)
        {
            if (string.IsNullOrEmpty(msg)) return;
            // the same message again only refreshes its card
            foreach (var t in toasts) if (t.src == msg) { t.life = Mathf.Max(t.life, t.age + seconds); return; }
            var card = Panel("Toast", toastRoot, new Vector2(0, showTouch ? 1 : 0), new Vector2(0, showTouch ? 1 : 0), Vector2.zero, new Vector2(520, 64), HudStyle.Glass);
            card.raycastTarget = false;
            Accent(card.transform, HudStyle.Teal, 12f);
            var g = card.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.blocksRaycasts = false;
            var tx = Txt("Text", card.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(480, 60), 27, TextAnchor.MiddleLeft, HudStyle.Ink);
            tx.text = Loc.T(msg);
            tx.horizontalOverflow = HorizontalWrapMode.Wrap;
            float h = Mathf.Max(64f, tx.preferredHeight + 22f);
            card.rectTransform.sizeDelta = new Vector2(520, h);
            tx.rectTransform.sizeDelta = new Vector2(480, h);
            toasts.Insert(0, new Toast { rt = card.rectTransform, g = g, t = tx, src = msg, life = Mathf.Max(1.5f, seconds), age = 0f });
            while (toasts.Count > 4) { Destroy(toasts[toasts.Count - 1].rt.gameObject); toasts.RemoveAt(toasts.Count - 1); }
        }

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

        void OnLocation(string name)
        {
            placeSrc = name;
            placeText.text = Loc.T(name);
            revealSrc = name;
            revealText.text = Loc.T(name).ToUpperInvariant();
            revealTimer = 4.2f;
        }

        void OnBanner(string title, string sub, Color c)
        {
            bannerTitleSrc = title;
            bannerSubSrc = sub;
            bannerTitle.text = Loc.T(title).ToUpperInvariant();
            bannerSub.text = Loc.T(sub);
            bannerTitle.color = c;
            bannerLine.color = c;
            bannerTimer = 5f;
        }

        static string WeatherName()
        {
            var ws = WeatherSystem.Instance;
            if (!ws) return "";
            int k = (int)ws.current;
            return Loc.English ? new[] { "Clear", "Cloudy", "Fog", "Rain" }[k] : new[] { "Saaf", "Baadal", "Kohra", "Baarish" }[k];
        }

        void Update()
        {
            if (GameInput.PausePressed && !dialogPanel.activeSelf) TogglePause();
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            var p = PlayerController.Instance;

            // hurt: a flash of red at the edges on every hit, a slow pulse under a third of the health
            float low = 0f;
            if (p)
            {
                var h = p.Health;
                if (h.LastDamageTime > Time.time - 0.05f) flash = 0.55f;
                if (h.Fraction < 0.33f && p.CurrentMode != PlayerController.Mode.Dead) low = 0.15f + 0.09f * Mathf.Sin(t * 5f);
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 0.9f);
            vignette.color = new Color(0.75f, 0.04f, 0.03f, Mathf.Max(flash, low));

            // money, and each change under it
            int money = GameState.Money;
            moneyText.text = "<color=#FFB829>₹</color>" + HudStyle.Rupees(money).Substring(money < 0 ? 2 : 1);
            if (lastMoney != int.MinValue && money != lastMoney)
            {
                int d = money - lastMoney;
                moneyDelta.text = (d > 0 ? "+" : "") + HudStyle.Rupees(d);
                moneyDelta.color = d > 0 ? HudStyle.Good : HudStyle.Coral;
                moneyDeltaTimer = 2.6f;
            }
            lastMoney = money;
            moneyDeltaTimer -= dt;
            moneyDeltaGroup.alpha = Mathf.Clamp01(moneyDeltaTimer * 1.5f) * Mathf.Clamp01((2.6f - moneyDeltaTimer) * 6f);
            moneyDeltaRt.anchoredPosition = new Vector2(-44, -80 + Mathf.Clamp01(2.6f - moneyDeltaTimer) * 6f);

            sideButtons.gameObject.SetActive(showTouch || Cursor.visible || pausePanel.activeSelf);

            // place, time and weather
            var dn = DayNightCycle.Instance;
            string clock = "";
            if (dn)
            {
                int hh = Mathf.FloorToInt(dn.timeOfDay), mm = Mathf.FloorToInt((dn.timeOfDay - hh) * 60f);
                int h12 = hh % 12 == 0 ? 12 : hh % 12;
                clock = $"{h12}:{mm:00} {(hh < 12 ? "AM" : "PM")}";
            }
            string w = WeatherName();
            clockText.text = clock + (w.Length > 0 ? "   <color=#33D1C2>◆</color>   " + w : "");
            if (string.IsNullOrEmpty(placeSrc)) placeText.text = "Nainital";

            if (dialogPanel.activeSelf)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) CloseDialog();
            }
            var kb2 = UnityEngine.InputSystem.Keyboard.current;
            if (kb2 != null && kb2.hKey.wasPressedThisFrame) helpPanel.SetActive(!helpPanel.activeSelf);

            // prompt: the key and what it does
            // the place card steps aside for the dialog, which sits over it
            if (areaInfo.activeSelf == dialogPanel.activeSelf) areaInfo.SetActive(!dialogPanel.activeSelf);

            var cur = PlayerInteractor.Current;
            bool showPrompt = cur && !dialogPanel.activeSelf && !pausePanel.activeSelf && !helpPanel.activeSelf && !Minimap.BigMapOpen;
            if (promptPanel.gameObject.activeSelf != showPrompt) promptPanel.gameObject.SetActive(showPrompt);
            if (showPrompt)
            {
                promptKeyChip.SetActive(!showTouch);
                promptText.text = Loc.T(cur.Prompt);
                float tx = showTouch ? 26f : 74f;
                promptText.rectTransform.anchoredPosition = new Vector2(tx, 0);
                promptPanel.sizeDelta = new Vector2(tx + promptText.preferredWidth + 28f, 70);
            }
            if (TouchControls.Instance) TouchControls.Instance.SetInteractVisible(showPrompt);

            UpdateSpeedometer();

            // mission card and timer
            var mmgr = MissionManager.Instance;
            bool active = mmgr && mmgr.Active != null && mmgr.Step != null;
            if (objPanel.gameObject.activeSelf != active) objPanel.gameObject.SetActive(active);
            if (active)
            {
                objTitle.text = Loc.T(mmgr.Active.title);
                string prog = Loc.T(mmgr.Step.Progress);
                objText.text = Loc.T(mmgr.Step.text) + (prog.Length > 0 ? "  <color=#FFB829>" + prog + "</color>" : "");
                objPanel.sizeDelta = new Vector2(560, Mathf.Max(150f, 104f + objText.preferredHeight + 10f));
            }
            bool timed = active && mmgr.Timed;
            if (timerPill.gameObject.activeSelf != timed) timerPill.gameObject.SetActive(timed);
            if (timed)
            {
                float left = Mathf.Max(0f, mmgr.TimeLeft);
                timerText.text = $"{Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";
                bool hurry = left < 20f;
                timerText.color = hurry ? HudStyle.Coral : HudStyle.Ink;
                timerPill.localScale = Vector3.one * (hurry ? 1f + 0.05f * Mathf.Abs(Mathf.Sin(t * 4f)) : 1f);
            }

            // the place's name as you enter it
            revealTimer -= dt;
            float rIn = Mathf.Clamp01((4.2f - revealTimer) * 2.5f), rOut = Mathf.Clamp01(revealTimer * 1.6f);
            revealGroup.alpha = Mathf.Min(rIn, rOut);
            revealLine.sizeDelta = new Vector2(Mathf.Lerp(10f, Mathf.Min(760f, revealText.preferredWidth + 80f), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((4.2f - revealTimer) * 1.4f))), 3);

            // mission banner: settles in from a little larger
            bannerTimer -= dt;
            float bIn = Mathf.Clamp01((5f - bannerTimer) * 3f);
            bannerGroup.alpha = Mathf.Min(bIn, Mathf.Clamp01(bannerTimer * 2f));
            bannerRt.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, bIn));
            bannerLine.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(60f, Mathf.Min(900f, bannerTitle.preferredWidth * 0.8f), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((5f - bannerTimer) * 2f))), 4);

            UpdateToasts(dt);
            UpdateEnemyBars();
        }

        void UpdateToasts(float dt)
        {
            float y = 0f;
            for (int i = 0; i < toasts.Count; i++)
            {
                var tc = toasts[i];
                tc.age += dt;
                float fin = Mathf.Clamp01(tc.age * 5f), fout = Mathf.Clamp01((tc.life - tc.age) * 2.5f);
                tc.g.alpha = Mathf.Min(fin, fout);
                float x = (1f - Mathf.SmoothStep(0f, 1f, fin)) * -40f;
                float h = tc.rt.sizeDelta.y;
                var target = new Vector2(x, showTouch ? -y : y);
                tc.rt.anchoredPosition = Vector2.Lerp(tc.rt.anchoredPosition, target, i == 0 && tc.age < 0.05f ? 1f : Mathf.Clamp01(dt * 14f));
                tc.rt.anchoredPosition = new Vector2(x, tc.rt.anchoredPosition.y);
                y += (h + 10f) * Mathf.Min(fin, Mathf.Clamp01((tc.life - tc.age) * 4f) + 0.0001f);
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (toasts[i].age > toasts[i].life) { Destroy(toasts[i].rt.gameObject); toasts.RemoveAt(i); }
        }

        void UpdateSpeedometer()
        {
            var car = CarController.Driving;
            bool on = car != null && !dialogPanel.activeSelf && !Minimap.BigMapOpen;   // not showing through the dialog or the map
            if (speedRt.gameObject.activeSelf != on) speedRt.gameObject.SetActive(on);
            if (!on) return;
            float kmh = car.SpeedKmh, f = Mathf.Clamp01(kmh / 160f);
            speedNum.text = Mathf.RoundToInt(kmh).ToString();
            speedArc.fillAmount = Mathf.Lerp(speedArc.fillAmount, 0.75f * f, Mathf.Clamp01(Time.unscaledDeltaTime * 10f));
            speedArc.color = f < 0.5f ? Color.Lerp(HudStyle.Teal, HudStyle.Marigold, f * 2f) : Color.Lerp(HudStyle.Marigold, HudStyle.Coral, (f - 0.5f) * 2f);
            float fwd = car.ForwardSpeed;
            speedGear.text = kmh < 1.5f ? "N" : fwd < -0.3f ? "R" : "D";
            speedGear.color = speedGear.text == "R" ? HudStyle.Coral : HudStyle.Ink;
            var dmg = car.GetComponent<VehicleDamage>();
            int hits = dmg ? dmg.hits : 0;
            for (int i = 0; i < dmgPips.Length; i++)
                dmgPips[i].color = i < hits ? (hits >= 7 ? HudStyle.Coral : hits >= 4 ? HudStyle.Marigold : new Color(1, 1, 1, 0.75f)) : new Color(1, 1, 1, 0.16f);
        }

        void UpdateEnemyBars()
        {
            var cam = Camera.main;
            foreach (var e in EnemyAI.All)
            {
                if (!e || enemyBars.Exists(b => b.e == e)) continue;
                var bg = Panel("EnemyBar", canvas.transform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 12), new Color(0, 0, 0, 0.62f), true);
                bg.raycastTarget = false;
                var fill = Panel("Fill", bg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(92, 8), HudStyle.Coral, true);
                fill.raycastTarget = false;
                enemyBars.Add((e, bg, fill));
                bg.transform.SetSiblingIndex(1);   // under the panels, over the vignette
            }
            var scale = canvas.GetComponent<RectTransform>().localScale.x;
            foreach (var (e, bg, fill) in enemyBars)
            {
                bool show = e && cam && !e.Dead && Time.time - e.Health.LastDamageTime < 6f;
                Vector3 sp = show ? cam.WorldToScreenPoint(e.transform.position + Vector3.up * 2.1f) : Vector3.zero;
                show &= sp.z > 0;
                if (bg.gameObject.activeSelf != show) bg.gameObject.SetActive(show);
                if (!show) continue;
                bg.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, 92f * e.Health.Fraction), 8);
            }
        }
    }
}
