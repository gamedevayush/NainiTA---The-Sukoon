using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Ride the Mallital - Snow View ropeway. While the cabin waits at a station, E (ticket Rs 300) puts you aboard; it
    /// carries you to the other end and lets you off beside the station. E while it waits gets you off early.
    /// </summary>
    [RequireComponent(typeof(Ropeway))]
    public class RopewayRide : Interactable
    {
        public static RopewayRide Riding { get; private set; }

        public int price = 300;

        Ropeway rope;
        Transform seat;
        PlayerController rider;
        bool fromTop;
        Vector3 ground;

        void Awake()
        {
            rope = GetComponent<Ropeway>();
            radius = 9f;
            seat = new GameObject("Seat").transform;
            seat.SetParent(transform, false);
            seat.localPosition = new Vector3(0f, -3.15f, -0.55f);   // on the cabin floor, facing along the cable
        }

        public override Vector3 Point => ground;
        public override string Prompt => rider ? "Cable car se utro" : "Cable car mein baitho (ticket Rs " + price + ")";

        public override bool CanInteract(PlayerController p)
        {
            if (!rope || !rope.Docked) return false;
            return rider ? rider == p : p.CurrentMode == PlayerController.Mode.Normal;
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
            fromTop = rope.AtTop;
            rope.Hold(3f);   // a moment to settle in before it moves off
            p.EnterMount(seat, transform);
            GameEvents.ShowToast("Cable car mein baith gaye - " + (fromTop ? "Mallital" : "Snow View") + " ki taraf. Nazara dekho!", 3.5f);
        }

        void Update()
        {
            if (rope && rope.Docked)   // where you stand to board: the ground under the waiting cabin
            {
                if (Physics.Raycast(transform.position + Vector3.down * 3.4f, Vector3.down, out var hit, 30f, ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                    ground = hit.point;
            }
            else ground = transform.position + Vector3.down * 1000f;   // out of reach while moving

            if (!rider) return;
            if (rider.CurrentMode != PlayerController.Mode.Mount) { rider = null; Riding = null; return; }
            if (rope.Docked && rope.AtTop != fromTop) Leave(true);
        }

        void Leave(bool arrived)
        {
            var r = rider;
            rider = null;
            Riding = null;
            r.ExitMount(ExitPoint());
            if (arrived) GameEvents.ShowToast("Pahunch gaye: " + (rope.AtTop ? "Snow View" : "Mallital"), 2.5f);
        }

        /// <summary>Firm, free ground beside the cabin.</summary>
        Vector3 ExitPoint()
        {
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
            Vector3 c = transform.position;
            foreach (var off in new[] { transform.right * 3f, -transform.right * 3f, transform.forward * 3.5f, -transform.forward * 3.5f, transform.right * 6f, -transform.right * 6f })
            {
                if (!Physics.Raycast(c + off + Vector3.up * 2f, Vector3.down, out var hit, 40f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.7f) continue;
                var t = Terrain.activeTerrain;
                if (t && hit.point.y > t.SampleHeight(hit.point) + t.transform.position.y + 1.6f) continue;   // a roof
                Vector3 feet = hit.point + Vector3.up * 0.1f;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.35f, mask, QueryTriggerInteraction.Ignore)) continue;
                return feet;
            }
            return ground + Vector3.up * 0.1f;
        }
    }
}
