using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The first scene (Scenes/Boot.unity, FrontEndSetup): the splash, the language on the very first start, then the
    /// loading screen - photographs of Uttarakhand, each with its place, a line about it and its photographer, slowly
    /// drifting and crossfading, tips, and the progress - while Nainital loads behind it (user, 2026-11-17: "splash,
    /// uttrakhandi loading screen with their photos"). It lives on into the Nainital scene until the front end has the
    /// world ready, then fades to black and goes; FrontEnd fades in the main menu from there.
    /// </summary>
    public class Boot : MonoBehaviour
    {
        public static Boot Instance { get; private set; }
        /// <summary>The front end says the world is warmed up and the menu can come in.</summary>
        public static bool WorldReady;

        Canvas canvas;
        GameObject bootEvents;
        RectTransform root;
        Image black;
        AsyncOperation op;
        float shown;   // the progress drawn
        readonly UiKit.LocTexts loc = new UiKit.LocTexts();

        struct Photo { public string file, hi, en, factHi, factEn, credit; }

        static readonly Photo[] Photos =
        {
            new Photo { file = "naini", hi = "Naini Jheel, Nainital", en = "Naini Lake, Nainital",
                factHi = "Kehte hain Sati ki aankh (nain) yahan giri thi - isliye naam pada Nainital.",
                factEn = "Legend says Sati's eye (nain) fell here - and so the town is called Nainital.",
                credit = "Harshit.S.R  /  CC BY-SA 4.0" },
            new Photo { file = "kedarnath", hi = "Kedarnath Dham", en = "Kedarnath Dham",
                factHi = "3,583 metre ki oonchai pe, barah jyotirlingon mein se ek.",
                factEn = "At 3,583 metres, one of the twelve Jyotirlingas.",
                credit = "Nehasnaps0306  /  CC BY-SA 4.0" },
            new Photo { file = "phoolon_ki_ghati", hi = "Phoolon ki Ghati, Chamoli", en = "Valley of Flowers, Chamoli",
                factHi = "Monsoon mein yahan 500 se zyada kism ke jungli phool khilte hain. UNESCO World Heritage.",
                factEn = "Over 500 kinds of wild flowers bloom here in the monsoon. A UNESCO World Heritage Site.",
                credit = "Rohit Sharma, UnpetitproleX  /  CC BY-SA 4.0" },
            new Photo { file = "har_ki_pauri", hi = "Har Ki Pauri, Haridwar", en = "Har Ki Pauri, Haridwar",
                factHi = "Har shaam Ganga aarti - hazaaron diye ek saath paani pe.",
                factEn = "The Ganga aarti every evening - thousands of lamps on the water at once.",
                credit = "Ekabhishek  /  CC BY-SA 3.0" },
            new Photo { file = "laxman_jhula", hi = "Laxman Jhula, Rishikesh", en = "Laxman Jhula, Rishikesh",
                factHi = "Duniya ki yoga capital - Ganga ke upar jhoolta hua pul.",
                factEn = "The yoga capital of the world - a bridge swaying over the Ganga.",
                credit = "Dey.sandip  /  CC BY-SA 4.0" },
            new Photo { file = "tungnath", hi = "Tungnath, Chopta", en = "Tungnath, Chopta",
                factHi = "Duniya ka sabse ooncha Shiv mandir - 3,680 metre pe.",
                factEn = "The highest Shiva temple in the world - at 3,680 metres.",
                credit = "Punita keshri  /  CC BY-SA 4.0" },
            new Photo { file = "panchachuli", hi = "Panchachuli, Munsiyari", en = "Panchachuli, Munsiyari",
                factHi = "Paanch chotiyan - kehte hain Pandavon ke paanch chulhe.",
                factEn = "Five peaks - said to be the five hearths of the Pandavas.",
                credit = "Pranavspn  /  CC BY-SA 3.0" },
            new Photo { file = "himalaya", hi = "Himalaya Darshan, Tungnath", en = "The Himalaya from Tungnath",
                factHi = "Nanda Devi, Trishul, Chaukhamba - ek hi nazar mein.",
                factEn = "Nanda Devi, Trishul, Chaukhamba - all in one sweep.",
                credit = "Padmanabhan07  /  CC BY-SA 4.0" },
            new Photo { file = "corbett", hi = "Jim Corbett National Park", en = "Jim Corbett National Park",
                factHi = "Bharat ka sabse purana national park - 1936 se.",
                factEn = "India's oldest national park - since 1936.",
                credit = "Samarchauhan01  /  CC BY-SA 4.0" },
            new Photo { file = "bhimtal", hi = "Bhimtal", en = "Bhimtal",
                factHi = "Kumaon ki sabse badi jheel - Nainital se bas 22 km.",
                factEn = "The largest lake in Kumaon - just 22 km from Nainital.",
                credit = "Subhrajyoti07  /  CC BY-SA 4.0" },
        };

        static readonly string[][] Tips =
        {
            new[] { "Mall Road pe bhutta haath mein lehrate mat chalo - bandar taak mein hain.", "Don't wave a corn cob on Mall Road - the monkeys are watching." },
            new[] { "Naav chahiye? Mallital ya Tallital ki jetty pe jao.", "Want a boat? Head to the Mallital or Tallital jetty." },
            new[] { "Kohre mein gaadi dheere chalao - pahadi mod achanak aate hain.", "Drive slowly in the fog - hill bends come suddenly." },
            new[] { "Radar pe sunehra 'M' = naya mission.", "A gold 'M' on the radar = a new mission." },
            new[] { "Raat ko Mall Road ki roshni jheel mein dekho.", "At night, watch the Mall Road lights on the lake." },
            new[] { "Jheel mein gir gaye? Kinare tak tairo aur Jump dabao.", "Fell in the lake? Swim to the bank and press Jump." },
            new[] { "Snow View se Himalaya dikhta hai - ropeway se jao.", "Snow View shows the Himalaya - take the ropeway up." },
            new[] { "Pause menu mein 'Save karo' - safar wahin se shuru hoga.", "'Save' in the pause menu - your journey picks up right there." },
        };

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
            WorldReady = false;
            DontDestroyOnLoad(gameObject);
            GameSettings.ApplyAll();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            bootEvents = UiKit.EnsureEventSystem();   // in the boot scene only: gone before Nainital comes in (its HUD makes its own)
            canvas = UiKit.Canvas("BootCanvas", 1000, transform);
            root = (RectTransform)canvas.transform;
            var cam = Camera.main;
            if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
        }

        IEnumerator Start()
        {
            // Nainital starts loading at once, behind the splash; it waits at 90 % until the loading screen lets it in
            if (SceneManager.sceneCountInBuildSettings > 1)
            {
                op = SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
                op.allowSceneActivation = false;
            }
            black = UiKit.FullImg("Black", root, Color.black);
            yield return Splash();
            if (PlayerPrefs.GetInt("nt_lang_set", 0) == 0) yield return LanguagePick();
            yield return Loading();
        }

        IEnumerator Fade(Image img, float from, float to, float time)
        {
            for (float t = 0f; t < time; t += Step)
            {
                var c = img.color; c.a = Mathf.Lerp(from, to, t / time); img.color = c;
                yield return null;
            }
            var e = img.color; e.a = to; img.color = e;
        }

        // ------------------------------------------------------------------ splash
        IEnumerator Splash()
        {
            var s = UiKit.Full("Splash", root);
            s.SetSiblingIndex(0);
            UiKit.FullImg("Geru", s, Aipan.GeruDeep);
            var vig = UiKit.FullImg("Vignette", s, new Color(0, 0, 0, 0.75f), HudStyle.Vignette);
            var big = UiKit.Img("BigMandala", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 1500), new Color(1, 1, 1, 0.045f), Aipan.Mandala);
            // the borders at the top and the bottom, and the hanging lines under the top one
            UiKit.Strip("TopBorder", s, Aipan.Border, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, 34), Color.white);
            UiKit.Strip("BottomBorder", s, Aipan.Border, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 34), Color.white);
            var drips = UiKit.Strip("Vasudhara", s, Aipan.Vasudhara, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(4000, 70), new Color(Aipan.Rice.r, Aipan.Rice.g, Aipan.Rice.b, 0f));

            var mandala = UiKit.Img("Mandala", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(330, 330), Aipan.Rice, Aipan.Mandala);
            mandala.type = Image.Type.Filled;
            mandala.fillMethod = Image.FillMethod.Radial360;
            mandala.fillOrigin = (int)Image.Origin360.Top;
            mandala.fillAmount = 0f;
            var seed = UiKit.Img("Seed", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(46, 46), Aipan.Saffron, HudStyle.Disc);

            var title = UiKit.Txt("Title", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(1600, 140), 132, TextAnchor.MiddleCenter, Aipan.Rice, true);
            title.text = UiKit.Spaced("NAINITA", 2);
            var line = UiKit.Strip("Line", s, Aipan.DotRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(10, 20), Aipan.Saffron);
            var sub = UiKit.Txt("Sub", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -196), new Vector2(1200, 50), 40, TextAnchor.MiddleCenter, Aipan.Saffron, true);
            sub.text = UiKit.Spaced("THE SUKOON", 1);
            var tag = UiKit.Txt("Tag", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1200, 40), 28, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
            tag.text = UiKit.Spaced("DEVBHOOMI  UTTARAKHAND", 1);
            var tg = new[] { title, sub, tag };
            foreach (var t in tg) t.color = new Color(t.color.r, t.color.g, t.color.b, 0f);

            var bell = Resources.Load<AudioClip>("Audio/temple_bell");
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.volume = 0.55f;

            black.transform.SetAsLastSibling();
            StartCoroutine(Fade(black, 1f, 0f, 0.5f));
            const float Total = 3.4f;
            bool rang = false;
            for (float t = 0f; t < Total; t += Step)
            {
                if (!rang && t > 0.15f && bell) { src.PlayOneShot(bell); rang = true; }
                float m = UiKit.EaseOut(Mathf.InverseLerp(0.15f, 1.35f, t));
                mandala.fillAmount = m;
                mandala.rectTransform.localEulerAngles = new Vector3(0, 0, -t * 9f);
                mandala.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, m);
                seed.rectTransform.localScale = Vector3.one * UiKit.EaseOutBack(Mathf.InverseLerp(0.05f, 0.5f, t));
                big.rectTransform.localEulerAngles = new Vector3(0, 0, t * 2.5f);
                float ta = UiKit.EaseOut(Mathf.InverseLerp(0.9f, 1.6f, t));
                title.color = new Color(Aipan.Rice.r, Aipan.Rice.g, Aipan.Rice.b, ta);
                title.rectTransform.anchoredPosition = new Vector2(0, -70 - 26f * (1f - ta));
                line.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(10f, 560f, UiKit.EaseInOut(Mathf.InverseLerp(1.2f, 2.0f, t))), 20);
                sub.color = new Color(Aipan.Saffron.r, Aipan.Saffron.g, Aipan.Saffron.b, UiKit.EaseOut(Mathf.InverseLerp(1.45f, 2.0f, t)));
                tag.color = new Color(1, 1, 1, 0.6f * UiKit.EaseOut(Mathf.InverseLerp(1.8f, 2.4f, t)));
                drips.color = new Color(Aipan.Rice.r, Aipan.Rice.g, Aipan.Rice.b, 0.55f * UiKit.EaseOut(Mathf.InverseLerp(0.4f, 1.4f, t)));
                vig.color = new Color(0, 0, 0, Mathf.Lerp(0.85f, 0.7f, m));
                yield return null;
            }
            yield return Fade(black, 0f, 1f, 0.4f);
            Destroy(s.gameObject);
        }

        // ------------------------------------------------------------------ the first start: Hindi or English
        IEnumerator LanguagePick()
        {
            var s = UiKit.Full("Language", root);
            s.SetSiblingIndex(0);
            UiKit.FullImg("Geru", s, Aipan.GeruDeep);
            UiKit.FullImg("Vignette", s, new Color(0, 0, 0, 0.7f), HudStyle.Vignette);
            UiKit.Img("BigMandala", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 1500), new Color(1, 1, 1, 0.04f), Aipan.Mandala);
            UiKit.Strip("TopBorder", s, Aipan.Border, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, 34), Color.white);
            UiKit.Strip("BottomBorder", s, Aipan.Border, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 34), Color.white);
            UiKit.Txt("Q", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1600, 70), 58, TextAnchor.MiddleCenter, Aipan.Rice, true).text = "Bhasha chuniye";
            UiKit.Txt("Q2", s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(1600, 50), 34, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.65f)).text = "Choose your language";
            UiKit.Strip("Line", s, Aipan.DotRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 196), new Vector2(380, 20), Aipan.Saffron);
            int picked = -1;
            void Card(string big, string small, float x, int id)
            {
                var c = UiKit.Panel("Card_" + big, s, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -40), new Vector2(560, 330), new Color(0, 0, 0, 0.35f));
                c.raycastTarget = true;
                UiKit.Img("Mandala", c.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(300, 300), new Color(1, 1, 1, 0.07f), Aipan.Mandala);
                var edge = UiKit.Strip("Edge", c.transform, Aipan.Border, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(560, 28), Color.white);
                UiKit.Txt("Big", c.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(540, 100), 84, TextAnchor.MiddleCenter, Aipan.Rice, true).text = big;
                UiKit.Txt("Small", c.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(540, 50), 32, TextAnchor.MiddleCenter, Aipan.Saffron).text = small;
                var b = c.gameObject.AddComponent<TapButton>();
                b.onClick = () => { if (picked < 0) { picked = id; Haptics.Medium(); } };
            }
            Card(UiKit.Spaced("HINDI"), "Hinglish mein khelo", -320, 0);
            Card(UiKit.Spaced("ENGLISH"), "Play in English", 320, 1);
            black.transform.SetAsLastSibling();
            yield return Fade(black, 1f, 0f, 0.35f);
            while (picked < 0) yield return null;
            Loc.SetEnglish(picked == 1);
            PlayerPrefs.SetInt("nt_lang_set", 1);
            PlayerPrefs.Save();
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Fade(black, 0f, 1f, 0.35f);
            Destroy(s.gameObject);
        }

        // ------------------------------------------------------------------ loading
        IEnumerator Loading()
        {
            var s = UiKit.Full("Loading", root);
            s.SetSiblingIndex(0);
            UiKit.FullImg("Night", s, Aipan.Night);
            // two photos crossfading, each filling the screen (cropped, never stretched)
            var photoA = PhotoImage(s, "PhotoA");
            var photoB = PhotoImage(s, "PhotoB");
            var shadeB = UiKit.Img("ShadeBottom", s, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 560), new Color(0, 0, 0, 0.88f), Aipan.ShadeUp);
            var shadeT = UiKit.Img("ShadeTop", s, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, 240), new Color(0, 0, 0, 0.55f), Aipan.ShadeUp);
            shadeT.rectTransform.localEulerAngles = new Vector3(0, 0, 180);

            // top left: the mark
            var logoM = UiKit.Img("LogoMandala", s, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(96, -84), new Vector2(84, 84), Aipan.Saffron, Aipan.Mandala);
            UiKit.Txt("Logo", s, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(156, -70), new Vector2(700, 60), 52, TextAnchor.MiddleLeft, Aipan.Rice, true).text = UiKit.Spaced("NAINITA");
            UiKit.Txt("LogoSub", s, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(158, -112), new Vector2(700, 30), 22, TextAnchor.MiddleLeft, Aipan.Saffron, true).text = UiKit.Spaced("THE SUKOON");
            var credit = UiKit.Txt("Credit", s, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-48, -60), new Vector2(900, 30), 20, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.55f));

            // bottom left: the place and a line about it
            var info = UiKit.Rect("Info", s, new Vector2(0, 0), new Vector2(0, 0), new Vector2(96, 196), new Vector2(1300, 300));
            var infoG = info.gameObject.AddComponent<CanvasGroup>();
            var tagBg = UiKit.Panel("Tag", info, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -20), new Vector2(232, 40), Aipan.Geru, true);
            UiKit.Txt("TagText", tagBg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(232, 40), 22, TextAnchor.MiddleCenter, Aipan.Rice, true, false).text = UiKit.Spaced("UTTARAKHAND");
            var place = UiKit.Txt("Place", info, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -68), new Vector2(1300, 100), 84, TextAnchor.MiddleLeft, Aipan.Rice, true);
            var fact = UiKit.Txt("Fact", info, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -172), new Vector2(1150, 90), 32, TextAnchor.UpperLeft, new Color(0.9f, 0.9f, 0.88f));

            // the progress: a thin bar over the Aipan border, a tip and the percentage above it, a turning mandala
            var tip = UiKit.Txt("Tip", s, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(96, 112), new Vector2(1400, 40), 27, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.8f));
            var pct = UiKit.Txt("Pct", s, new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-96, 112), new Vector2(300, 50), 40, TextAnchor.MiddleRight, Aipan.Rice, true);
            var spin = UiKit.Img("Spin", s, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-300, 112), new Vector2(54, 54), Aipan.Saffron, Aipan.Mandala);
            var track = UiKit.Panel("Track", s, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 72), new Vector2(10, 8), new Color(1, 1, 1, 0.16f), true);
            track.rectTransform.anchorMin = new Vector2(0, 0); track.rectTransform.anchorMax = new Vector2(1, 0);
            track.rectTransform.offsetMin = new Vector2(96, 68); track.rectTransform.offsetMax = new Vector2(-96, 76);
            var fill = UiKit.Panel("Fill", track.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 8), Aipan.Saffron, true);
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.offsetMin = Vector2.zero;
            var glow = UiKit.Img("Glow", fill.transform, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60), new Color(Aipan.Saffron.r, Aipan.Saffron.g, Aipan.Saffron.b, 0.6f), HudStyle.Glow);
            UiKit.Strip("Border", s, Aipan.Border, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 30), Color.white);

            // the photos in a shuffled order, Nainital first
            var order = new int[Photos.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 1; i--) { int j = Random.Range(1, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            int shownPhoto = 0, tipIndex = Random.Range(0, Tips.Length);
            Image front = photoA, back = photoB;
            float photoT = 0f, tipT = 0f, fadeT = 1f;
            const float PerPhoto = 5.5f, Cross = 1.1f;
            Vector2 panA = RandomPan(), panB = RandomPan();
            void ShowPhoto(Image img, int idx)
            {
                var p = Photos[order[idx % order.Length]];
                var tex = Resources.Load<Texture2D>("Loading/" + p.file);
                if (tex) img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                var fit = img.GetComponent<AspectRatioFitter>();
                if (tex) fit.aspectRatio = (float)tex.width / tex.height;
                place.text = Loc.English ? p.en : p.hi;
                fact.text = Loc.English ? p.factEn : p.factHi;
                credit.text = (Loc.English ? "Photo: " : "Tasveer: ") + p.credit + "  /  Wikimedia Commons";
            }
            ShowPhoto(front, 0);
            front.color = Color.white;
            back.color = new Color(1, 1, 1, 0);
            tip.text = (Loc.English ? "<color=#FFA91F>TIP</color>   " : "<color=#FFA91F>SUJHAAV</color>   ") + Tips[tipIndex][Loc.English ? 1 : 0];

            yield return Fade(black, 1f, 0f, 0.45f);
            float started = Time.unscaledTime;
            bool activated = false;
            float readyAt = -1f;
            while (true)
            {
                float dt = Step;
                // photo: a slow drift and zoom; the next one fades in over it
                photoT += dt;
                float kb = photoT / (PerPhoto + Cross);
                front.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.03f, 1.12f, kb);
                front.rectTransform.anchoredPosition = panA * kb;
                if (photoT > PerPhoto)
                {
                    if (fadeT >= 1f)
                    {
                        shownPhoto++;
                        ShowPhoto(back, shownPhoto);
                        back.transform.parent.SetSiblingIndex(2);   // the incoming photo over the outgoing one, under the shades
                        panB = RandomPan();
                        fadeT = 0f;
                        infoG.alpha = 0f;
                    }
                    fadeT = Mathf.Min(1f, fadeT + dt / Cross);
                    back.color = new Color(1, 1, 1, UiKit.EaseInOut(fadeT));
                    back.rectTransform.localScale = Vector3.one * 1.03f;
                    back.rectTransform.anchoredPosition = Vector2.zero;
                    if (fadeT >= 1f)
                    {
                        (front, back) = (back, front);
                        back.color = new Color(1, 1, 1, 0);
                        panA = panB;
                        photoT = 0f;
                    }
                }
                infoG.alpha = Mathf.MoveTowards(infoG.alpha, photoT > PerPhoto - 0.4f && photoT <= PerPhoto ? 0f : 1f, dt * 2.5f);
                info.anchoredPosition = new Vector2(96 + (1f - infoG.alpha) * -20f, 196);

                tipT += dt;
                if (tipT > 7f)
                {
                    tipT = 0f;
                    tipIndex = (tipIndex + 1) % Tips.Length;
                    tip.text = (Loc.English ? "<color=#FFA91F>TIP</color>   " : "<color=#FFA91F>SUJHAAV</color>   ") + Tips[tipIndex][Loc.English ? 1 : 0];
                }
                spin.rectTransform.localEulerAngles = new Vector3(0, 0, -Time.unscaledTime * 60f);
                logoM.rectTransform.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * 8f);

                // progress: loading 0-80 %, the scene coming in 80-90 %, the world warming up 90-100 %
                float target;
                if (op == null) target = 1f;
                else if (!activated) target = Mathf.Clamp01(op.progress / 0.9f) * 0.8f;
                else if (!op.isDone) target = 0.86f;
                else target = WorldReady ? 1f : 0.92f;
                shown = Mathf.MoveTowards(shown, target, dt * (target >= 1f ? 0.8f : 0.45f));
                fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, shown * (track.rectTransform.rect.width)), 0);
                pct.text = Mathf.RoundToInt(shown * 100f) + "%";

                // a loading screen worth seeing: at least one full photo before the world comes in
                if (op != null && !activated && op.progress >= 0.899f && Time.unscaledTime - started > 3.2f)
                {
                    activated = true;
                    if (bootEvents) Destroy(bootEvents);
                    op.allowSceneActivation = true;
                }
                if (op == null || (op.isDone && WorldReady) || (op.isDone && Time.unscaledTime - started > 40f))
                {
                    if (readyAt < 0f) readyAt = Time.unscaledTime;
                    if (shown >= 0.999f || Time.unscaledTime - readyAt > 1.5f) break;
                }
                yield return null;
            }
            pct.text = "100%";
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Fade(black, 0f, 1f, 0.45f);
            Instance = null;
            Destroy(gameObject);   // FrontEnd is black underneath and fades the menu in
        }

        /// <summary>A frame's time, at most 1/20 s: the first frames of a scene loading behind us can take seconds, and the
        /// splash would jump to its end.</summary>
        static float Step => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        static Vector2 RandomPan() => Random.insideUnitCircle.normalized * Random.Range(20f, 46f);

        Image PhotoImage(Transform parent, string name)
        {
            var holder = UiKit.Full(name + "Holder", parent);
            var img = UiKit.Img(name, holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080), Color.white);
            var fit = img.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;
            holder.SetSiblingIndex(1);
            return img;
        }
    }
}
