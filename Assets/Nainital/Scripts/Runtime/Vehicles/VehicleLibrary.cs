using UnityEngine;

namespace Nainital
{
    /// <summary>The real vehicle models (user's library, 2026-11-11: Ertiga, Innova, Bolero, WagonR, Amaze, Scorpio, pickups,
    /// tourist coach and bus; each under 20k triangles with LODs). Built by VehicleKit into Resources/VehicleLibrary.
    /// VehicleFactory uses one of these whenever the library has a model of the wanted kind.</summary>
    public class VehicleLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string name;
            public GameObject prefab;
            public Vector3 size;        // width (x), height (y), length (z), metres; pivot at the bottom centre
            public CarType type;
            public bool taxi;           // white hill taxi (Ertiga, Innova, Bolero): also picked for CarType.Taxi
            public float weight = 1f;   // how often it turns up among its kind
            public bool tint = true;    // body is plain white paint: the paint colour can be multiplied on
            public float wheelRadius;   // the model's tyre radius (hub height), 0 if its tyres are part of the body
        }

        public Entry[] entries = new Entry[0];
        /// <summary>Two-wheelers (2026-11-18: Hero Splendor, the scooty): the town's traffic riders and the police bike
        /// (BikeRide). Built by VehicleKit from Vehicles/Bikes.</summary>
        public Entry[] bikes = new Entry[0];

        public Entry Bike(string name)
        {
            if (bikes == null) return null;
            foreach (var b in bikes) if (b != null && b.prefab && b.name == name) return b;
            return null;
        }

        /// <summary>A traffic two-wheeler, weighted (r in 0..1); null if there is none.</summary>
        public Entry PickBike(float r)
        {
            if (bikes == null) return null;
            float total = 0f;
            foreach (var b in bikes) if (b != null && b.prefab && b.weight > 0f) total += b.weight;
            if (total <= 0f) return null;
            float x = r * total;
            Entry last = null;
            foreach (var b in bikes)
            {
                if (b == null || !b.prefab || b.weight <= 0f) continue;
                last = b;
                x -= b.weight;
                if (x <= 0f) return b;
            }
            return last;
        }

        static VehicleLibrary cached;
        static bool loaded;

        public static VehicleLibrary Instance
        {
            get
            {
                if (!loaded) { cached = Resources.Load<VehicleLibrary>("VehicleLibrary"); loaded = true; }
                return cached;
            }
        }

        public bool Has(CarType t) => Pick(t, 0.5f) >= 0;

        /// <summary>A model of this kind (weighted by r in 0..1), or -1 if there is none.</summary>
        public int Pick(CarType t, float r)
        {
            float total = 0f;
            for (int i = 0; i < entries.Length; i++) if (Fits(entries[i], t)) total += entries[i].weight;
            if (total <= 0f) return -1;
            float x = r * total;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!Fits(entries[i], t)) continue;
                x -= entries[i].weight;
                if (x <= 0f) return i;
            }
            for (int i = entries.Length - 1; i >= 0; i--) if (Fits(entries[i], t)) return i;
            return -1;
        }

        static bool Fits(Entry e, CarType t) => e != null && e.prefab && e.weight > 0f && (t == CarType.Taxi ? e.taxi : e.type == t);   // weight 0: never in traffic (the ambulance)
    }
}
