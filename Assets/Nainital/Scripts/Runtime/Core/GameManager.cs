using UnityEngine;

namespace Nainital
{
    /// <summary>Boots runtime systems (input, HUD) and applies per-platform quality.</summary>
    [DefaultExecutionOrder(-200)]
    public class GameManager : MonoBehaviour
    {
        public Material atlasMaterial;
        public Material markerMaterial;
        public Material rainMaterial;
        public Texture2D minimap;
        public float worldSize = 4000f;
        public float worldCx, worldCz;   // the map square's centre
        public string[] blipNames;
        public Vector3[] blipPositions;
        public float pcFogEnd = 7000f, mobileFogEnd = 2000f;   // PC sees the far ridges (WorldBuilder.FarRidges)

        void Awake()
        {
            GameAssets.AtlasMaterial = atlasMaterial;
            GameAssets.MarkerMaterial = markerMaterial;
            GameAssets.RainMaterial = rainMaterial;
            GameAssets.Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameState.Load();
            Time.timeScale = 1f;
            Minimap.MapTexture = minimap;
            Minimap.WorldSize = worldSize;
            Minimap.WorldCx = worldCx;
            Minimap.WorldCz = worldCz;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;

            bool mobile = Application.isMobilePlatform;
            RenderSettings.fogEndDistance = mobile ? mobileFogEnd : pcFogEnd;
            RenderSettings.fogStartDistance = mobile ? 300f : 1200f;  // the atmosphere pass does the aerial perspective
            var cam = Camera.main;
            if (cam) cam.farClipPlane = (mobile ? mobileFogEnd : pcFogEnd) + 200f;

            if (!FindAnyObjectByType<GameInput>()) new GameObject("GameInput").AddComponent<GameInput>();
            if (!FindAnyObjectByType<HUD>()) new GameObject("HUD").AddComponent<HUD>();
        }

        void Start()
        {
            GameEvents.ShowToast("Nainital mein swagat hai!  (H = controls, M = map)", 4f);
        }
    }
}
