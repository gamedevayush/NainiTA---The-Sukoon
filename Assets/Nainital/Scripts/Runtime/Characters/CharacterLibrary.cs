using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The rigged, textured people (Tripo models auto-rigged in Blender by
    /// Tools/rig_tripo_character.py). Lives at Resources/CharacterLibrary so the editor world
    /// builder and the runtime crowd spawner both find it. Built by "Nainital/4. Import rigged characters".
    /// </summary>
    public class CharacterLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string id;
            public string displayName;
            public GameObject prefab;
            public HumanoidRig.BodyType bodyType;
            public float height = 1.7f;
            [Tooltip("Relative chance of being picked for a crowd (0 = never).")]
            public float weight = 1f;

            public bool Female => bodyType == HumanoidRig.BodyType.Woman || bodyType == HumanoidRig.BodyType.Girl || bodyType == HumanoidRig.BodyType.OldWoman;
            public bool Child => bodyType == HumanoidRig.BodyType.Boy || bodyType == HumanoidRig.BodyType.Girl;
            public bool Elder => bodyType == HumanoidRig.BodyType.OldMan || bodyType == HumanoidRig.BodyType.OldWoman;
        }

        public Entry[] entries = new Entry[0];

        static CharacterLibrary cached;

        public static CharacterLibrary Instance
        {
            get
            {
                if (!cached) cached = Resources.Load<CharacterLibrary>("CharacterLibrary");
                return cached;
            }
        }

        public Entry Find(string id)
        {
            foreach (var e in entries) if (e != null && e.prefab && e.id == id) return e;
            return null;
        }

        /// <summary>
        /// Weighted random pick. child/elder: null = any. Filters are relaxed (age first, then child)
        /// if nothing matches, so a request always returns someone of the right sex when possible.
        /// </summary>
        public Entry Pick(bool female, bool? child, bool? elder, System.Func<float> rand01)
        {
            float total = 0f;
            foreach (var e in entries) if (Fits(e, female, child, elder)) total += e.weight;
            if (total <= 0f)
            {
                if (elder.HasValue) return Pick(female, child, null, rand01);
                if (child.HasValue) return Pick(female, null, null, rand01);
                return null;
            }
            float r = rand01() * total;
            Entry last = null;
            foreach (var e in entries)
            {
                if (!Fits(e, female, child, elder)) continue;
                last = e;
                r -= e.weight;
                if (r <= 0f) return e;
            }
            return last;
        }

        public Entry Pick(bool female, bool? child, bool? elder) => Pick(female, child, elder, () => Random.value);

        public Entry Pick(bool female, bool? child, bool? elder, System.Random rnd) => Pick(female, child, elder, () => (float)rnd.NextDouble());

        static bool Fits(Entry e, bool female, bool? child, bool? elder) =>
            e != null && e.prefab && e.weight > 0f && e.Female == female &&
            (!child.HasValue || e.Child == child.Value) && (!elder.HasValue || e.Elder == elder.Value);

        /// <summary>Sizes a CharacterController to a skinned character of the given height.</summary>
        public static void FitController(CharacterController cc, float height)
        {
            cc.height = Mathf.Max(0.9f, height + 0.04f);
            cc.radius = height < 1.5f ? 0.24f : 0.3f;
            cc.center = new Vector3(0, cc.height * 0.5f + 0.02f, 0);
        }

        /// <summary>Puts a library look on a HumanoidRig (call before the rig is enabled).</summary>
        public static void Apply(Entry e, HumanoidRig rig, CharacterController cc)
        {
            rig.skinModel = e.prefab;
            rig.bodyType = e.bodyType;
            rig.scale = 1f;
            if (cc) FitController(cc, e.height);
        }
    }
}
