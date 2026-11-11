// Naini Lake water for URP: transparent, depth-based absorption, refraction of the lake bed,
// fresnel reflections (sky / reflection probe / street lamps via PBR), sun glints and shore foam.
// Needs "Depth Texture" and "Opaque Texture" enabled on the URP asset (ProjectSetup does that).
Shader "Nainital/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Tint", Color) = (0.55, 0.78, 0.72, 1)
        _DeepColor ("Deep Water Color", Color) = (0.015, 0.075, 0.07, 1)
        _Absorption ("Absorption (per metre)", Vector) = (0.42, 0.16, 0.13, 0)
        _Clarity ("Clarity", Range(0.1, 3)) = 1.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _WaveStrength ("Wave Normal Strength", Range(0, 3)) = 0.85
        _WaveSpeed ("Wave Speed", Float) = 1
        _Refraction ("Refraction", Range(0, 0.1)) = 0.035
        _FoamWidth ("Foam Width (m)", Range(0, 2)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _Absorption;
                half _Clarity;
                half _Smoothness;
                half _WaveStrength;
                float _WaveSpeed;
                half _Refraction;
                half _FoamWidth;
            CBUFFER_END

            float4 _SunDir;
            float _NightFactor;
            float _LakeProbeReady;   // set by DayNightCycle once the realtime lake probe has rendered

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // Same palette as the Nainital/Sky shader, so the lake mirrors the sky even without a probe.
            half3 AnalyticSky(float3 d)
            {
                float up = saturate(d.y);
                float hb = pow(1.0 - up, 4.0);
                half3 dayC = lerp(half3(0.20, 0.42, 0.78), half3(0.70, 0.80, 0.90), hb);
                half3 nightC = lerp(half3(0.0012, 0.002, 0.005), half3(0.005, 0.007, 0.012), hb);
                half3 c = lerp(dayC, nightC, _NightFactor);
                return lerp(c, unity_FogColor.rgb, saturate(hb * 0.75 - 0.05));
            }

            float2 Wave(float2 p, float t, float2 d, float k, float a, float sp)
            {
                float ph = dot(d, p) * k + t * sp;
                return d * (a * k * cos(ph));
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float3 WaterNormal(float3 positionWS, float dist)
            {
                float t = _Time.y * _WaveSpeed;
                float2 p = positionWS.xz;
                float2 s = Wave(p, t, normalize(float2(1.0, 0.25)), 0.21, 0.10, 0.9)
                         + Wave(p, t, normalize(float2(-0.6, 1.0)), 0.37, 0.05, 1.3)
                         + Wave(p, t, normalize(float2(0.3, -1.0)), 0.93, 0.022, 2.1)
                         + Wave(p, t, normalize(float2(-1.0, -0.4)), 1.71, 0.011, 2.9);
                // fine ripples fade with distance (avoids shimmering far away)
                float fine = saturate(1.0 - dist / 160.0);
                s += (Wave(p, t, normalize(float2(0.8, 0.9)), 3.13, 0.006, 3.7)
                    + Wave(p, t, normalize(float2(-0.2, 0.7)), 5.9, 0.003, 4.9)
                    + Wave(p, t, normalize(float2(0.9, -0.3)), 9.7, 0.0016, 6.1)) * fine;
                float n1 = VNoise(p * 0.9 + t * 0.35) - 0.5, n2 = VNoise(p.yx * 1.3 - t * 0.27) - 0.5;
                s += float2(n1, n2) * 0.05 * fine;
                return normalize(float3(-s.x * _WaveStrength, 1.0, -s.y * _WaveStrength));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float surfEye = -TransformWorldToView(i.positionWS).z;
                float3 n = WaterNormal(i.positionWS, surfEye);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // ---- water thickness along the view ray
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thick = max(0.0, sceneEye - surfEye);

                // ---- refraction (ignore objects in front of the water)
                float2 offs = n.xz * _Refraction * saturate(thick * 0.5);
                float2 uvR = screenUV + offs;
                float sceneEyeR = LinearEyeDepth(SampleSceneDepth(uvR), _ZBufferParams);
                if (sceneEyeR < surfEye) { uvR = screenUV; sceneEyeR = sceneEye; }
                float thickR = max(0.0, sceneEyeR - surfEye);
                half3 below = SampleSceneColor(uvR);

                // ---- Beer-Lambert absorption + in-scattering (greenish Himalayan lake)
                float3 trans = exp(-_Absorption.xyz * thickR / _Clarity);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 scatterLight = (mainLight.color * saturate(mainLight.direction.y) * 0.35 + ambient) * (1.0 - 0.9 * _NightFactor);
                half3 body = below * trans * _ShallowColor.rgb + _DeepColor.rgb * scatterLight * (1.0 - trans);

                // ---- reflections + speculars (sky, probe, sun, street lamps) from URP PBR with black albedo
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = 0;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = half3(0, 0, 0);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = half3(0, 0, 0);
                s.metallic = 0;
                s.specular = half3(0, 0, 0);
                s.smoothness = _Smoothness;
                s.occlusion = 0;          // direct speculars only (sun, moon, street lamps); reflections added below
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);
                half3 surface = UniversalFragmentPBR(inputData, s).rgb;

                // fresnel reflection: analytic sky, upgraded to the live lake probe (hills, lit windows) in play mode
                half NdotV = saturate(dot(n, V));
                half F = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);
                float3 R = reflect(-V, n);
                R.y = abs(R.y);
                half3 env = AnalyticSky(R);
                if (_LakeProbeReady > 0.5)
                    env = lerp(env, GlossyEnvironmentReflection(R, i.positionWS, 0.04, 1.0, screenUV), 0.85);
                half3 col = body * (1.0 - F) + env * F * lerp(0.9, 0.6, _NightFactor) + surface;

                // ---- shore foam
                float foamMask = saturate(1.0 - thick / max(0.01, _FoamWidth));
                float foamNoise = VNoise(i.positionWS.xz * 2.5 + _Time.y * 0.6);
                col = lerp(col, ambient + mainLight.color * 0.6, foamMask * smoothstep(0.35, 0.8, foamNoise) * 0.6 * (1.0 - 0.85 * _NightFactor));

                // ---- fog (per pixel: the lake mesh has huge triangles)
                float4 clip = TransformWorldToHClip(i.positionWS);
                col = MixFog(col, ComputeFogFactor(clip.z));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
