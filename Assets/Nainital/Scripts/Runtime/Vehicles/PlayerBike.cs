using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The player on a bike or a scooty (owner, 21 Nov 2026: "bike scooty pe bethne ka abhi nhi bna kya?"). On every
    /// two-wheeler of the traffic: E asks it to stop - the rider and the pillion get off (the owner's rule: whoever gets
    /// in, everyone else gets out) - then the player swings a leg over (the riding pack's Mount clip) onto the setup's
    /// rider seat: hands on the grips, feet on the pegs (VehicleParts.Sit). W / S: on / brake, A / D: steer, Space:
    /// brake, horn; it stops short of walls and vehicles. E (slowed down) puts it on its side stand and the player steps
    /// off at the seat's way in; it stays there to be ridden again.
    /// </summary>
    [RequireComponent(typeof(BikeRide))]
    public class PlayerBike : Interactable
    {
        /// <summary>The two-wheeler the player rides now (null on foot).</summary>
        public static PlayerBike Riding { get; private set; }

        BikeRide bike;
        PlayerController rider;
        TrafficBike traffic;
        bool asked;
        static int blockMask = -1;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public BikeRide Bike => bike ? bike : bike = GetComponent<BikeRide>();
        public bool HasRider => rider;
        bool Scooty => name.ToLowerInvariant().Contains("scooty");
        float MaxSpeed => (Scooty ? 13f : 17f) * PlayerStats.BikeSpeed;   // bike skill (PlayerStats)

        void Awake()
        {
            bike = GetComponent<BikeRide>();
            radius = 2.4f;
            if (blockMask < 0) blockMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9));
            bike.OnCrash += Thrown;
        }

        /// <summary>Knocked over with the player on it (a car hit it, it met a wall hard: BikeRide.Hit): off he goes with the
        /// speed he had, hurt by it like the traffic's riders.</summary>
        void Thrown(Vector3 v, GameObject by)
        {
            if (!rider) return;
            var p = rider;
            rider = null;
            Riding = null;
            p.ExitBike(Ground(transform.position - Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized * 0.9f) + Vector3.up * 0.3f);
            var m = p.GetComponent<CharacterMotor>();
            if (m) m.impulse += v * 0.7f + Vector3.up * 3f;
            var h = p.GetComponent<Health>();
            if (h && v.magnitude > 4.5f) h.Damage(v.magnitude * 3f, transform.position, by);
            Debug.Log($"[Nainital] player thrown off {name} at {v.magnitude:F1} m/s by {(by ? by.name : "the hit")}");
        }

        bool Fallen => Bike && (Bike.Crashed || (GetComponent<TrafficBike>() is TrafficBike t && t.Down));

        public override string Prompt => rider ? "Utro" : asked ? (Scooty ? "Scooty" : "Bike") + " ruk rahi hai..." :
            Fallen ? (Scooty ? "Scooty" : "Bike") + " uthao aur chalao" :
            (Scooty ? "Scooty" : "Bike") + ((traffic = GetComponent<TrafficBike>()) ? " rok ke baitho" : " pe baitho");
        public override Vector3 Point => transform.position + Vector3.up * 0.6f;

        // a fallen one too, once it has stopped tumbling: its rider was thrown off, the player picks it up
        public override bool CanInteract(PlayerController p) =>
            !rider && !asked && Riding == null && CarController.Driving == null && Bike && Bike.Settled;

        public override void Interact(PlayerController p)
        {
            if (p.Boarding || p.Ragdolled) return;   // still climbing on / off, or on the ground
            if (rider == p) { Exit(); return; }
            if (!CanInteract(p)) return;
            traffic = GetComponent<TrafficBike>();
            if (Fallen)
            {
                // nobody is on it (thrown): up on its wheels, the traffic lets it go, the player rides it
                Bike.Recover();
                if (traffic) Destroy(traffic);
                Mount(p);
                return;
            }
            if (traffic)
            {
                asked = true;   // it pulls up, the rider and the pillion get off, then Mount (TrafficBike.TakeOver)
                traffic.RequestTakeover(this, p);
                GameEvents.ShowToast((Scooty ? "Scooty" : "Bike") + " ruk rahi hai... sawari utregi, phir aap baithoge", 2f);
                return;
            }
            Mount(p);
        }

        /// <summary>The traffic gave up the bike, or the player walked off before it stopped.</summary>
        public void Released() => asked = false;

        /// <summary>The player onto the rider's seat.</summary>
        public void Mount(PlayerController p)
        {
            asked = false;
            if (!p || Riding) return;
            rider = p;
            Riding = this;
            var vl = GetComponent<VehicleLamps>();
            if (vl) vl.signal = 0;   // the traffic's last indicator (its pull-up to the left) is not the player's
            p.BoardBike(this);   // to its side, the leg over (Mount), onto the seat, then EnterBike
            if (AudioManager.Instance) AudioManager.Instance.CarEnter(transform.position);
            GameEvents.ShowToast("W/S: race-brake, A/D: mod, Space: brake, E: utro", 3.5f);
        }

        void Exit()
        {
            if (!rider) return;
            var p = rider;
            var vp = Bike.Parts;
            Vector3 at;
            if (vp && vp.HasSetup) at = vp.WayOut(0, out _);
            else at = transform.position - transform.right * 0.9f;
            var brb = GetComponent<Rigidbody>();
            Vector3 vel = brb ? brb.linearVelocity : Bike.Forward * Bike.Speed;
            if (new Vector3(vel.x, 0f, vel.z).magnitude > PlayerController.BailSpeed)
            {
                // moving (owner, 22 Nov 2026: "speed me kisi bhi vehicle se exit krte hain to ragdoll bnke girega"): he jumps
                // off, a ragdoll hurt by the speed; nobody holds the bike up, so over it goes with what it had
                p.BailOut(at, vel, at - transform.position);
                rider = null;
                Riding = null;
                Bike.Crash(vel, null, true);
                return;
            }
            rider = null;
            Riding = null;
            p.AlightBike(this, Ground(at) + Vector3.up * 0.1f);   // the leg back over (Dismount), off to its side
            Bike.Park();
        }

        /// <summary>Off at once (the player died, was arrested, fell off).</summary>
        public void ForceExit() { if (rider) { Bike.Park(); var p = rider; rider = null; Riding = null; p.ExitBike(Ground(transform.position - transform.right * 0.9f) + Vector3.up * 0.1f); } }

        void FixedUpdate()
        {
            if (!rider || !Bike || Bike.Crashed || rider.Boarding) return;   // climbing on: BikeRide holds it (brakes on)
            float dt = Time.fixedDeltaTime;
            Vector2 mv = GameInput.UiBlocking ? Vector2.zero : GameInput.Move;
            mv.y = VehicleFuel.Drive(this, mv.y, Bike.Speed, dt);   // fuel: burns while riding, no pull once dry (VehicleFuel)
            // the controls straight to the machine (owner, 22 Nov 2026: "auto braking kyun hori...khade khade mudri hai...
            // actual drive do na usko"): W throttle, S / Space brakes (S standing: walk it back), A/D the handlebar. It turns
            // only as it rolls, coasts off the throttle, and nothing brakes for the rider - a wall hit hard throws him off.
            float throttle = Mathf.Clamp01(mv.y);
            float brake = GameInput.JumpHeld ? 1f : Mathf.Clamp01(-mv.y);
            Bike.Ride(throttle, brake, Mathf.Clamp(mv.x, -1f, 1f), MaxSpeed, mv.y < -0.5f && !GameInput.JumpHeld, dt);
            if (GameInput.HornPressed) VehicleSound.HornOf(gameObject, transform.position + Vector3.up, false);   // this bike's own horn
        }

        void Update()
        {
            // the rig leans with the bike (the riding pack's lean loops)
            if (!rider || !rider.Rig) return;
            rider.Rig.lean = Mathf.Clamp(Bike.Steer / 30f, -1f, 1f);
        }

        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 2f, p.z), Vector3.down, out var h, 6f, ~((1 << 2) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore)) return h.point;
            return new Vector3(p.x, top, p.z);
        }

        void OnDestroy() { if (Riding == this) ForceExit(); }
    }
}
