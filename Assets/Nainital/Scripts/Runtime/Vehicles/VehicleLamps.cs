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
        TrafficCar traffic;
        CarController car;
        float timer;

        void Start()
        {
            traffic = GetComponent<TrafficCar>();
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
            Apply();
        }

        void Apply()
        {
            if (!lamps) return;
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.NightFactor : 0f;
            float rain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            bool moving = traffic || (car && CarController.Driving == car);
            bool on = moving && (night > 0.3f || rain > 0.5f);
            if (lamps.enabled != on) lamps.enabled = on;
        }
    }
}
