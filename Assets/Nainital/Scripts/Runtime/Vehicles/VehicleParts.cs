using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The moving parts and the seats of a vehicle from the unbranded set (Vehicles/New, docs/VEHICLES.md), driven the
    /// way the Vehicle Setup tool previews them (owner, 21 Nov 2026: "jo baaki kaam hai vo poora krlo"):
    /// every Wheel_* spins about its own axle, a car's front wheels steer, a handlebar (bikes, scooties, the auto, the
    /// e-rickshaw) turns about its own raked axis with Wheel_F under it, a Steering wheel turns three times as far, a
    /// door opens on its hinge or by its own baked clip (the buses' fold / slide) while someone gets in or out, a Dmg_*
    /// shape key dents the side that was hit, and the wheels bounce with CarController's suspension. The steering angle
    /// and the speed come from CarController or BikeRide on the same object.
    /// The seats are the approved setup's (Resources/VehicleSetup/&lt;model&gt;.json): Sit puts a person on seat n in its
    /// pose, the head under its roof (HumanoidRig.seatRoof), the hands on the wheel / the grips and the feet on the floor /
    /// the pegs (HandIK). Runs before HandIK, so the hands follow the wheel turned this frame.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public class VehicleParts : MonoBehaviour
    {
        public string model;
        /// <summary>The model itself (the FBX's root): the setup's points are in its space.</summary>
        public Transform body;
        /// <summary>What seated people, their hand / foot targets and the lamps hang from: the vehicle, or a two-wheeler's
        /// leaning body (BikeRide's Lean) so they lean with it. The body sits at its origin, unturned.</summary>
        public Transform seatParent;
        public VehicleSetupData setup;
        public float wheelRadius = 0.3f;
        /// <summary>CarController's suspension points (FL, FR, RL, RR, vehicle space): the wheels bounce with them.</summary>
        public Transform[] pivots;
        /// <summary>The doors' own clip (the buses) and which doors it moves.</summary>
        public AnimationClip doorClip;
        public string[] clipDoors = new string[0];

        /// <summary>Degrees, + = right. Read from CarController / BikeRide every frame (or set by hand when neither).</summary>
        [System.NonSerialized] public float steer;
        [System.NonSerialized] public float speed;

        public bool HasSetup => setup != null && setup.seats != null && setup.seats.Length > 0;
        public Transform Frame => body ? body : transform;
        public Transform SeatParent => seatParent ? seatParent : transform;
        public int SeatCount => HasSetup ? setup.seats.Length : 0;
        public bool TwoWheeler => VehicleSetup.TwoWheeler(model);

        class Wheel { public Transform t; public Quaternion rest; public Vector3 restPos, axle, up; public bool front, viaBar; public int pivot = -1; }
        class Door { public Transform t; public Quaternion rest; public VehicleSetupData.Door d; public bool byClip; public float open, target, closeAt = -1f; }
        readonly List<Wheel> wheels = new List<Wheel>();
        readonly List<(Transform t, Quaternion rest)> bars = new List<(Transform, Quaternion)>(), steerings = new List<(Transform, Quaternion)>();
        readonly List<Door> doors = new List<Door>();
        readonly List<(SkinnedMeshRenderer r, int i, string side)> dents = new List<(SkinnedMeshRenderer, int, string)>();
        // no Dmg_* keys (the unbranded set): the body's own mesh, copied on the first knock and pushed in where it was hit
        MeshFilter bodyMesh;
        Mesh dentMesh;
        Vector3[] dentVerts;
        float[] dentMoved;
        Vector3[] pivotRest;
        float spin;
        bool clipDirty = true;
        CarController car;
        BikeRide bike;
        Rigidbody rb;
        Transform[] handL, handR, footL, footR;
        Transform[] occupant;
        bool ready;

        /// <summary>Find the parts of the model under `body` (named as in docs/VEHICLES.md section 3) and read its setup.</summary>
        public void Init(Transform modelRoot, string modelName, Transform seatsOn = null, AnimationClip clip = null, string[] doorsOfClip = null)
        {
            body = modelRoot;
            model = modelName;
            seatParent = seatsOn ? seatsOn : transform;
            doorClip = clip;
            clipDoors = doorsOfClip ?? new string[0];
            setup = VehicleSetup.Get(model);
            car = GetComponent<CarController>();
            bike = GetComponent<BikeRide>();
            rb = GetComponent<Rigidbody>();
            wheels.Clear(); bars.Clear(); steerings.Clear(); doors.Clear(); dents.Clear();
            bodyMesh = null; dentVerts = null; dentMoved = null;
            var saved = new Dictionary<string, VehicleSetupData.Door>();
            if (setup != null) foreach (var d in setup.doors) if (d != null && !string.IsNullOrEmpty(d.name)) saved[d.name] = d;
            float rSum = 0f;
            int rN = 0;
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Wheel_", System.StringComparison.OrdinalIgnoreCase))
                {
                    string k = n.Substring(6).ToUpperInvariant();
                    var inv = Quaternion.Inverse(t.parent.rotation) * transform.rotation;
                    wheels.Add(new Wheel { t = t, rest = t.localRotation, restPos = t.localPosition, front = k.StartsWith("F"), axle = inv * Vector3.right, up = inv * Vector3.up,
                                           pivot = k == "FL" ? 0 : k == "FR" ? 1 : k == "RL" ? 2 : k == "RR" ? 3 : k == "R" ? 4 : -1 });
                    float r = Radius(t);
                    if (r > 0.1f) { rSum += r; rN++; }
                }
                else if (n.StartsWith("Handle", System.StringComparison.OrdinalIgnoreCase) || n.StartsWith("Fork", System.StringComparison.OrdinalIgnoreCase))
                    bars.Add((t, t.localRotation));
                else if (n.StartsWith("Steering", System.StringComparison.OrdinalIgnoreCase))
                    steerings.Add((t, t.localRotation));
                else if (n.StartsWith("Door_", System.StringComparison.OrdinalIgnoreCase))
                {
                    bool byClip = doorClip && System.Array.IndexOf(clipDoors, n) >= 0;
                    if (!saved.TryGetValue(n, out var d))
                    {
                        d = new VehicleSetupData.Door { name = n, hinge = n.ToLowerInvariant().Contains("back") ? DoorHinge.Top : DoorHinge.Side };
                        var rr = t.GetComponent<Renderer>();
                        if (rr && d.hinge == DoorHinge.Side) d.flip = transform.InverseTransformPoint(rr.bounds.center).x > 0f;
                    }
                    doors.Add(new Door { t = t, rest = t.localRotation, d = d, byClip = byClip });
                }
                if (n == "Body" && !bodyMesh) { var bmf = t.GetComponent<MeshFilter>(); if (bmf && bmf.sharedMesh) bodyMesh = bmf; }
                var sk = t.GetComponent<SkinnedMeshRenderer>();
                if (sk && sk.sharedMesh)
                    for (int i = 0; i < sk.sharedMesh.blendShapeCount; i++)
                    {
                        string bn = sk.sharedMesh.GetBlendShapeName(i);
                        int dot = bn.IndexOf("Dmg_", System.StringComparison.OrdinalIgnoreCase);
                        if (dot >= 0) dents.Add((sk, i, bn.Substring(dot + 4).ToLowerInvariant()));
                    }
            }
            foreach (var w in wheels)
                for (var p = w.t.parent; p && p != body; p = p.parent)
                    if (bars.Exists(b => b.t == p)) { w.viaBar = true; break; }
            if (rN > 0) wheelRadius = rSum / rN;
            int seats = SeatCount;
            handL = new Transform[seats]; handR = new Transform[seats]; footL = new Transform[seats]; footR = new Transform[seats];
            occupant = new Transform[seats];
            ready = true;
        }

        /// <summary>A wheel's radius from its mesh (a disc: of its three extents the middle one; the world box of a turned
        /// wheel overstates it).</summary>
        static float Radius(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return 0f;
            Vector3 e = Vector3.Scale(mf.sharedMesh.bounds.extents, t.lossyScale);
            float[] r = { Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z) };
            System.Array.Sort(r);
            return r[1];
        }

        /// <summary>CarController's four suspension points for this model (vehicle space, at the hubs): the front pair at the
        /// front wheels (a three-wheeler's single front wheel: 12 cm either side of it), the rear pair at Wheel_RL / RR, or
        /// at the two tyres of a one-piece Wheel_R (its width less a tyre's).</summary>
        public Vector3[] SuspensionPoints()
        {
            var pts = new Vector3[4];
            Wheel Find(string k) => wheels.Find(w => w.t.name.Substring(6).ToUpperInvariant() == k);
            Vector3 P(Wheel w) => transform.InverseTransformPoint(w.t.position);
            var fl = Find("FL"); var fr = Find("FR"); var f = Find("F");
            var rl = Find("RL"); var rr = Find("RR"); var r = Find("R");
            if (fl != null && fr != null) { pts[0] = P(fl); pts[1] = P(fr); }
            else if (f != null) { pts[0] = P(f) + Vector3.left * 0.12f; pts[1] = P(f) + Vector3.right * 0.12f; }
            else { pts[0] = new Vector3(-0.7f, wheelRadius, 1.3f); pts[1] = new Vector3(0.7f, wheelRadius, 1.3f); }
            if (rl != null && rr != null) { pts[2] = P(rl); pts[3] = P(rr); }
            else if (r != null)
            {
                float half = 0.45f;
                var rend = r.t.GetComponent<Renderer>();
                if (rend)
                {
                    Vector3 lo = transform.InverseTransformPoint(rend.bounds.min), hi = transform.InverseTransformPoint(rend.bounds.max);
                    float w = Mathf.Abs(hi.x - lo.x) * 0.5f;
                    half = Mathf.Max(0.05f, w - Mathf.Min(0.12f, w * 0.15f));
                }
                Vector3 c = P(r);
                c.x = 0f;
                pts[2] = c + Vector3.left * half; pts[3] = c + Vector3.right * half;
            }
            else { pts[2] = new Vector3(-0.7f, wheelRadius, -1.3f); pts[3] = new Vector3(0.7f, wheelRadius, -1.3f); }
            for (int i = 0; i < 4; i++) pts[i].y = Mathf.Max(pts[i].y, 0.1f);
            return pts;
        }

        public void SetPivots(Transform[] p)
        {
            pivots = p;
            pivotRest = new Vector3[p.Length];
            for (int i = 0; i < p.Length; i++) pivotRest[i] = p[i] ? p[i].localPosition : Vector3.zero;
        }

        // ------------------------------------------------------------------ every frame
        void Start()
        {
            // added after Init on a car (VehicleFactory adds CarController last)
            car = GetComponent<CarController>();
            bike = GetComponent<BikeRide>();
            rb = GetComponent<Rigidbody>();
        }

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (car) { steer = car.SteerAngle; speed = car.ForwardSpeed; }
            else if (bike) { steer = bike.Steer; speed = bike.Speed; }
            else if (rb && !rb.isKinematic) speed = Vector3.Dot(rb.linearVelocity, transform.forward);
            spin = Mathf.Repeat(spin + speed / Mathf.Max(0.1f, wheelRadius) * Mathf.Rad2Deg * dt, 360f);

            // the doors: open while someone gets in / out, shut a moment later
            bool clipMoved = false;
            float clipOpen = 0f;
            foreach (var d in doors)
            {
                if (d.closeAt >= 0f && Time.time >= d.closeAt) { d.target = 0f; d.closeAt = -1f; }
                float o = Mathf.MoveTowards(d.open, d.target, dt / 0.45f);
                if (o != d.open && d.byClip) clipMoved = true;
                d.open = o;
                if (d.byClip) clipOpen = Mathf.Max(clipOpen, o);
            }
            // a baked clip puts every part it carries back at rest: first, then the wheels and the handlebar on top
            if (doorClip && (clipMoved || clipDirty))
            {
                doorClip.SampleAnimation(body.gameObject, Mathf.SmoothStep(0f, 1f, clipOpen) * doorClip.length);
                clipDirty = false;
            }
            foreach (var (t, rest) in bars) if (t) t.localRotation = rest * Quaternion.AngleAxis(steer, Vector3.up);
            foreach (var (t, rest) in steerings) if (t) t.localRotation = rest * Quaternion.AngleAxis(steer * 3f, Vector3.up);
            foreach (var w in wheels)
            {
                if (!w.t) continue;
                float s = w.front && !w.viaBar ? steer : 0f;
                w.t.localRotation = Quaternion.AngleAxis(s, w.up) * Quaternion.AngleAxis(spin, w.axle) * w.rest;
                if (pivots != null && pivotRest != null && w.pivot >= 0)
                {
                    Vector3 off = w.pivot < 4 ? Bounce(w.pivot) : (Bounce(2) + Bounce(3)) * 0.5f;
                    w.t.localPosition = w.restPos + w.t.parent.InverseTransformVector(transform.TransformVector(off));
                }
            }
            foreach (var d in doors)
            {
                if (!d.t || d.byClip) continue;
                float a = d.d.openDeg * Mathf.SmoothStep(0f, 1f, d.open) * (d.d.flip ? -1f : 1f);
                Vector3 axis = d.d.hinge == DoorHinge.Side ? Vector3.up : Vector3.right;
                if (d.d.hinge == DoorHinge.Bottom) a = -a;
                d.t.localRotation = Quaternion.AngleAxis(a, axis) * d.rest;
            }
            // a two-wheeler's people lean with the steering (the motorbike pack's turn loops; Steer +38 = full right)
            if (TwoWheeler && occupant != null)
                foreach (var o in occupant)
                {
                    var rig = o ? o.GetComponentInChildren<HumanoidRig>() : null;
                    if (rig) rig.lean = Mathf.Clamp(steer / 38f, -1f, 1f);
                }
        }

        Vector3 Bounce(int i) => i < pivots.Length && pivots[i] ? pivots[i].localPosition - pivotRest[i] : Vector3.zero;

        /// <summary>The handlebar / steering wheel straight (while targets and lamps are hung on them).</summary>
        void AtRest()
        {
            foreach (var (t, rest) in bars) if (t) t.localRotation = rest;
            foreach (var (t, rest) in steerings) if (t) t.localRotation = rest;
        }

        // ------------------------------------------------------------------ doors, dents
        /// <summary>Open the door nearest seat n's way in (if the model has one), and shut it `hold` seconds later.</summary>
        public void OpenDoorFor(int seat, float hold = 1.6f)
        {
            if (doors.Count == 0) return;
            Vector3 at = HasSetup && seat >= 0 && seat < SeatCount ? setup.seats[seat].entry : Vector3.zero;
            Door best = null;
            float bd = float.MaxValue;
            foreach (var d in doors)
            {
                if (!d.t) continue;
                var r = d.t.GetComponent<Renderer>();
                Vector3 c = Frame.InverseTransformPoint(r ? r.bounds.center : d.t.position);
                float dist = (new Vector2(c.x, c.z) - new Vector2(at.x, at.z)).sqrMagnitude;
                if (dist < bd) { bd = dist; best = d; }
            }
            if (best == null) return;
            best.target = 1f;
            best.closeAt = Time.time + hold;
        }

        /// <summary>A knock at a world point: the Dmg_* shape key of that side (Front / Rear / Left / Right / Top) a bit deeper.</summary>
        public void Dent(Vector3 worldPoint, float dv)
        {
            if (dents.Count == 0) { DentMesh(worldPoint, dv); return; }
            Vector3 p = Frame.InverseTransformPoint(worldPoint);
            var info = GetComponent<VehicleInfo>();
            float hl = info ? info.length * 0.5f : 2f, hw = info ? info.width * 0.5f : 0.9f, h = info ? info.height : 1.6f;
            float fz = Mathf.Abs(p.z) / hl, fx = Mathf.Abs(p.x) / hw, fy = p.y / h;
            string side = fy > 0.85f && fy > fz && fy > fx ? "top" : fz >= fx ? (p.z > 0f ? "front" : "rear") : (p.x > 0f ? "right" : "left");
            foreach (var (r, i, s) in dents)
                if (r && s == side) r.SetBlendShapeWeight(i, Mathf.Min(100f, r.GetBlendShapeWeight(i) + dv * 9f));
        }

        const float MaxDent = 0.18f;

        /// <summary>A knock on a model without Dmg_* keys (owner, 21 Nov 2026: "takkar par hi dent" - shape keys would keep
        /// every traffic body skinned, a cost every frame on a phone): the body mesh is copied on the first knock (the FBX
        /// are Read/Write, NewVehicleImport) and pushed in around the point that was hit - wider and deeper the harder the
        /// knock, crumpled by noise, at most MaxDent at any point however often it is hit. Nothing costs until a hit.</summary>
        void DentMesh(Vector3 worldPoint, float dv)
        {
            if (!bodyMesh) return;
            if (dentVerts == null)
            {
                var src = bodyMesh.sharedMesh;
                if (!src || !src.isReadable) return;
                dentMesh = Instantiate(src);
                dentMesh.name = src.name + "_dented";
                dentMesh.MarkDynamic();
                bodyMesh.sharedMesh = dentMesh;
                dentVerts = dentMesh.vertices;
                dentMoved = new float[dentVerts.Length];
            }
            var bt = bodyMesh.transform;
            float s = Mathf.Max(1e-4f, bt.lossyScale.x);
            Vector3 p = bt.InverseTransformPoint(worldPoint);
            // the contact is on the box collider, 10-20 cm proud of the skin: dent round the skin's nearest point
            float best = float.MaxValue;
            Vector3 near = p;
            foreach (var v in dentVerts) { float d2 = (v - p).sqrMagnitude; if (d2 < best) { best = d2; near = v; } }
            p = near;
            Bounds b = dentMesh.bounds;
            // inward: toward the body's middle line at that height (front, rear, sides); straight down on the roof
            Vector3 dir = new Vector3(b.center.x, p.y, b.center.z) - p;
            bool roof = p.y > b.max.y - 0.25f / s;
            dir = roof || dir.sqrMagnitude < 1e-6f ? Vector3.down : dir.normalized;
            float r = Mathf.Clamp(0.3f + dv * 0.02f, 0.3f, 0.65f) / s;
            float depth = Mathf.Clamp(dv * 0.02f, 0.03f, 0.12f);   // a 15 m/s knock (dv ~5) ~10 cm at the centre
            bool any = false;
            for (int i = 0; i < dentVerts.Length; i++)
            {
                Vector3 v = dentVerts[i];
                float d = (v - p).magnitude;
                if (d >= r) continue;
                float f = 1f - d / r;
                f = f * f * (3f - 2f * f);
                float crumple = 0.65f + 0.7f * Mathf.PerlinNoise(v.x * 9f * s + v.z * 5f * s, v.y * 9f * s + v.z * 3f * s);
                float amt = Mathf.Min(depth * f * crumple, MaxDent - dentMoved[i]);
                if (amt <= 0f) continue;
                dentVerts[i] = v + dir * (amt / s);
                dentMoved[i] += amt;
                any = true;
            }
            if (!any) return;
            dentMesh.vertices = dentVerts;
            dentMesh.RecalculateNormals();
            int n = 0; float deep = 0f;
            foreach (var m in dentMoved) { if (m > 0.005f) n++; if (m > deep) deep = m; }
            DentedVerts = n; DentDeepest = deep;
        }

        /// <summary>How many body vertices a knock has moved more than 5 mm, and the deepest (m) - for the checks.</summary>
        public int DentedVerts { get; private set; }
        public float DentDeepest { get; private set; }

        void OnDestroy() { if (dentMesh) Destroy(dentMesh); }

        // ------------------------------------------------------------------ seats
        public VehicleSetupData.Seat SeatData(int i) => HasSetup && i >= 0 && i < SeatCount ? setup.seats[i] : null;

        /// <summary>Seat n's root in the vehicle's space (SeatParent's), or the old guess when there is no setup.</summary>
        public Vector3 SeatLocal(int i) => SeatData(i) is var s && s != null ? s.pos : Vector3.zero;

        /// <summary>Where the person in seat n stands to get in / out (world).</summary>
        public Vector3 EntryWorld(int i) => SeatData(i) is var s && s != null ? Frame.TransformPoint(s.entry) : transform.position;

        /// <summary>Where seat n's person sits and how he faces (world; what Sit places him at).</summary>
        public Vector3 SeatWorld(int i) => SeatData(i) is var s && s != null ? Frame.TransformPoint(s.pos) : transform.position;
        public Quaternion SeatRotWorld(int i) => SeatData(i) is var s && s != null ? Frame.rotation * Quaternion.Euler(s.euler) : transform.rotation;
        public SeatPose PoseOf(int i) => SeatData(i) is var s && s != null ? s.pose : SeatPose.Sit;
        /// <summary>It has doors that open (a car, a bus; an auto / e-rickshaw has none): getting in is the "Entering Car" clip.</summary>
        public bool HasDoors => doors.Count > 0;

        /// <summary>The old two-seat numbering (0 driver, 1 front passenger: VehicleSeats, PoliceCar) as a setup seat: the
        /// passenger is the seat beside the driver (nearest to him along the vehicle, the other side).</summary>
        public int SeatFor(int legacy)
        {
            if (!HasSetup || legacy <= 0) return 0;
            var d = setup.seats[0];
            int best = Mathf.Min(1, SeatCount - 1);
            float bd = float.MaxValue;
            for (int i = 1; i < SeatCount; i++)
            {
                var s = setup.seats[i];
                float score = Mathf.Abs(s.pos.z - d.pos.z) + (Mathf.Sign(s.pos.x) == Mathf.Sign(d.pos.x) && Mathf.Abs(d.pos.x) > 0.05f ? 1f : 0f);
                if (score < bd) { bd = score; best = i; }
            }
            return best;
        }

        public Transform Occupant(int i) => occupant != null && i >= 0 && i < occupant.Length ? occupant[i] : null;

        /// <summary>The seat this person sits on, -1 if none.</summary>
        public int SeatOf(Transform person)
        {
            if (occupant != null && person) for (int i = 0; i < occupant.Length; i++) if (occupant[i] == person) return i;
            return -1;
        }

        /// <summary>Seat n's way out (world, on the setup's entry point; the other side when something stands there) and
        /// which way the person faces once out. Opens the door on that side.</summary>
        public Vector3 WayOut(int seat, out Vector3 face)
        {
            var s = SeatData(seat);
            Vector3 seatW = s != null ? Frame.TransformPoint(s.pos) : transform.position;
            Vector3 e = EntryWorld(seat);
            if (s != null && Physics.CheckCapsule(e + Vector3.up * 0.5f, e + Vector3.up * 1.5f, 0.3f, ~((1 << 9) | (1 << 2) | (1 << 8) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore))
            {
                Vector3 other = Frame.TransformPoint(new Vector3(-s.entry.x, s.entry.y, s.entry.z));
                if (!Physics.CheckCapsule(other + Vector3.up * 0.5f, other + Vector3.up * 1.5f, 0.3f, ~((1 << 9) | (1 << 2) | (1 << 8) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore)) e = other;
            }
            face = e - seatW;
            face.y = 0f;
            if (face.sqrMagnitude < 0.01f) face = transform.right;
            face.Normalize();
            OpenDoorFor(seat);
            return e;
        }

        /// <summary>A free seat other than the driver's, front rows first with a little shuffle; -1 if full.</summary>
        public int FreePassengerSeat()
        {
            if (!HasSetup) return -1;
            int n = SeatCount, pick = -1;
            float best = float.MaxValue;
            for (int i = 1; i < n; i++)
            {
                if (occupant[i]) continue;
                float score = i + Random.Range(0f, 3f);
                if (score < best) { best = score; pick = i; }
            }
            return pick;
        }

        /// <summary>Seat a person (its root; the HumanoidRig under it) on seat n: pose, place, head under the roof, hands on
        /// the wheel / grips and feet on the floor / pegs (HandIK, added if missing).</summary>
        public HandIK Sit(Transform person, HumanoidRig rig, int i)
        {
            var s = SeatData(i);
            if (s == null || !person) return null;
            person.SetParent(SeatParent, false);
            person.localPosition = SeatParent.InverseTransformPoint(Frame.TransformPoint(s.pos));
            person.localRotation = Quaternion.Inverse(SeatParent.rotation) * Frame.rotation * Quaternion.Euler(s.euler);
            VehicleSetup.Pose(rig, s.pose);
            if (rig) rig.seatRoof = s.roof >= 0f ? s.roof - s.pos.y : float.NaN;
            Targets(i);
            var ik = person.GetComponent<HandIK>();
            if (!ik) ik = person.gameObject.AddComponent<HandIK>();
            ik.vehicle = SeatParent;
            ik.maxBend = VehicleSetup.DefaultBend(s);
            ik.weight = 1f;
            bool hands = s.hands && (s.pose == SeatPose.Drive || s.pose == SeatPose.Ride);
            ik.left = hands ? handL[i] : null;
            ik.right = hands ? handR[i] : null;
            ik.footLeft = s.feet ? footL[i] : null;
            ik.footRight = s.feet ? footR[i] : null;
            ik.enabled = true;
            occupant[i] = person;
            return ik;
        }

        /// <summary>The person leaves its seat (still parented: the caller moves it out): hands and feet free, no roof.</summary>
        public void Leave(Transform person)
        {
            if (!person || occupant == null) return;
            for (int i = 0; i < occupant.Length; i++) if (occupant[i] == person) occupant[i] = null;
            var ik = person.GetComponent<HandIK>();
            if (ik) { ik.left = ik.right = ik.footLeft = ik.footRight = null; ik.maxBend = -1f; ik.enabled = false; }
            var rig = person.GetComponentInChildren<HumanoidRig>(true);
            if (rig) { rig.seatRoof = float.NaN; rig.pillion = false; rig.lean = 0f; }
        }

        /// <summary>Seat n's wrist and ankle targets, made once: the wrists on the steering part (they turn with it), the
        /// ankles on the vehicle (a two-wheeler's leaning body), turned like the seat.</summary>
        void Targets(int i)
        {
            if (footL[i]) return;
            var s = setup.seats[i];
            AtRest();
            Transform on = SeatParent;
            if (!string.IsNullOrEmpty(s.handsOn))
                foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (t.name == s.handsOn) { on = t; break; }
            handL[i] = Point("Hand L " + i, on, Frame.TransformPoint(s.handL));
            handR[i] = Point("Hand R " + i, on, Frame.TransformPoint(s.handR));
            footL[i] = Point("Foot L " + i, SeatParent, Frame.TransformPoint(s.footL));
            footR[i] = Point("Foot R " + i, SeatParent, Frame.TransformPoint(s.footR));
            footL[i].rotation = footR[i].rotation = Frame.rotation * Quaternion.Euler(s.euler);
        }

        static Transform Point(string name, Transform parent, Vector3 world)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = world;
            return t;
        }

        // ------------------------------------------------------------------ lamps
        /// <summary>The setup's head and tail lamps as glowing two-sided quads (off until VehicleLamps / VehicleFX light
        /// them): one mesh per part they hang on (the body, or the handlebar so a bike's headlight turns with it).</summary>
        public void BuildLamps(Material head, Material tail, out Renderer[] heads, out Renderer[] tails)
        {
            heads = tails = new Renderer[0];
            if (setup == null || !head || !tail) return;
            AtRest();
            heads = LampSet(setup.head, head, "Lamps");
            tails = LampSet(setup.tail, tail, "Tail");
        }

        Renderer[] LampSet(VehicleSetupData.Lamp[] lamps, Material mat, string name)
        {
            var byPart = new Dictionary<Transform, List<(Vector3 p, Quaternion r, Vector2 s)>>();
            void Add(Transform on, Vector3 p, Quaternion r, Vector2 s)
            {
                if (!byPart.TryGetValue(on, out var list)) byPart[on] = list = new List<(Vector3, Quaternion, Vector2)>();
                list.Add((p, r, s));
            }
            foreach (var l in lamps)
            {
                if (l == null) continue;
                Transform on = SeatParent;
                if (!string.IsNullOrEmpty(l.on))
                    foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (t.name == l.on) { on = t; break; }
                Add(on, l.pos, Quaternion.Euler(l.euler), l.size);
                if (l.mirror) Add(on, new Vector3(-l.pos.x, l.pos.y, l.pos.z), Quaternion.Euler(l.euler.x, -l.euler.y, -l.euler.z), l.size);
            }
            var list2 = new List<Renderer>();
            foreach (var kv in byPart)
            {
                var verts = new List<Vector3>();
                var tris = new List<int>();
                foreach (var (p, r, s) in kv.Value)
                {
                    // the quad in vehicle space, then into the part's own space
                    Vector3 ax = r * Vector3.right * (s.x * 0.5f), ay = r * Vector3.up * (s.y * 0.5f);
                    int b = verts.Count;
                    foreach (var c in new[] { p - ax - ay, p + ax - ay, p + ax + ay, p - ax + ay })
                        verts.Add(kv.Key.InverseTransformPoint(Frame.TransformPoint(c)));
                    tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 });   // both faces
                }
                var mesh = new Mesh { name = model + "_" + name };
                if (!Application.isPlaying) mesh.hideFlags = HideFlags.DontSave;   // the parked cars' edit-mode preview
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(name) { layer = gameObject.layer };
                go.transform.SetParent(kv.Key, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.enabled = false;
                list2.Add(mr);
            }
            return list2.ToArray();
        }

        static Material amber;

        /// <summary>The indicators (owner, 21 Nov 2026: "turn hogi to indicator degi pehle"): small amber two-sided quads
        /// just outside the first head lamp (or pair) and the first tail lamp, on the part each hangs on (a bike's front
        /// pair turns with its handlebar); a single centre lamp (a two-wheeler's) gets one each side of it. Off until
        /// VehicleLamps blinks them. `head` is the head lamp material the amber copies.</summary>
        public void BuildIndicators(Material head, out Renderer[] left, out Renderer[] right)
        {
            left = right = new Renderer[0];
            if (setup == null || !head) return;
            if (!amber) { amber = new Material(head) { name = "M_LampIndicator" }; amber.SetColor("_BaseColor", new Color(5f, 1.9f, 0.08f)); }
            AtRest();
            var L = new Dictionary<Transform, List<(Vector3 p, Quaternion r, Vector2 s)>>();
            var R = new Dictionary<Transform, List<(Vector3 p, Quaternion r, Vector2 s)>>();
            void Add(Dictionary<Transform, List<(Vector3, Quaternion, Vector2)>> d, Transform on, Vector3 p, Quaternion r, Vector2 s)
            {
                if (!d.TryGetValue(on, out var list)) d[on] = list = new List<(Vector3, Quaternion, Vector2)>();
                list.Add((p, r, s));
            }
            foreach (var set in new[] { setup.head, setup.tail })
            {
                if (set == null || set.Length == 0 || set[0] == null) continue;
                var l = set[0];
                Transform on = SeatParent;
                if (!string.IsNullOrEmpty(l.on))
                    foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (t.name == l.on) { on = t; break; }
                Vector2 s = TwoWheeler ? new Vector2(0.05f, 0.04f) : new Vector2(0.075f, 0.05f);
                var rot = Quaternion.Euler(l.euler);
                var mir = Quaternion.Euler(l.euler.x, -l.euler.y, -l.euler.z);
                if (l.mirror)
                {
                    float ax = Mathf.Abs(l.pos.x) + l.size.x * 0.5f + 0.045f;
                    bool origLeft = l.pos.x < 0f;
                    Add(L, on, new Vector3(-ax, l.pos.y, l.pos.z), origLeft ? rot : mir, s);
                    Add(R, on, new Vector3(ax, l.pos.y, l.pos.z), origLeft ? mir : rot, s);
                }
                else
                {
                    float dx = l.size.x * 0.5f + 0.09f;
                    Add(L, on, new Vector3(l.pos.x - dx, l.pos.y, l.pos.z), rot, s);
                    Add(R, on, new Vector3(l.pos.x + dx, l.pos.y, l.pos.z), rot, s);
                }
            }
            left = QuadSets(L, amber, "IndicatorL");
            right = QuadSets(R, amber, "IndicatorR");
        }

        /// <summary>Two-sided quads (vehicle space) as one mesh per part they hang on, renderers off.</summary>
        Renderer[] QuadSets(Dictionary<Transform, List<(Vector3 p, Quaternion r, Vector2 s)>> byPart, Material mat, string name)
        {
            var list2 = new List<Renderer>();
            foreach (var kv in byPart)
            {
                var verts = new List<Vector3>();
                var tris = new List<int>();
                foreach (var (p, r, s) in kv.Value)
                {
                    Vector3 ax = r * Vector3.right * (s.x * 0.5f), ay = r * Vector3.up * (s.y * 0.5f);
                    int b = verts.Count;
                    foreach (var c in new[] { p - ax - ay, p + ax - ay, p + ax + ay, p - ax + ay })
                        verts.Add(kv.Key.InverseTransformPoint(Frame.TransformPoint(c)));
                    tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 });
                }
                var mesh = new Mesh { name = model + "_" + name };
                if (!Application.isPlaying) mesh.hideFlags = HideFlags.DontSave;
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(name) { layer = gameObject.layer };
                go.transform.SetParent(kv.Key, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.enabled = false;
                list2.Add(mr);
            }
            return list2.ToArray();
        }

        /// <summary>Where the first head lamp pair is (vehicle space; for the driven car's real spot lights).</summary>
        public bool HeadLampPoints(out Vector3 left, out Vector3 right)
        {
            left = right = Vector3.zero;
            if (setup == null || setup.head.Length == 0) return false;
            var l = setup.head[0];
            Vector3 p = transform.InverseTransformPoint(Frame.TransformPoint(l.pos));
            left = l.mirror ? new Vector3(-Mathf.Abs(p.x), p.y, p.z) : p;
            right = l.mirror ? new Vector3(Mathf.Abs(p.x), p.y, p.z) : p;
            return true;
        }
    }
}
