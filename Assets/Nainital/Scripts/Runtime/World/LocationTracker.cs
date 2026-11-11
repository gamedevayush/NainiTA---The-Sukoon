using UnityEngine;

namespace Nainital
{
    /// <summary>Shows the name of the place the player is in (Mallital, Mall Road, Naina Devi Temple ...).</summary>
    public class LocationTracker : MonoBehaviour
    {
        public string[] names;
        public Vector3[] centers;
        public float[] radii;

        string current = "";
        float timer;

        void Update()
        {
            timer -= Time.deltaTime;
            var p = PlayerController.Instance;
            if (timer > 0f || !p || names == null) return;
            timer = 0.5f;
            Vector3 pos = p.transform.position;
            string best = "Nainital";
            float bestR = float.MaxValue;
            if (p.InWater || p.CurrentMode == PlayerController.Mode.Boat || LakeArea.Contains(pos))
            {
                best = "Naini Jheel";
                bestR = 0f;
            }
            else
            {
                for (int i = 0; i < names.Length; i++)
                {
                    Vector3 d = pos - centers[i];
                    d.y = 0;
                    if (d.sqrMagnitude <= radii[i] * radii[i] && radii[i] < bestR)
                    {
                        bestR = radii[i];
                        best = names[i];
                    }
                }
            }
            if (best != current)
            {
                current = best;
                GameEvents.RaiseLocation(best);
            }
        }
    }
}
