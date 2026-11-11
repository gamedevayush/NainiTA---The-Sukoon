using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nainital
{
    /// <summary>
    /// One place that merges keyboard/mouse, gamepad and on-screen touch controls.
    /// PC: WASD move, mouse look, Space jump, Shift sprint, Alt walk, LMB punch, F kick,
    ///     RMB/Q block, Ctrl/C roll, E interact, N night toggle, Esc free cursor.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameInput : MonoBehaviour
    {
        public static Vector2 Move;
        public static Vector2 Look;           // degrees this frame: x = yaw, y = pitch (up positive)
        public static float Zoom;             // metres this frame
        public static bool JumpPressed, AttackPressed, KickPressed, RollPressed, InteractPressed, NightTogglePressed;
        public static bool BlockHeld, SprintHeld, WalkHeld, JumpHeld, MapPressed, PausePressed, HornPressed;
        public static bool Paused;
        public static float LastLookTime;
        public static bool TouchJumpHeld;
        public static bool UiBlocking;        // dialog open: gameplay ignores input

        // Fed by TouchControls
        public static Vector2 TouchMove;
        public static Vector2 TouchLookPixels;
        public static bool TouchJump, TouchAttack, TouchKick, TouchRoll, TouchInteract, TouchBlock, TouchSprint, TouchMap, TouchHorn;
        float pinchPrev = -1f;

        public static bool UsingTouch { get; private set; }

        public float mouseSensitivity = 0.11f;
        public float touchSensitivity = 0.16f;
        public float stickLookSpeed = 170f;

        static bool Take(ref bool flag) { bool v = flag; flag = false; return v; }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) UsingTouch = true;
            if (kb != null && kb.anyKey.wasPressedThisFrame) UsingTouch = false;

            // Cursor lock for mouse look (desktop only)
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool clickConsumedForLock = false;
            if (!Application.isMobilePlatform && mouse != null)
            {
                if (!locked && mouse.leftButton.wasPressedThisFrame && !UiBlocking &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    clickConsumedForLock = true;
                }
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                if (UiBlocking && locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                locked = Cursor.lockState == CursorLockMode.Locked;
            }

            // Move
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            }
            if (pad != null)
            {
                Vector2 s = pad.leftStick.ReadValue();
                if (s.sqrMagnitude > move.sqrMagnitude) move = s;
            }
            if (TouchMove.sqrMagnitude > move.sqrMagnitude) move = TouchMove;
            Move = Vector2.ClampMagnitude(move, 1f);

            // Two fingers on the right half of the screen: pinch to zoom the camera (and no look while pinching)
            float pinchZoom = 0f;
            var ts = Touchscreen.current;
            if (ts != null && !UiBlocking)
            {
                int n = 0;
                Vector2 fa = default, fb = default;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    Vector2 tp = t.position.ReadValue();
                    if (tp.x < Screen.width * 0.45f) continue;   // the joystick finger
                    if (n == 0) fa = tp; else if (n == 1) fb = tp;
                    n++;
                }
                if (n == 2)
                {
                    float dist = (fa - fb).magnitude;
                    if (pinchPrev > 0f) pinchZoom = (dist - pinchPrev) * 0.012f * (Screen.dpi > 0 ? 400f / Screen.dpi : 1f);
                    pinchPrev = dist;
                    TouchLookPixels = Vector2.zero;
                }
                else pinchPrev = -1f;
            }

            // Look
            Vector2 look = Vector2.zero;
            if (mouse != null && locked) look += mouse.delta.ReadValue() * mouseSensitivity;
            if (pad != null) look += pad.rightStick.ReadValue() * stickLookSpeed * Time.unscaledDeltaTime;
            float dpiK = Screen.dpi > 0 ? Mathf.Clamp(420f / Screen.dpi, 0.5f, 1.5f) : 1f;   // the same swipe turns the same amount on any screen
            look += TouchLookPixels * touchSensitivity * dpiK;
            TouchLookPixels = Vector2.zero;
            Look = look;
            if (look.sqrMagnitude > 0.01f) LastLookTime = Time.unscaledTime;

            Zoom = pinchZoom;
            if (mouse != null && locked) Zoom = mouse.scroll.ReadValue().y * 0.004f;
            if (pad != null)
            {
                if (pad.dpad.up.isPressed) Zoom += 4f * Time.deltaTime;
                if (pad.dpad.down.isPressed) Zoom -= 4f * Time.deltaTime;
            }

            bool lmb = mouse != null && locked && !clickConsumedForLock && mouse.leftButton.wasPressedThisFrame;
            bool rmb = mouse != null && locked && mouse.rightButton.isPressed;

            bool tJump = Take(ref TouchJump), tAttack = Take(ref TouchAttack), tKick = Take(ref TouchKick),
                 tRoll = Take(ref TouchRoll), tInteract = Take(ref TouchInteract);
            JumpPressed = tJump || (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            AttackPressed = tAttack || lmb || (pad != null && pad.buttonWest.wasPressedThisFrame);
            KickPressed = tKick || (kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            RollPressed = tRoll || (kb != null && (kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)) ||
                          (pad != null && pad.buttonEast.wasPressedThisFrame);
            InteractPressed = tInteract || (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            NightTogglePressed = (kb != null && kb.nKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            BlockHeld = rmb || (kb != null && kb.qKey.isPressed) || (pad != null && pad.leftTrigger.isPressed) || TouchBlock;
            SprintHeld = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && (pad.leftStickButton.isPressed || pad.rightTrigger.isPressed)) || TouchSprint;
            WalkHeld = kb != null && kb.leftAltKey.isPressed;
            JumpHeld = TouchJumpHeld || (kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed);
            MapPressed = Take(ref TouchMap) || (kb != null && kb.mKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame);
            PausePressed = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame);
            HornPressed = Take(ref TouchHorn) || (kb != null && kb.gKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
        }
    }
}
