using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The screen of the story (owner, 23 Nov 2026: "cutscene, dialogue and choice UI system bnaalo proper"): cinema bars,
    /// a fade to black, subtitles with the speaker's name (typed out; a tap, click, Space, E, Enter or the pad's A shows the
    /// whole line, the next one moves on), choices (1-4 / arrows + Enter / a tap / the pad, an optional timer), the hold-to-
    /// skip ring (Esc / Tab / the pad's Start), a QTE meter (tap fast: it fills per press and drains on its own), the
    /// objective line and a blur + dark edges for the half-awake views (a depth-of-field + vignette volume). Driven by
    /// Cutscene; the parts work on their own too (a mission can just ask a question). Text through Loc (Hindi shaped there).
    /// </summary>
    public class StoryUI : MonoBehaviour
    {
        public static StoryUI Instance { get; private set; }
        public static StoryUI Ensure()
        {
            if (!Instance) { var go = new GameObject("Story UI"); DontDestroyOnLoad(go); Instance = go.AddComponent<StoryUI>(); }
            return Instance;
        }

        public const float BarFrac = 0.115f;   // each cinema bar, of the screen's height
        Canvas canvas;
        RectTransform barTop, barBottom;
        Image fade;
        CanvasGroup talkGroup, choiceGroup, skipGroup, qteGroup, objGroup;
        Text nameText, bodyText, nextMark, objText, skipText, qteText, qteKey;
        Image skipFill, qteFill, talkPanel;
        RectTransform choiceRoot;
        TapButton screenTap;
        Volume blurVol;
        DepthOfField dof;
        Vignette vig;

        float barsTarget, bars, objLife, blurTarget, blur;
        bool tapped;
        float skipHold;
        /// <summary>Skip is allowed now (a cutscene runs and the ring shows).</summary>
        public bool SkipAllowed { get; set; }
        /// <summary>The skip was held long enough (read and cleared by Cutscene).</summary>
        public bool SkipRequested { get; set; }
        public bool Talking { get; private set; }
        /// <summary>Test hooks (Editor/StoryTest over the bridge): the next line, a choice by index, QTE presses.</summary>
        public static bool TestAdvance;
        public static int TestPick = -1, TestPresses;
        public bool Choosing { get; private set; }

        void Awake()
        {
            Instance = this;
            canvas = UiKit.Canvas("Story canvas", 850, transform);
            UiKit.EnsureEventSystem(transform);
            var root = canvas.transform;

            // a tap anywhere moves the talk on (under the choices, over the game)
            var tapImg = UiKit.FullImg("Tap", root, new Color(0, 0, 0, 0));
            tapImg.raycastTarget = true;
            screenTap = tapImg.gameObject.AddComponent<TapButton>();
            screenTap.pressScale = 1f;
            screenTap.onClick = () => tapped = true;
            tapImg.gameObject.SetActive(false);

            barTop = UiKit.Img("Bar top", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, Color.black).rectTransform;
            barBottom = UiKit.Img("Bar bottom", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, Color.black).rectTransform;
            foreach (var b in new[] { barTop, barBottom }) { b.anchorMin = new Vector2(0f, b.anchorMin.y); b.anchorMax = new Vector2(1f, b.anchorMax.y); }

            // the subtitle: a soft glass strip over the bottom bar, the name above the line in marigold
            talkGroup = Group("Talk", root);
            talkPanel = UiKit.Panel("Talk panel", talkGroup.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1320f, 168f), new Color(0.02f, 0.03f, 0.045f, 0.72f));
            nameText = UiKit.Txt("Name", talkPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -14f), new Vector2(900f, 40f), 30, TextAnchor.UpperLeft, HudStyle.Marigold, true);
            bodyText = UiKit.Txt("Line", talkPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1250f, 110f), 34, TextAnchor.UpperLeft, HudStyle.Ink);
            nextMark = UiKit.Txt("Next", talkPanel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 12f), new Vector2(60f, 40f), 34, TextAnchor.LowerRight, HudStyle.Teal, true);
            nextMark.text = "▶";

            // the choices, stacked above the subtitle
            choiceGroup = Group("Choices", root);
            choiceRoot = UiKit.Rect("Choice list", choiceGroup.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 226f), new Vector2(900f, 400f));

            // hold to skip (bottom right)
            skipGroup = Group("Skip", root);
            var ring = UiKit.Img("Skip ring", skipGroup.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -34f), new Vector2(54f, 54f), new Color(1, 1, 1, 0.22f), HudStyle.RingSprite);
            skipFill = UiKit.Img("Skip fill", ring.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f), HudStyle.Marigold, HudStyle.RingSprite);
            skipFill.type = Image.Type.Filled;
            skipFill.fillMethod = Image.FillMethod.Radial360;
            skipFill.fillOrigin = 2;
            skipFill.fillAmount = 0f;
            skipText = UiKit.Txt("Skip text", skipGroup.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-106f, -44f), new Vector2(420f, 36f), 24, TextAnchor.MiddleRight, HudStyle.InkDim);

            // the QTE: a big key and a bar in the middle
            qteGroup = Group("QTE", root);
            qteKey = UiKit.Txt("QTE key", qteGroup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(240f, 120f), 86, TextAnchor.MiddleCenter, HudStyle.Marigold, true);
            qteText = UiKit.Txt("QTE text", qteGroup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(900f, 50f), 32, TextAnchor.MiddleCenter, HudStyle.Ink, true);
            var qbg = UiKit.Panel("QTE bar", qteGroup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(620f, 26f), new Color(1, 1, 1, 0.16f), true);
            qteFill = UiKit.Img("QTE fill", qbg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(620f, 26f), HudStyle.Coral, HudStyle.RoundedSmall);
            qteFill.type = Image.Type.Filled;
            qteFill.fillMethod = Image.FillMethod.Horizontal;

            // the objective (top middle, under the bar)
            objGroup = Group("Objective", root);
            objText = UiKit.Txt("Objective", objGroup.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 60f), 34, TextAnchor.MiddleCenter, HudStyle.Ink, true);

            fade = UiKit.FullImg("Fade", root, new Color(0, 0, 0, 0));
            fade.transform.SetAsLastSibling();
        }

        static CanvasGroup Group(string name, Transform parent)
        {
            var rt = UiKit.Full(name, parent);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.blocksRaycasts = false;
            return g;
        }

        // ------------------------------------------------------------------ bars, fade, blur, objective
        public void Bars(bool on) => barsTarget = on ? 1f : 0f;
        public void BarsNow(bool on) { barsTarget = bars = on ? 1f : 0f; }

        public float FadeAlpha => fade.color.a;
        public void FadeNow(float a) => fade.color = new Color(0, 0, 0, Mathf.Clamp01(a));
        public IEnumerator Fade(float to, float seconds)
        {
            float from = fade.color.a;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                FadeNow(Mathf.Lerp(from, to, UiKit.EaseInOut(t / seconds)));
                yield return null;
            }
            FadeNow(to);
        }

        /// <summary>0 sharp .. 1 a half-awake blur with dark edges.</summary>
        public void Blur(float v) => blurTarget = Mathf.Clamp01(v);
        public void BlurNow(float v) { blurTarget = blur = Mathf.Clamp01(v); ApplyBlur(); }

        public void Objective(string text, float seconds = 6f)
        {
            objText.text = text;
            objLife = string.IsNullOrEmpty(text) ? 0f : seconds;
        }

        // ------------------------------------------------------------------ talk
        /// <summary>One line: the name, the text typed out. `auto` > 0: moves on by itself after that long (a tap still
        /// hurries it); else waits for a tap. `onShown` once the whole line shows.</summary>
        public IEnumerator Say(string who, string line, Color nameColor, float auto = 0f, Func<bool> skipping = null)
        {
            Talking = true;
            nameText.text = who ?? "";
            nameText.color = nameColor;
            bodyText.text = "";
            talkGroup.alpha = 1f;
            screenTap.gameObject.SetActive(true);
            tapped = false;
            string text = line ?? "";
            float cps = 52f, shown = 0f;
            while (shown < text.Length)
            {
                if (skipping != null && skipping()) break;
                if (Advance()) break;
                shown += cps * Time.unscaledDeltaTime;
                bodyText.text = text.Substring(0, Mathf.Min(text.Length, Mathf.FloorToInt(shown)));
                nextMark.enabled = false;
                yield return null;
            }
            bodyText.text = text;
            float wait = auto > 0f ? auto : float.MaxValue;
            float minRead = 0.25f;
            for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
            {
                if (skipping != null && skipping()) break;
                nextMark.enabled = Mathf.Repeat(Time.unscaledTime, 1f) < 0.65f;
                if (t > minRead && Advance()) break;
                yield return null;
            }
            screenTap.gameObject.SetActive(false);
            Talking = false;
        }

        public void HideTalk() { talkGroup.alpha = 0f; screenTap.gameObject.SetActive(false); }

        bool Advance()
        {
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            bool k = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                     || (pad != null && pad.buttonSouth.wasPressedThisFrame)
                     || (mouse != null && Cursor.lockState == CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame);
            if (tapped) { tapped = false; return true; }
            if (TestAdvance) { TestAdvance = false; return true; }
            return k;
        }

        // ------------------------------------------------------------------ choices
        readonly List<(Image bg, Text t, Text num)> rows = new List<(Image, Text, Text)>();

        /// <summary>Asks: the options as rows (a number each); `picked` gets the index. timeLimit > 0: a bar runs down and
        /// `fallback` is taken when it runs out.</summary>
        public IEnumerator Choose(IList<string> options, Action<int> picked, float timeLimit = 0f, int fallback = 0, string prompt = null)
        {
            Choosing = true;
            foreach (var r in rows) if (r.bg) Destroy(r.bg.gameObject);
            rows.Clear();
            int n = Mathf.Clamp(options.Count, 1, 4);
            int sel = 0, chosen = -1;
            const float h = 74f, gap = 12f;
            if (!string.IsNullOrEmpty(prompt)) { talkGroup.alpha = 1f; nameText.text = ""; bodyText.text = prompt; nextMark.enabled = false; }
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                float y = (n - 1 - i) * (h + gap);
                var bg = UiKit.Panel("Choice " + i, choiceRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(880f, h), HudStyle.GlassDeep);
                bg.raycastTarget = true;
                var tb = bg.gameObject.AddComponent<TapButton>();
                tb.onClick = () => chosen = idx;
                tb.onHover = on => { if (on) sel = idx; };
                var num = UiKit.Txt("Num", bg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(50f, h), 32, TextAnchor.MiddleCenter, HudStyle.Marigold, true);
                num.text = (i + 1).ToString();
                var t = UiKit.Txt("Text", bg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(780f, h), 31, TextAnchor.MiddleLeft, HudStyle.Ink, false, false);
                t.text = options[i];
                rows.Add((bg, t, num));
            }
            choiceGroup.alpha = 1f;
            choiceGroup.blocksRaycasts = true;
            if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            float left = timeLimit;
            Image timer = null;
            if (timeLimit > 0f)
            {
                timer = UiKit.Img("Timer", choiceRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(880f, 6f), HudStyle.Teal, HudStyle.RoundedSmall);
                timer.type = Image.Type.Filled; timer.fillMethod = Image.FillMethod.Horizontal;
            }
            yield return null;   // the key that opened this does not pick
            while (chosen < 0)
            {
                var kb = Keyboard.current; var pad = Gamepad.current;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) chosen = 0;
                    else if (n > 1 && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)) chosen = 1;
                    else if (n > 2 && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)) chosen = 2;
                    else if (n > 3 && (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)) chosen = 3;
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) sel = (sel + n - 1) % n;
                    if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) sel = (sel + 1) % n;
                    if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) chosen = sel;
                }
                if (TestPick >= 0) { chosen = Mathf.Clamp(TestPick, 0, n - 1); TestPick = -1; }
                if (pad != null)
                {
                    if (pad.dpad.up.wasPressedThisFrame) sel = (sel + n - 1) % n;
                    if (pad.dpad.down.wasPressedThisFrame) sel = (sel + 1) % n;
                    if (pad.buttonSouth.wasPressedThisFrame) chosen = sel;
                }
                for (int i = 0; i < rows.Count; i++)
                {
                    bool on = i == sel;
                    rows[i].bg.color = on ? new Color(0.10f, 0.30f, 0.30f, 0.95f) : HudStyle.GlassDeep;
                    rows[i].t.color = on ? Color.white : HudStyle.InkDim;
                }
                if (timeLimit > 0f)
                {
                    left -= Time.unscaledDeltaTime;
                    if (timer) timer.fillAmount = Mathf.Clamp01(left / timeLimit);
                    if (left <= 0f) chosen = Mathf.Clamp(fallback, 0, n - 1);
                }
                yield return null;
            }
            if (AudioManager.Instance) AudioManager.Instance.Click();
            // the picked row lights for a moment
            for (int i = 0; i < rows.Count; i++) rows[i].bg.color = i == chosen ? new Color(0.85f, 0.6f, 0.12f, 0.95f) : new Color(0.03f, 0.045f, 0.065f, 0.4f);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) yield return null;
            choiceGroup.alpha = 0f;
            choiceGroup.blocksRaycasts = false;
            foreach (var r in rows) if (r.bg) Destroy(r.bg.gameObject);
            rows.Clear();
            if (timer) Destroy(timer.gameObject);
            if (!string.IsNullOrEmpty(prompt)) talkGroup.alpha = 0f;
            Choosing = false;
            picked?.Invoke(chosen);
        }

        // ------------------------------------------------------------------ QTE
        /// <summary>Tap fast: each press fills by `per` (of 100), it drains `decay` a second; done at 100. `onProgress` gets
        /// 0..1 every frame (a cutscene scrubs an escape by it). Ends true at 100, false when `limit` seconds run out.</summary>
        public IEnumerator Mash(string prompt, Action<bool> done, float per = 7f, float decay = 14f, float limit = 20f, Action<float> onProgress = null)
        {
            qteGroup.alpha = 1f;
            qteText.text = prompt;
            bool touch = GameInput.UsingTouch;
            qteKey.text = touch ? "TAP" : "E";
            screenTap.gameObject.SetActive(true);
            tapped = false;
            float v = 0f, t = 0f;
            while (v < 100f && t < limit)
            {
                var kb = Keyboard.current; var pad = Gamepad.current;
                bool press = TestPresses > 0 || tapped || (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                tapped = false;
                if (TestPresses > 0) TestPresses--;
                if (press) { v += per; qteKey.rectTransform.localScale = Vector3.one * 1.25f; Haptics.Selection(); }
                v = Mathf.Max(0f, v - decay * Time.unscaledDeltaTime);
                qteKey.rectTransform.localScale = Vector3.Lerp(qteKey.rectTransform.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
                qteFill.fillAmount = v / 100f;
                qteFill.color = Color.Lerp(HudStyle.Coral, HudStyle.Good, v / 100f);
                onProgress?.Invoke(Mathf.Clamp01(v / 100f));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            qteGroup.alpha = 0f;
            screenTap.gameObject.SetActive(false);
            done?.Invoke(v >= 100f);
        }

        // ------------------------------------------------------------------ every frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bars = Mathf.MoveTowards(bars, barsTarget, dt * 2.2f);
            float h = UiKit.EaseInOut(bars) * BarFrac * ((RectTransform)canvas.transform).rect.height;
            barTop.sizeDelta = new Vector2(0f, h);
            barBottom.sizeDelta = new Vector2(0f, h);

            if (objLife > 0f) { objLife -= dt; objGroup.alpha = Mathf.Clamp01(objLife * 2f) * Mathf.Clamp01(objGroup.alpha + dt * 4f); }
            else objGroup.alpha = 0f;

            // hold to skip
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool hold = SkipAllowed && ((kb != null && (kb.escapeKey.isPressed || kb.tabKey.isPressed)) || (pad != null && pad.startButton.isPressed));
            skipHold = hold ? skipHold + dt : Mathf.Max(0f, skipHold - dt * 3f);
            skipFill.fillAmount = Mathf.Clamp01(skipHold / 0.9f);
            if (skipHold >= 0.9f) { SkipRequested = true; skipHold = 0f; }
            skipText.text = GameInput.UsingTouch ? "" : Loc.L("Skip: Esc dabaake rakho", "Skip: hold Esc");
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, SkipAllowed ? (skipHold > 0f ? 1f : 0.55f) : 0f, dt * 3f);

            if (!Mathf.Approximately(blur, blurTarget)) { blur = Mathf.MoveTowards(blur, blurTarget, dt * 0.6f); ApplyBlur(); }
        }

        void ApplyBlur()
        {
            if (!blurVol)
            {
                if (blur <= 0f) return;
                var go = new GameObject("Story blur");
                go.transform.SetParent(transform, false);
                blurVol = go.AddComponent<Volume>();
                blurVol.isGlobal = true;
                blurVol.priority = 20f;
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                dof = p.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(0f);
                dof.gaussianEnd.Override(1f);
                dof.gaussianMaxRadius.Override(1.5f);
                dof.highQualitySampling.Override(true);
                vig = p.Add<Vignette>(true);
                vig.intensity.Override(0f);
                vig.smoothness.Override(0.9f);
                blurVol.sharedProfile = p;
            }
            blurVol.weight = blur;
            blurVol.enabled = blur > 0.001f;
            if (dof) { dof.gaussianEnd.Override(Mathf.Lerp(30f, 0.5f, blur)); dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.5f, blur)); }
            if (vig) vig.intensity.Override(Mathf.Lerp(0f, 0.55f, blur));
        }

        /// <summary>Everything off (a cutscene ended or was cut short).</summary>
        public void Clear()
        {
            HideTalk();
            choiceGroup.alpha = 0f; choiceGroup.blocksRaycasts = false;
            qteGroup.alpha = 0f;
            Bars(false);
            BlurNow(0f);
            SkipAllowed = false; SkipRequested = false;
        }
    }
}
