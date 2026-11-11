using UnityEngine;

namespace Nainital
{
    /// <summary>Sets the weathering that Nainital/AtlasLit puts on everything built from the palette atlas: house paint dulled
    /// a little and a world-space grime texture (stains running down walls, dust on tops). Globals, so the SRP batcher keeps
    /// batching; set in the editor too (ExecuteAlways) so the scene view matches the game.</summary>
    [ExecuteAlways]
    public class SurfaceLook : MonoBehaviour
    {
        public Texture2D grime;
        [Range(0.02f, 1f)] public float scale = 0.18f;     // repeats per metre
        [Range(0f, 1f)] public float strength = 0.85f;
        [Range(0f, 1f)] public float muted = 0.18f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            Shader.SetGlobalTexture("_NainitalGrimeTex", grime ? grime : Texture2D.grayTexture);
            Shader.SetGlobalFloat("_NainitalGrimeScale", scale);
            Shader.SetGlobalFloat("_NainitalGrimeStrength", grime ? strength : 0f);
            Shader.SetGlobalFloat("_NainitalMuted", muted);
        }
    }
}
