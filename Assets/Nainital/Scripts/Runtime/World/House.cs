using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The player's house on the Zoo Road (FREEROAM #6; built by LandmarkBuilder.House). While the player is near: the
    /// lamps inside are on and the fireplace burns (real flames, a flickering warm light). The bed sleeps till the next
    /// morning and saves (HouseBed), the almari shows everything bought and changes the clothes (HouseStorage).
    /// </summary>
    public class House : MonoBehaviour
    {
        public Light[] lamps = new Light[0];
        public Vector3 fireAt;
        ParticleSystem flames;
        Light fireLight;
        float flick, checkT;
        bool lit;

        void Update()
        {
            checkT -= Time.deltaTime;
            if (checkT <= 0f)
            {
                checkT = 0.5f;
                var p = PlayerController.Instance;
                bool near = p && (p.transform.position - transform.position).sqrMagnitude < 40f * 40f;
                if (near != lit) Light(near);
            }
            if (lit && fireLight)
            {
                flick += Time.deltaTime * 9f;
                fireLight.intensity = 2.2f + Mathf.PerlinNoise(flick, 0.4f) * 1.6f;
            }
        }

        void Light(bool on)
        {
            lit = on;
            foreach (var l in lamps) if (l) l.enabled = on;
            if (on && !flames)
            {
                var host = new GameObject("Fireplace fire").transform;
                host.SetParent(transform, true);
                host.position = fireAt;
                flames = VehicleDamage.Fire(host, Vector3.zero);
                if (flames)
                {
                    var main = flames.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    var em = flames.emission;
                    em.rateOverTime = 40f;
                    var sh = flames.shape;
                    sh.radius = 0.25f;
                }
                fireLight = new GameObject("Fireplace light").AddComponent<Light>();
                fireLight.transform.SetParent(host, false);
                fireLight.transform.localPosition = Vector3.up * 0.4f;
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1f, 0.55f, 0.2f);
                fireLight.range = 6f;
                fireLight.shadows = LightShadows.None;
            }
            if (flames) { if (on) flames.Play(); else flames.Stop(); }
            if (fireLight) fireLight.enabled = on;
        }
    }

    /// <summary>A full-screen black for fades (the bed), on top of the HUD.</summary>
    static class Fade
    {
        static Image img;
        public static Image Get()
        {
            if (img) return img;
            var hud = HUD.Instance;
            var canvas = hud ? hud.GetComponentInChildren<Canvas>() : null;
            var go = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas ? canvas.transform : null, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
            return img;
        }
    }

    /// <summary>The almari's panel: the outfits (Pehno) and the rest of the things bought, counted by kind.</summary>
    public class HouseUI : MonoBehaviour
    {
        static HouseUI inst;
        public static bool IsOpen => inst && inst.panel && inst.panel.activeSelf;
        HUD h;
        GameObject panel;
        RectTransform list;
        Transform at;
        readonly List<GameObject> rows = new List<GameObject>();

        public static void Open(Transform from)
        {
            if (!inst)
            {
                var hud = HUD.Instance;
                if (!hud) return;
                inst = hud.gameObject.AddComponent<HouseUI>();
                inst.Build(hud);
            }
            inst.at = from;
            inst.panel.SetActive(true);
            inst.panel.transform.SetAsLastSibling();
            GameInput.UiBlocking = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            inst.Refresh();
        }

        void Build(HUD hud)
        {
            h = hud;
            var root = hud.GetComponentInChildren<Canvas>().transform;
            var p = h.Panel("HousePanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 880), HudStyle.GlassDeep);
            panel = p.gameObject;
            h.TxtB("Title", p.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(760, 54), 42, TextAnchor.MiddleCenter, HudStyle.Marigold).text = Loc.T("Almari");
            list = h.Rect("List", p.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(760, 680));
            h.Btn("Band karo", p.transform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(260, 62), Close, 28).GetComponent<Image>().color = new Color(0.45f, 0.10f, 0.09f, 0.95f);
            panel.SetActive(false);
            Inventory.Changed += Refresh;
        }

        void OnDestroy() { Inventory.Changed -= Refresh; }

        void Close()
        {
            panel.SetActive(false);
            GameInput.UiBlocking = FrontEnd.MenuOpen;
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
            var p = PlayerController.Instance;
            if (at && p && Vector3.Distance(p.transform.position, at.position) > 5f) Close();
        }

        void Refresh()
        {
            if (!IsOpen) return;
            foreach (var g in rows) if (g) Destroy(g);
            rows.Clear();
            float y = 0f;
            void Head(string key)
            {
                var t = h.TxtB("Head", list, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(740, 40), 28, TextAnchor.MiddleLeft, HudStyle.Teal);
                t.text = Loc.T(key);
                rows.Add(t.gameObject);
                y += 46f;
            }
            Head("Kapde");
            var outfits = new List<string> { Inventory.DefaultOutfit };
            foreach (var o in Inventory.Outfits()) if (!outfits.Contains(o)) outfits.Add(o);
            foreach (var o in outfits)
            {
                var row = h.Panel("Row", list, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(740, 60), new Color(1, 1, 1, 0.06f), true);
                rows.Add(row.gameObject);
                h.TxtB("Name", row.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(480, 56), 25, TextAnchor.MiddleLeft, HudStyle.Ink).text = Loc.T(Inventory.NameKey(o));
                bool worn = Inventory.WornOutfit == o;
                string id = o;
                var b = h.Btn(worn ? "Pehna hai" : "Pehno", row.transform, new Vector2(1, 0.5f), new Vector2(-92, 0), new Vector2(160, 48), () => { Inventory.Wear(id); Refresh(); }, 22);
                b.interactable = !worn;
                b.GetComponent<Image>().color = worn ? new Color(0.25f, 0.27f, 0.30f, 0.8f) : new Color(0.12f, 0.45f, 0.30f, 0.95f);
                y += 66f;
            }
            Head("Saaman");
            int n = 0;
            var line = new System.Text.StringBuilder();
            foreach (var id in Inventory.Owned)
            {
                if (id.StartsWith("outfit_")) continue;
                if (line.Length > 0) line.Append("   ◆   ");
                line.Append(Loc.T(Inventory.NameKey(id)));
                n++;
            }
            var txt = h.Txt("Things", list, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(740, 360), 24, TextAnchor.UpperLeft, HudStyle.Ink);
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.text = n > 0 ? line.ToString() : Loc.T("Abhi kuch nahi kharida - Mall Road, Bada Bazar aur Tibetan Market ki dukaanon se lao");
            rows.Add(txt.gameObject);
        }
    }
}
