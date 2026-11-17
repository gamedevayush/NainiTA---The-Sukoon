using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>Small helpers for the screens built in code: the boot screens (no HUD there) and the menus.</summary>
    public static class UiKit
    {
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.6f;
            return c;
        }

        /// <summary>An event system if there is none (the new one, or null).</summary>
        public static GameObject EnsureEventSystem(Transform parent = null)
        {
            if (Object.FindAnyObjectByType<EventSystem>()) return null;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (parent) es.transform.SetParent(parent, false);
            return es;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
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

        public static RectTransform Full(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(rt);
            return rt;
        }

        public static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        public static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, Sprite sprite = null)
        {
            var img = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
            img.color = c;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static Image FullImg(string name, Transform parent, Color c, Sprite sprite = null)
        {
            var img = Full(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, bool small = false)
        {
            var img = Img(name, parent, anchor, pivot, pos, size, c, small ? HudStyle.RoundedSmall : HudStyle.Rounded);
            img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>An Aipan strip tiled sideways (Aipan.Border, Vasudhara, DotRow).</summary>
        public static Image Strip(string name, Transform parent, Sprite s, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
        {
            var img = Img(name, parent, anchor, pivot, pos, size, c, s);
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 100f / Mathf.Max(1f, size.y) * s.rect.height / 100f;   // one tile as tall as the strip
            return img;
        }

        public static Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false, bool shadow = true)
        {
            var t = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Text>();
            t.font = bold ? HudStyle.Bold : HudStyle.Regular;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.lineSpacing = 0.95f;
            if (shadow)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.6f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        /// <summary>Wide-set capitals ("N A I N I T A"), the way the titles are set.</summary>
        public static string Spaced(string s, int spaces = 1)
        {
            var sb = new System.Text.StringBuilder();
            string gap = new string(' ', spaces);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0) sb.Append(s[i - 1] == ' ' ? "  " : gap);
                if (s[i] != ' ') sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
        public static float EaseOutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.4f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

        /// <summary>Text that follows the language: Hindi (Roman) and English.</summary>
        public class LocTexts
        {
            readonly List<(Text t, string hi, string en)> list = new List<(Text, string, string)>();
            public Text Add(Text t, string hi, string en) { list.Add((t, hi, en)); t.text = Loc.English ? en : hi; return t; }
            public void Refresh() { foreach (var (t, hi, en) in list) if (t) t.text = Loc.English ? en : hi; }
        }

        public static string L(string hi, string en) => Loc.English ? en : hi;
    }

    /// <summary>A button made of whatever is under it: a tap runs it, with a tick on the phone and the click sound, and it
    /// dips a little while held.</summary>
    public class TapButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action onClick;
        public System.Action<bool> onHover;
        public bool interactable = true;
        public float pressScale = 0.96f;
        bool down, over;
        float k = 1f;

        public bool Hovered => over;
        public bool Held => down;

        public void OnPointerDown(PointerEventData e) { if (!interactable) return; down = true; Haptics.Selection(); }
        public void OnPointerUp(PointerEventData e) { down = false; }
        public void OnPointerEnter(PointerEventData e) { over = true; onHover?.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; onHover?.Invoke(false); }
        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) return;
            if (AudioManager.Instance) AudioManager.Instance.Click();
            onClick?.Invoke();
        }

        void Update()
        {
            k = Mathf.MoveTowards(k, down ? pressScale : 1f, Time.unscaledDeltaTime * 1.5f);
            transform.localScale = new Vector3(k, k, 1f);
        }
    }
}
