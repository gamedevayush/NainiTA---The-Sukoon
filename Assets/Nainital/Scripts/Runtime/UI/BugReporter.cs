using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// "Bug batao" (user, 2026-11-12: "unity ke andar ek option do, capture bug; vo photo aur text maange, main yahan bug
    /// bata doon photo ke saath"). F8 on a PC, the Bug button on a phone, or Nainital > Bug capture karo in the editor.
    /// The screen is grabbed first, as it is; then a box asks what is wrong (and, in the editor, for any extra photos).
    /// Save writes a folder per report: screen.png, the extra photos, and note.txt with the words plus everything needed
    /// to find the spot again (player and camera position and direction, the hour, the weather, the car, the fps).
    /// In the editor and on a PC the folders go to BugReports next to Assets; on a phone to the app's data folder.
    /// </summary>
    public class BugReporter : MonoBehaviour
    {
        public static BugReporter Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.panel && Instance.panel.activeSelf;

        Canvas canvas;
        GameObject panel;
        RawImage preview;
        InputField input;
        Text extrasLabel, status;
        Texture2D shot;
        readonly System.Collections.Generic.List<string> extras = new System.Collections.Generic.List<string>();
        string where;
        float fps = 60f;
        Font font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("Bug Reporter");
            DontDestroyOnLoad(go);
            go.AddComponent<BugReporter>();
        }

        void Awake() => Instance = this;

        public static string Folder => Application.isEditor || !Application.isMobilePlatform
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "../BugReports"))
            : Path.Combine(Application.persistentDataPath, "BugReports");

        public static void Open() { if (Instance && !IsOpen) Instance.StartCoroutine(Instance.Grab()); }

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame && !IsOpen) Open();
        }

        IEnumerator Grab()
        {
            yield return new WaitForEndOfFrame();
            if (shot) Destroy(shot);
            shot = ScreenCapture.CaptureScreenshotAsTexture();
            where = Describe();
            extras.Clear();
            Build();
            preview.texture = shot;
            input.text = "";
            extrasLabel.text = "";
            status.text = "";
            panel.SetActive(true);
            GameInput.UiBlocking = true;
            GameInput.TouchMove = Vector2.zero;
            input.Select();
            input.ActivateInputField();
        }

        string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("time: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("platform: " + Application.platform + (Application.isEditor ? " (editor)" : "") + ", screen " + Screen.width + "x" + Screen.height + $", fps {fps:F0}");
            var p = PlayerController.Instance;
            if (p) sb.AppendLine($"player: {p.transform.position.x:F1},{p.transform.position.y:F1},{p.transform.position.z:F1} facing {p.transform.eulerAngles.y:F0}");
            var cam = Camera.main;
            if (cam)
            {
                Vector3 c = cam.transform.position, f = cam.transform.forward;
                sb.AppendLine($"camera: {c.x:F1},{c.y:F1},{c.z:F1} looking {f.x:F2},{f.y:F2},{f.z:F2}");
                Vector3 l = c + f * 20f;
                sb.AppendLine($"shot spec: bug;{c.x:F1},{c.y:F1},{c.z:F1};{l.x:F1},{l.y:F1},{l.z:F1};{(DayNightCycle.Instance ? DayNightCycle.Instance.timeOfDay : 12f):F1}");
            }
            if (DayNightCycle.Instance) sb.AppendLine($"hour: {DayNightCycle.Instance.timeOfDay:F2}");
            if (WeatherSystem.Instance) sb.AppendLine("weather: " + WeatherSystem.Instance.current);
            var car = CarController.Driving;
            if (car) sb.AppendLine($"driving: {car.name} at {car.SpeedKmh:F0} km/h");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the box
        void Build()
        {
            if (panel) return;
            font = GameAssets.Font ? GameAssets.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cgo = new GameObject("Bug Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            if (!FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(transform, false);

            panel = Box("Bug panel", cgo.transform, Vector2.zero, new Vector2(4000, 4000), new Color(0, 0, 0, 0.6f)).gameObject;
            var box = Box("Box", panel.transform, Vector2.zero, new Vector2(1500, 900), new Color(0.05f, 0.07f, 0.1f, 0.97f));
            Label(box, "Bug batao", new Vector2(0, 400), new Vector2(1400, 70), 44, new Color(1f, 0.85f, 0.3f));

            var pv = new GameObject("Shot", typeof(RectTransform), typeof(RawImage));
            pv.transform.SetParent(box, false);
            var prt = pv.GetComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(-360, 60);
            prt.sizeDelta = new Vector2(680, 680f * Screen.height / Mathf.Max(1, Screen.width));
            preview = pv.GetComponent<RawImage>();

            Label(box, "Kya galat hai? (jagah, kya dikh raha hai, kya hona chahiye)", new Vector2(370, 300), new Vector2(700, 50), 26, Color.white);
            var field = Box("Text box", box, new Vector2(370, 80), new Vector2(700, 380), new Color(1, 1, 1, 0.95f));
            var txt = Label(field, "", Vector2.zero, new Vector2(670, 360), 28, Color.black);
            txt.alignment = TextAnchor.UpperLeft;
            txt.supportRichText = false;
            var ph = Label(field, "Yahan likho...", Vector2.zero, new Vector2(670, 360), 28, new Color(0, 0, 0, 0.35f));
            ph.alignment = TextAnchor.UpperLeft;
            input = field.gameObject.AddComponent<InputField>();
            input.textComponent = txt;
            input.placeholder = ph;
            input.lineType = InputField.LineType.MultiLineNewline;
            input.targetGraphic = field.GetComponent<Image>();

            extrasLabel = Label(box, "", new Vector2(370, -150), new Vector2(700, 50), 22, new Color(0.7f, 0.9f, 1f));
            status = Label(box, "", new Vector2(0, -400), new Vector2(1400, 50), 24, new Color(0.6f, 1f, 0.6f));
#if UNITY_EDITOR
            Button(box, "Aur photo jodo", new Vector2(160, -250), new Vector2(300, 80), AddPhoto);
#endif
            Button(box, "Save karo", new Vector2(520, -250), new Vector2(300, 80), Save);
            Button(box, "Rehne do", new Vector2(520, -340), new Vector2(300, 64), Close);
            panel.SetActive(false);
        }

        RectTransform Box(string name, Transform parent, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = c;
            return rt;
        }

        Text Label(Transform parent, string s, Vector2 pos, Vector2 size, int fontSize, Color c)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.color = c;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = s;
            return t;
        }

        void Button(Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            var rt = Box(label, parent, pos, size, new Color(0.16f, 0.3f, 0.45f, 1f));
            var b = rt.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => onClick());
            Label(rt, label, Vector2.zero, size, 30, Color.white);
        }

#if UNITY_EDITOR
        void AddPhoto()
        {
            string f = UnityEditor.EditorUtility.OpenFilePanel("Photo jodo", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(f)) return;
            extras.Add(f);
            extrasLabel.text = extras.Count + " photo jodi: " + Path.GetFileName(f);
        }
#endif

        void Save()
        {
            string dir = Path.Combine(Folder, System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(dir);
            if (shot) File.WriteAllBytes(Path.Combine(dir, "screen.png"), shot.EncodeToPNG());
            for (int i = 0; i < extras.Count; i++)
                try { File.Copy(extras[i], Path.Combine(dir, $"photo_{i + 1}{Path.GetExtension(extras[i])}"), true); } catch { }
            File.WriteAllText(Path.Combine(dir, "note.txt"), "BUG: " + input.text.Trim() + "\n\n" + where, Encoding.UTF8);
            Debug.Log("[Nainital] bug report saved -> " + dir);
            GameEvents.ShowToast("Bug save ho gaya. Claude ko bolo: \"bug dekho\"", 3f);
            Close();
        }

        void Close()
        {
            if (panel) panel.SetActive(false);
            GameInput.UiBlocking = false;
        }
    }
}
