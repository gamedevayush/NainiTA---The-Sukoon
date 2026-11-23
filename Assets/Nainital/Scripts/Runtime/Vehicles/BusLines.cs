using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The town's timetabled government buses (owner, 24 Nov 2026: "bus stand se har 5 minute me ek bus chalegi.. one to
    /// khurpatal .. one to himalaya darshan ...one to haldwani side...and one to bhimtal road jo bhi last valid point ho...
    /// or har 10 minutes me bus stand se mall road hote huye, first stop DSA / naina devi gate..next is jama masjid, uska
    /// next is SBM toilet tiraha, then eco cave park then bara patthar, then lovers point, then khurpatal view point, then
    /// sariyatal lake, then woodland waterpark, last khurpatal..and reverse..vaapsi me jama masjid skip kyunki vo one way
    /// tha.. aate pe via bara bajaar road"). Places in local map metres (x east, z north; world.json pois / the landmark
    /// builders), snapped to the nearest bus-wide road when the service starts. The road planner keeps every one-way:
    /// up the Lower Mall, back down the Upper Mall, out past Jama Masjid, back by Bada Bazar.
    /// The Kaladhungi road's stops are held to that road (24 Nov: snapped to the nearest bus road they sat on side roads
    /// and the safari looped in and out of them, 28.5 km). BusService runs them.
    /// </summary>
    public static class BusLines
    {
        public class Stop
        {
            public string name;      // as announced and on the display
            public Vector2 at;
            public bool edge;        // the map's edge: the bus drives on out of the world (Haldwani, Bhimtal)
            public string road;      // snap only to this road (null: the nearest bus-wide road)
            public Stop(string n, float x, float z, bool e = false, string onRoad = null) { name = n; at = new Vector2(x, z); edge = e; road = onRoad; }
        }

        public class Line
        {
            public string id;            // Resources/BusDisplay/<id>.png
            public string title;         // the display's headline
            public Stop[] stops;         // in the order driven
            public float every;          // seconds between departures from its first stop (0: run by the intercity rota)
            public float offset;         // seconds after the service starts
            public bool local;           // calls at every stop (the safari); an intercity runs straight to its end
        }

        public static readonly Stop BusStand = new Stop("Tallital Bus Stand", 811f, -1170f);
        static readonly Stop NainaDeviGate = new Stop("DSA / Naina Devi Gate", 15f, 138f);   // on Mall Road at the DSA ground (the gate itself, -77,-143, is 58 m off any bus road)
        static readonly Stop JamaMasjid = new Stop("Jama Masjid", -152.5f, 51.5f);
        static readonly Stop SbmTiraha = new Stop("SBM Toilet Tiraha", -150f, 20f);
        static readonly Stop EcoCave = new Stop("Eco Cave Park", -937f, 182f, false, "Kaldhungi-Nainital Road");
        static readonly Stop Barapathar = new Stop("Bara Patthar", -1252f, -126f, false, "Kaldhungi-Nainital Road");
        static readonly Stop LoversPoint = new Stop("Lovers Point", -1424f, -378f, false, "Kaldhungi-Nainital Road");
        static readonly Stop KhurpatalView = new Stop("Khurpatal View Point", -1853f, -1150f, false, "Kaldhungi-Nainital Road");   // the map's "Khurpatal Lake View Point", between Lovers Point and Sariyatal (the other "Khurpatal View Point", -2491,-1760, lies past Sariyatal: no way back up)
        static readonly Stop Sariyatal = new Stop("Sariyatal Lake", -2336f, -1225f, false, "Kaldhungi-Nainital Road");
        static readonly Stop Woodland = new Stop("Woodland Waterfall", -2358f, -1393f, false, "Kaldhungi-Nainital Road");   // its gate is on the Kaladhungi road (the Bypass passes close too)
        static readonly Stop Khurpatal = new Stop("Khurpatal", -2618f, -2168f);
        static readonly Stop HimalayaDarshan = new Stop("Himalaya Darshan", -342f, 1376f);
        static readonly Stop Haldwani = new Stop("Haldwani", 141f, -3263f, true);      // Nainital - Haldwani Highway at the map's edge
        static readonly Stop Bhimtal = new Stop("Bhimtal", 2366f, -312f, true);        // Nainital - Bhowali Road at the map's edge (Bhimtal is by Bhowali)

        /// <summary>The safari both ways (every 10 min each way) and the four intercity runs both ways (the rota: one
        /// leaves the bus stand every 5 min, one comes in every 5 min).</summary>
        public static readonly Line[] All =
        {
            new Line { id = "safari_out", title = "NAINITAL SAFARI", local = true, every = 600f, offset = 20f,
                       stops = new[] { BusStand, NainaDeviGate, JamaMasjid, SbmTiraha, EcoCave, Barapathar, LoversPoint, KhurpatalView, Sariyatal, Woodland, Khurpatal } },
            // back: Jama Masjid skipped - its road is one-way out; the planner brings it by Bada Bazar
            new Line { id = "safari_back", title = "NAINITAL SAFARI", local = true, every = 600f, offset = 320f,
                       stops = new[] { Khurpatal, Woodland, Sariyatal, KhurpatalView, LoversPoint, Barapathar, EcoCave, SbmTiraha, NainaDeviGate, BusStand } },
            new Line { id = "to_khurpatal", title = "UTTARAKHAND ROADWAYS", stops = new[] { BusStand, Khurpatal } },
            new Line { id = "to_himalaya", title = "UTTARAKHAND ROADWAYS", stops = new[] { BusStand, HimalayaDarshan } },
            new Line { id = "to_haldwani", title = "UTTARAKHAND ROADWAYS", stops = new[] { BusStand, Haldwani } },
            new Line { id = "to_bhimtal", title = "UTTARAKHAND ROADWAYS", stops = new[] { BusStand, Bhimtal } },
            new Line { id = "from_khurpatal", title = "UTTARAKHAND ROADWAYS", stops = new[] { Khurpatal, BusStand } },
            new Line { id = "from_himalaya", title = "UTTARAKHAND ROADWAYS", stops = new[] { HimalayaDarshan, BusStand } },
            new Line { id = "from_haldwani", title = "UTTARAKHAND ROADWAYS", stops = new[] { Haldwani, BusStand } },
            new Line { id = "from_bhimtal", title = "UTTARAKHAND ROADWAYS", stops = new[] { Bhimtal, BusStand } },
        };

        /// <summary>The intercity rota: out every 5 min in this order, in every 5 min (2.5 min apart from the outs).</summary>
        public static readonly string[] OutRota = { "to_khurpatal", "to_himalaya", "to_haldwani", "to_bhimtal" };
        public static readonly string[] InRota = { "from_haldwani", "from_bhimtal", "from_khurpatal", "from_himalaya" };
        public const float IntercityEvery = 300f;

        /// <summary>The display's running text: where from and where to (and the safari's stops).</summary>
        public static string DisplayText(Line l)
        {
            var a = l.stops[0].name.ToUpperInvariant();
            var b = l.stops[l.stops.Length - 1].name.ToUpperInvariant();
            if (!l.local) return $"{l.title}   {a}  >>  {b}   ";
            var sb = new System.Text.StringBuilder(l.title + "   ");
            for (int i = 0; i < l.stops.Length; i++) sb.Append(i == 0 ? "" : "  >  ").Append(l.stops[i].name.ToUpperInvariant());
            return sb.Append("   ").ToString();
        }

        public static Line Get(string id)
        {
            foreach (var l in All) if (l.id == id) return l;
            return null;
        }
    }
}
