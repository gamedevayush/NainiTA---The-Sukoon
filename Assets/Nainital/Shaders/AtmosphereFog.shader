// Full-screen atmospheric pass (URP Full Screen Pass Renderer Feature):
// exponential height fog that pools in the Nainital valley + sun/moon in-scattering glow (aerial perspective).
// One full-screen pass with a single depth sample: cheap enough for phones.
Shader "Nainital/AtmosphereFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "AtmosphereFog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _HazeColor;      // ambient haze colour (set by DayNightCycle)
            float4 _SunHazeColor;   // colour of light scattered towards the sun / moon
            float4 _HazeLightDir;   // towards the sun by day / the moon by night (DayNightCycle)
            float _HazeDensity;     // base extinction per metre at _HazeBase height
            float _HazeBase;        // world height where the haze is densest (valley floor)
            float _HazeFalloff;     // how fast it thins with altitude
            float _HazeMax;         // cap so distant hills never fully vanish

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_HazeDensity <= 0.0) return col;

                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = raw <= 0.000001;
                #else
                    bool sky = raw >= 0.999999;
                #endif
                float3 wpos = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 cam = _WorldSpaceCameraPos;
                float3 v = wpos - cam;
                float len = max(length(v), 1e-3);
                float3 dir = v / len;
                float dist = sky ? 4000.0 : len;

                // analytic integral of density(y) = D * exp(-k (y - base)) along the ray
                float k = _HazeFalloff;
                float h0 = cam.y - _HazeBase;
                float dy = dir.y * dist * k;
                float shape = abs(dy) > 0.001 ? (1.0 - exp(-dy)) / dy : 1.0;
                float optical = _HazeDensity * exp(-k * h0) * dist * shape;
                float fog = saturate(1.0 - exp(-optical));
                fog = min(fog, _HazeMax);
                if (sky) fog *= saturate(1.0 - dir.y * 2.5);

                float sunAmt = pow(saturate(dot(dir, normalize(_HazeLightDir.xyz + float3(0, 1e-4, 0)))), 5.0);
                half3 haze = lerp(_HazeColor.rgb, _SunHazeColor.rgb, sunAmt);
                col.rgb = lerp(col.rgb, haze, fog);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
