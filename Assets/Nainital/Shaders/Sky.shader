// Day/night skybox: gradient sky blended into the fog colour at the horizon, sun, moon, twinkling stars.
Shader "Nainital/Sky"
{
    Properties
    {
        _ZenithDay ("Zenith Day", Color) = (0.30, 0.46, 0.68, 1)
        _HorizonDay ("Horizon Day", Color) = (0.76, 0.81, 0.86, 1)
        _ZenithNight ("Zenith Night", Color) = (0.005, 0.01, 0.03, 1)
        _HorizonNight ("Horizon Night", Color) = (0.03, 0.05, 0.10, 1)
        _SunsetColor ("Sunset Color", Color) = (1.0, 0.45, 0.20, 1)
        _SunDir ("Sun Dir", Vector) = (0, 0.5, 0.8, 0)
        _MoonDir ("Moon Dir", Vector) = (0, 0.5, -0.8, 0)
        _NightFactor ("Night", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _ZenithDay, _HorizonDay, _ZenithNight, _HorizonNight, _SunsetColor;
            float4 _SunDir, _MoonDir;
            float _NightFactor;
            float _CloudCover;   // global, 0 = few clouds, 1 = overcast (WeatherSystem); unset = 0

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Hash12(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash12(i), Hash12(i + float2(1, 0)), f.x), lerp(Hash12(i + float2(0, 1)), Hash12(i + float2(1, 1)), f.x), f.y);
            }
            float Fbm2(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += Noise2(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float3 moon = normalize(_MoonDir.xyz);
                float day = 1.0 - _NightFactor;
                float up = saturate(d.y);
                float hb = pow(1.0 - up, 4.0);

                float3 dayCol = lerp(_ZenithDay.rgb, _HorizonDay.rgb, hb);
                float3 nightCol = lerp(_ZenithNight.rgb, _HorizonNight.rgb, hb) * 0.22;   // a real mountain night
                float3 col = lerp(nightCol, dayCol, day);

                // sunset / sunrise glow towards the sun
                float sunLow = saturate(1.0 - abs(sun.y) * 3.5);
                float toward = pow(saturate(dot(normalize(float3(d.x, 0, d.z) + 1e-5), normalize(float3(sun.x, 0, sun.z) + 1e-5)) * 0.5 + 0.5), 5.0);
                col += _SunsetColor.rgb * sunLow * toward * hb * 1.3;

                // match the distance fog at the horizon so far hills melt into the sky
                col = lerp(col, unity_FogColor.rgb, saturate(hb * 0.75 - 0.05));

                // sun disc + halo
                float sd = dot(d, sun);
                float sunVis = saturate(sun.y * 8.0 + 0.6) * day;
                col += float3(1.0, 0.9, 0.72) * (smoothstep(0.99955, 0.99975, sd) * 18.0 + pow(saturate(sd), 400.0) * 1.2) * sunVis;

                // moon disc + glow
                float md = dot(d, moon);
                col += float3(0.85, 0.9, 1.0) * smoothstep(0.99935, 0.9996, md) * 2.2 * _NightFactor;
                col += float3(0.25, 0.3, 0.45) * pow(saturate(md), 80.0) * 0.35 * _NightFactor;

                // drifting cumulus clouds (projected on a dome above the valley)
                if (d.y > 0.0)
                {
                    float2 cp = d.xz / (d.y + 0.12) * 1.6 + float2(_Time.y * 0.012, _Time.y * 0.004);
                    float c = Fbm2(cp);
                    float cc = _CloudCover > 0.001 ? _CloudCover : 0.35;
                    float cover = smoothstep(lerp(0.56, 0.22, cc), lerp(0.86, 0.5, cc), c) * saturate(d.y * 5.0);
                    float shade = saturate(Fbm2(cp + 0.35) * 1.4 - 0.2);
                    float3 litCol = lerp(float3(1.0, 0.98, 0.95), _SunsetColor.rgb * 1.2, sunLow * 0.8);
                    float3 cloudDay = lerp(litCol * 0.65, litCol, shade);
                    float3 cloudNight = float3(0.008, 0.009, 0.013) + float3(0.04, 0.045, 0.06) * pow(saturate(md), 20.0);
                    float3 cloudCol = lerp(cloudNight, cloudDay, day) * lerp(1.0, 0.5, saturate(cc - 0.55) * 2.2);
                    col = lerp(col, cloudCol, cover * 0.9);
                }

                // stars
                float3 sp = d * 260.0;
                float3 cell = floor(sp);
                float r = Hash13(cell);
                float3 f = frac(sp) - 0.5;
                float star = step(0.9965, r) * smoothstep(0.32, 0.0, length(f));
                float tw = 0.55 + 0.45 * sin(_Time.y * (1.5 + r * 7.0) + r * 60.0);
                col += star * tw * saturate(d.y * 4.0) * _NightFactor * 1.6;

                // below the horizon: valley haze
                col = lerp(col, unity_FogColor.rgb, saturate(-d.y * 6.0));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
