using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>One objective inside a mission.</summary>
    public abstract class MissionStep
    {
        public string text;
        public float timeLimit;           // seconds, 0 = none
        public virtual Vector3? Target => null;
        public virtual float Radius => 6f;
        public virtual void Begin() { }
        public virtual void End() { }
        public abstract bool Done();
        public virtual string Progress => "";

        protected static PlayerController P => PlayerController.Instance;

        protected static bool Near(Vector3 t, float r)
        {
            if (!P) return false;
            Vector3 pos = P.CurrentMode == PlayerController.Mode.Vehicle && P.Car ? P.Car.transform.position : P.transform.position;
            Vector3 d = pos - t;
            d.y *= 0.3f;
            return d.sqrMagnitude < r * r;
        }
    }

    public class GoToStep : MissionStep
    {
        readonly Vector3 target;
        readonly float radius;
        readonly bool needCar, needBoat, pressE;
        public GoToStep(string text, Vector3 target, float radius = 6f, bool needCar = false, bool needBoat = false, bool pressE = false, float timeLimit = 0f)
        {
            this.text = text; this.target = target; this.radius = radius; this.needCar = needCar; this.needBoat = needBoat; this.pressE = pressE; this.timeLimit = timeLimit;
        }
        public override Vector3? Target => target;
        public override float Radius => radius;
        public override bool Done()
        {
            if (!P) return false;
            if (needCar && P.CurrentMode != PlayerController.Mode.Vehicle) return false;
            if (needBoat && P.CurrentMode != PlayerController.Mode.Boat) return false;
            if (!Near(target, radius)) return false;
            return !pressE || GameInput.InteractPressed;
        }
        public override string Progress => pressE && Near(target, radius) ? "(E dabao)" : "";
    }

    public class EnterModeStep : MissionStep
    {
        readonly PlayerController.Mode mode;
        readonly Vector3? hint;
        public EnterModeStep(string text, PlayerController.Mode mode, Vector3? hint) { this.text = text; this.mode = mode; this.hint = hint; }
        public override Vector3? Target => hint;
        public override bool Done() => P && P.CurrentMode == mode;
    }

    public class DefeatStep : MissionStep
    {
        readonly List<EnemyAI> enemies;
        public DefeatStep(string text, List<EnemyAI> enemies) { this.text = text; this.enemies = enemies; }
        public override Vector3? Target
        {
            get
            {
                foreach (var e in enemies) if (e && !e.Dead) return e.transform.position;
                return null;
            }
        }
        int Alive { get { int n = 0; foreach (var e in enemies) if (e && !e.Dead) n++; return n; } }
        public override bool Done() => enemies.Count > 0 && Alive == 0;
        public override string Progress => $"({enemies.Count - Alive}/{enemies.Count})";
    }

    public class VisitAllStep : MissionStep
    {
        readonly List<(string name, Vector3 pos)> places;
        readonly HashSet<int> visited = new HashSet<int>();
        public VisitAllStep(string text, List<(string, Vector3)> places) { this.text = text; this.places = places; }
        public override float Radius => 22f;
        public override Vector3? Target
        {
            get
            {
                if (!P) return null;
                float best = float.MaxValue; Vector3? t = null;
                for (int i = 0; i < places.Count; i++)
                {
                    if (visited.Contains(i)) continue;
                    float d = (places[i].pos - P.transform.position).sqrMagnitude;
                    if (d < best) { best = d; t = places[i].pos; }
                }
                return t;
            }
        }
        public override bool Done()
        {
            for (int i = 0; i < places.Count; i++)
                if (!visited.Contains(i) && Near(places[i].pos, 22f))
                {
                    visited.Add(i);
                    GameEvents.ShowToast("Photo kheench li: " + places[i].name, 2.5f);
                }
            return visited.Count == places.Count;
        }
        public override string Progress => $"({visited.Count}/{places.Count})";
    }

    public class CountEventStep : MissionStep
    {
        readonly int need;
        int count;
        readonly bool monkeys;
        readonly Vector3? hint;
        public CountEventStep(string text, int need, bool monkeys, Vector3? hint) { this.text = text; this.need = need; this.monkeys = monkeys; this.hint = hint; }
        public override Vector3? Target => hint;
        void Inc() => count++;
        public override void Begin()
        {
            count = 0;
            if (monkeys) GameEvents.MonkeyScared += Inc; else GameEvents.Ate += Inc;
        }
        public override void End()
        {
            if (monkeys) GameEvents.MonkeyScared -= Inc; else GameEvents.Ate -= Inc;
        }
        public override bool Done() => count >= need;
        public override string Progress => $"({count}/{need})";
    }

    public class Mission
    {
        public string id, title, intro;
        public int reward;
        public bool nightOnly;
        public Vector3 giver;
        public List<MissionStep> steps = new List<MissionStep>();
    }
}
