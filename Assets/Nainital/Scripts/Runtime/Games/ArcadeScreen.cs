using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The screen of a game parlour machine (owner, 23 Nov 2026: "capitol ke neeche video games ki dukaan me pool wgera,
    /// bowling and basic playable games rkhne hain"). A cabinet (ArcadeCabinet) opens it full screen; the game runs inside
    /// a lit "monitor" in a dark bezel with its score and time; Esc / the X closes it. The player stands still meanwhile
    /// (GameInput.StoryLock). Three games, all drawn with uGUI shapes and played with keys, mouse, a pad or touch:
    ///  - Bandar Bhagao: monkeys pop out of nine holes, hit them before they duck (click / tap, or 1-9 / the numpad), 30 s.
    ///  - Pahadi Dash: three lanes down a hill road, dodge what comes (A / D, arrows, tap a side); faster and faster.
    ///  - Pool: a practice table - drag back from the white ball and let go (or A / D to aim, hold Space for power), pot
    ///    all fifteen in as few shots as you can.
    /// </summary>
    public class ArcadeScreen : MonoBehaviour
    {
        public enum Game { Bandar, Dash, Pool }

        public static ArcadeScreen Open(Game g, string title)
        {
            if (Current) return null;
            var go = new GameObject("Arcade " + g);
            var s = go.AddComponent<ArcadeScreen>();
            s.game = g;
            s.title = title;
            return s;
        }

        public static ArcadeScreen Current { get; private set; }
        /// <summary>The best score of each game this session (the cabinets show it).</summary>
        public static readonly Dictionary<Game, int> Best = new Dictionary<Game, int>();
        /// <summary>The last game's result (tests).</summary>
        public static string LastResult;

        Game game;
        string title;
        Canvas canvas;
        RectTransform field;
        Text scoreText, infoText, titleText;
        float time;
        int score;
        bool over;
        float overT;
        IArcade logic;

        public const float W = 1000f, H = 620f;   // the monitor, canvas units

        interface IArcade { void Begin(ArcadeScreen s); void Tick(float dt); void End(); }

        void Awake() => Current = this;

        void Start()
        {
            canvas = UiKit.Canvas("Arcade canvas", 870, transform);
            UiKit.EnsureEventSystem(transform);
            var root = canvas.transform;
            UiKit.FullImg("Dim", root, new Color(0f, 0f, 0f, 0.86f)).raycastTarget = true;
            var bezel = UiKit.Panel("Bezel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(W + 60f, H + 150f), new Color(0.08f, 0.06f, 0.12f, 1f));
            titleText = UiKit.Txt("Title", bezel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(W, 54f), 40, TextAnchor.MiddleCenter, HudStyle.Marigold, true);
            titleText.text = UiKit.Spaced(title.ToUpperInvariant());
            var screen = UiKit.Img("Screen", bezel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(W, H), new Color(0.02f, 0.03f, 0.05f, 1f));
            field = screen.rectTransform;
            var mask = screen.gameObject.AddComponent<RectMask2D>();
            scoreText = UiKit.Txt("Score", bezel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 12f), new Vector2(500f, 50f), 32, TextAnchor.MiddleLeft, HudStyle.Ink, true);
            infoText = UiKit.Txt("Info", bezel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 12f), new Vector2(560f, 50f), 26, TextAnchor.MiddleRight, HudStyle.InkDim);
            var x = UiKit.Panel("Close", bezel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(54f, 54f), HudStyle.Coral, true);
            x.raycastTarget = true;
            x.gameObject.AddComponent<TapButton>().onClick = Close;
            UiKit.Txt("X", x.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f), 34, TextAnchor.MiddleCenter, Color.white, true).text = "X";

            GameInput.StoryLock = true;
            GameInput.UiBlocking = true;
            if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (HUD.Instance) HUD.Instance.SetAlpha(0f);
            logic = game == Game.Bandar ? new Bandar() : game == Game.Dash ? new Dash() : (IArcade)new Pool();
            logic.Begin(this);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (over)
            {
                overT += dt;
                bool again = overT > 0.8f && ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || Tapped());
                if (again) Restart();
                return;
            }
            time += dt;
            scoreText.text = Loc.F("Score: {0}", score);   // (Pool writes its shots over it in Tick)
            logic.Tick(dt);
        }

        static bool Tapped()
        {
            var m = Mouse.current; var t = Touchscreen.current;
            return (m != null && m.leftButton.wasPressedThisFrame) || (t != null && t.primaryTouch.press.wasPressedThisFrame);
        }

        void Restart()
        {
            for (int i = field.childCount - 1; i >= 0; i--) Destroy(field.GetChild(i).gameObject);
            over = false; overT = 0f; time = 0f; score = 0;
            logic = game == Game.Bandar ? new Bandar() : game == Game.Dash ? new Dash() : (IArcade)new Pool();
            logic.Begin(this);
        }

        void GameOver(string line)
        {
            over = true;
            overT = 0f;
            logic.End();
            int best = Best.TryGetValue(game, out int b) ? b : 0;
            bool better = game == Game.Pool ? (score > 0 && (best == 0 || score < best)) : score > best;
            if (better) Best[game] = score;
            LastResult = $"{game}: {score}";
            Debug.Log("[Nainital] arcade " + LastResult);
            var p = UiKit.Panel("Over", field, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 250f), HudStyle.GlassDeep);
            var t = UiKit.Txt("Over text", p.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 230f), 34, TextAnchor.MiddleCenter, HudStyle.Ink, true);
            t.text = line + "\n" + (better ? Loc.L("Naya record!", "New best!") + "\n" : "") + "<size=24>" + Loc.L("Phir khelo: Space / tap", "Again: Space / tap") + "</size>";
        }

        public void Close()
        {
            logic?.End();
            GameInput.StoryLock = false;
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            if (HUD.Instance) HUD.Instance.SetAlpha(1f);
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        void OnDestroy() { if (Current == this) { Current = null; GameInput.StoryLock = false; } }

        // ------------------------------------------------------------------ helpers for the games
        Image Box(string name, Vector2 pos, Vector2 size, Color c, Sprite s = null)
        {
            var img = UiKit.Img(name, field, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), pos, size, c, s);
            return img;
        }

        Text Label(Vector2 pos, Vector2 size, int fs, Color c, string text, bool bold = true)
        {
            var t = UiKit.Txt("Label", field, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), pos, size, fs, TextAnchor.MiddleCenter, c, bold);
            t.text = text;
            return t;
        }

        /// <summary>The pointer on the monitor (canvas units from its bottom left), or false.</summary>
        bool Pointer(out Vector2 p, out bool down, out bool held, out bool up)
        {
            p = default; down = held = up = false;
            Vector2 screen;
            var t = Touchscreen.current;
            var m = Mouse.current;
            if (t != null && (t.primaryTouch.press.isPressed || t.primaryTouch.press.wasReleasedThisFrame))
            {
                screen = t.primaryTouch.position.ReadValue();
                down = t.primaryTouch.press.wasPressedThisFrame; held = t.primaryTouch.press.isPressed; up = t.primaryTouch.press.wasReleasedThisFrame;
            }
            else if (m != null)
            {
                screen = m.position.ReadValue();
                down = m.leftButton.wasPressedThisFrame; held = m.leftButton.isPressed; up = m.leftButton.wasReleasedThisFrame;
            }
            else return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(field, screen, null, out var local)) return false;
            p = local + field.rect.size * 0.5f;
            return true;
        }

        // ------------------------------------------------------------------ Bandar Bhagao
        class Bandar : IArcade
        {
            ArcadeScreen s;
            readonly Image[] holes = new Image[9], monkeys = new Image[9];
            readonly float[] up = new float[9], life = new float[9];
            float next;
            const float Length = 30f;

            public void Begin(ArcadeScreen sc)
            {
                s = sc;
                s.Box("Grass", new Vector2(W * 0.5f, H * 0.5f), new Vector2(W, H), new Color(0.16f, 0.32f, 0.14f));
                for (int i = 0; i < 9; i++)
                {
                    Vector2 c = Hole(i);
                    holes[i] = s.Box("Hole", c, new Vector2(150f, 60f), new Color(0.12f, 0.08f, 0.05f), HudStyle.Disc);
                    monkeys[i] = s.Box("Monkey", c + new Vector2(0f, 30f), new Vector2(96f, 96f), new Color(0.55f, 0.38f, 0.22f), HudStyle.Disc);
                    var face = UiKit.Img("Face", monkeys[i].transform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 50f), new Color(0.93f, 0.72f, 0.58f), HudStyle.Disc);
                    foreach (float ex in new[] { -12f, 12f }) UiKit.Img("Eye", face.transform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(ex, 0f), new Vector2(9f, 9f), Color.black, HudStyle.Disc);
                    monkeys[i].gameObject.SetActive(false);
                    s.Label(c + new Vector2(0f, -46f), new Vector2(60f, 30f), 22, new Color(1f, 1f, 1f, 0.35f), (i + 1).ToString(), false);
                }
                s.infoText.text = Loc.L("Bandar pe tap / click karo, ya 1-9", "Tap / click the monkeys, or 1-9");
            }

            static Vector2 Hole(int i) => new Vector2(220f + (i % 3) * 280f, 130f + (2 - i / 3) * 180f);

            public void Tick(float dt)
            {
                float left = Length - s.time;
                s.infoText.text = Loc.F("Samay: {0}", Mathf.CeilToInt(Mathf.Max(0f, left)));
                if (left <= 0f) { s.GameOver(Loc.F("Bandar bhagaaye: {0}", s.score)); return; }
                next -= dt;
                float pace = Mathf.Lerp(1.0f, 0.45f, s.time / Length);
                if (next <= 0f)
                {
                    next = pace * UnityEngine.Random.Range(0.6f, 1.1f);
                    int i = UnityEngine.Random.Range(0, 9);
                    if (life[i] <= 0f) { life[i] = Mathf.Lerp(1.2f, 0.65f, s.time / Length); monkeys[i].gameObject.SetActive(true); monkeys[i].color = new Color(0.55f, 0.38f, 0.22f); }
                }
                int hit = -1;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
                    Key[] pad = { Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad1, Key.Numpad2, Key.Numpad3 };
                    for (int i = 0; i < 9; i++) if (kb[keys[i]].wasPressedThisFrame || kb[pad[i]].wasPressedThisFrame) hit = i;
                }
                if (s.Pointer(out var p, out bool down, out _, out _) && down)
                    for (int i = 0; i < 9; i++) if ((p - (Hole(i) + new Vector2(0f, 30f))).sqrMagnitude < 70f * 70f) hit = i;
                if (hit >= 0)
                {
                    if (life[hit] > 0f && up[hit] > 0.5f) { s.score++; life[hit] = -0.25f; monkeys[hit].color = new Color(1f, 0.4f, 0.3f); Haptics.Selection(); if (AudioManager.Instance) AudioManager.Instance.Click(); }
                }
                for (int i = 0; i < 9; i++)
                {
                    if (life[i] > 0f) { life[i] -= dt; up[i] = Mathf.MoveTowards(up[i], 1f, dt * 7f); if (life[i] <= 0f) life[i] = -0.01f; }
                    else if (life[i] < 0f) { life[i] = Mathf.Min(0f, life[i] + dt); up[i] = Mathf.MoveTowards(up[i], 0f, dt * 6f); }
                    else up[i] = Mathf.MoveTowards(up[i], 0f, dt * 6f);
                    var rt = monkeys[i].rectTransform;
                    rt.anchoredPosition = Hole(i) + new Vector2(0f, -20f + 60f * up[i]);
                    rt.localScale = new Vector3(1f, Mathf.Max(0.05f, up[i]), 1f);
                    monkeys[i].gameObject.SetActive(up[i] > 0.02f);
                }
            }

            public void End() { }
        }

        // ------------------------------------------------------------------ Pahadi Dash
        class Dash : IArcade
        {
            ArcadeScreen s;
            Image car;
            int lane = 1;
            float carX, speed, spawn, dist;
            readonly List<(Image img, int lane, float y)> things = new List<(Image, int, float)>();
            readonly List<Image> marks = new List<Image>();
            static float LaneX(int l) => W * 0.5f + (l - 1) * 230f;
            static readonly (string name, Color c, Vector2 size)[] Kinds =
            {
                ("GAAY", new Color(0.92f, 0.9f, 0.85f), new Vector2(120f, 90f)), ("PATTHAR", new Color(0.45f, 0.45f, 0.48f), new Vector2(110f, 80f)),
                ("TAXI", new Color(0.95f, 0.8f, 0.15f), new Vector2(110f, 170f)), ("BUS", new Color(0.2f, 0.45f, 0.85f), new Vector2(130f, 260f)),
                ("BANDAR", new Color(0.55f, 0.38f, 0.22f), new Vector2(80f, 80f)),
            };

            public void Begin(ArcadeScreen sc)
            {
                s = sc;
                s.Box("Hill", new Vector2(W * 0.5f, H * 0.5f), new Vector2(W, H), new Color(0.13f, 0.27f, 0.13f));
                s.Box("Road", new Vector2(W * 0.5f, H * 0.5f), new Vector2(720f, H), new Color(0.2f, 0.2f, 0.22f));
                for (int i = 0; i < 8; i++)
                    foreach (float x in new[] { LaneX(0) + 115f, LaneX(1) + 115f })
                        marks.Add(s.Box("Mark", new Vector2(x, i * 90f), new Vector2(8f, 46f), new Color(1f, 1f, 1f, 0.6f)));
                car = s.Box("Car", new Vector2(LaneX(1), 90f), new Vector2(100f, 160f), new Color(0.85f, 0.15f, 0.15f), HudStyle.RoundedSmall);
                car.type = Image.Type.Sliced;
                UiKit.Img("Glass", car.transform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 40f), new Color(0.6f, 0.8f, 0.95f), HudStyle.RoundedSmall);
                carX = LaneX(1);
                speed = 380f;
                s.infoText.text = Loc.L("A / D ya screen ke dono taraf tap", "A / D or tap either side");
            }

            public void Tick(float dt)
            {
                var kb = Keyboard.current; var pad = Gamepad.current;
                if (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)) lane = Mathf.Max(0, lane - 1);
                if (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) lane = Mathf.Min(2, lane + 1);
                if (pad != null && pad.dpad.left.wasPressedThisFrame) lane = Mathf.Max(0, lane - 1);
                if (pad != null && pad.dpad.right.wasPressedThisFrame) lane = Mathf.Min(2, lane + 1);
                if (s.Pointer(out var p, out bool down, out _, out _) && down && p.y > 0f && p.y < H) lane = Mathf.Clamp(lane + (p.x < W * 0.5f ? -1 : 1), 0, 2);
                carX = Mathf.MoveTowards(carX, LaneX(lane), dt * 1400f);
                car.rectTransform.anchoredPosition = new Vector2(carX, 90f);

                speed += dt * 14f;
                dist += speed * dt;
                s.score = Mathf.FloorToInt(dist / 100f);
                foreach (var m in marks)
                {
                    var ap = m.rectTransform.anchoredPosition;
                    ap.y -= speed * dt;
                    if (ap.y < -45f) ap.y += 8 * 90f;
                    m.rectTransform.anchoredPosition = ap;
                }
                spawn -= dt;
                if (spawn <= 0f)
                {
                    spawn = Mathf.Clamp(260f / speed, 0.35f, 0.9f) * UnityEngine.Random.Range(0.8f, 1.5f);
                    var k = Kinds[UnityEngine.Random.Range(0, Kinds.Length)];
                    int l = UnityEngine.Random.Range(0, 3);
                    var img = s.Box(k.name, new Vector2(LaneX(l), H + k.size.y), k.size, k.c, HudStyle.RoundedSmall);
                    img.type = Image.Type.Sliced;
                    var t = UiKit.Txt("Name", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, k.size, 20, TextAnchor.MiddleCenter, Color.black, true, false);
                    t.text = k.name;
                    things.Add((img, l, H + k.size.y));
                }
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    var th = things[i];
                    th.y -= speed * dt;
                    th.img.rectTransform.anchoredPosition = new Vector2(LaneX(th.lane), th.y);
                    things[i] = th;
                    float half = th.img.rectTransform.sizeDelta.y * 0.5f;
                    if (Mathf.Abs(LaneX(th.lane) - carX) < 90f && Mathf.Abs(th.y - 90f) < half + 70f)
                    {
                        car.color = new Color(0.4f, 0.4f, 0.4f);
                        Haptics.Selection();
                        s.GameOver(Loc.F("Takkar! Doori: {0}", s.score));
                        return;
                    }
                    if (th.y < -half - 20f) { UnityEngine.Object.Destroy(th.img.gameObject); things.RemoveAt(i); }
                }
            }

            public void End() { }
        }

        // ------------------------------------------------------------------ Pool (practice)
        class Pool : IArcade
        {
            ArcadeScreen s;
            class Ball { public Vector2 p, v; public Image img; public bool potted; public int n; }
            readonly List<Ball> balls = new List<Ball>();
            Ball cue;
            Image cueStick, aimLine;
            readonly Vector2[] pockets = new Vector2[6];
            const float R = 13f, PocketR = 28f, Friction = 0.985f;
            Rect table;
            float aimAng = 0f, power;   // at the rack
            bool dragging, charging;
            Vector2 dragFrom;
            int shots, left;
            static readonly Color[] Colors =
            {
                new Color(0.95f, 0.8f, 0.1f), new Color(0.15f, 0.3f, 0.85f), new Color(0.85f, 0.15f, 0.15f), new Color(0.45f, 0.15f, 0.6f),
                new Color(0.95f, 0.45f, 0.1f), new Color(0.1f, 0.55f, 0.25f), new Color(0.5f, 0.15f, 0.1f), new Color(0.05f, 0.05f, 0.05f),
            };

            bool Moving { get { foreach (var b in balls) if (!b.potted && b.v.sqrMagnitude > 4f) return true; return false; } }

            public void Begin(ArcadeScreen sc)
            {
                s = sc;
                table = new Rect(60f, 50f, W - 120f, H - 100f);
                s.Box("Wood", new Vector2(W * 0.5f, H * 0.5f), new Vector2(W - 40f, H - 30f), new Color(0.35f, 0.18f, 0.08f));
                s.Box("Cloth", table.center, table.size, new Color(0.05f, 0.42f, 0.25f));
                int k = 0;
                foreach (float y in new[] { table.yMin, table.yMax })
                    foreach (float x in new[] { table.xMin, table.center.x, table.xMax })
                    {
                        pockets[k++] = new Vector2(x, y);
                        s.Box("Pocket", new Vector2(x, y), Vector2.one * PocketR * 2f, Color.black, HudStyle.Disc);
                    }
                aimLine = s.Box("Aim", Vector2.zero, new Vector2(4f, 300f), new Color(1f, 1f, 1f, 0.35f));
                aimLine.rectTransform.pivot = new Vector2(0.5f, 0f);
                // the rack: a triangle of fifteen, the white on the other side
                Vector2 apex = new Vector2(table.xMin + table.width * 0.72f, table.center.y);
                int n = 0;
                for (int row = 0; row < 5; row++)
                    for (int i = 0; i <= row; i++)
                    {
                        var p = apex + new Vector2(row * R * 1.75f, (i - row * 0.5f) * R * 2.02f);
                        balls.Add(MakeBall(p, n < 7 ? Colors[n] : n == 7 ? Colors[7] : Color.Lerp(Colors[n % 7], Color.white, 0.35f), n + 1));
                        n++;
                    }
                cue = MakeBall(new Vector2(table.xMin + table.width * 0.25f, table.center.y), Color.white, 0);
                balls.Add(cue);
                cueStick = s.Box("Cue", Vector2.zero, new Vector2(8f, 260f), new Color(0.85f, 0.7f, 0.45f));
                cueStick.rectTransform.pivot = new Vector2(0.5f, 1f);
                left = 15;
                s.infoText.text = Loc.L("White ball se peeche khencho aur chhodo (ya A/D + Space)", "Drag back from the white ball and let go (or A/D + Space)");
            }

            Ball MakeBall(Vector2 p, Color c, int n)
            {
                var img = s.Box("Ball " + n, p, Vector2.one * R * 2f, c, HudStyle.Disc);
                return new Ball { p = p, img = img, n = n };
            }

            public void Tick(float dt)
            {
                bool moving = Moving;
                if (!moving)
                {
                    if (cue.potted)
                    {
                        cue.potted = false; cue.p = new Vector2(table.xMin + table.width * 0.25f, table.center.y); cue.v = Vector2.zero; cue.img.gameObject.SetActive(true);
                    }
                    Aim(dt);
                }
                else { cueStick.enabled = false; aimLine.enabled = false; }
                // the physics in small steps
                int steps = 6;
                float h = dt / steps;
                for (int st = 0; st < steps; st++)
                {
                    foreach (var b in balls)
                    {
                        if (b.potted) continue;
                        b.p += b.v * h;
                        if (b.p.x < table.xMin + R) { b.p.x = table.xMin + R; b.v.x = -b.v.x * 0.8f; }
                        if (b.p.x > table.xMax - R) { b.p.x = table.xMax - R; b.v.x = -b.v.x * 0.8f; }
                        if (b.p.y < table.yMin + R) { b.p.y = table.yMin + R; b.v.y = -b.v.y * 0.8f; }
                        if (b.p.y > table.yMax - R) { b.p.y = table.yMax - R; b.v.y = -b.v.y * 0.8f; }
                        foreach (var pk in pockets)
                            if ((b.p - pk).sqrMagnitude < (PocketR - 2f) * (PocketR - 2f))
                            {
                                b.potted = true; b.v = Vector2.zero; b.img.gameObject.SetActive(false);
                                if (b != cue) { left--; Haptics.Selection(); }
                                else GameEvents.ShowToast(Loc.L("White ball gir gayi", "Scratch"), 1.2f);
                                break;
                            }
                    }
                    for (int i = 0; i < balls.Count; i++)
                        for (int j = i + 1; j < balls.Count; j++)
                        {
                            var a = balls[i]; var b = balls[j];
                            if (a.potted || b.potted) continue;
                            Vector2 d = b.p - a.p;
                            float dist = d.magnitude;
                            if (dist >= 2f * R || dist < 1e-4f) continue;
                            Vector2 nrm = d / dist;
                            float overlap = 2f * R - dist;
                            a.p -= nrm * overlap * 0.5f; b.p += nrm * overlap * 0.5f;
                            float rel = Vector2.Dot(a.v - b.v, nrm);
                            if (rel > 0f) { a.v -= nrm * rel * 0.97f; b.v += nrm * rel * 0.97f; }
                        }
                }
                float fr = Mathf.Pow(Friction, dt * 60f);
                foreach (var b in balls)
                {
                    if (b.potted) continue;
                    b.v *= fr;
                    if (b.v.sqrMagnitude < 9f) b.v = Vector2.zero;
                    b.img.rectTransform.anchoredPosition = b.p;
                }
                s.score = shots;
                s.scoreText.text = Loc.F("Shots: {0}", shots);
                s.infoText.text = Loc.F("Bachi: {0}", left);
                if (left <= 0 && !Moving) s.GameOver(Loc.F("Sab pot! {0} shots mein", shots));
            }

            void Aim(float dt)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) aimAng += 70f * dt;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) aimAng -= 70f * dt;
                    if (kb.spaceKey.isPressed) { charging = true; power = Mathf.Min(1f, power + dt * 0.7f); }
                    else if (charging) { charging = false; Shoot(); }
                }
                if (s.Pointer(out var p, out bool down, out bool held, out bool up))
                {
                    if (down && (p - cue.p).sqrMagnitude < 120f * 120f) { dragging = true; dragFrom = p; }
                    if (dragging && held)
                    {
                        Vector2 pull = p - cue.p;
                        if (pull.sqrMagnitude > 25f) aimAng = Mathf.Atan2(-pull.y, -pull.x) * Mathf.Rad2Deg;
                        power = Mathf.Clamp01(pull.magnitude / 220f);
                    }
                    if (dragging && up) { dragging = false; if (power > 0.05f) Shoot(); else power = 0f; }
                }
                Vector2 dir = new Vector2(Mathf.Cos(aimAng * Mathf.Deg2Rad), Mathf.Sin(aimAng * Mathf.Deg2Rad));
                cueStick.enabled = true;
                aimLine.enabled = true;
                float back = R + 10f + power * 90f;
                cueStick.rectTransform.anchoredPosition = cue.p - dir * back;
                cueStick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, aimAng + 90f);
                aimLine.rectTransform.anchoredPosition = cue.p;
                aimLine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, aimAng - 90f);
            }

            void Shoot()
            {
                Vector2 dir = new Vector2(Mathf.Cos(aimAng * Mathf.Deg2Rad), Mathf.Sin(aimAng * Mathf.Deg2Rad));
                cue.v = dir * Mathf.Lerp(250f, 1900f, power);
                power = 0f;
                shots++;
                if (AudioManager.Instance) AudioManager.Instance.Click();
            }

            public void End() { }
        }
    }

    /// <summary>A parlour machine: E opens its game (Rs 20 a go).</summary>
    public class ArcadeCabinet : Interactable
    {
        public ArcadeScreen.Game game;
        public string title = "Arcade";
        public int price = 20;

        void Awake() { radius = 1.8f; }

        public override string Prompt => Loc.F("{0} khelo (Rs {1})", title, price);
        public override bool CanInteract(PlayerController p) => !ArcadeScreen.Current && !BowlingLane.Playing && !Cutscene.Playing && p.CurrentMode == PlayerController.Mode.Normal;

        public override void Interact(PlayerController p)
        {
            if (!GameState.TrySpend(price)) { GameEvents.ShowToast(Loc.F("Rs {0} chahiye", price), 2f); return; }
            ArcadeScreen.Open(game, title);
        }
    }
}
