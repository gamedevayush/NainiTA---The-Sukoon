using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Keeps a population of AI cars/taxis/buses on the roads around the player (GTA-style).</summary>
    public class TrafficManager : MonoBehaviour
    {
        public RoadNetwork network;
        public Material material;
        public int maxCars = 16;
        public float spawnMin = 70f, spawnMax = 240f, despawn = 320f;

        readonly List<TrafficCar> active = new List<TrafficCar>();
        float timer;

        void Start()
        {
            if (Application.isMobilePlatform) maxCars = Mathf.Min(maxCars, 8);
        }

        void Update()
        {
            if (!network || network.roads == null || network.roads.Length == 0) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            var cam = Camera.main;
            var p = PlayerController.Instance;
            if (!cam || !p) return;
            Vector3 c = p.transform.position;

            // by the hour (TownClock): a few cars before eight, the evening rush from five to ten
            int target = Mathf.Max(1, Mathf.RoundToInt(maxCars * TownClock.Traffic(TownClock.Hour)));
            bool thinned = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var car = active[i];
                if (!car) { active.RemoveAt(i); continue; }
                float d2 = (car.transform.position - c).sqrMagnitude;
                bool far = d2 > despawn * despawn;
                if (!far && !thinned && active.Count > target && d2 > 50f * 50f)   // too many for the hour: one goes, out of sight
                {
                    Vector3 vp = cam.WorldToViewportPoint(car.transform.position);
                    far = vp.z < 0 || vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f;
                    thinned = far;
                }
                if (far)
                {
                    Destroy(car.gameObject);
                    active.RemoveAt(i);
                }
            }
            if (active.Count < target) TrySpawn(c, cam);
        }

        void TrySpawn(Vector3 c, Camera cam)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int r = Random.Range(0, network.roads.Length);
                var pts = network.roads[r].pts;
                if (pts.Length < 6) continue;
                int i = Random.Range(2, pts.Length - 2);
                Vector3 pos = pts[i];
                float d = Vector3.Distance(pos, c);
                if (d < spawnMin || d > spawnMax) continue;
                Vector3 vp = cam.WorldToViewportPoint(pos);
                if (d < 140f && vp.z > 0 && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f) continue; // don't pop in view
                bool clear = true;
                foreach (var a in active) if (a && (a.transform.position - pos).sqrMagnitude < 400f) { clear = false; break; }
                if (!clear) continue;

                // Nainital's roads: mostly white hill taxis, then private cars, goods pickups and tourist coaches
                float roll = Random.value;
                CarType type = roll < 0.36f ? CarType.Taxi : roll < 0.52f ? CarType.Hatchback : roll < 0.64f ? CarType.Sedan : roll < 0.76f ? CarType.Suv
                             : roll < 0.89f ? CarType.Pickup : CarType.Bus;
                if (type == CarType.Bus && network.roads[r].width < 6f) type = CarType.Taxi;
                var lib = VehicleLibrary.Instance;
                if (lib && lib.entries.Length > 0 && !lib.Has(type)) type = CarType.Taxi;
                int model = lib ? lib.Pick(type, Random.value) : -1;
                int paint = type == CarType.Taxi || type == CarType.Bus || type == CarType.Pickup ? Px.CarWhite
                          : VehicleFactory.CarPaints[Random.Range(0, VehicleFactory.CarPaints.Length)];
                // a real car: CarController physics (its own mass), driven by TrafficCar
                var go = VehicleFactory.Create(type, paint, material, true, "Traffic " + type, model);
                go.transform.SetParent(transform, true);
                var t = go.AddComponent<TrafficCar>();
                t.type = type;
                t.cruise = type == CarType.Bus || type == CarType.Pickup ? Random.Range(7f, 9f) : Random.Range(9f, 13f);
                t.Begin(network, r, i, Random.value < 0.5f ? 1 : -1);
                var entry = go.AddComponent<TrafficCarEntry>();   // the player can take it over
                entry.type = type;
                entry.paint = paint;
                entry.model = model;
                entry.material = material;
                active.Add(t);
                return;
            }
        }
    }
}
