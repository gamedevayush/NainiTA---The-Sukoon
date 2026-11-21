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
        float MaxSpeed => Scooty ? 13f : 17f;

        void Awake()
        {
            bike = GetComponent<BikeRide>();
            radius = 2.4f;
            if (blockMask < 0) blockMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9));
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
            p.EnterBike(this);
            if (AudioManager.Instance) AudioManager.Instance.CarEnter(transform.position);
            GameEvents.ShowToast("W/S: race-brake, A/D: mod, Space: brake, E: utro", 3.5f);
        }

        void Exit()
        {
            if (!rider) return;
            if (Bike.Speed > 3f) { GameEvents.ShowToast("Pehle " + (Scooty ? "scooty" : "bike") + " roko (S / Space)", 1.6f); return; }
            var p = rider;
            var vp = Bike.Parts;
            Vector3 at;
            if (vp && vp.HasSetup) at = vp.WayOut(0, out _);
            else at = transform.position - transform.right * 0.9f;
            rider = null;
            Riding = null;
            p.ExitBike(Ground(at) + Vector3.up * 0.1f);
            Bike.Park();
        }

        /// <summary>Off at once (the player died, was arrested, fell off).</summary>
        public void ForceExit() { if (rider) { Bike.Park(); var p = rider; rider = null; Riding = null; p.ExitBike(Ground(transform.position - transform.right * 0.9f) + Vector3.up * 0.1f); } }

        void FixedUpdate()
        {
            if (!rider || !Bike || Bike.Crashed) return;
            float dt = Time.fixedDeltaTime;
            Vector2 mv = GameInput.UiBlocking ? Vector2.zero : GameInput.Move;
            float target = mv.y > 0.05f ? mv.y * MaxSpeed : 0f;
            if (GameInput.JumpHeld || mv.y < -0.05f) target = 0f;
            // steering: a wide lock walking pace, less as it gathers speed
            float speed = Bike.Speed;
            float lockDeg = speed < 1f ? 60f : Mathf.Lerp(40f, 16f, Mathf.Clamp01(speed / MaxSpeed));
            Vector3 aim = transform.position + Quaternion.Euler(0f, mv.x * lockDeg, 0f) * Bike.Forward * 6f;
            // walls, vehicles: stop short (the bike moves itself, it does not bounce off)
            float look = 1.2f + speed * 0.55f;
            // from above the ground under the front wheel (on a steep road the bike's own height there is in the hill)
            Vector3 from = Ground(transform.position + Bike.Forward * (Bike.length * 0.5f)) + Vector3.up * 0.75f;
            // the hill road rising ahead is not a wall: a slope under ~37 deg is ridden up, a steeper face (a hillside cut,
            // a wall) stops it
            int n = Physics.SphereCastNonAlloc(from, 0.35f, Bike.Forward, hits, look, blockMask, QueryTriggerInteraction.Ignore);
            float wall = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance <= 0f ? h.collider is TerrainCollider : h.normal.y > 0.8f) continue;   // the hill it starts in is ground
                wall = Mathf.Min(wall, h.distance);
            }
            if (wall < float.MaxValue) target = Mathf.Min(target, Mathf.Max(0f, wall - 0.6f) * 1.5f);
            Bike.Drive(aim, target, dt, Mathf.Abs(mv.x) > 0.3f);
            if (GameInput.HornPressed && AudioManager.Instance) AudioManager.Instance.HornAt(transform.position + Vector3.up, false);
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
