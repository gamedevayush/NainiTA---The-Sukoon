using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Head lamps and tail lamps (user, 2026-11-11: "no lights in car"): glowing quads on the model, lit after dusk on
    /// traffic and on the car the player drives; a parked car stands dark. The driven car also has real spot lights
    /// (CarController). Checked twice a second.
    /// </summary>
    public class VehicleLamps : MonoBehaviour
    {
        public Renderer lamps;
        /// <summary>More of them (the unbranded set: one per part the lamps hang on - a bike's headlight on its handlebar).</summary>
        public Renderer[] more = new Renderer[0];
        /// <summary>Driven by the game itself (police, ambulance): lamps after dusk like traffic.</summary>
        public bool always;
        TrafficCar traffic;
        CarController car;
        float timer;

        TrafficBike trafficBike;
        BikeRide bike;

        void Start()
        {
            traffic = GetComponent<TrafficCar>();
            trafficBike = GetComponent<TrafficBike>();
            bike = GetComponent<BikeRide>();
            car = GetComponent<CarController>();
            timer = Random.Range(0f, 0.5f);
            Apply();
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            if (!traffic) traffic = GetComponent<TrafficCar>();
            if (!trafficBike) trafficBike = GetComponent<TrafficBike>();
            Apply();
        }

        void Apply()
        {
            if (!lamps && more.Length == 0) return;
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.NightFactor : 0f;
            float rain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            // a two-wheeler is always ridden (traffic, police): lit until it is knocked over
            bool riding = bike && !bike.Crashed && !(trafficBike && trafficBike.Down);
            bool moving = traffic || always || riding || (car && CarController.Driving == car);
            if (bike && (bike.Crashed || (trafficBike && trafficBike.Down))) moving = false;
            bool on = moving && (night > 0.3f || rain > 0.5f);
            if (lamps && lamps.enabled != on) lamps.enabled = on;
            foreach (var r in more) if (r && r.enabled != on) r.enabled = on;
        }
    }
}
