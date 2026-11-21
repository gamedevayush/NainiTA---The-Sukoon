using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Head lamps and tail lamps (user, 2026-11-11: "no lights in car"): glowing quads on the model, lit after dusk on
    /// traffic and on the car the player drives; a parked car stands dark. The driven car also has real spot lights
    /// (CarController). Checked twice a second.
    /// Indicators and the dipper (owner, 21 Nov 2026: "gaadi agar aage kahin turn hogi to indicator degi pehle..or stucked
    /// hain to random dipper bhi de skti hai"): amber quads at the outer edge of the head and tail lamps blink on the side
    /// set in `signal` (TrafficCar / TrafficBike set it before a turn); Flash() flicks the head lamps bright twice.
    /// </summary>
    public class VehicleLamps : MonoBehaviour
    {
        public Renderer lamps;
        /// <summary>More of them (the unbranded set: one per part the lamps hang on - a bike's headlight on its handlebar).</summary>
        public Renderer[] more = new Renderer[0];
        /// <summary>The indicators, left and right (VehicleParts.BuildIndicators; empty on the old models).</summary>
        public Renderer[] indL = new Renderer[0], indR = new Renderer[0];
        /// <summary>Driven by the game itself (police, ambulance): lamps after dusk like traffic.</summary>
        public bool always;
        /// <summary>The indicator to blink: -1 left, +1 right, 0 none (set every frame by the traffic driving it).</summary>
        [System.NonSerialized] public int signal;
        TrafficCar traffic;
        CarController car;
        float timer;
        bool headsOn;
        float flashT = -1f;
        bool blinkWas;
        int signalWas;
        static MaterialPropertyBlock bright;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        TrafficBike trafficBike;
        BikeRide bike;

        /// <summary>Two quick flicks of the head lamps (the dipper), day or night.</summary>
        public void Flash() { if (flashT < 0f) flashT = 0f; }
        public bool Flashing => flashT >= 0f;

        void Start()
        {
            traffic = GetComponent<TrafficCar>();
            trafficBike = GetComponent<TrafficBike>();
            bike = GetComponent<BikeRide>();
            car = GetComponent<CarController>();
            timer = Random.Range(0f, 0.5f);
            Apply();
            Indicate(false, 0);
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0.5f;
                if (!traffic) traffic = GetComponent<TrafficCar>();
                if (!trafficBike) trafficBike = GetComponent<TrafficBike>();
                Apply();
            }
            // indicators: 1.5 blinks a second, lit for the first 55 % of each
            bool blink = signal != 0 && Mathf.Repeat(Time.time * 1.5f + GetInstanceID() * 0.137f, 1f) < 0.55f;
            if (blink != blinkWas || signal != signalWas) Indicate(blink, signal);
            // the dipper: bright, dark, bright (each 0.14 s), then back as they were
            if (flashT >= 0f)
            {
                flashT += Time.deltaTime;
                bool on = flashT < 0.14f || (flashT > 0.28f && flashT < 0.42f);
                if (flashT > 0.5f) { flashT = -1f; on = false; }
                Bright(on);
            }
        }

        void Indicate(bool blink, int side)
        {
            blinkWas = blink; signalWas = side;
            foreach (var r in indL) if (r) r.enabled = blink && side < 0;
            foreach (var r in indR) if (r) r.enabled = blink && side > 0;
        }

        void Bright(bool on)
        {
            if (bright == null) { bright = new MaterialPropertyBlock(); bright.SetColor(BaseColor, new Color(14f, 13f, 11f)); }
            void One(Renderer r)
            {
                if (!r) return;
                r.SetPropertyBlock(on ? bright : null);
                r.enabled = on || headsOn;
            }
            One(lamps);
            foreach (var r in more) One(r);
        }

        void Apply()
        {
            if (!lamps && more.Length == 0) return;
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.NightFactor : 0f;
            float rain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            // a two-wheeler is always ridden (traffic, police): lit until it is knocked over
            bool riding = bike && !bike.Crashed && !(trafficBike && trafficBike.Down);
            // a bike the player rode and left on its stand stands dark like a parked car
            if (riding && !trafficBike && !(PlayerBike.Riding && PlayerBike.Riding.gameObject == gameObject) && GetComponent<PlayerBike>()) riding = false;
            bool moving = traffic || always || riding || (car && CarController.Driving == car);
            if (bike && (bike.Crashed || (trafficBike && trafficBike.Down))) moving = false;
            headsOn = moving && (night > 0.3f || rain > 0.5f);
            if (flashT >= 0f) return;   // mid-flash: Update puts them back after it
            if (lamps && lamps.enabled != headsOn) lamps.enabled = headsOn;
            foreach (var r in more) if (r && r.enabled != headsOn) r.enabled = headsOn;
        }
    }
}
