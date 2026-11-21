using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    public enum AnimalKind { Cow, Dog, Cat }

    /// <summary>
    /// A street cow, dog or cat (free-roam batch #8, owner 21 Nov 2026: "Tripo models, rigged by us; they cannot be hurt -
    /// touched they run off very fast; cows on the road stop the traffic"). Spawned and pooled by AnimalManager.
    ///
    /// Wanders round its spot with ground following (CharacterMotor on a CharacterController, layer 9 like the townsfolk,
    /// so every vehicle's obstacle cast already stops for it), plays its clips (legacy Animation: idle / walk / run and a
    /// special idle - a cow chews, a dog sits, a cat sits or licks). No Health: nothing can hurt it. Touched by the player
    /// (walking into it, a punch's reach) or by any vehicle (a knock: CarController.People gives it an impulse, or a body
    /// within reach) it flees fast for a few seconds: a cow plods off at a trot, a dog bolts (and may bark), a cat dashes and
    /// hops onto a wall when one is near. Dogs and cats also get out of a moving vehicle's way; a cow does not: it stands in
    /// the road and the traffic waits and honks (AnimalManager.CowAhead, the TrafficCar hook) until it ambles off.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class Animal : MonoBehaviour
    {
        public AnimalKind kind;
        public Vector3 home;
        public float homeRadius = 10f;
        [System.NonSerialized] public bool pooled;

        public static readonly List<Animal> Live = new List<Animal>();

        /// <summary>Per kind: speeds (m/s), the clips' own speeds (what the Blender script keyed: Tools/animals/rig_animal.py
        /// prints them), body size, how fast it turns and how long it flees.</summary>
        public class Spec
        {
            public float walk, run, flee, clipWalk, clipRun, accel, turn, length, height, radius, fleeMin, fleeMax;
            public string special1, special2;   // its idle extras (clip keys)
        }

        public static readonly Spec CowSpec = new Spec
        {
            walk = 0.9f, run = 3.4f, flee = 3.4f, clipWalk = 1.0f, clipRun = 3.0f, accel = 3f, turn = 110f,
            length = 2.2f, height = 1.45f, radius = 0.42f, fleeMin = 4f, fleeMax = 6f, special1 = "chew", special2 = "chew",
        };
        public static readonly Spec DogSpec = new Spec
        {
            walk = 1.0f, run = 6.5f, flee = 8f, clipWalk = 0.95f, clipRun = 5.9f, accel = 16f, turn = 420f,
            length = 0.95f, height = 0.62f, radius = 0.2f, fleeMin = 3f, fleeMax = 5f, special1 = "sit", special2 = "sit",
        };
        public static readonly Spec CatSpec = new Spec
        {
            walk = 0.6f, run = 5f, flee = 6.5f, clipWalk = 0.57f, clipRun = 3.5f, accel = 20f, turn = 540f,
            length = 0.5f, height = 0.3f, radius = 0.13f, fleeMin = 2f, fleeMax = 3.2f, special1 = "sit", special2 = "lick",
        };
        public static Spec SpecOf(AnimalKind k) => k == AnimalKind.Cow ? CowSpec : k == AnimalKind.Dog ? DogSpec : CatSpec;

        enum State { Idle, Special, Walk, Flee, MoveOff, Hop, Perch }

        public Spec spec;
        CharacterMotor motor;
        Animation anim;
        readonly Dictionary<string, string> clips = new Dictionary<string, string>();
        string playing;
        State state;
        float stateT, scanT, barkCd, mooCd, lastImpulse, waitedAt = -99f, waitedT, moveOffAfter, stuckT, hopT;
        string special;
        Vector3 target, dir, hopFrom, hopTo;
        bool onWall;

        static int wallMask = -1;
        static readonly Collider[] near = new Collider[12];
        static readonly string[] Keys = { "idle", "walk", "run", "chew", "sit", "lick" };

        public bool Fleeing => state == State.Flee || state == State.Hop;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            if (wallMask < 0) wallMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
        }

        void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        void OnDisable() => Live.Remove(this);

        /// <summary>Fresh from the pool at its spot (AnimalManager).</summary>
        public void Begin(AnimalKind k, Vector3 at, float radius)
        {
            kind = k;
            spec = SpecOf(k);
            home = at;
            homeRadius = radius;
            onWall = false;
            waitedT = 0f;
            waitedAt = -99f;
            stuckT = 0f;
            barkCd = Random.Range(4f, 12f);
            mooCd = Random.Range(10f, 30f);
            if (!anim) SetupAnim();
            lastImpulse = 0f;
            if (motor) { motor.velocity = Vector3.zero; motor.impulse = Vector3.zero; if (motor.Controller) motor.Controller.enabled = true; }
            Idle(Random.Range(0.5f, 4f));
            // a third start in their special idle (a cow chewing in the road, a dog / cat sitting)
            if (Random.value < 0.35f) Special(Random.Range(6f, 16f));
        }

        // ------------------------------------------------------------------ animation (legacy clips of the FBX)
        void SetupAnim()
        {
            anim = GetComponentInChildren<Animation>(true);
            clips.Clear();
            if (!anim) return;
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.BasedOnRenderers;
            foreach (AnimationState s in anim)
            {
                string n = s.name.ToLowerInvariant();
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                s.wrapMode = WrapMode.Loop;
                foreach (var k in Keys)
                    if (n == k || (!clips.ContainsKey(k) && n.EndsWith(k))) clips[k] = s.name;
            }
        }

        void Play(string key, float speed)
        {
            if (!anim) return;
            if (!clips.TryGetValue(key, out var name) && !clips.TryGetValue("idle", out name)) return;
            if (playing != name) { anim.CrossFade(name, key == "run" || playing == null ? 0.18f : 0.3f); playing = name; }
            var st = anim[name];
            if (st != null) st.speed = speed;
        }

        // ------------------------------------------------------------------ states
        void Idle(float t) { state = State.Idle; stateT = t; }

        void Special(float t)
        {
            state = State.Special;
            stateT = t;
            special = Random.value < 0.5f ? spec.special1 : spec.special2;
        }

        void WalkTo(Vector3 p) { state = State.Walk; target = p; stateT = 25f; stuckT = 0f; }

        /// <summary>Run off from this point (a touch, a knock, a car about to hit it).</summary>
        public void Flee(Vector3 from, float seconds = -1f)
        {
            if (state == State.Hop) return;
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            dir = away.normalized;
            bool fresh = state != State.Flee;
            state = State.Flee;
            stateT = seconds > 0f ? seconds : Random.Range(spec.fleeMin, spec.fleeMax);
            onWall = false;
            if (fresh) Startled();
            if (kind == AnimalKind.Cat && fresh && TryHop(dir)) return;
        }

        /// <summary>A car waits on this cow in the road (AnimalManager.CowAhead, every physics step it waits): after a few
        /// seconds of horns it ambles off to the side it is already nearer.</summary>
        public void WaitedOn(Transform car)
        {
            if (!car) return;
            if (Time.time - waitedAt > 1.5f) { waitedT = 0f; moveOffAfter = Random.Range(3f, 9f); }
            waitedAt = Time.time;
            if (state == State.Flee || state == State.MoveOff || state == State.Hop) return;
            if (waitedT > 2f && mooCd <= 0f && Random.value < 0.5f) { Sound("cow", 0.55f); mooCd = Random.Range(15f, 35f); }
            if (waitedT < moveOffAfter) return;
            Vector3 rel = transform.position - car.position;
            Vector3 side = car.right * (Vector3.Dot(rel, car.right) >= 0f ? 1f : -1f);
            side.y = 0f;
            target = transform.position + side.normalized * Random.Range(3.5f, 5f) + car.forward * Random.Range(-1f, 2f);
            state = State.MoveOff;
            stateT = 12f;
            stuckT = 0f;
        }

        /// <summary>A horn close by (the player's car / bike, GameEvents.Horn): a cow in front moves off sooner, a dog
        /// may bark, a cat close by bolts.</summary>
        public void HornNear(Vector3 at, Vector3 fwd)
        {
            Vector3 rel = transform.position - at;
            rel.y = 0f;
            float d = rel.magnitude;
            if (kind == AnimalKind.Cow) { if (d < 22f && Vector3.Dot(rel, fwd) > 0f) { waitedAt = Time.time; waitedT = Mathf.Max(waitedT, 2f); moveOffAfter = Mathf.Min(moveOffAfter, Random.Range(1f, 3f)); } }
            else if (kind == AnimalKind.Dog) { if (d < 18f && barkCd <= 0f && Random.value < 0.5f) Bark(); }
            else if (d < 8f) Flee(at);
        }

        void Startled()
        {
            if (kind == AnimalKind.Dog && Random.value < 0.6f) Bark();
            else if (kind == AnimalKind.Cow && mooCd <= 0f && Random.value < 0.5f) { Sound("cow", 0.6f); mooCd = Random.Range(12f, 30f); }
            AnimalManager.Touches++;
        }

        void Bark()
        {
            barkCd = Random.Range(6f, 16f);
            Sound("dog_" + Random.Range(1, 4), 0.55f);
        }

        void Sound(string clip, float vol)
        {
            var au = AudioManager.Instance;
            if (!au) return;
            float pitch = kind == AnimalKind.Dog ? Random.Range(0.95f, 1.12f) : Random.Range(0.92f, 1.05f);
            au.PlayAt(au.Clip(clip), transform.position + Vector3.up * spec.height * 0.7f, vol, pitch, 70f);
        }

        // ------------------------------------------------------------------ senses
        /// <summary>The body as a segment along its length (a cow is 2.2 m long, its capsule only 0.84 m round).</summary>
        void Body(out Vector3 a, out Vector3 b)
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            f.Normalize();
            float h = Mathf.Max(0f, spec.length * 0.5f - spec.radius);
            Vector3 c = transform.position + Vector3.up * spec.height * 0.5f;
            a = c + f * h;
            b = c - f * h;
        }

        static float SegDist2D(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
            Vector2 ab = B - A;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(P, A + ab * t);
        }

        void Scan()
        {
            Body(out var a, out var b);
            Vector3 c = (a + b) * 0.5f;

            // knocked (a car through it, a police bike, anything that pushes a character): the impulse jumps
            float imp = motor.impulse.magnitude;
            if (imp > lastImpulse + 0.8f) Flee(transform.position - motor.impulse);
            lastImpulse = imp;

            // the player on foot walking into it / swinging at it
            var p = PlayerController.Instance;
            if (p && p.CurrentMode == PlayerController.Mode.Normal)
            {
                Vector3 pp = p.transform.position;
                float dy = pp.y - transform.position.y;
                if (dy > -1f && dy < spec.height + 1.8f)
                {
                    float d = SegDist2D(pp, a, b);
                    if (d < spec.radius + 0.55f) Flee(pp);
                    else if (kind != AnimalKind.Cow && d < 3.2f && GameInput.SprintHeld && GameInput.Move.sqrMagnitude > 0.1f) Flee(pp);   // a runner close by
                    else if (kind == AnimalKind.Dog && d < 6f && barkCd <= 0f && state != State.Flee)
                    {
                        if (Random.value < (TownClock.Hour < 6f || TownClock.Hour > 21f ? 0.6f : 0.3f)) Bark();
                        else barkCd = Random.Range(4f, 9f);
                    }
                }
            }

            // vehicles: a body within reach = a touch; a moving one about to reach a dog / cat = out of its way
            int n = Physics.OverlapSphereNonAlloc(c, spec.length * 0.5f + 16f, near, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = near[i];
                var rb = col.attachedRigidbody;
                Transform car = rb ? rb.transform : col.transform;
                Vector3 cp = col is MeshCollider mc && !mc.convex ? col.bounds.ClosestPoint(c) : col.ClosestPoint(c);
                if (SegDist2D(cp, a, b) < spec.radius + 0.3f && Mathf.Abs(cp.y - c.y) < spec.height + 1f)
                {
                    Vector3 from = cp;
                    Vector3 v = VelocityOf(car, rb);
                    if (v.sqrMagnitude > 1f)   // away to the side of its path, not down the road in front of it
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, v.normalized);
                        from = transform.position - side * Mathf.Sign(Vector3.Dot(transform.position - car.position, side) + 0.001f) - v.normalized * 0.5f;
                    }
                    Flee(from);
                    return;
                }
                if (kind == AnimalKind.Cow || state == State.Flee || state == State.Hop) continue;
                Vector3 vel = VelocityOf(car, rb);
                vel.y = 0f;
                float sp = vel.magnitude;
                if (sp < 2f) continue;
                Vector3 fwd = vel / sp;
                Vector3 rel = transform.position - car.position;
                rel.y = 0f;
                float ahead = Vector3.Dot(rel, fwd);
                if (ahead < -1f || ahead > 4f + sp * 1.8f) continue;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                float lat = Vector3.Dot(rel, right);
                var info = car.GetComponent<VehicleInfo>();
                if (Mathf.Abs(lat) > (info ? info.width : 1.8f) * 0.5f + 1f) continue;
                Flee(transform.position - right * (lat >= 0f ? 1f : -1f), Random.Range(1.2f, 2f));
                return;
            }
        }

        static Vector3 VelocityOf(Transform car, Rigidbody rb)
        {
            var tc = car.GetComponent<TrafficCar>();
            if (tc) return car.forward * tc.Speed;
            var br = car.GetComponent<BikeRide>();
            if (br) return br.Forward * br.Speed;
            return rb && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
        }

        /// <summary>Clear ground ahead: no wall, no big drop, not the lake.</summary>
        bool Clear(Vector3 d, float dist)
        {
            Vector3 o = transform.position + Vector3.up * Mathf.Max(0.25f, spec.height * 0.55f);
            if (Physics.SphereCast(o, spec.radius * 0.8f, d, out var h, dist, wallMask, QueryTriggerInteraction.Ignore) && h.normal.y < 0.6f) return false;
            Vector3 q = transform.position + d * dist + Vector3.up * 1.5f;
            if (!Physics.Raycast(q, Vector3.down, out var g, 5f, wallMask, QueryTriggerInteraction.Ignore)) return false;   // a drop
            if (g.point.y < LakeArea.WaterY + 0.15f) return false;
            return true;
        }

        /// <summary>The way to go, bent round walls / drops: straight, else 50 / 100 / 140 degrees either side.</summary>
        Vector3 Steer(Vector3 want, float dist)
        {
            if (Clear(want, dist)) return want;
            float s = Random.value < 0.5f ? 1f : -1f;
            foreach (float ang in new[] { 50f, 100f, 140f })
                for (int k = 0; k < 2; k++)
                {
                    Vector3 d = Quaternion.Euler(0f, ang * (k == 0 ? s : -s), 0f) * want;
                    if (Clear(d, dist)) return d;
                }
            return -want;
        }

        /// <summary>A cat in a hurry: a wall top 0.45-2.3 m up within 2.5 m (ahead or 50 degrees either side) - up it goes.</summary>
        bool TryHop(Vector3 d0)
        {
            Vector3 pos = transform.position;
            for (int k = 0; k < 3; k++)
            {
                Vector3 d = k == 0 ? d0 : Quaternion.Euler(0f, k == 1 ? 50f : -50f, 0f) * d0;
                if (!Physics.Raycast(pos + Vector3.up * 0.25f, d, out var w, 2.5f, wallMask, QueryTriggerInteraction.Ignore) || w.normal.y > 0.5f) continue;
                Vector3 face = -new Vector3(w.normal.x, 0f, w.normal.z).normalized;
                foreach (float inset in new[] { 0.3f, 0.15f })
                {
                    Vector3 over = w.point + face * inset;
                    if (!Physics.Raycast(over + Vector3.up * 2.6f, Vector3.down, out var top, 2.6f, wallMask, QueryTriggerInteraction.Ignore)) continue;
                    float rise = top.point.y - pos.y;
                    if (top.normal.y < 0.8f || rise < 0.45f || rise > 2.3f) continue;
                    if (Physics.CheckSphere(top.point + Vector3.up * 0.32f, 0.16f, wallMask, QueryTriggerInteraction.Ignore)) continue;
                    hopFrom = pos;
                    hopTo = top.point + Vector3.up * 0.02f;
                    hopT = 0f;
                    state = State.Hop;
                    var cc = motor.Controller;
                    if (cc) cc.enabled = false;
                    AnimalManager.Hops++;
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            if (spec == null) spec = SpecOf(kind);
            barkCd -= dt;
            mooCd -= dt;
            if (Time.time - waitedAt < 0.4f) waitedT += dt;

            if (state == State.Hop) { Hop(dt); return; }
            scanT -= dt;
            if (scanT <= 0f) { scanT = 0.15f; Scan(); }
            if (state == State.Hop) return;

            Vector3 desired = Vector3.zero;
            Vector3 face = Vector3.zero;
            stateT -= dt;
            switch (state)
            {
                case State.Idle:
                case State.Perch:
                    if (stateT <= 0f) Next();
                    break;
                case State.Special:
                    if (stateT <= 0f) Next();
                    break;
                case State.Walk:
                case State.MoveOff:
                {
                    Vector3 to = target - transform.position;
                    to.y = 0f;
                    if (to.magnitude < Mathf.Max(0.4f, spec.radius * 1.5f) || stateT <= 0f)
                    {
                        if (state == State.MoveOff) { home = transform.position; waitedT = 0f; }
                        Idle(Random.Range(2f, 7f));
                        break;
                    }
                    Vector3 d = Steer(to.normalized, Mathf.Max(1f, spec.length));
                    desired = d * spec.walk * (state == State.MoveOff ? 1.25f : 1f);
                    face = d;
                    stuckT = motor.HorizontalVelocity.magnitude < spec.walk * 0.25f ? stuckT + dt : 0f;
                    if (stuckT > 1.5f) Idle(Random.Range(1f, 3f));
                    break;
                }
                case State.Flee:
                {
                    dir = Steer(dir, Mathf.Max(1.5f, spec.flee * 0.45f));
                    desired = dir * spec.flee;
                    face = dir;
                    if (stateT <= 0f)
                    {
                        home = transform.position;   // it settles where it ran to, not back by the danger
                        Idle(Random.Range(2f, 5f));
                    }
                    break;
                }
            }
            if (onWall && state != State.Perch && state != State.Idle) onWall = false;

            motor.Move(desired, state == State.Flee ? spec.accel * 1.5f : spec.accel, dt);
            if (face.sqrMagnitude > 0.01f) motor.FaceTowards(face, state == State.Flee ? spec.turn * 1.6f : spec.turn, dt);

            // clips: by what it does and how fast it really goes
            float v = motor.HorizontalVelocity.magnitude;
            if (state == State.Special || state == State.Perch) Play(state == State.Perch ? "sit" : special, 1f);
            else if (v < 0.12f) Play("idle", 1f);
            else if (v < (spec.walk + spec.run) * 0.5f && state != State.Flee) Play("walk", Mathf.Clamp(v / spec.clipWalk, 0.5f, 1.8f));
            else Play("run", Mathf.Clamp(v / spec.clipRun, 0.6f, 1.7f));
        }

        /// <summary>What next after standing a while: wander a few metres round its spot, its special idle, or stand on.</summary>
        void Next()
        {
            if (onWall) { state = State.Perch; stateT = Random.Range(8f, 20f); if (Random.value < 0.3f) onWall = false; return; }
            float r = Random.value;
            if (r < 0.5f)
            {
                Vector2 o = Random.insideUnitCircle * homeRadius;
                Vector3 p = home + new Vector3(o.x, 0f, o.y);
                if ((p - transform.position).sqrMagnitude < 1f) p = transform.position + transform.forward * 2f;
                WalkTo(p);
            }
            else if (r < 0.8f) Special(Random.Range(5f, 15f));
            else Idle(Random.Range(2f, 6f));
        }

        void Hop(float dt)
        {
            hopT += dt / 0.5f;
            float t = Mathf.Clamp01(hopT);
            float rise = Mathf.Max(0f, hopTo.y - hopFrom.y);
            Vector3 p = Vector3.Lerp(hopFrom, hopTo, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * (0.35f + rise * 0.3f);
            transform.position = p;
            Vector3 d = hopTo - hopFrom;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 900f * dt);
            Play("run", 1.4f);
            if (t < 1f) return;
            motor.Teleport(hopTo);
            if (motor.Controller) motor.Controller.enabled = true;   // (Teleport keeps it as it was: off for the hop)
            home = hopTo;
            homeRadius = 0.6f;
            onWall = true;
            state = State.Perch;
            stateT = Random.Range(10f, 25f);
        }
    }
}
