using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A small eatery's sit-out (owner, 21 Nov 2026, FREEROAM #5: "5-6 restaurants, people at the tables eating (tea, pizza
    /// ...). The player sits -> a menu opens -> 10-15 s later someone brings the order (hunger / health)"). Built by
    /// LandmarkBuilder.Shops: tables, chairs, an umbrella each, a counter with the name board; this component holds the
    /// seats (one RestaurantSeat each) and the kitchen door (where the waiter comes from).
    /// While the player is near, townsfolk sit at most tables with a plate and a cup (spawned, seated, gone again when
    /// the player leaves). The player sits on a free chair (E) -> ShopUI shows the menu -> pays -> 10-15 s later a waiter
    /// walks out from the counter with the plate, sets it down, walks back -> PlayerStats.Eat.
    /// </summary>
    public class Restaurant : MonoBehaviour
    {
        public static readonly List<Restaurant> All = new List<Restaurant>();

        /// <summary>The (made-up) name, a Loc key.</summary>
        public string title = "Dhaba";
        /// <summary>The meals it serves (ShopCatalog meal_ ids).</summary>
        public string[] menu = new string[0];
        public Transform kitchen;
        public RestaurantSeat[] seats = new RestaurantSeat[0];

        readonly List<GameObject> diners = new List<GameObject>();
        bool populated;
        float checkT;

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); Clear(); }

        void Update()
        {
            checkT -= Time.deltaTime;
            if (checkT > 0f) return;
            checkT = 1f;
            var p = PlayerController.Instance;
            float d = p ? Vector3.Distance(p.transform.position, transform.position) : 999f;
            if (!populated && d < 70f) Populate();
            else if (populated && d > 110f) Clear();
        }

        /// <summary>Townsfolk at two thirds of the seats, never the one the player is on.</summary>
        void Populate()
        {
            populated = true;
            var pm = PoliceManager.Instance;
            if (!pm) return;
            int n = 0;
            foreach (var s in seats)
            {
                if (!s || s.taken) continue;
                if ((s.index + transform.GetInstanceID()) % 3 == 0) continue;   // a third stay free (for the player)
                var go = pm.MakePerson("Diner", s.transform.position + Vector3.up * 0.1f);
                go.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                var cc = go.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                var rig = go.GetComponentInChildren<HumanoidRig>(true);
                if (rig) { rig.gameObject.SetActive(true); rig.seated = true; rig.speed = 0f; }
                diners.Add(go);
                // their order in front of them: chai, pizza, a plate of the menu's first dish
                if (s.table && menu.Length > 0)
                {
                    Vector3 toSeat = s.transform.position - s.table.position; toSeat.y = 0f;
                    var meal = ShopCatalog.Get(menu[(s.index + n) % menu.Length]);
                    var plate = Plate(s.table.position + toSeat.normalized * 0.22f, meal);
                    if (plate) diners.Add(plate);
                }
                s.diner = go;
                n++;
            }
        }

        void Clear()
        {
            populated = false;
            foreach (var g in diners) if (g) Destroy(g);
            diners.Clear();
            foreach (var s in seats) if (s) s.diner = null;
        }

        /// <summary>The order is paid: the waiter brings it in 10-15 s.</summary>
        public void Serve(RestaurantSeat seat, ShopItem meal)
        {
            StartCoroutine(Bring(seat, meal));
        }

        IEnumerator Bring(RestaurantSeat seat, ShopItem meal)
        {
            GameEvents.ShowToast("Order de diya - thoda intezaar karo", 3f);
            float wait = Random.Range(7f, 11f);
            yield return new WaitForSeconds(wait);
            var pm = PoliceManager.Instance;
            Vector3 from = kitchen ? kitchen.position : transform.position;
            Vector3 to = seat.table ? seat.table.position : seat.transform.position;
            Vector3 stand = to + (seat.transform.position - to).normalized * -0.2f + Vector3.Cross(Vector3.up, (seat.transform.position - to).normalized) * 0.75f;
            var waiter = pm ? pm.MakeWaiter(from) : null;
            if (waiter)
            {
                Vector3 pos = from;
                for (float t = 0f; t < 12f; t += Time.deltaTime)   // walk over
                {
                    Vector3 d = stand - pos; d.y = 0f;
                    if (d.magnitude < 0.25f) break;
                    pos += d.normalized * Mathf.Min(1.5f * Time.deltaTime, d.magnitude);
                    pos.y = Ground(pos);
                    waiter.Hold(pos, d, 1.5f);
                    yield return null;
                }
                waiter.Hold(pos, to - pos, 0f);
                yield return new WaitForSeconds(1.2f);
            }
            else yield return new WaitForSeconds(3f);
            var plate = Plate(to, meal);
            Eat(seat, meal);
            if (waiter)
            {
                Vector3 pos = waiter.transform.position;
                for (float t = 0f; t < 12f; t += Time.deltaTime)   // and back
                {
                    Vector3 d = from - pos; d.y = 0f;
                    if (d.magnitude < 0.3f) break;
                    pos += d.normalized * Mathf.Min(1.5f * Time.deltaTime, d.magnitude);
                    pos.y = Ground(pos);
                    waiter.Hold(pos, d, 1.5f);
                    yield return null;
                }
                Destroy(waiter.gameObject);
            }
            yield return new WaitForSeconds(20f);
            if (plate) Destroy(plate);
        }

        void Eat(RestaurantSeat seat, ShopItem meal)
        {
            PlayerStats.Eat(meal.food, meal.health);
            var p = PlayerController.Instance;
            if (p && p.Health && meal.health > 0f) p.Health.Heal(meal.health);
            GameEvents.ShowToast(Loc.F("Waiter: \"Lijiye, aapka {0}\"", Loc.T(meal.key)), 3f);
            GameEvents.RaiseAte();
            Haptics.Success();
        }

        static float Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 1.5f, p.z), Vector3.down, out var hit, 4f,
                    ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return top;
        }

        /// <summary>A plate and a cup on the table (a few boxes and discs, atlas colours).</summary>
        static GameObject Plate(Vector3 table, ShopItem meal)
        {
            var mb = new MeshBuilder();
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite), food = Atlas.SolidUV(meal != null && meal.id.Contains("pizza") ? Pal.Saffron : meal != null && meal.id.Contains("chai") ? Pal.WoodLight : Pal.JacketYellow);
            mb.Cylinder(new Vector3(0f, 0f, 0f), 0.13f, 0.12f, 0.015f, 14, white, true);
            mb.Cylinder(new Vector3(0f, 0.015f, 0f), 0.09f, 0.07f, 0.03f, 12, food, true);
            mb.Cylinder(new Vector3(0.2f, 0f, 0.05f), 0.04f, 0.035f, 0.09f, 10, white, true);
            var go = new GameObject("Order");
            go.transform.position = table + Vector3.up * 0.01f;
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Order");
            go.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.AtlasMaterial;
            return go;
        }
    }
}
