using UnityEngine;

namespace Nainital
{
    /// <summary>Baked tree instances (from OSM woods + slope rules) for the GPU-instanced renderer.</summary>
    public class TreeField : ScriptableObject
    {
        public float[] data;            // stride 6: x, y, z, type, scale, yaw
        public Mesh[] lod0;             // per type
        public Mesh[] lod1;             // per type
        public Mesh[] lod2;             // per type, very far
        public float[] trunkRadius;     // per type, for colliders
        public float[] trunkHeight;
        public bool[] cards;            // per type: lod0 = bark (submesh 0) + leaf cards (submesh 1); lod1/lod2 = impostor cards
        public int Count => data == null ? 0 : data.Length / 6;
    }
}
