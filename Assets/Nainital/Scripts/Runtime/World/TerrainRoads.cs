using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Hands the road mask and the road textures to the terrain shader (Shaders/NainitalTerrainRoads.hlsl) as globals, and
    /// keeps the wetness in step with the rain. The roads are part of the ground (user, 2026-11-16: "saari roads terrain me
    /// bake karo, koi road separate mesh na ho"): the world builder paints every carriageway, footpath and paving into the
    /// mask instead of building a mesh for it.
    /// </summary>
    [ExecuteAlways]
    public class TerrainRoads : MonoBehaviour
    {
        public Texture2D mask, asphalt, asphaltNormal, concrete, paver, paverNormal, dirt;
        public Vector4 rect;                                           // x0, z0, width, depth (m)
        public Vector4 tiles = new Vector4(5f, 6f, 3.2f, 70f);         // asphalt, concrete, pavers, dirt (m a repeat)
        public Color redTint = new Color(1.25f, 0.62f, 0.5f), greyTint = new Color(0.95f, 0.95f, 0.95f);
        [Range(0f, 1f)] public float dryWetness;                       // wetness with no rain (editor preview)
        /// <summary>The mask holds signed distances to each surface's edge, +-sdfRange metres over 0..1 (0: plain coverage,
        /// an older build). WorldBuilder.RoadMask.cs CoverageToDistance.</summary>
        public float sdfRange;

        static readonly int MaskId = Shader.PropertyToID("_NtRoadMask"), RectId = Shader.PropertyToID("_NtRoadRect"),
            AsphaltId = Shader.PropertyToID("_NtAsphalt"), AsphaltNId = Shader.PropertyToID("_NtAsphaltN"),
            ConcreteId = Shader.PropertyToID("_NtConcrete"), PaverId = Shader.PropertyToID("_NtPaver"), PaverNId = Shader.PropertyToID("_NtPaverN"),
            DirtId = Shader.PropertyToID("_NtDirt"), TilesId = Shader.PropertyToID("_NtTiles"), RedId = Shader.PropertyToID("_NtRedTint"),
            GreyId = Shader.PropertyToID("_NtGreyTint"), WetId = Shader.PropertyToID("_NtWetness"), OnId = Shader.PropertyToID("_NtRoadsOn"),
            SdfId = Shader.PropertyToID("_NtRoadSdf");

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void OnDisable() => Shader.SetGlobalFloat(OnId, 0f);

        public void Apply()
        {
            bool ok = mask && asphalt && concrete && paver && dirt;
            Shader.SetGlobalFloat(OnId, ok ? 1f : 0f);
            if (!ok) return;
            Shader.SetGlobalTexture(MaskId, mask);
            Shader.SetGlobalTexture(AsphaltId, asphalt);
            Shader.SetGlobalTexture(AsphaltNId, asphaltNormal ? asphaltNormal : Texture2D.normalTexture);
            Shader.SetGlobalTexture(ConcreteId, concrete);
            Shader.SetGlobalTexture(PaverId, paver);
            Shader.SetGlobalTexture(PaverNId, paverNormal ? paverNormal : Texture2D.normalTexture);
            Shader.SetGlobalTexture(DirtId, dirt);
            Shader.SetGlobalVector(RectId, new Vector4(rect.x, rect.y, 1f / Mathf.Max(1f, rect.z), 1f / Mathf.Max(1f, rect.w)));
            Shader.SetGlobalVector(TilesId, tiles);
            Shader.SetGlobalColor(RedId, redTint);
            Shader.SetGlobalColor(GreyId, greyTint);
            Shader.SetGlobalFloat(WetId, dryWetness);
            Shader.SetGlobalFloat(SdfId, sdfRange);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float rain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            Shader.SetGlobalFloat(WetId, Mathf.Max(dryWetness, rain));
        }
    }
}
