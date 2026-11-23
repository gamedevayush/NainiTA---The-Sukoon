using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// A bowling lane in the game parlour (owner, 23 Nov 2026: "video games ki dukaan me pool wgera, bowling"). It builds its
    /// own lane at runtime from this transform (origin = the foul line's middle, +z down the lane): the wooden lane, the two
    /// gutters, the pin deck, the pit and the ten pins, all real physics (a 6 kg ball, 1.5 kg pins), so the pins fall as they
    /// are hit. E at the line: play five frames. A / D (or drag) moves the ball across, hold Space / the screen to charge,
    /// let go to roll; Q / E (or the drag's slant) curve the aim. A pin that tips or slides off its spot is down; two rolls a
    /// frame, a strike earns 10 more, a spare 5. Esc stops.
    /// </summary>
    public class BowlingLane : Interactable
    {
        public float length = 14f;          // a parlour lane (a real one is 18.3 m)
        public const float LaneW = 1.05f, Gutter = 0.24f, BallR = 0.11f;
        public static BowlingLane Playing { get; private set; }
        /// <summary>The last game's total (tests).</summary>
        public static int LastTotal = -1;
        /// <summary>Test hook (Editor/StoryTest.Bowl): rolls left to throw on their own, straight, at 75% power.</summary>
        public static int TestRolls;

        readonly List<Transform> pins = new List<Transform>();
        readonly List<Vector3> pinSpots = new List<Vector3>();
        Rigidbody ball;
        static Material wood, gutterMat, pinMat, ballMat, darkMat;
        bool built;

        // the game
        int frame, roll, total, firstRoll;
        readonly List<string> sheet = new List<string>();
        float aimX, angle, charge;
        bool charging, rolling;
        Canvas canvas;
        Text info, scoreLine;
        Image meter;
        Camera cam;
        ThirdPersonCamera gameCam;

        void Awake() { radius = 2.2f; }

        public override Vector3 Point => transform.TransformPoint(new Vector3(0f, 0.2f, -1.2f));
        public override string Prompt => Loc.L("Bowling khelo (Rs 50)", "Play bowling (Rs 50)");
        public override bool CanInteract(PlayerController p) => !Playing && !ArcadeScreen.Current && !Cutscene.Playing && p.CurrentMode == PlayerController.Mode.Normal;

        public override void Interact(PlayerController p)
        {
            if (!GameState.TrySpend(50)) { GameEvents.ShowToast(Loc.F("Rs {0} chahiye", 50), 2f); return; }
            StartCoroutine(Game());
        }

        static Material Mat(Color c, float smooth)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        GameObject Piece(PrimitiveType t, Vector3 local, Vector3 scale, Material m, bool keepCollider = true)
        {
            var g = GameObject.CreatePrimitive(t);
            g.transform.SetParent(transform, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!keepCollider) Destroy(g.GetComponent<Collider>());
            return g;
        }

        void Start() => Build();

        void Build()
        {
            if (built) return;
            built = true;
            if (!wood)
            {
                wood = Mat(new Color(0.78f, 0.58f, 0.34f), 0.85f);
                gutterMat = Mat(new Color(0.12f, 0.12f, 0.14f), 0.5f);
                pinMat = Mat(new Color(0.96f, 0.95f, 0.92f), 0.7f);
                ballMat = Mat(new Color(0.12f, 0.2f, 0.75f), 0.9f);
                darkMat = Mat(new Color(0.05f, 0.05f, 0.06f), 0.2f);
            }
            float half = LaneW * 0.5f;
            // a raised dark base under all of it (16 cm: the lane, gutters and approach sit above the hall's floor, nothing
            // shares a surface with it - they flickered, owner 23 Nov 2026)
            Piece(PrimitiveType.Cube, new Vector3(0f, -0.13f, length * 0.5f - 0.6f), new Vector3(LaneW + 2f * Gutter + 0.4f, 0.06f, length + 1.2f), darkMat);
            Piece(PrimitiveType.Cube, new Vector3(0f, -0.04f, -1.0f), new Vector3(LaneW + 2f * Gutter + 0.4f, 0.08f, 2f), wood);   // the approach
            // the lane: a slab with its top at y = 0, gutters a little lower either side, side boards, the pit at the end
            Piece(PrimitiveType.Cube, new Vector3(0f, -0.05f, length * 0.5f), new Vector3(LaneW, 0.1f, length), wood);
            foreach (float s in new[] { -1f, 1f })
            {
                Piece(PrimitiveType.Cube, new Vector3(s * (half + Gutter * 0.5f), -0.09f, length * 0.5f), new Vector3(Gutter, 0.1f, length), gutterMat);
                Piece(PrimitiveType.Cube, new Vector3(s * (half + Gutter + 0.05f), 0.08f, length * 0.5f + 0.5f), new Vector3(0.1f, 0.35f, length + 1f), darkMat);
            }
            Piece(PrimitiveType.Cube, new Vector3(0f, -0.35f, length + 0.55f), new Vector3(LaneW + 2f * Gutter, 0.1f, 1.1f), darkMat);            // the pit floor
            Piece(PrimitiveType.Cube, new Vector3(0f, 0.4f, length + 1.15f), new Vector3(LaneW + 2f * Gutter + 0.2f, 1.6f, 0.1f), darkMat);      // the back curtain
            Piece(PrimitiveType.Cube, new Vector3(0f, 1.05f, length - 0.3f), new Vector3(LaneW + 2f * Gutter + 0.2f, 0.5f, 0.1f), darkMat, false);   // the masking board
            var foul = Piece(PrimitiveType.Cube, new Vector3(0f, 0.002f, 0f), new Vector3(LaneW, 0.004f, 0.03f), darkMat, false);
            // the ten pins on their spots: rows of 1, 2, 3, 4, 30.5 cm apart
            float spot0 = length - 1.4f;
            for (int row = 0; row < 4; row++)
                for (int i = 0; i <= row; i++)
                    pinSpots.Add(new Vector3((i - row * 0.5f) * 0.305f, 0f, spot0 + row * 0.264f));
            foreach (var sp in pinSpots) pins.Add(MakePin(sp));
            var b = Piece(PrimitiveType.Sphere, new Vector3(0f, BallR, -0.5f), Vector3.one * BallR * 2f, ballMat);
            ball = b.AddComponent<Rigidbody>();
            ball.mass = 6f;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.isKinematic = true;
            b.name = "Bowling ball";
        }

        Transform MakePin(Vector3 spot)
        {
            var g = new GameObject("Pin");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = spot;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(g.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.19f, 0f);
            body.transform.localScale = new Vector3(0.12f, 0.19f, 0.12f);
            body.GetComponent<Renderer>().sharedMaterial = pinMat;
            var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(neck.GetComponent<Collider>());
            neck.transform.SetParent(g.transform, false);
            neck.transform.localPosition = new Vector3(0f, 0.27f, 0f);
            neck.transform.localScale = new Vector3(0.125f, 0.012f, 0.125f);
            neck.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.8f, 0.1f, 0.1f), 0.6f);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.centerOfMass = new Vector3(0f, 0.12f, 0f);
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;
            return g.transform;
        }

        void ResetPins(bool all)
        {
            for (int i = 0; i < pins.Count; i++)
            {
                var p = pins[i];
                if (!all && !p.gameObject.activeSelf) continue;
                var rb = p.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                p.gameObject.SetActive(true);
                p.localPosition = pinSpots[i];
                p.localRotation = Quaternion.identity;
            }
        }

        void ReadyBall()
        {
            ball.isKinematic = true;
            ball.transform.localPosition = new Vector3(aimX, BallR + 0.01f, -0.4f);
            ball.transform.localRotation = Quaternion.identity;
        }

        int Down()
        {
            int n = 0;
            for (int i = 0; i < pins.Count; i++)
            {
                var p = pins[i];
                if (!p.gameObject.activeSelf) continue;
                bool tipped = Vector3.Dot(p.up, transform.up) < 0.8f;
                bool moved = (p.localPosition - pinSpots[i]).sqrMagnitude > 0.12f * 0.12f || p.localPosition.y < -0.05f;
                if (tipped || moved) n++;
            }
            return n;
        }

        void ClearDown()
        {
            for (int i = 0; i < pins.Count; i++)
            {
                var p = pins[i];
                if (!p.gameObject.activeSelf) continue;
                bool tipped = Vector3.Dot(p.up, transform.up) < 0.8f;
                bool moved = (p.localPosition - pinSpots[i]).sqrMagnitude > 0.12f * 0.12f || p.localPosition.y < -0.05f;
                if (tipped || moved) p.gameObject.SetActive(false);
                else { var rb = p.GetComponent<Rigidbody>(); rb.isKinematic = true; p.localPosition = pinSpots[i]; p.localRotation = Quaternion.identity; }
            }
        }

        // ------------------------------------------------------------------ playing
        IEnumerator Game()
        {
            Build();
            Playing = this;
            GameInput.StoryLock = true;
            GameInput.UiBlocking = true;
            if (HUD.Instance) HUD.Instance.SetAlpha(0f);
            cam = Camera.main;
            gameCam = ThirdPersonCamera.Instance;
            // the player steps to the side of the approach, out of the camera's way
            var pl = PlayerController.Instance;
            var motor = pl ? pl.GetComponent<CharacterMotor>() : null;
            if (motor) { motor.Teleport(transform.TransformPoint(new Vector3(-LaneW * 0.5f - Gutter - 0.9f, 0.1f, -1.4f))); pl.transform.rotation = transform.rotation; }
            if (gameCam) gameCam.enabled = false;
            MakeUi();
            frame = 0; total = 0; sheet.Clear();
            ResetPins(true);
            const int Frames = 5;
            bool quit = false;
            for (frame = 0; frame < Frames && !quit; frame++)
            {
                firstRoll = 0;
                for (roll = 0; roll < 2 && !quit; roll++)
                {
                    aimX = 0f; angle = 0f; charge = 0f;
                    ReadyBall();
                    // aim and charge
                    rolling = false;
                    while (!rolling)
                    {
                        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { quit = true; break; }
                        Aim(Time.unscaledDeltaTime);
                        Watch(false);
                        yield return null;
                    }
                    if (quit) break;
                    // the roll
                    foreach (var p in pins) if (p.gameObject.activeSelf) p.GetComponent<Rigidbody>().isKinematic = false;
                    ball.isKinematic = false;
                    float speed = Mathf.Lerp(4.5f, 10.5f, charge);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    ball.linearVelocity = transform.TransformDirection(dir) * speed;
                    ball.angularVelocity = transform.TransformDirection(Vector3.right) * speed / BallR;
                    float t = 0f;
                    while (t < 9f)
                    {
                        t += Time.deltaTime;
                        Watch(true);
                        if (ball.transform.localPosition.z > length + 0.4f && t > 1f) break;
                        if (t > 2f && ball.linearVelocity.sqrMagnitude < 0.05f) break;
                        yield return null;
                    }
                    for (float k = 0f; k < 1.6f; k += Time.deltaTime) { Watch(true); yield return null; }   // the pins settle
                    int down = Down();
                    int scored = down;
                    if (roll == 0)
                    {
                        firstRoll = down;
                        if (down == 10) { scored += 10; sheet.Add("X"); total += scored; Say(Loc.L("STRIKE!", "STRIKE!")); ResetPins(true); break; }
                        ClearDown();
                    }
                    else
                    {
                        bool spare = firstRoll + down == 10;
                        if (spare) scored += 5;
                        sheet.Add(spare ? firstRoll + "/" : firstRoll + "-" + down);
                        if (spare) Say(Loc.L("SPARE!", "SPARE!"));
                        ResetPins(true);
                    }
                    total += scored;
                    yield return null;
                }
            }
            LastTotal = total;
            Debug.Log($"[Nainital] bowling: {total} ({string.Join(" ", sheet)})");
            if (!quit) { Say(Loc.F("Khel khatam! Score: {0}", total)); for (float k = 0f; k < 2.5f; k += Time.unscaledDeltaTime) yield return null; }
            Stop();
        }

        void Aim(float dt)
        {
            if (TestRolls > 0) { TestRolls--; aimX = 0.05f; angle = 0f; charge = 0.75f; rolling = true; return; }
            var kb = Keyboard.current; var pad = Gamepad.current;
            float mx = 0f;
            if (kb != null) { if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mx -= 1f; if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mx += 1f; }
            if (pad != null) mx += pad.leftStick.ReadValue().x;
            aimX = Mathf.Clamp(aimX + mx * 0.6f * dt, -LaneW * 0.5f + BallR, LaneW * 0.5f - BallR);
            if (kb != null) { if (kb.qKey.isPressed) angle -= 6f * dt; if (kb.eKey.isPressed) angle += 6f * dt; }
            if (pad != null) angle += pad.rightStick.ReadValue().x * 6f * dt;
            angle = Mathf.Clamp(angle, -4f, 4f);
            bool hold = (kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed);
            var t = Touchscreen.current;
            var m = Mouse.current;
            bool pointer = (t != null && t.primaryTouch.press.isPressed) || (m != null && m.leftButton.isPressed);
            if (pointer)
            {
                // drag: across moves the ball, its slant tilts the aim; holding charges
                Vector2 d = t != null && t.primaryTouch.press.isPressed ? t.primaryTouch.delta.ReadValue() : m.delta.ReadValue();
                aimX = Mathf.Clamp(aimX + d.x * 0.0015f, -LaneW * 0.5f + BallR, LaneW * 0.5f - BallR);
                hold = true;
            }
            if (hold) { charging = true; charge = Mathf.PingPong(Time.unscaledTime * 1.1f, 1f); }
            else if (charging) { charging = false; rolling = true; }
            ball.transform.localPosition = new Vector3(aimX, BallR + 0.01f, -0.4f);
            meter.fillAmount = charging ? charge : 0f;
        }

        void Watch(bool follow)
        {
            if (info)
            {
                info.text = Loc.L("A/D: jagah  Q/E: tircha  Space dabaake chhodo", "A/D: move  Q/E: angle  hold & release Space");
                scoreLine.text = Loc.F("Frame {0}/5  Roll {1}  Score {2}   {3}", frame + 1, roll + 1, total, string.Join("  ", sheet));
            }
            if (!cam) return;
            Vector3 at = follow ? transform.InverseTransformPoint(ball.transform.position) : new Vector3(aimX, 0f, -0.4f);
            float z = Mathf.Min(at.z, length - 3.5f);
            Vector3 pos = transform.TransformPoint(new Vector3(at.x * 0.5f, 1.25f, z - 2.4f));
            Vector3 look = transform.TransformPoint(new Vector3(0f, 0.2f, Mathf.Min(z + 6f, length)));
            cam.transform.position = Vector3.Lerp(cam.transform.position, pos, follow ? 0.12f : 0.3f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), 0.2f);
        }

        void Say(string s) => GameEvents.ShowToast(s, 1.6f);

        void MakeUi()
        {
            canvas = UiKit.Canvas("Bowling UI", 860, transform);
            var panel = UiKit.Panel("Bar", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1200f, 120f), HudStyle.Glass);
            scoreLine = UiKit.Txt("Score", panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1160f, 44f), 32, TextAnchor.MiddleCenter, HudStyle.Ink, true);
            info = UiKit.Txt("Info", panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1160f, 34f), 24, TextAnchor.MiddleCenter, HudStyle.InkDim);
            var bg = UiKit.Panel("Meter bg", canvas.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(34f, 360f), new Color(1, 1, 1, 0.15f), true);
            meter = UiKit.Img("Meter", bg.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(34f, 360f), HudStyle.Marigold, HudStyle.RoundedSmall);
            meter.type = Image.Type.Filled;
            meter.fillMethod = Image.FillMethod.Vertical;
            meter.fillAmount = 0f;
        }

        void Stop()
        {
            if (canvas) Destroy(canvas.gameObject);
            ReadyBall();
            ResetPins(true);
            if (gameCam) { gameCam.enabled = true; gameCam.SnapBehind(); }
            GameInput.StoryLock = false;
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            if (HUD.Instance) HUD.Instance.SetAlpha(1f);
            Playing = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Playing == this) Stop();
        }
    }
}
