using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The phone's camera (owner, 21 Nov 2026: "camera with selfie, saves to the phone gallery"). The HUD goes, the view
    /// becomes the phone's: the back camera at the player's eyes, or the selfie camera ~0.9 m in front of the face looking
    /// back at it. Drag to look, scroll / pinch to zoom. The shot (no UI on it) is saved in the game's own gallery
    /// (persistentDataPath/Gallery, the phone's Gallery app) and also to the device: on a phone through NativeGallery
    /// (Photos / Gallery, album "NainiTA"), on a PC in Pictures\NainiTA.
    /// </summary>
    public class PhoneCamera : MonoBehaviour
    {
        public static PhoneCamera Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.open;

        bool open, selfie, busy;
        float yaw, pitch, fov = 55f, flash;
        Canvas canvas;
        Image flashImg;
        RawImage thumb;
        float thumbT;
        Text modeText;
        ThirdPersonCamera tpc;
        float savedFov;
        Light fill;   // the selfie's soft light on the face (the phone's screen), stronger at night

        public static string GalleryDir => Path.Combine(Application.persistentDataPath, "Gallery");

        public static PhoneCamera Ensure()
        {
            if (!Instance) Instance = new GameObject("PhoneCamera").AddComponent<PhoneCamera>();
            return Instance;
        }

        /// <summary>The saved shots, newest first.</summary>
        public static List<string> Photos()
        {
            var l = new List<string>();
            try
            {
                if (Directory.Exists(GalleryDir))
                    foreach (var f in Directory.GetFiles(GalleryDir))
                        if (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) l.Add(f);
            }
            catch { }
            l.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)));
            return l;
        }

        public void Open(bool asSelfie = false)
        {
            var p = PlayerController.Instance;
            var cam = Camera.main;
            if (!p || !cam) return;
            if (p.CurrentMode != PlayerController.Mode.Normal) { GameEvents.ShowToast("Camera paidal chalte hue hi khulega", 2.5f); return; }
            open = true;
            selfie = asSelfie;
            tpc = ThirdPersonCamera.Instance;
            if (tpc) tpc.enabled = false;
            savedFov = cam.fieldOfView;
            yaw = p.transform.eulerAngles.y;
            pitch = 0f;
            fov = 55f;
            if (HUD.Instance) HUD.Instance.SetAlpha(0f);
            GameInput.UiBlocking = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!canvas) BuildUi();
            canvas.gameObject.SetActive(true);
            UpdateModeText();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            var cam = Camera.main;
            if (cam) cam.fieldOfView = savedFov;
            if (tpc) { tpc.enabled = true; tpc.SnapBehind(); }
            if (HUD.Instance) HUD.Instance.SetAlpha(1f);
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            if (canvas) canvas.gameObject.SetActive(false);
            if (fill) fill.enabled = false;
            var rig = PlayerController.Instance ? PlayerController.Instance.Rig : null;
            if (rig) rig.speed = 0f;
        }

        /// <summary>Take a photo now (the shutter button; PhoneTest).</summary>
        public void Snap() { if (open && !busy) StartCoroutine(Shoot()); }

        void Flip()
        {
            selfie = !selfie;
            pitch = 0f;
            UpdateModeText();
        }

        void UpdateModeText() { if (modeText) modeText.text = Loc.T(selfie ? "Selfie" : "Camera"); }

        void BuildUi()
        {
            var go = new GameObject("PhoneCameraUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.6f;
            var h = HUD.Instance;
            Transform root = go.transform;
            // a thin frame line and the mode at the top
            modeText = h.TxtB("Mode", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(400, 50), 34, TextAnchor.MiddleCenter, HudStyle.Marigold);
            h.Txt("Hint", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(900, 36), 24, TextAnchor.MiddleCenter, HudStyle.InkDim).text =
                Loc.T(Application.isMobilePlatform ? "Drag karke dekho, do ungli se zoom" : "Drag karke dekho  |  Space: photo  |  F: selfie  |  T: band");
            // shutter (big, right middle), flip, close
            var shutter = h.Img("Shutter", root, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120, 0), new Vector2(150, 150), new Color(1f, 1f, 1f, 0.92f));
            shutter.sprite = HudStyle.Disc;
            var ring = h.Img("Ring", shutter.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176, 176), new Color(1, 1, 1, 0.9f));
            ring.sprite = HudStyle.RingSprite;
            ring.raycastTarget = false;
            var sb = shutter.gameObject.AddComponent<Button>();
            sb.onClick.AddListener(() => { if (!busy) StartCoroutine(Shoot()); });
            h.Btn("Selfie / Back", root, new Vector2(1, 0.5f), new Vector2(-120, -170), new Vector2(210, 60), Flip, 24);
            h.Btn("Band karo", root, new Vector2(1, 0.5f), new Vector2(-120, 170), new Vector2(210, 60), Close, 24);
            thumb = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            thumb.transform.SetParent(root, false);
            var trt = thumb.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0, 0);
            trt.pivot = new Vector2(0, 0);
            trt.anchoredPosition = new Vector2(40, 40);
            trt.sizeDelta = new Vector2(320, 180);
            thumb.gameObject.SetActive(false);
            flashImg = h.Img("Flash", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0));
            var frt = flashImg.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            flashImg.raycastTarget = false;
            canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (flashImg && flash > 0f) { flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 3f); flashImg.color = new Color(1, 1, 1, flash); }
            if (thumb && thumbT > 0f) { thumbT -= Time.unscaledDeltaTime; if (thumbT <= 0f) thumb.gameObject.SetActive(false); }
            if (!open) return;
            var p = PlayerController.Instance;
            if (!p || p.CurrentMode != PlayerController.Mode.Normal || p.Health && p.Health.IsDead) { Close(); return; }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame && !busy) StartCoroutine(Shoot());
                if (kb.fKey.wasPressedThisFrame) Flip();
                if (kb.tKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            }
            // look: drag (mouse or one finger), zoom: wheel or two fingers
            Vector2 d = Vector2.zero;
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed && !OverUi()) d += m.delta.ReadValue() * 0.12f;
            if (m != null) fov -= m.scroll.ReadValue().y * 0.02f;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                int n = 0; Vector2 a = default, b = default, da = default, db = default;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (n == 0) { a = t.position.ReadValue(); da = t.delta.ReadValue(); }
                    else if (n == 1) { b = t.position.ReadValue(); db = t.delta.ReadValue(); }
                    n++;
                }
                if (n == 1 && a.x < Screen.width * 0.82f) d += da * 0.09f;
                else if (n >= 2)
                {
                    float now = (a - b).magnitude, before = ((a - da) - (b - db)).magnitude;
                    fov -= (now - before) * 0.05f;
                }
            }
            fov = Mathf.Clamp(fov, 22f, 70f);
            yaw += d.x * (selfie ? -1f : 1f);
            pitch = Mathf.Clamp(pitch - d.y, selfie ? -25f : -60f, selfie ? 35f : 70f);
            if (p.Rig) { p.Rig.speed = 0f; p.Rig.grounded = true; }
        }

        static bool OverUi() => UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        void LateUpdate()
        {
            if (!open) return;
            var p = PlayerController.Instance;
            var cam = Camera.main;
            if (!p || !cam) return;
            cam.fieldOfView = fov;
            if (!fill)
            {
                fill = new GameObject("Selfie light").AddComponent<Light>();
                fill.type = LightType.Point;
                fill.range = 3f;
                fill.shadows = LightShadows.None;
                fill.color = new Color(0.95f, 0.97f, 1f);
                fill.transform.SetParent(transform, false);
            }
            var dn = DayNightCycle.Instance;
            fill.enabled = selfie;
            fill.intensity = Mathf.Lerp(0.4f, 2.2f, dn ? dn.NightFactor : 0f);
            Transform head = p.Rig && p.Rig.Anim && p.Rig.Anim.isHuman ? p.Rig.Anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 eye = head ? head.position + Vector3.up * 0.06f : p.transform.position + Vector3.up * 1.62f;
            if (!selfie)
            {
                // the back camera: from the eyes; the body turns with the view
                p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
                cam.transform.SetPositionAndRotation(eye + r * Vector3.forward * 0.25f, r);
            }
            else
            {
                // the selfie: the phone held out ~0.9 m in front of the face, a little above, looking back at it; the player
                // faces the phone
                p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Quaternion r = Quaternion.Euler(-pitch * 0.6f, yaw, 0f);
                Vector3 pos = eye + r * new Vector3(0.12f, 0.12f, 0.9f);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(eye + Vector3.down * 0.08f - pos, Vector3.up));
                fill.transform.position = pos + Vector3.down * 0.1f;
            }
        }

        IEnumerator Shoot()
        {
            busy = true;
            canvas.enabled = false;
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); } catch (Exception e) { Debug.LogWarning("[Nainital] photo: " + e.Message); }
            canvas.enabled = true;
            flash = 0.85f;
            var au = AudioManager.Instance;
            if (au) au.Click();
            Haptics.Selection();
            if (shot)
            {
                string name = "NainiTA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                byte[] jpg = shot.EncodeToJPG(90);
                try
                {
                    Directory.CreateDirectory(GalleryDir);
                    File.WriteAllBytes(Path.Combine(GalleryDir, name), jpg);
                }
                catch (Exception e) { Debug.LogWarning("[Nainital] photo save: " + e.Message); }
                string where = SaveToDevice(jpg, name);
                // a small preview in the corner
                thumb.texture = shot;
                thumb.gameObject.SetActive(true);
                thumbT = 1.6f;
                GameEvents.ShowToast("Photo save ho gayi" + where, 2f);
                StartCoroutine(DropLater(shot, 2f));
            }
            busy = false;
        }

        IEnumerator DropLater(Texture2D t, float s)
        {
            yield return new WaitForSecondsRealtime(s);
            if (thumb && thumb.texture == t) thumb.texture = null;
            Destroy(t);
        }

        /// <summary>To the device's own photos: on a phone through the NativeGallery package (yasirkula, MIT, v1.9.5 in
        /// Packages/manifest.json; owner's OK 22 Nov 2026) into the album "NainiTA" - it asks for the photo permission itself;
        /// on a PC to Pictures\NainiTA. Returns a " - where" note for the toast.</summary>
        static string SaveToDevice(byte[] bytes, string name)
        {
            if (Application.isMobilePlatform)
            {
                try
                {
                    NativeGallery.SaveImageToGallery(bytes, "NainiTA", name, (ok, path) =>
                    {
                        if (!ok) GameEvents.ShowToast("Phone ki Gallery mein save nahi hui (photos ki permission do)", 3.5f);
                    });
                    return " (phone ki Gallery mein bhi)";   // (whole toasts are in the language table: "Photo save ho gayi (...)")
                }
                catch (Exception e) { Debug.LogWarning("[Nainital] NativeGallery: " + e.Message); }
                return " (game ki Gallery mein)";
            }
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "NainiTA");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name), bytes);
                return " - Pictures\\NainiTA";
            }
            catch { return ""; }
        }
    }
}
