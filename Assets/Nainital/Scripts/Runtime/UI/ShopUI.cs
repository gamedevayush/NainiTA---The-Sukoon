using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nainital
{
    /// <summary>
    /// The shop counter / the restaurant menu (FREEROAM #5). A glass card in the HUD: the shop's name and place, the money,
    /// a row per item - its name, the price, and Kharido (kept: goes to the house via Inventory) / Abhi khao (eaten now:
    /// PlayerStats.Eat) / Pehno (an outfit already bought) / Pehna hai (worn) / Le liya (kept and owned) - or, at a
    /// restaurant, Order karo (Restaurant.Serve brings it to the table). Every word through Loc.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        static ShopUI inst;
        public static bool IsOpen => inst && inst.panel && inst.panel.activeSelf;

        HUD h;
        GameObject panel;
        RectTransform list;
        Text title, sub;
        Shop shop;
        Restaurant rest;
        RestaurantSeat seat;
        readonly List<GameObject> rows = new List<GameObject>();

        static ShopUI Get()
        {
            if (inst) return inst;
            var hud = HUD.Instance;
            if (!hud) return null;
            inst = hud.gameObject.AddComponent<ShopUI>();
            inst.Build(hud);
            return inst;
        }

        public static void Open(Shop s) { var u = Get(); if (u) u.Show(s, null, null); }
        public static void OpenMenu(Restaurant r, RestaurantSeat seat) { var u = Get(); if (u) u.Show(null, r, seat); }

        void Build(HUD hud)
        {
            h = hud;
            var root = hud.GetComponentInChildren<Canvas>().transform;
            var p = h.Panel("ShopPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 840), HudStyle.GlassDeep);
            panel = p.gameObject;
            title = h.TxtB("Title", p.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(700, 54), 42, TextAnchor.MiddleCenter, HudStyle.Marigold);
            sub = h.Txt("Sub", p.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(700, 36), 26, TextAnchor.MiddleCenter, HudStyle.InkDim);
            h.Img("Line", p.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -124), new Vector2(680, 2), HudStyle.GlassLine).raycastTarget = false;
            list = h.Rect("List", p.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(700, 600));
            var close = h.Btn("Band karo", p.transform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(260, 62), Close, 28);
            close.GetComponent<Image>().color = new Color(0.45f, 0.10f, 0.09f, 0.95f);
            panel.SetActive(false);
            Inventory.Changed += Refresh;
            Loc.Changed += Refresh;
        }

        void OnDestroy() { Inventory.Changed -= Refresh; Loc.Changed -= Refresh; }

        void Show(Shop s, Restaurant r, RestaurantSeat st)
        {
            shop = s; rest = r; seat = st;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            GameInput.UiBlocking = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
            shop = null; rest = null; seat = null;
            GameInput.UiBlocking = FrontEnd.MenuOpen || (HUD.Instance && HUD.Instance.DialogOpen) || Phone.IsOpen;
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
            var p = PlayerController.Instance;
            if (shop && p && Vector3.Distance(p.transform.position, shop.transform.position) > 6f) Close();   // walked off
        }

        void Refresh()
        {
            if (!IsOpen) return;
            foreach (var g in rows) if (g) Destroy(g);
            rows.Clear();
            string[] ids = shop ? shop.items : rest ? rest.menu : new string[0];
            title.text = Loc.T(shop ? shop.shopName : rest ? rest.title : "");
            sub.text = (shop ? Loc.T(shop.place) + "   ◆   " : "") + Loc.T("Jeb mein") + "  " + HudStyle.Rupees(GameState.Money);
            float y = 0f;
            foreach (var id in ids)
            {
                var it = ShopCatalog.Get(id);
                if (it == null) continue;
                var row = h.Panel("Row", list, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(700, 66), new Color(1, 1, 1, 0.06f), true);
                row.raycastTarget = false;
                rows.Add(row.gameObject);
                h.TxtB("Name", row.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(380, 60), 26, TextAnchor.MiddleLeft, HudStyle.Ink).text = Loc.T(it.key);
                h.Txt("Price", row.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-206, 0), new Vector2(120, 60), 26, TextAnchor.MiddleRight, HudStyle.Marigold).text = it.price > 0 ? HudStyle.Rupees(it.price) : "";
                string label;
                bool enabled = true;
                bool outfit = id.StartsWith("outfit_");
                if (rest) label = "Order karo";
                else if (outfit && Inventory.Has(id)) { label = Inventory.WornOutfit == id ? "Pehna hai" : "Pehno"; enabled = Inventory.WornOutfit != id; }
                else if (it.Keep && Inventory.Has(id)) { label = "Le liya"; enabled = false; }
                else label = it.now ? "Abhi khao" : it.service ? "Recharge karo" : "Kharido";
                string capturedId = id;
                var b = h.Btn(label, row.transform, new Vector2(1, 0.5f), new Vector2(-92, 0), new Vector2(170, 52), () => Buy(capturedId), 22);
                b.interactable = enabled;
                b.GetComponent<Image>().color = enabled ? new Color(0.12f, 0.45f, 0.30f, 0.95f) : new Color(0.25f, 0.27f, 0.30f, 0.8f);
                y += 74f;
            }
        }

        void Buy(string id)
        {
            var it = ShopCatalog.Get(id);
            if (it == null) return;
            if (id.StartsWith("outfit_") && Inventory.Has(id))
            {
                Inventory.Wear(id);
                GameEvents.ShowToast(Loc.F("Pehan liya: {0}", Loc.T(it.key)), 2.5f);
                Refresh();
                return;
            }
            if (!GameState.TrySpend(it.price)) { GameEvents.ShowToast("Itne paise nahi hain", 2.5f); Haptics.Light(); return; }
            Haptics.Success();
            if (rest)
            {
                var r = rest; var s = seat;
                Close();
                if (r && s) r.Serve(s, it);
                return;
            }
            if (it.now)
            {
                PlayerStats.Eat(it.food, it.health);
                var p = PlayerController.Instance;
                if (p && p.Health && it.health > 0f) p.Health.Heal(it.health);
                GameEvents.ShowToast(Loc.F("Kha liya: {0}", Loc.T(it.key)), 2.5f);
                GameEvents.RaiseAte();
            }
            else if (it.service) GameEvents.ShowToast("Recharge ho gaya!", 2.5f);
            else
            {
                Inventory.Add(id);
                if (id.StartsWith("outfit_")) { Inventory.Wear(id); GameEvents.ShowToast(Loc.F("Kharid ke pehan liya: {0}", Loc.T(it.key)), 3f); }
                else GameEvents.ShowToast(Loc.F("Kharid liya: {0} - ghar pe rakh diya", Loc.T(it.key)), 3f);
            }
            Refresh();
        }
    }
}
