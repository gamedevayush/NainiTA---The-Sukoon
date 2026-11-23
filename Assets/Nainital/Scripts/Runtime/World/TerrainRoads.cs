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

        static byte[] kinds;     // per cell: 0 ground, 1 asphalt, 2 black tarmac, 3 grey pavers, 4 red pavers
        static int kindsN;
        static Vector4 kindsRect;
        const int KindsN = 2048;

        /// <summary>The surface painted into the ground at a world point: 0 none, 1 asphalt, 2 black tarmac, 3 grey pavers,
        /// 4 red pavers (footsteps). Read once from the mask on the GPU (the mask is compressed, not readable on the CPU).</summary>
        public static int SurfaceAt(Vector3 p)
        {
            if (kinds == null || kindsN == 0) return 0;
            float u = (p.x - kindsRect.x) / kindsRect.z, v = (p.z - kindsRect.y) / kindsRect.w;
            if (u < 0f || v < 0f || u >= 1f || v >= 1f) return 0;
            return kinds[(int)(v * kindsN) * kindsN + (int)(u * kindsN)];
        }

        void Start()
        {
            if (!Application.isPlaying || !mask || !SystemInfo.supportsAsyncGPUReadback) return;
            var rt = RenderTexture.GetTemporary(KindsN, KindsN, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(mask, rt);
            var r4 = rect;
            UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req =>
            {
                RenderTexture.ReleaseTemporary(rt);
                if (req.hasError) return;
                var data = req.GetData<Color32>();
                var k = new byte[KindsN * KindsN];
                for (int i = 0; i < k.Length; i++)
                {
                    var c = data[i];
                    // signed distances: > 0.5 inside (sdfRange > 0), else plain coverage
                    byte lim = sdfRange > 0f ? (byte)132 : (byte)128;
                    int best = 0; byte bv = lim;
                    if (c.r > bv) { best = 1; bv = c.r; }
                    if (c.g > bv) { best = 2; bv = c.g; }
                    if (c.b > bv) { best = 3; bv = c.b; }
                    if (c.a > bv) { best = 4; }
                    k[i] = (byte)best;
                }
                kinds = k; kindsN = KindsN; kindsRect = r4;
            });
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float rain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            Shader.SetGlobalFloat(WetId, Mathf.Max(dryWetness, rain));
        }
    }
}
