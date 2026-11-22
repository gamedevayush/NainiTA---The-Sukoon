using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Riding a bus as a passenger (owner, 22 Nov 2026: "bus me as passenger bethne ka bhi rkho"): E at its door - a moving
    /// traffic bus pulls up for him first - he walks up, climbs in and sits on a free passenger seat of the setup; the bus
    /// goes on along its way (TrafficCar drives it), the camera on the bus. E again: off at the door when it stands, a jump
    /// as a ragdoll when it moves (PlayerController.BailOut: "speed me kisi bhi vehicle se exit ... ragdoll").
    /// On every traffic bus (TrafficManager) and the bus of TrafficBusTest. The driver's way in stays TrafficCarEntry's.
    /// </summary>
    public class BusPassenger : Interactable
    {
        public static BusPassenger Riding { get; private set; }

        /// <summary>The player rides in `go` (nobody may destroy it under him: TrafficManager, TrafficCar.Leave).</summary>
        public static bool Carries(GameObject go) => Riding && go && Riding.gameObject == go;

        /// <summary>Off `go` at once if he rides in it (it is about to go away).</summary>
        public static void Evict(GameObject go) { if (Carries(go)) Riding.ForceExit(); }

        VehicleParts vp;
        Rigidbody rb;
        TrafficCar traffic;
        PlayerController rider, asker;
        int seat = -1;
        float waitT = -1f;

        void Awake()
        {
            vp = GetComponent<VehicleParts>();
            rb = GetComponent<Rigidbody>();
            traffic = GetComponent<TrafficCar>();
            radius = 3f;
        }

        float Speed => rb ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude : 0f;
        bool Waiting => waitT >= 0f;

        public override string Prompt => rider ? "Bus se utro" : Waiting ? "Bus ruk rahi hai..." : "Bus mein sawari karo";

        /// <summary>The passenger door: the way in of the first passenger seat (the setup's), else the bus's left side ahead
        /// of the middle (Indian buses: the door on the left, the driver on the right).</summary>
        public override Vector3 Point
        {
            get
            {
                if (!vp) vp = GetComponent<VehicleParts>();
                if (vp && vp.HasSetup && vp.SeatCount > 1) return vp.EntryWorld(1);
                var info = GetComponent<VehicleInfo>();
                float w = info ? info.width : 2.4f, l = info ? info.length : 9.6f;
                return transform.TransformPoint(new Vector3(-(w * 0.5f + 0.3f), 0.5f, l * 0.25f));
            }
        }

        public override bool CanInteract(PlayerController p) =>
            !rider && !Riding && !Waiting && CarController.Driving == null && vp && vp.HasSetup && (!traffic || !traffic.Wrecked) && vp.FreePassengerSeat() >= 0;

        public override void Interact(PlayerController p)
        {
            if (!p || p.Boarding || p.Ragdolled) return;
            if (rider == p) { GetOff(); return; }
            if (!CanInteract(p)) return;
            if (Speed > 0.6f && traffic)
            {
                // moving: the driver pulls up for him, then he climbs in (Update)
                traffic.StopForBoarding(14f);
                asker = p;
                waitT = 0f;
                GameEvents.ShowToast("Bus ruk rahi hai... phir chadhoge", 2f);
                return;
            }
            Board(p);
        }

        void Update()
        {
            if (Waiting)
            {
                waitT += Time.deltaTime;
                if (!asker || waitT > 14f || (asker.transform.position - Point).sqrMagnitude > 14f * 14f) waitT = -1f;   // he walked off / it never stopped
                else if (Speed < 0.4f) { waitT = -1f; Board(asker); }
            }
            // gone another way (died, the menu, the police): the seat is free again
            if (rider && rider.CurrentMode != PlayerController.Mode.Mount && !rider.Boarding) Release();
        }

        void Board(PlayerController p)
        {
            int s = vp.FreePassengerSeat();
            if (s < 0) { GameEvents.ShowToast("Bus bhari hai", 1.6f); return; }
            seat = s;
            rider = p;
            Riding = this;
            if (traffic) traffic.StopForBoarding(4f);   // it waits while he climbs in
            p.BoardPassenger(vp, seat);
            GameEvents.ShowToast("Sawari: E - utro (chalti bus se kooda to chot lagegi)", 3f);
        }

        /// <summary>Straight onto a passenger seat, no walk (a test: TrafficBusTest starts with him aboard).</summary>
        public bool SeatNow(PlayerController p)
        {
            if (!p || !vp || !vp.HasSetup || Riding) return false;
            int s = vp.FreePassengerSeat();
            if (s < 0) return false;
            seat = s;
            rider = p;
            Riding = this;
            p.SitPassenger(vp, seat);
            return true;
        }

        void GetOff()
        {
            var p = rider;
            Vector3 at = vp.WayOut(seat, out _) + Vector3.up * 0.1f;
            Vector3 v = rb ? rb.linearVelocity : Vector3.zero;
            Release();
            if (new Vector3(v.x, 0f, v.z).magnitude > PlayerController.BailSpeed) p.BailOut(at, v, at - transform.position);
            else
            {
                if (traffic) traffic.StopForBoarding(3f);   // it waits while he steps down
                p.AlightPassenger(vp, at);
            }
        }

        /// <summary>Off at once (he died, the police took him, the bus is going away).</summary>
        public void ForceExit()
        {
            var p = rider;
            if (!p) return;
            Vector3 at = vp ? vp.WayOut(seat, out _) : transform.position - transform.right * 2f;
            Release();
            if (vp) vp.Leave(p.transform);
            p.ExitMount(at + Vector3.up * 0.1f);
        }

        void Release()
        {
            rider = null;
            seat = -1;
            if (Riding == this) Riding = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Riding == this) Riding = null;
        }
    }
}
