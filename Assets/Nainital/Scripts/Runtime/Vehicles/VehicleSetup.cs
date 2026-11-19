using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Hand-made setup of one vehicle model (user, 2026-11-19: "ek unity ka scene ... jahan par saari vehicles ki setting
    /// hogi"): every seat with its pose and the spot outside where the sitter gets in and out, the head and tail lamps,
    /// how each door opens. Made in the Vehicle Setup tool (Nainital > Vehicle Setup, scene VehicleSetup) and saved as
    /// Resources/VehicleSetup/&lt;model&gt;.json. Everything is in the vehicle's own space (x right, y up, z forward,
    /// the origin on the ground under the middle). A seat is the sitter's ROOT; HumanoidRig puts the hips 0.51 m above
    /// it for Sit / Drive. "approved" = the user said OK; the game only uses approved setups.
    /// </summary>
    [Serializable]
    public class VehicleSetupData
    {
        public string model;
        public bool approved;
        public Seat[] seats = new Seat[0];
        public Lamp[] head = new Lamp[0];
        public Lamp[] tail = new Lamp[0];
        public Door[] doors = new Door[0];
        public string saved;   // when, for the record

        [Serializable]
        public class Seat
        {
            public SeatPose pose;
            public Vector3 pos;        // the sitter's root
            public Vector3 euler;      // the sitter's rotation (yaw mostly)
            public Vector3 entry;      // where he stands outside to get in / out
            /// <summary>Hands on the steering wheel / handlebar grips (HandIK): the wrists' points in vehicle space with
            /// the steering straight; at play time they ride on the steering object (handsOn) as it turns.</summary>
            public bool hands;
            public Vector3 handL, handR;
            public string handsOn;     // the model's steering object (Steering*, Handle*, Fork*), "" = the body
        }

        [Serializable]
        public class Lamp
        {
            public Vector3 pos;
            public Vector3 euler;
            public Vector2 size = new Vector2(0.22f, 0.12f);
            public bool mirror = true; // a second lamp at -x
        }

        [Serializable]
        public class Door
        {
            public string name;        // the model's child object (Door_FL, Door_Back ...): its origin sits on the hinge
            public DoorHinge hinge;
            public float openDeg = 65f;
            public bool flip;          // opens the other way
        }
    }

    public enum SeatPose { Drive, Sit, Ride, Pillion, SideSaddle }
    public enum DoorHinge { Side, Top, Bottom }

    public static class VehicleSetup
    {
        public const string ResourceDir = "VehicleSetup";
        static readonly Dictionary<string, VehicleSetupData> cache = new Dictionary<string, VehicleSetupData>();

        /// <summary>The saved setup of a model, or null. approvedOnly: what the game uses.</summary>
        public static VehicleSetupData Get(string model, bool approvedOnly = true)
        {
            if (string.IsNullOrEmpty(model)) return null;
            if (!cache.TryGetValue(model, out var d))
            {
                var t = Resources.Load<TextAsset>(ResourceDir + "/" + model);
                d = t ? JsonUtility.FromJson<VehicleSetupData>(t.text) : null;
                cache[model] = d;
            }
            return d != null && (d.approved || !approvedOnly) ? d : null;
        }

        public static void Forget(string model) { if (model != null) cache.Remove(model); }

        /// <summary>The rig flags for a pose (PoliceOfficer.SitIn does the same for its two poses).</summary>
        public static void Pose(HumanoidRig rig, SeatPose p)
        {
            if (!rig) return;
            rig.seated = p == SeatPose.Drive || p == SeatPose.Sit || p == SeatPose.SideSaddle;
            rig.driving = p == SeatPose.Drive;
            rig.riding = p == SeatPose.Ride || p == SeatPose.Pillion;
            rig.pillion = p == SeatPose.Pillion;
            rig.speed = 0f;
            rig.grounded = true;
        }

        /// <summary>How many people a kind of vehicle carries (driver included) - the tool's starting number.</summary>
        public static int Capacity(string model)
        {
            string m = (model ?? "").ToLowerInvariant();
            if (m.Contains("bus_roadways") || m == "touristbus") return 33;
            if (m.Contains("bus_tourist") || m == "touristcoach") return 36;
            if (m.Contains("bike") || m.Contains("scooty") || m == "splendor") return 2;
            if (m.Contains("tirri")) return 5;
            if (m.Contains("tuktuk")) return 4;
            if (m.Contains("pickup") || m.Contains("truck") || m == "tata407" || m == "boleropickup" || m == "ambulance108") return 3;
            if (m.Contains("van")) return 2;
            if (m.Contains("mpv_large") || m == "innovacrysta") return 8;
            if (m.Contains("mpv") || m.Contains("suv") || m == "ertiga" || m == "bolero" || m == "scorpio") return 7;
            return 5;
        }

        public static bool TwoWheeler(string model)
        {
            string m = (model ?? "").ToLowerInvariant();
            return m.Contains("bike") || m.Contains("scooty") || m == "splendor";
        }

        /// <summary>The auto and the e-rickshaw (user, 2026-11-20: "auto ke liye three wheeled and handle too"): one front
        /// wheel turned by a handlebar, like a bike, and two rear wheels; the driver sits and holds the grips.</summary>
        public static bool ThreeWheeler(string model)
        {
            string m = (model ?? "").ToLowerInvariant();
            return m.Contains("tuktuk") || m.Contains("tirri") || m.Contains("rickshaw");
        }

        /// <summary>Steered by a handlebar, not a steering wheel.</summary>
        public static bool HandlebarSteered(string model) => TwoWheeler(model) || ThreeWheeler(model);
    }

    /// <summary>Clicks in the Scene view pick this object, not the mesh under it (the setup tool's seats, lamps, entry
    /// points).</summary>
    [SelectionBase]
    public class SetupHandle : MonoBehaviour
    {
        public enum Kind { Seat, Entry, Head, Tail, HandL, HandR }
        public Kind kind;
        public int index;

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Seat ? new Color(0.2f, 0.8f, 1f) : kind == Kind.Entry ? new Color(0.2f, 1f, 0.3f)
                         : kind == Kind.Head ? new Color(1f, 0.95f, 0.6f) : kind == Kind.HandL ? new Color(0.3f, 0.5f, 1f)
                         : kind == Kind.HandR ? new Color(1f, 0.55f, 0.1f) : new Color(1f, 0.2f, 0.2f);
            if (kind == Kind.HandL || kind == Kind.HandR) { Gizmos.DrawWireSphere(transform.position, 0.05f); return; }
            if (kind == Kind.Entry) { Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.25f); Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.7f); }
            else if (kind == Kind.Seat) { Gizmos.DrawWireCube(transform.position + Vector3.up * 0.51f, new Vector3(0.4f, 0.05f, 0.4f)); Gizmos.DrawRay(transform.position + Vector3.up * 0.51f, transform.forward * 0.5f); }
        }
    }

    /// <summary>The setup tool's mirrored lamp: keeps its twin at -x of this one.</summary>
    public class SetupMirror : MonoBehaviour
    {
        public Transform twin;

        void LateUpdate()
        {
            if (!twin) return;
            Vector3 p = transform.localPosition, e = transform.localEulerAngles;
            twin.localPosition = new Vector3(-p.x, p.y, p.z);
            twin.localEulerAngles = new Vector3(e.x, -e.y, -e.z);
            twin.localScale = transform.localScale;
        }

        void OnDestroy() { if (twin) Destroy(twin.gameObject); }
    }

    /// <summary>The setup tool's "player gets in" preview: the sitter in the seat climbs out to the entry point and steps
    /// away, the player walks up to the same point and takes the seat (TrafficCar.DriverOut / HandOver do this in the
    /// game).</summary>
    public class SetupEnterPreview : MonoBehaviour
    {
        public static IEnumerator Run(Transform vehicle, Transform sitter, HumanoidRig sitterRig, Transform player, HumanoidRig playerRig,
                                      Vector3 seatLocal, Quaternion seatRot, Vector3 entryLocal, SeatPose pose, bool carDoor = true)
        {
            Vector3 entry = vehicle.TransformPoint(entryLocal);
            Vector3 side = entry - vehicle.TransformPoint(new Vector3(0f, entryLocal.y, entryLocal.z));
            side.y = 0f;
            if (side.sqrMagnitude < 0.01f) side = vehicle.right;
            side.Normalize();
            // 1. the old sitter climbs out to the entry point
            bool car = pose == SeatPose.Drive || pose == SeatPose.Sit;
            if (sitter)
            {
                var sik = sitter.GetComponent<HandIK>();
                if (sik) sik.enabled = false;   // his hands leave the wheel
                sitter.SetParent(vehicle.parent, true);
                Vector3 a = sitter.position;
                Quaternion ra = sitter.rotation;
                // in a car he is pulled out (Mixamo "Pulled From Seat"); off a two-wheeler he just gets off
                if (sitterRig && car) { sitterRig.PlayGesture("PulledOut", 1.6f); yield return new WaitForSeconds(0.5f); }
                if (sitterRig) sitterRig.seated = sitterRig.driving = sitterRig.riding = sitterRig.pillion = false;
                for (float t = 0f; t < 1f; t += Time.deltaTime / (car ? 1.1f : 0.8f))
                {
                    sitter.position = Vector3.Lerp(a, entry, t);
                    sitter.rotation = Quaternion.Slerp(ra, Quaternion.LookRotation(side), t);
                    yield return null;
                }
                // 2. and walks off 2.5 m
                Vector3 b = entry, c = entry + side * 2.5f;
                if (sitterRig) sitterRig.speed = 1.4f;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 1.8f) { sitter.position = Vector3.Lerp(b, c, t); yield return null; }
                if (sitterRig) sitterRig.speed = 0f;
            }
            // 3. the player walks up to the entry point from 4 m out
            Vector3 start = entry + side * 4f + vehicle.forward * 1.5f;
            player.gameObject.SetActive(true);
            player.position = start;
            player.rotation = Quaternion.LookRotation(entry - start);
            if (playerRig) { playerRig.seated = playerRig.driving = playerRig.riding = playerRig.pillion = false; playerRig.speed = 1.4f; playerRig.grounded = true; }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 2.2f) { player.position = Vector3.Lerp(start, entry, t); yield return null; }
            if (playerRig) playerRig.speed = 0f;
            // 4. and gets in
            Vector3 seat = vehicle.TransformPoint(seatLocal);
            Quaternion sr = vehicle.rotation * seatRot;
            Quaternion r0 = player.rotation;
            // into a car: Mixamo "Entering Car" while he slides onto the seat; onto a two-wheeler: a swing of the leg
            if (playerRig && car && carDoor) playerRig.PlayGesture("CarEnter", 1.8f);   // an auto has no door to duck through
            VehicleSetup.Pose(playerRig, pose);
            for (float t = 0f; t < 1f; t += Time.deltaTime / (car ? 1.6f : 0.8f))
            {
                player.position = Vector3.Lerp(entry, seat, t);
                player.rotation = Quaternion.Slerp(r0, sr, t);
                yield return null;
            }
            player.SetParent(vehicle, true);
            player.localPosition = seatLocal;
            player.localRotation = seatRot;
            var pik = player.GetComponent<HandIK>();
            if (pik) pik.enabled = true;   // and his go onto it
        }
    }
}
