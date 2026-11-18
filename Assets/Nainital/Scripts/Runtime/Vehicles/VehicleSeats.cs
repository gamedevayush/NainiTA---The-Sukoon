using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Where the driver (and the front passenger) of a vehicle sit (user, 2026-11-18: "vehicles pe player aur NPC ki
    /// position theek nahi hai"). Returns the sitter's ROOT in vehicle space; HumanoidRig puts the hips 0.51 m above the
    /// root (its seat lock), whatever the character's size. Right-hand drive: the driver on the right (+x).
    /// The hip point comes from the model: behind the front axle for cars (cab-over trucks, vans and buses: over or ahead
    /// of it), a little inside the body side, at a height for the kind of vehicle; a table can correct single models.
    /// </summary>
    public static class VehicleSeats
    {
        public const float HipAboveRoot = 0.51f;

        // per model corrections of the hip point (vehicle space, metres), measured in game captures
        static readonly Dictionary<string, Vector3> Fix = new Dictionary<string, Vector3>
        {
        };

        /// <summary>The hip point of seat 0 (driver) or 1 (front passenger).</summary>
        public static Vector3 Hip(GameObject vehicle, int seat = 0)
        {
            var info = vehicle.GetComponent<VehicleInfo>();
            var car = vehicle.GetComponent<CarController>();
            CarType type = car ? car.type : CarType.Sedan;
            float W = info ? info.width : 1.8f, H = info ? info.height : 1.6f, L = info ? info.length : 4.2f, R = info ? info.wheelRadius : 0.33f;
            float body = info && info.bodyWidth > 0.5f ? info.bodyWidth : Mathf.Min(W, L > 6.5f ? 2.5f : 1.9f);
            float front = info && info.frontAxleZ != 0f ? info.frontAxleZ : L * 0.3f;
            bool big = type == CarType.Bus || L > 6.5f;
            bool cabOver = big || type == CarType.Pickup && H > 2.3f || (info && info.model == "Ambulance108");
            Vector3 hip;
            if (big) hip = new Vector3(body * 0.5f - 0.55f, Mathf.Max(1.25f, R * 2.1f + 0.35f), front + 0.55f);
            else if (cabOver) hip = new Vector3(body * 0.5f - 0.5f, R * 2f + 0.62f, front - 0.25f);
            // cars: low enough that a seated man's head (~1 m over his hips) stays under the roof
            else hip = new Vector3(body * 0.5f - 0.48f, Mathf.Min(R + H * 0.2f + 0.12f, H - 1.1f), front - 1.05f);
            if (info && Fix.TryGetValue(info.model ?? "", out var f)) hip += f;
            if (seat == 1) hip.x = -hip.x;
            return hip;
        }

        /// <summary>The sitter's root for seat 0 / 1 (hips HipAboveRoot over it).</summary>
        public static Vector3 Root(GameObject vehicle, int seat = 0) => Hip(vehicle, seat) - Vector3.up * HipAboveRoot;
    }
}
