using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Ride the Mallital - Snow View ropeway (owner, 23 Nov 2026: "ropeways interactable bnaao... proper both sides
    /// entry"). Each cabin has its bay at both stations: you go in at the station's ENTRY, up the stairs to the platform,
    /// and while the cabins wait there, E at your cabin's door (ticket Rs 300) puts you aboard; it carries you across and
    /// lets you out onto the other station's platform, by its door. E while it still waits gets you off again.
    /// doorLow / doorHigh: the platform spot at this cabin's door at Mallital / Snow View (set by the builder).
    /// </summary>
    public class RopewayRide : Interactable
    {
        public static RopewayRide Riding { get; private set; }

        public int price = 300;
        public int index;                  // which cabin of the Ropeway
        public Vector3 doorLow, doorHigh;  // on the platforms, outside this cabin's door
        public Ropeway rope;

        Transform seat;
        PlayerController rider;
        bool fromTop;

        void Awake()
        {
            if (!rope) rope = GetComponent<Ropeway>();
            radius = 2.6f;
            seat = new GameObject("Seat").transform;
            seat.SetParent(transform, false);
            seat.localPosition = new Vector3(0f, -3.15f, -0.55f);   // on the cabin floor, facing along the cable
        }

        bool AtTop => rope && rope.AtTopOf(index);
        Vector3 Door => AtTop ? doorHigh : doorLow;
        bool HasDoors => doorLow != Vector3.zero || doorHigh != Vector3.zero;

        /// <summary>Where you stand to get in: this cabin's door on the platform it waits at (out of reach while it moves).</summary>
        public override Vector3 Point
        {
            get
            {
                if (!rope || !rope.Docked) return transform.position + Vector3.down * 1000f;
                if (HasDoors) return Door;
                return transform.position + Vector3.down * 3.4f;
            }
        }

        public override string Prompt => rider ? "Cable car se utro" : "Cable car mein baitho (ticket Rs " + price + ")";

        public override bool CanInteract(PlayerController p)
        {
            if (!rope || !rope.Docked) return false;
            return rider ? rider == p : p.CurrentMode == PlayerController.Mode.Normal && !Riding;
        }

        public override void Interact(PlayerController p)
        {
            if (rider)
            {
                if (rope.Docked) Leave(false);
                else GameEvents.ShowToast("Station aane do, phir utarna", 2f);
                return;
            }
            if (!GameState.TrySpend(price)) { GameEvents.ShowToast("Ticket ke liye Rs " + price + " chahiye!", 2.5f); return; }
            rider = p;
            Riding = this;
            fromTop = AtTop;
            rope.Hold(4f);   // a moment to settle in before it moves off
            p.EnterMount(seat, transform);
            GameEvents.ShowToast("Cable car mein baith gaye - " + (fromTop ? "Mallital" : "Snow View") + " ki taraf. Nazara dekho!", 3.5f);
        }

        void Update()
        {
            if (!rider) return;
            if (rider.CurrentMode != PlayerController.Mode.Mount) { rider = null; Riding = null; return; }
            if (rope.Docked && AtTop != fromTop) Leave(true);
        }

        void Leave(bool arrived)
        {
            var r = rider;
            rider = null;
            Riding = null;
            r.ExitMount(HasDoors ? Door + Vector3.up * 0.1f : ExitPoint());
            if (arrived) GameEvents.ShowToast("Pahunch gaye: " + (AtTop ? "Snow View" : "Mallital") + " - bahar ka raasta seedhiyon se", 2.5f);
        }

        /// <summary>Firm, free ground beside the cabin (a ropeway built without platforms).</summary>
        Vector3 ExitPoint()
        {
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
            Vector3 c = transform.position;
            foreach (var off in new[] { transform.right * 3f, -transform.right * 3f, transform.forward * 3.5f, -transform.forward * 3.5f })
            {
                if (!Physics.Raycast(c + off + Vector3.up * 2f, Vector3.down, out var hit, 40f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.7f) continue;
                Vector3 feet = hit.point + Vector3.up * 0.1f;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.35f, mask, QueryTriggerInteraction.Ignore)) continue;
                return feet;
            }
            return c + Vector3.down * 3.2f;
        }
    }
}
