using UnityEngine;

namespace Nainital
{
    /// <summary>Ambient sailing yacht (Nainital Boat Club style) wandering around the lake.</summary>
    public class Yacht : MonoBehaviour
    {
        public float speed = 2.4f;
        Vector3 target;
        System.Random rng;
        float bob;

        void Start()
        {
            rng = new System.Random(GetInstanceID());
            target = LakeArea.RandomPointInside(rng);
            bob = (float)rng.NextDouble() * 10f;
        }

        void Update()
        {
            if (!LakeArea.Instance) return;
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 to = target - p;
            to.y = 0;
            if (to.magnitude < 6f) target = LakeArea.RandomPointInside(rng);
            var want = Quaternion.LookRotation(to.normalized, Vector3.up);
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, want.eulerAngles.y, 18f * dt);
            Vector3 next = p + Quaternion.Euler(0, yaw, 0) * Vector3.forward * speed * dt;
            if (!LakeArea.ContainsInner(next)) { target = LakeArea.RandomPointInside(rng); next = p; }
            bob += dt;
            next.y = LakeArea.WaterY + Mathf.Sin(bob) * 0.06f;
            transform.SetPositionAndRotation(next, Quaternion.Euler(Mathf.Sin(bob * 0.8f) * 2f, yaw, 6f + Mathf.Sin(bob * 0.6f) * 3f));
        }
    }
}
