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
            var lib = VehicleLibrary.Instance;
            for (int i = 0; i < positions.Length; i++)
            {
                var type = (CarType)types[i];
                int model = models != null && i < models.Length ? models[i] : -1;
                // the scene was built against an older library (21 Nov 2026: the unbranded set replaced the branded models):
                // a model that is gone or of another kind becomes one of this kind, the same one every time
                if (lib && model >= 0 && (model >= lib.entries.Length || lib.entries[model] == null || !lib.entries[model].prefab || lib.entries[model].weight <= 0f ||
                                          !(lib.entries[model].type == type || (type == CarType.Taxi && lib.entries[model].taxi))))
                    model = -1;
                if (lib && model < 0) model = lib.Pick(type, Mathf.Repeat(i * 0.618034f + 0.21f, 1f));
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
