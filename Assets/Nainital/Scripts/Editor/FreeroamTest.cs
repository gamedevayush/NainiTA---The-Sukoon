using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Steps for the bridge to play-test the free-roam batch (docs/FREEROAM.md #4-#10) in Play, one call at a time:
    /// "call FreeroamTest.Fuel" (Bridge/arg.txt picks the pump / shop / restaurant by name). Each step logs
    /// "[Nainital] fr ..." lines; the slow ones (filling, the waiter, sleep, a workout) are read back by a later Report call.
    /// </summary>
    public static class FreeroamTest
    {
        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)"); }
        static PlayerController P => PlayerController.Instance;
        static void Log(string s) => Debug.Log("[Nainital] fr " + s);
        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        /// <summary>The player at `at` (onto the ground below unless inside), facing `face`, the camera behind.</summary>
        static void Put(Vector3 at, Vector3 face, bool groundFromAbove = true)
        {
            if (CarController.Driving) CarController.Driving.Interact(P);   // out of the car first (a teleport kept the driver in a rolling bus)
            if (groundFromAbove && Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var g, 80f, ~((1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore))
                at = g.point + Vector3.up * 0.15f;
            P.GetComponent<CharacterMotor>().Teleport(at);
            Vector3 d = face - at; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) P.transform.rotation = Quaternion.LookRotation(d.normalized);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SnapBehind();
        }

        static void Money(int atLeast) { if (GameState.Money < atLeast) GameState.AddMoney(atLeast - GameState.Money); }

        // ------------------------------------------------------------------ #4 fuel
        /// <summary>A car between the dispensers of the pump named in Bridge/arg.txt (Tallital / Sukhatal), 10 % in its tank,
        /// the player at the wheel, stopped: VehicleFuel fills it in ~4 s. Read back with FuelReport.</summary>
        public static void Fuel()
        {
            Need();
            string q = Arg();
            FuelStation st = null;
            foreach (var s in FuelStation.All) if (s && (q.Length == 0 || s.place == q)) { st = s; break; }
            if (!st) throw new System.Exception($"no pump '{q}' ({FuelStation.All.Count} in the world)");
            CarController car = CarController.Driving;
            if (!car)
            {
                float bd = float.MaxValue;
                foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
                {
                    if (c.GetComponent<TrafficCar>()) continue;
                    float d = Vector3.Distance(c.transform.position, P.transform.position);
                    if (d < bd) { bd = d; car = c; }
                }
            }
            if (!car) throw new System.Exception("no drivable car");
            Money(3000);
            Vector3 pos = st.transform.TransformPoint(new Vector3(0f, 0.7f, -0.3f));
            var rb = car.GetComponent<Rigidbody>();
            if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = pos; rb.rotation = st.transform.rotation; }
            car.transform.SetPositionAndRotation(pos, st.transform.rotation);
            Physics.SyncTransforms();
            if (CarController.Driving != car) { Put(pos + st.transform.right * 2.2f, pos); car.Interact(P); }
            var f = VehicleFuel.For(car);
            f.litres = f.capacity * 0.1f;
            Log($"fuel: {car.name} on '{st.title}' (inside {st.Contains(car.transform.position)}), tank {f.litres:F1}/{f.capacity:F0} L, money {GameState.Money}");
        }

        public static void FuelReport()
        {
            Need();
            var f = VehicleFuel.Player;
            Log(f ? $"fuel now {f.litres:F1}/{f.capacity:F0} L ({f.Fraction * 100f:F0} %), filling {f.Filling}, money {GameState.Money}" : "fuel: not driving");
        }

        // ------------------------------------------------------------------ #5 shops
        static Shop FindShop()
        {
            string q = Arg();
            foreach (var x in Object.FindObjectsByType<Shop>(FindObjectsSortMode.None)) if (q.Length == 0 || x.shopName.Contains(q)) return x;
            throw new System.Exception("no shop '" + q + "'");
        }

        /// <summary>At the door of the shop named in Bridge/arg.txt, its panel open.</summary>
        public static void ShopOpen()
        {
            Need();
            var s = FindShop();
            Money(3000);
            Put(s.transform.position - s.transform.forward * 1.4f, s.transform.position);
            s.Interact(P);
            Log($"shop '{s.shopName}' ({s.place}) open {ShopUI.IsOpen}, {s.items.Length} items, at {s.transform.position:F1}");
        }

        static readonly MethodInfo BuyM = typeof(ShopUI).GetMethod("Buy", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Outside the shop named in Bridge/arg.txt, panel closed (a busy street for the umbrella test).</summary>
        public static void Near()
        {
            Need();
            var s = FindShop();
            Put(s.transform.position - s.transform.forward * 3f, s.transform.position);
            Log($"near '{s.shopName}' at {s.transform.position:F1}");
        }

        /// <summary>Buys the open shop's first item not owned yet (or the menu's first dish).</summary>
        public static void ShopBuy()
        {
            Need();
            var ui = Object.FindAnyObjectByType<ShopUI>();
            if (!ui || !ShopUI.IsOpen) throw new System.Exception("no shop panel open");
            var shop = (Shop)typeof(ShopUI).GetField("shop", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
            var rest = (Restaurant)typeof(ShopUI).GetField("rest", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
            string[] ids = shop ? shop.items : rest ? rest.menu : new string[0];
            string id = null;
            foreach (var x in ids) if (!Inventory.Has(x)) { id = x; break; }
            if (id == null) throw new System.Exception("everything owned");
            int before = GameState.Money;
            BuyM.Invoke(ui, new object[] { id });
            Log($"bought {id} ({ShopCatalog.Get(id)?.price}): money {before} -> {GameState.Money}, owned {Inventory.Has(id)}, worn {Inventory.WornOutfit}");
        }

        // ------------------------------------------------------------------ #5 restaurants
        /// <summary>On a chair that stays free at the restaurant named in Bridge/arg.txt: the player sits, the menu opens;
        /// hunger set to 20 % first so the meal shows. Then ShopBuy orders, Report ~15 s later.</summary>
        public static void Sit()
        {
            Need();
            string q = Arg();
            Restaurant r = null;
            foreach (var x in Restaurant.All) if (x && (q.Length == 0 || x.title.Contains(q))) { r = x; break; }
            if (!r) throw new System.Exception($"no restaurant '{q}' ({Restaurant.All.Count})");
            RestaurantSeat seat = null;
            foreach (var s in r.seats) if (s && (s.index + r.transform.GetInstanceID()) % 3 == 0) { seat = s; break; }
            if (!seat) seat = r.seats[0];
            Money(3000);
            PlayerStats.DebugHunger(0.2f);
            Put(seat.transform.position - seat.transform.forward * 0.7f, seat.transform.position, false);
            seat.Interact(P);
            int diners = 0;
            foreach (var s in r.seats) if (s && s.diner) diners++;
            Log($"sat at '{r.title}' seat {seat.index}, menu open {ShopUI.IsOpen}, diners {diners}, mode {P.CurrentMode}");
        }

        // ------------------------------------------------------------------ #6 house
        static Vector3 Inside(Component c, float off)
        {
            var house = Object.FindAnyObjectByType<House>();
            Vector3 d = (house ? house.transform.position : c.transform.position) - c.transform.position; d.y = 0f;
            return c.transform.position + (d.sqrMagnitude > 0.01f ? d.normalized : -c.transform.forward) * off;
        }

        /// <summary>Beside the bed, inside the house (lamps and the fireplace come on near it).</summary>
        public static void Bed()
        {
            Need();
            var bed = Object.FindAnyObjectByType<HouseBed>();
            if (!bed) throw new System.Exception("no house bed");
            Vector3 at = Inside(bed, 1.3f); at.y = bed.transform.position.y - 0.2f;
            Put(at, bed.transform.position, false);
            var house = Object.FindAnyObjectByType<House>();
            Log($"at the bed {bed.transform.position:F1}; house lamps {(house ? house.lamps.Length : 0)}");
        }

        public static void Sleep()
        {
            Need();
            var bed = Object.FindAnyObjectByType<HouseBed>();
            var dn = DayNightCycle.Instance;
            Log($"sleeping at {(dn ? dn.timeOfDay : -1f):F2}");
            bed.Interact(P);
        }

        public static void Almari()
        {
            Need();
            var al = Object.FindAnyObjectByType<HouseStorage>();
            if (!al) throw new System.Exception("no almari");
            Vector3 at = Inside(al, 1.2f); at.y = al.transform.position.y - 0.85f;
            Put(at, al.transform.position, false);
            al.Interact(P);
            Log($"almari open {HouseUI.IsOpen}, owned {Inventory.Owned.Count}");
        }

        // ------------------------------------------------------------------ #7 gym + stats
        public static void Gym()
        {
            Need();
            GymStation best = null;
            float bd = float.MaxValue;
            string kind = Arg();   // "pull", "pedal", "twist" or empty (the nearest)
            foreach (var g in Object.FindObjectsByType<GymStation>(FindObjectsSortMode.None))
            {
                if (kind.Length > 0 && g.kind != kind) continue;
                float d = Vector3.Distance(g.transform.position, P.transform.position);
                if (d < bd) { bd = d; best = g; }
            }
            if (!best) throw new System.Exception("no gym station");
            Put(best.transform.position - best.transform.forward * 1.2f, best.transform.position);
            Log($"gym '{best.kind}' at {best.transform.position:F1}: muscle {PlayerStats.Get(PlayerStats.Stat.Muscle):F2} stamina {PlayerStats.Get(PlayerStats.Stat.Stamina):F2}");
            best.Interact(P);
        }

        // ------------------------------------------------------------------ #9 radio
        /// <summary>Radio to the station index in Bridge/arg.txt (0 = the player's own songs).</summary>
        public static void RadioPick()
        {
            Need();
            var r = Radio.Instance;
            if (!r) throw new System.Exception("no radio");
            int.TryParse(Arg(), out int i);
            r.Select(i);
            Log($"radio -> {r.CurrentKey} (stations {string.Join(", ", r.StationKeys)}); folders {string.Join(" | ", Radio.UserFolders())}");
        }

        // ------------------------------------------------------------------ #10 umbrellas
        public static void Rain() { Need(); WeatherSystem.Instance.Set(WeatherSystem.Kind.Rain); Log("rain on"); }
        public static void Clear() { Need(); WeatherSystem.Instance.Set(WeatherSystem.Kind.Clear); Log("sky clear"); }

        // ------------------------------------------------------------------ the phone's photos -> the phone's gallery
        /// <summary>The NativeGallery package as PhoneCamera.SaveToDevice finds it (by reflection; used on a phone only):
        /// the type, its assembly and the byte[] SaveImageToGallery overload. Edit or Play mode.</summary>
        public static void GalleryCheck()
        {
            System.Type ng = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                ng = asm.GetType("NativeGallery");
                if (ng != null) break;
            }
            if (ng == null) { Log("gallery: NativeGallery NOT found"); return; }
            string found = "none";
            foreach (var mi in ng.GetMethods())
            {
                if (mi.Name != "SaveImageToGallery") continue;
                var ps = mi.GetParameters();
                if (ps.Length >= 3 && ps[0].ParameterType == typeof(byte[])) { found = mi.ToString(); break; }
            }
            Log($"gallery: NativeGallery in {ng.Assembly.GetName().Name}; byte[] save: {found}");
        }

        // ------------------------------------------------------------------ everything at once
        public static void Report()
        {
            Need();
            var sb = new System.Text.StringBuilder("state:\n");
            var f = VehicleFuel.Player;
            sb.Append($"  money {GameState.Money}; driving {(CarController.Driving ? CarController.Driving.name : "-")}; fuel {(f ? $"{f.litres:F1}/{f.capacity:F0} filling {f.Filling}" : "-")}\n");
            sb.Append($"  player at {P.transform.position:F1} mode {P.CurrentMode}; shop panel {ShopUI.IsOpen}; almari {HouseUI.IsOpen}\n");
            var dn = DayNightCycle.Instance;
            sb.Append($"  time {(dn ? dn.timeOfDay : -1f):F2}; rain {(WeatherSystem.Instance ? WeatherSystem.Instance.Rain : -1f):F2}\n");
            int umb = 0;
            foreach (var t in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if (t.name == "Umbrella" && t.gameObject.activeInHierarchy) umb++;
            int walkers = 0;
            foreach (var w in Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None)) if ((w.transform.position - P.transform.position).sqrMagnitude < 90f * 90f) walkers++;
            sb.Append($"  umbrellas {umb} (walkers within 90 m {walkers})\n");
            int orders = 0;
            foreach (var t in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if (t.name == "Order") orders++;
            sb.Append($"  plates on tables {orders}; waiters {GameObject.FindObjectsByType<Medic>(FindObjectsSortMode.None).Length}\n");
            var r = Radio.Instance;
            if (r)
            {
                var src = r.GetComponent<AudioSource>();
                sb.Append($"  radio {r.CurrentKey}: playing {src.isPlaying} clip {(src.clip ? src.clip.name : "-")} t {src.time:F1} vol {src.volume:F2}, own songs {r.UserCount}\n");
            }
            sb.Append("  stats: ").Append(PlayerStats.Report().Replace("\n", " | "));
            Log(sb.ToString());
        }
    }
}
