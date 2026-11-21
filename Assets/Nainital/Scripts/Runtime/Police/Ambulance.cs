using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The 108 ambulance (Ambulance108 model, red-blue light bar, its own wailing siren): drives to an accident along the
    /// roads (RouteDriver), stops as close as the road (or the held-up traffic) lets it, and two of its crew carry each
    /// body to it on a stretcher: out of the back, to the body, lift, back, in. Then it drives off towards the hospital
    /// with the siren on and is gone once out of sight. A crewman killed (by the player): the other gets back in and it
    /// leaves; what is left is cleared later out of sight (IncidentManager).
    /// </summary>
    [RequireComponent(typeof(RouteDriver))]
    public class Ambulance : MonoBehaviour
    {
        public IncidentManager.Incident incident;
        public EmergencyLights lights;
        public bool Leaving => mode == Mode.Leave;
        public string Phase => mode + " " + phase + (body ? " body" : "");

        enum Mode { Drive, Work, Leave }
        Mode mode;
        RouteDriver drive;
        VehicleInfo info;
        Medic front, back;
        Transform stretcher;
        Casualty body;
        int phase;
        float modeT, phaseT, nearSlowT;
        Vector3 sPos, parkGoal;
        float sYaw;
        const float StretcherSpeed = 1.5f, Top = 0.74f;

        // the phone's 108 call for the player (EmergencyCalls, 22 Nov 2026): one medic walks to the player, dresses the
        // wounds (health back to full) and the ambulance goes
        bool forPlayer;
        Medic helper;
        Vector3 hPos;
        int hPhase;
        float hT;
        Vector3 Target => forPlayer ? (PlayerController.Instance ? PlayerController.Instance.transform.position : transform.position) : incident.pos;

        public void BeginForPlayer(Vector3 at)
        {
            forPlayer = true;
            Begin(new IncidentManager.Incident { pos = at });
        }

        public void Begin(IncidentManager.Incident inc)
        {
            incident = inc;
            drive = GetComponent<RouteDriver>();
            info = GetComponent<VehicleInfo>();
            drive.Begin();
            drive.cruise = 14f;
            drive.offRoad = false;
            drive.ram = false;
            drive.arriveDist = 6f;
            int k = RoadRoute.ReadyAny() ? RoadRoute.Nearest(inc.pos, 120f) : -1;
            parkGoal = k >= 0 ? RoadRoute.Pos(k) : inc.pos;
            drive.SetGoal(parkGoal, true);
            mode = Mode.Drive;
            if (lights) lights.lights = lights.siren = true;
        }

        float L => info ? info.length : 5.2f;
        Vector3 Rear => Ground(transform.position - transform.forward * (L * 0.5f + 1.5f));

        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 2f, p.z), Vector3.down, out var hit, 6f,
                    ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, Mathf.Max(p.y, top), p.z);
        }

        void Update()
        {
            if (!drive) return;
            float dt = Time.deltaTime;
            modeT += dt;
            switch (mode)
            {
                case Mode.Drive: Driving(dt); break;
                case Mode.Work: if (forPlayer) Treating(dt); else Working(dt); break;
                case Mode.Leave: Gone(); break;
            }
        }

        void Driving(float dt)
        {
            if (forPlayer && modeT > 2f && Mathf.FloorToInt(modeT) % 5 == 0 && drive.HasGoal)
            {
                // the player moved: follow to the road nearest them now
                int kk = RoadRoute.ReadyAny() ? RoadRoute.Nearest(Target, 120f) : -1;
                Vector3 g = kk >= 0 ? RoadRoute.Pos(kk) : Target;
                if ((g - parkGoal).sqrMagnitude > 20f * 20f) { parkGoal = g; drive.SetGoal(g, true); }
            }
            float toInc = Vector3.Distance(transform.position, Target);
            if (Mathf.Abs(drive.Speed) < 0.6f && toInc < 45f) nearSlowT += dt; else nearSlowT = 0f;
            bool there = drive.Arrived || toInc < 14f || nearSlowT > 2.5f || (modeT > 150f && toInc < 80f);
            if (!there) return;
            // parked: lights stay on, the siren stops, the crew comes out of the back with the stretcher
            drive.stop = true;
            if (lights) lights.siren = false;
            mode = Mode.Work;
            modeT = 0f;
            var pm = PoliceManager.Instance;
            Vector3 r = Rear;
            if (forPlayer)
            {
                hPos = r + transform.right * 0.8f;
                helper = pm.MakeMedic(hPos);
                hPhase = 0;
                hT = 0f;
                return;
            }
            front = pm.MakeMedic(r + transform.right * 0.6f);
            back = pm.MakeMedic(r - transform.right * 0.6f);
            stretcher = MakeStretcher();
            sPos = r;
            sYaw = Quaternion.LookRotation(-transform.forward).eulerAngles.y;
            PlaceStretcher();
            phase = 0;
        }

        void Working(float dt)
        {
            phaseT += dt;
            if (!front || !back || front.Dead || back.Dead)
            {
                // a crewman is down: whoever can gets back in, and off they go
                if (body) { body.transform.SetParent(null, true); body.onStretcher = false; body = null; }
                if (front && !front.Dead) Destroy(front.gameObject);
                if (back && !back.Dead) Destroy(back.gameObject);
                if (stretcher) Destroy(stretcher.gameObject);
                Leave();
                return;
            }
            Vector3 rear = Rear;
            switch (phase)
            {
                case 0:   // the next body
                    body = IncidentManager.NextBody(incident, rear);
                    if (!body) { Finish(); return; }
                    body.onStretcher = true;   // claimed
                    phase = 1;
                    phaseT = 0f;
                    break;
                case 1:   // carry the empty stretcher to it
                    if (!body || body.gone) { phase = 0; break; }
                    if (MoveStretcher(body.transform.position, dt) || phaseT > 90f) { phase = 2; phaseT = 0f; }
                    break;
                case 2:   // lift
                    if (!body || body.gone) { phase = 0; break; }
                    Hold();
                    if (phaseT > 1.8f)
                    {
                        var cc = body.GetComponent<CharacterController>();
                        if (cc) cc.enabled = false;
                        body.transform.SetParent(stretcher, false);
                        body.transform.localPosition = new Vector3(0f, Top - 0.04f, 0.85f);
                        body.transform.localRotation = Quaternion.identity;
                        if (body.rig) body.rig.dead = true;
                        LayOnStretcher(body);
                        phase = 3;
                        phaseT = 0f;
                    }
                    break;
                case 3:   // back to the van
                    if (MoveStretcher(rear, dt) || phaseT > 90f)
                    {
                        if (body) { body.transform.SetParent(null, true); body.Collect(); }
                        body = null;
                        stretcher.gameObject.SetActive(false);
                        phase = 4;
                        phaseT = 0f;
                    }
                    break;
                case 4:   // in; the next one?
                    Hold();
                    if (phaseT > 1.2f) { stretcher.gameObject.SetActive(true); phase = 0; }
                    break;
            }
        }

        /// <summary>The 108 call for the player: walk over, dress the wounds, walk back, go.</summary>
        void Treating(float dt)
        {
            hT += dt;
            var p = PlayerController.Instance;
            if (!helper || helper.Dead || !p) { if (helper && !helper.Dead) Destroy(helper.gameObject); Leave(); return; }
            Vector3 pp = p.transform.position;
            bool gone = (pp - transform.position).sqrMagnitude > 70f * 70f || p.CurrentMode != PlayerController.Mode.Normal;
            switch (hPhase)
            {
                case 0:   // to the player
                    if (gone || hT > 90f) { hPhase = 2; hT = 0f; break; }
                    if (Walk(pp, 1.1f, dt)) { hPhase = 1; hT = 0f; }
                    break;
                case 1:   // bandages
                    helper.Hold(hPos, pp - hPos, 0f);
                    if (gone) { hPhase = 2; hT = 0f; break; }
                    if (hT > 3.5f)
                    {
                        var h = p.Health;
                        if (h) h.Heal(h.max);
                        GameEvents.ShowToast("108: marham-patti ho gayi - health poori. \"Dhyaan rakhiye!\"", 3.5f);
                        hPhase = 2;
                        hT = 0f;
                    }
                    break;
                case 2:   // back and away
                    if (Walk(Rear, 0.5f, dt) || hT > 40f)
                    {
                        Destroy(helper.gameObject);
                        Leave();
                    }
                    break;
            }
        }

        /// <summary>The helper walks towards p at a walking pace; true once within stopAt.</summary>
        bool Walk(Vector3 p, float stopAt, float dt)
        {
            Vector3 to = p - hPos;
            to.y = 0f;
            float d = to.magnitude;
            if (d <= stopAt) { helper.Hold(hPos, d > 0.01f ? to : transform.forward, 0f); return true; }
            hPos += to / d * Mathf.Min(1.6f * dt, d - stopAt);
            hPos.y = Ground(hPos).y;
            helper.Hold(hPos, to, 1.6f);
            return false;
        }

        void Finish()
        {
            if (front) Destroy(front.gameObject);
            if (back) Destroy(back.gameObject);
            if (stretcher) Destroy(stretcher.gameObject);
            Leave();
        }

        void Leave()
        {
            mode = Mode.Leave;
            modeT = 0f;
            drive.stop = false;
            drive.arriveDist = 10f;
            if (lights) lights.lights = lights.siren = true;
            // towards the BD Pandey District Hospital when it is reachable, else just away
            Vector3 goal = WantedSystem.Hospital;
            var p = PlayerController.Instance;
            if (p && Vector3.Distance(goal, p.transform.position) < 200f)
            {
                Vector3 away = transform.position - p.transform.position;
                away.y = 0f;
                goal = transform.position + (away.sqrMagnitude > 1f ? away.normalized : transform.forward) * 280f;
            }
            int k = RoadRoute.ReadyAny() ? RoadRoute.Nearest(goal, 300f) : -1;
            drive.SetGoal(k >= 0 ? RoadRoute.Pos(k) : goal, true);
        }

        void Gone()
        {
            var p = PlayerController.Instance;
            float d = p ? Vector3.Distance(p.transform.position, transform.position) : 999f;
            var cam = Camera.main;
            Vector3 vp = cam ? cam.WorldToViewportPoint(transform.position) : Vector3.back;
            bool seen = vp.z > 0f && vp.z < 200f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
            if ((d > 160f && !seen) || modeT > 80f || drive.Arrived && !seen) Destroy(gameObject);
        }

        // ------------------------------------------------------------------ the stretcher
        Transform MakeStretcher()
        {
            var mb = new MeshBuilder();
            Vector2 steel = Atlas.ExtraUV(Px.Chrome), black = Atlas.ExtraUV(Px.Tyre), orange = Atlas.SolidUV(Pal.SareeOrange), white = Atlas.SolidUV(Pal.ShirtWhite);
            const float len = 2.0f, w = 0.56f;
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(sx * w * 0.5f, Top - 0.08f, 0f), new Vector3(0.035f, 0.035f, len + 0.5f), steel);   // rails with handles
                foreach (float sz in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(sx * w * 0.42f, (Top - 0.1f) * 0.5f, sz * len * 0.38f), new Vector3(0.03f, Top - 0.1f, 0.03f), steel);   // legs
                    mb.Box(new Vector3(sx * w * 0.42f, 0.05f, sz * len * 0.38f), new Vector3(0.06f, 0.1f, 0.06f), black);                    // wheels
                }
                mb.Box(new Vector3(sx * (w * 0.5f), Top - 0.08f, (len * 0.5f + 0.18f) * 1f), new Vector3(0.045f, 0.05f, 0.16f), black); // grips
                mb.Box(new Vector3(sx * (w * 0.5f), Top - 0.08f, -(len * 0.5f + 0.18f)), new Vector3(0.045f, 0.05f, 0.16f), black);
            }
            mb.Box(new Vector3(0f, Top - 0.05f, 0f), new Vector3(w, 0.07f, len), orange);                  // mattress
            mb.Box(new Vector3(0f, Top + 0.0f, -len * 0.4f), new Vector3(w * 0.8f, 0.06f, 0.3f), white);  // pillow end
            var go = new GameObject("Stretcher");
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Stretcher");
            go.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.AtlasMaterial;
            return go.transform;
        }

        /// <summary>A body posed by a clip (lying on its back after the death clip, or face down after being knocked
        /// over) lies anywhere round its root: turn it so head-to-hips runs along the stretcher (head to the pillow end, -Z)
        /// and move it so the hips are over the middle, resting on the mattress.</summary>
        void LayOnStretcher(Casualty c)
        {
            var anim = c.rig ? c.rig.Anim : null;
            if (!anim || !anim.isHuman) return;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (!hips || !head) return;
            Vector3 along = stretcher.InverseTransformDirection(head.position - hips.position);
            along.y = 0f;
            if (along.sqrMagnitude > 0.01f)
            {
                float yaw = Vector3.SignedAngle(along, Vector3.back, Vector3.up);
                c.transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * c.transform.localRotation;
            }
            Vector3 h = stretcher.InverseTransformPoint(hips.position);
            float low = float.MaxValue;
            foreach (var smr in c.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.enabled && smr.isVisible || smr.enabled) low = Mathf.Min(low, stretcher.InverseTransformPoint(smr.bounds.center).y - smr.bounds.extents.y * 0.25f);
            c.transform.localPosition += new Vector3(-h.x, 0f, 0.15f - h.z);
            Vector3 hp = stretcher.InverseTransformPoint(hips.position);
            c.transform.localPosition += new Vector3(0f, Top + 0.1f - hp.y, 0f);
        }

        void PlaceStretcher()
        {
            var rot = Quaternion.Euler(0f, sYaw, 0f);
            stretcher.SetPositionAndRotation(Ground(sPos), rot);
            Vector3 f = rot * Vector3.forward;
            front.Hold(sPos + f * 1.3f, f, 0f);
            back.Hold(sPos - f * 1.3f, f, 0f);
        }

        /// <summary>Carry the stretcher towards p; true when it is there.</summary>
        bool MoveStretcher(Vector3 p, float dt)
        {
            Vector3 to = p - sPos;
            to.y = 0f;
            float d = to.magnitude;
            float sp = 0f;
            if (d > 0.35f)
            {
                float yaw = Quaternion.LookRotation(to / d).eulerAngles.y;
                sYaw = Mathf.MoveTowardsAngle(sYaw, yaw, 120f * dt);
                if (Mathf.Abs(Mathf.DeltaAngle(sYaw, yaw)) < 50f)
                {
                    sp = Mathf.Min(StretcherSpeed, d * 1.5f);
                    sPos += to / d * sp * dt;
                }
            }
            var rot = Quaternion.Euler(0f, sYaw, 0f);
            Vector3 g = Ground(sPos);
            sPos.y = g.y;
            stretcher.SetPositionAndRotation(g, rot);
            Vector3 f = rot * Vector3.forward;
            front.Hold(sPos + f * 1.3f, f, sp);
            back.Hold(sPos - f * 1.3f, f, sp);
            return d <= 0.35f;
        }

        /// <summary>Stand at the handles without moving.</summary>
        void Hold()
        {
            var rot = Quaternion.Euler(0f, sYaw, 0f);
            Vector3 f = rot * Vector3.forward;
            front.Hold(sPos + f * 1.3f, f, 0f);
            back.Hold(sPos - f * 1.3f, f, 0f);
        }

        void OnDestroy()
        {
            if (body) { body.transform.SetParent(null, true); body.onStretcher = false; }
            if (front) Destroy(front.gameObject);
            if (back) Destroy(back.gameObject);
            if (stretcher) Destroy(stretcher.gameObject);
        }
    }

    /// <summary>One of the 108 crew: walks to his end of the stretcher and holds the handles (Ambulance moves him).</summary>
    [RequireComponent(typeof(CharacterMotor), typeof(Health))]
    public class Medic : MonoBehaviour
    {
        public HumanoidRig rig;
        CharacterMotor motor;
        Health health;
        GameObject lastHurtBy;
        Vector3 slot, face;
        float slotSpeed;
        bool hasSlot;

        public bool Dead => health && health.IsDead;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            health = GetComponent<Health>();
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            health.Damaged += (a, from, src) =>
            {
                lastHurtBy = src;
                if (WantedSystem.ByPlayer(src)) WantedSystem.Ensure().Assault(transform.position, false);
                if (rig) rig.PlayHit();
            };
            health.Died += () =>
            {
                rig.carrying = false;
                rig.dead = true;
                if (WantedSystem.ByPlayer(lastHurtBy)) WantedSystem.Ensure().Kill(transform.position, false);
                IncidentManager.Report(gameObject, rig, () => { if (this) Destroy(gameObject); });
            };
        }

        /// <summary>Be at p (one end of the stretcher), facing f, walking at speed sp.</summary>
        public void Hold(Vector3 p, Vector3 f, float sp)
        {
            slot = p;
            face = f;
            slotSpeed = sp;
            hasSlot = true;
        }

        void Update()
        {
            if (!rig) return;
            float dt = Time.deltaTime;
            if (Dead) { rig.dead = true; rig.speed = 0f; motor.Move(Vector3.zero, 20f, dt); return; }
            rig.carrying = hasSlot;
            if (!hasSlot) { motor.Move(Vector3.zero, 10f, dt); rig.speed = 0f; return; }
            Vector3 to = slot - transform.position;
            to.y = 0f;
            if (to.magnitude > 2.2f) { motor.Teleport(slot + Vector3.up * 0.15f); to = Vector3.zero; }   // stuck behind something: catch up
            Vector3 desired = Vector3.ClampMagnitude(to * 6f, 3.5f);
            motor.Move(desired, 25f, dt);
            motor.FaceTowards(face, 360f, dt);
            rig.speed = Mathf.Max(slotSpeed, motor.HorizontalVelocity.magnitude * 0.6f);
            rig.grounded = true;
        }
    }
}
