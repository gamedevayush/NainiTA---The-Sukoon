using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Parked (enterable) cars, taxis and buses. Built from code on enable: preview-only in the Editor,
    /// fully drivable in play mode.
    /// </summary>
    [ExecuteAlways]
    public class ParkedVehicles : MonoBehaviour
    {
        public Material material;
        public Vector3[] positions;
        public Quaternion[] rotations;
        public int[] types;
        public int[] paints;
        public int[] models;   // VehicleLibrary entries (-1 or missing: any of the kind)

        Transform spawned;

        void OnEnable()
        {
            if (positions == null || material == null) return;
            var old = transform.Find("Spawned");
            if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
            spawned = new GameObject("Spawned").transform;
            spawned.SetParent(transform, false);
            if (!Application.isPlaying) spawned.gameObject.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < positions.Length; i++)
            {
                var type = (CarType)types[i];
                int model = models != null && i < models.Length ? models[i] : -1;
                var go = VehicleFactory.Create(type, paints[i], material, Application.isPlaying, "Parked " + type, model);
                go.transform.SetParent(spawned, false);
                go.transform.SetPositionAndRotation(positions[i], rotations[i]);
            }
        }

        void OnDisable()
        {
            if (Application.isPlaying || !spawned) return;
            DestroyImmediate(spawned.gameObject);
        }
    }
}
