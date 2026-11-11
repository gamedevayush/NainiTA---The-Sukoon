using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>On-screen controls for phones: left joystick, right-side look pad (two fingers pinch to zoom), action
    /// buttons, and touch versions of the keyboard-only things - the map, the cheats (UDAAN jetpack, SAFAR places list,
    /// JEEVAN, AMAR) and the places list itself. While the jetpack is on, Jump / Block / Use read Upar / Neeche / Utro.</summary>
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }
        HUD hud;
        Transform root;
        GameObject interactButton, cheatPanel, placesPanel;
        Text jumpLabel, blockLabel, useLabel;
        readonly List<GameObject> fightButtons = new List<GameObject>();
        bool sprintToggle, wasFlying;
        static Sprite disc, ring;
        static readonly Color BtnFill = new Color(0.06f, 0.08f, 0.10f, 0.62f), BtnOn = new Color(0.16f, 0.50f, 0.28f, 0.75f);

        /// <summary>Anti-aliased disc, or a ring of the given thickness (fraction of the radius), as a UI sprite.</summary>
        static Sprite Round(int n, float thickness)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float r = n * 0.5f - 1f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f));
                    float a = Mathf.Clamp01(r - d);
                    if (thickness > 0f) a *= Mathf.Clamp01(d - (r - r * thickness));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        public void Build(HUD hud, Transform root)
        {
            Instance = this;
            this.hud = hud;
            this.root = root;
            if (!disc) { disc = Round(160, 0f); ring = Round(160, 0.09f); }
            // the radar goes up under the health bar: the bottom-left corner is the thumb's (the joystick)
            var radar = root.Find("Radar") as RectTransform;
            if (radar)
            {
                radar.anchorMin = radar.anchorMax = new Vector2(0, 1);
                radar.pivot = new Vector2(0, 1);
                radar.anchoredPosition = new Vector2(28, -72);
                radar.localScale = Vector3.one * 0.82f;
                var obj = root.Find("Objective") as RectTransform;   // the mission box moves right of it
                if (obj) obj.anchoredPosition = new Vector2(30 + radar.sizeDelta.x * 0.82f + 20, -78);
            }
            // a tap that moves a few pixels on a 500 dpi screen is still a tap, not a drag (the places list scrolls)
            if (EventSystem.current) EventSystem.current.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt((Screen.dpi > 0 ? Screen.dpi : 320f) * 0.06f));

            // Look pad: right 60% of the screen, behind the buttons
            var look = hud.Img("LookPad", root, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(1250, 820), new Color(1, 1, 1, 0.001f));
            look.gameObject.AddComponent<LookPad>();

            // Joystick: bottom-left
            var area = hud.Img("StickArea", root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(620, 620), new Color(1, 1, 1, 0.001f));
            var baseImg = hud.Img("StickBase", area.transform, new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(230, 230), new Vector2(260, 260), new Color(0, 0, 0, 0.22f));
            baseImg.sprite = disc;
            var baseRing = hud.Img("StickRing", baseImg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260), new Color(1, 1, 1, 0.45f));
            baseRing.sprite = ring;
            baseRing.raycastTarget = false;
            var knob = hud.Img("StickKnob", baseImg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116, 116), new Color(1, 1, 1, 0.55f));
            knob.sprite = disc;
            knob.raycastTarget = false;
            var js = area.gameObject.AddComponent<Joystick>();
            js.baseRect = baseImg.rectTransform;
            js.knob = knob.rectTransform;

            // Buttons: bottom-right cluster
            jumpLabel = Hold(hud, root, "Jump", new Vector2(-150, 150), 150, () => { GameInput.TouchJump = true; GameInput.TouchJumpHeld = true; }, () => GameInput.TouchJumpHeld = false)
                .GetComponentInChildren<Text>();
            fightButtons.Add(Hold(hud, root, "Punch", new Vector2(-330, 120), 130, () => GameInput.TouchAttack = true, null));
            fightButtons.Add(Hold(hud, root, "Kick", new Vector2(-300, 280), 110, () => GameInput.TouchKick = true, null));
            blockLabel = Hold(hud, root, "Block", new Vector2(-470, 110), 110, () => { if (CarController.Driving) GameInput.TouchHorn = true; else GameInput.TouchBlock = true; }, () => GameInput.TouchBlock = false)
                .GetComponentInChildren<Text>();
            fightButtons.Add(Hold(hud, root, "Roll", new Vector2(-150, 330), 110, () => GameInput.TouchRoll = true, null));
            var sprint = Hold(hud, root, "Daudo", new Vector2(-470, 260), 100, null, null);
            sprint.GetComponent<HoldButton>().onDown = () =>
            {
                sprintToggle = !sprintToggle;
                GameInput.TouchSprint = sprintToggle;
                sprint.GetComponent<Image>().color = sprintToggle ? BtnOn : BtnFill;
            };
            interactButton = Hold(hud, root, "Use", new Vector2(-150, 510), 130, () => GameInput.TouchInteract = true, null);
            interactButton.GetComponent<Image>().color = new Color(0.85f, 0.6f, 0.12f, 0.75f);
            useLabel = interactButton.GetComponentInChildren<Text>();

            // under the HUD's Din/Raat, Help and Menu buttons (top right): the map and the cheats
            hud.Btn("Map", root, new Vector2(1, 1), new Vector2(-110, -326), new Vector2(180, 54), () => GameInput.TouchMap = true);
            var cb = hud.Btn("Cheat", root, new Vector2(1, 1), new Vector2(-110, -388), new Vector2(180, 54), ToggleCheats);
            cb.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.5f, 0.75f);
        }

        GameObject Hold(HUD hud, Transform root, string label, Vector2 pos, float size, System.Action down, System.Action up)
        {
            var img = hud.Img("Touch_" + label, root, new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size), BtnFill);
            img.sprite = disc;
            var edge = hud.Img("Ring", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), new Color(1, 1, 1, 0.5f));
            edge.sprite = ring;
            edge.raycastTarget = false;
            var t = hud.Txt("Label", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), size >= 140 ? 30 : 25, TextAnchor.MiddleCenter, Color.white);
            t.fontStyle = FontStyle.Bold;
            t.text = label;
            var hb = img.gameObject.AddComponent<HoldButton>();
            hb.onDown = down;
            hb.onUp = up;
            return img.gameObject;
        }

        public void SetInteractVisible(bool v)
        {
            v |= Jetpack.Flying;   // "Utro" takes the jetpack off
            if (interactButton && interactButton.activeSelf != v) interactButton.SetActive(v);
        }

        int lastMode = -1;

        /// <summary>The right-hand buttons change with what the player is doing: on foot (fight), flying the jetpack
        /// (Upar / Neeche / Utro) or driving (Brake = handbrake, Horn; the stick steers and accelerates).</summary>
        void Update()
        {
            bool fly = Jetpack.Flying;
            bool drive = CarController.Driving != null;
            int mode = fly ? 1 : drive ? 2 : 0;
            if (mode == lastMode) return;
            lastMode = mode;
            wasFlying = fly;
            if (jumpLabel) jumpLabel.text = mode == 1 ? "Upar" : mode == 2 ? "Brake" : "Jump";
            if (blockLabel) blockLabel.text = mode == 1 ? "Neeche" : mode == 2 ? "Horn" : "Block";
            if (useLabel) useLabel.text = mode == 1 ? "Utro" : mode == 2 ? "Utro" : "Use";
            foreach (var b in fightButtons) if (b) b.SetActive(mode == 0);
            SetInteractVisible(false);
        }

        // ------------------------------------------------------------------ panels
        RectTransform Panel(string name, Vector2 size, string title)
        {
            var dim = hud.Img(name, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0, 0, 0, 0.55f));
            var box = hud.Img("Box", dim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.04f, 0.06f, 0.09f, 0.95f));
            var t = hud.Txt("Title", box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(size.x - 40, 60), 40, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            t.text = title;
            dim.transform.SetAsLastSibling();
            return box.rectTransform;
        }

        void Open(GameObject panel)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            GameInput.UiBlocking = true;
            GameInput.TouchMove = Vector2.zero;
        }

        void Close(GameObject panel)
        {
            if (!panel || !panel.activeSelf) return;
            panel.SetActive(false);
            GameInput.UiBlocking = false;
        }

        /// <summary>The cheats as buttons (the typed codes need a keyboard).</summary>
        public void ToggleCheats()
        {
            if (cheatPanel && cheatPanel.activeSelf) { Close(cheatPanel); return; }
            if (!cheatPanel)
            {
                var box = Panel("CheatPanel", new Vector2(640, 776), "Cheats");
                cheatPanel = box.parent.gameObject;
                float y = -110;
                void B(string label, string code)
                {
                    var b = hud.Btn(label, box, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(560, 74), () => { if (AudioManager.Instance) AudioManager.Instance.Click(); Close(cheatPanel); CheatCodes.Run(code); }, 30);
                    b.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
                    y -= 88;
                }
                B("UDAAN  -  Jetpack", "UDAAN");
                B("SAFAR  -  Kahin bhi jao", "SAFAR");
                B("JEEVAN  -  Poori health", "JEEVAN");
                B("AMAR  -  Chot nahi lagegi", "AMAR");
                B("GAADI  -  Car paas aayegi", "GAADI");
                B("JEEP  -  Bolero / Scorpio", "JEEP");
                hud.Btn("Band karo", box, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 66), () => Close(cheatPanel), 28);
            }
            Open(cheatPanel);
        }

        /// <summary>The SAFAR list as a scrolling list of big buttons; pick(i) is called with the chosen index and the
        /// list closes once the teleport has let go of the screen (GameInput.UiBlocking cleared).</summary>
        public void ShowPlaces(IList<string> names, System.Action<int> pick)
        {
            if (placesPanel) Destroy(placesPanel);
            var box = Panel("PlacesPanel", new Vector2(820, 960), "Kahan jaana hai?");
            placesPanel = box.parent.gameObject;

            var view = hud.Img("View", box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(760, 740), new Color(1, 1, 1, 0.02f));
            view.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.transform, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            const float rowH = 88f, gap = 10f;
            content.sizeDelta = new Vector2(0, names.Count * (rowH + gap));
            for (int i = 0; i < names.Count; i++)
            {
                int idx = i;
                var b = hud.Btn(names[i], content, new Vector2(0.5f, 1), new Vector2(0, -(rowH * 0.5f + i * (rowH + gap))), new Vector2(720, rowH), () =>
                {
                    pick(idx);
                    if (!GameInput.UiBlocking && placesPanel) Destroy(placesPanel);   // the teleport worked
                }, 30);
                b.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            }
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            sr.viewport = view.rectTransform;
            hud.Btn("Band karo", box, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 66), () =>
            {
                if (placesPanel) Destroy(placesPanel);
                GameInput.UiBlocking = false;
            }, 28);
            placesPanel.transform.SetAsLastSibling();
            GameInput.UiBlocking = true;
            GameInput.TouchMove = Vector2.zero;
        }
    }

    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action onDown, onUp;
        public void OnPointerDown(PointerEventData e) => onDown?.Invoke();
        public void OnPointerUp(PointerEventData e) => onUp?.Invoke();
    }

    public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform baseRect, knob;
        Vector2 home;
        const float Radius = 110f;

        void Start() => home = baseRect.anchoredPosition;

        public void OnPointerDown(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local);
            baseRect.localPosition = local;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, e.position, e.pressEventCamera, out var local);
            Vector2 v = Vector2.ClampMagnitude(local, Radius);
            knob.anchoredPosition = v;
            GameInput.TouchMove = v / Radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            knob.anchoredPosition = Vector2.zero;
            baseRect.anchoredPosition = home;
            GameInput.TouchMove = Vector2.zero;
        }
    }

    public class LookPad : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e)
        {
            Vector2 d = e.delta;
            GameInput.TouchLookPixels += new Vector2(d.x, d.y);
        }
    }
}
