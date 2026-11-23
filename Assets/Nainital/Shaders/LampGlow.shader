// Street-lamp glow at night (user, 2026-11-11: "lights nahi hai game me, saare roads me, popular places tak lights"):
// a soft halo round every bulb (a camera-facing billboard) and a warm pool of light on the road beneath it, added on
// top of the scene. Cheap on a phone: one batched mesh per area, no real lights. _NtLampGlow (set by LampManager from
// the time of day) fades it all in at dusk and out at dawn.
//   vertex position: halo centre (billboards) or the pool corner (pools); uv: 0..1 across the quad;
//   uv2.x: halo half-size in metres (0 = a pool lying on the ground); colour: tint, alpha = strength.
Shader "Nainital/LampGlow"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "LampGlow"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Blend One One
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _NtLampGlow;

            struct Attributes { float3 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct Varyings { float4 pos : SV_POSITION; half4 col : COLOR; float2 uv : TEXCOORD0; half halo : TEXCOORD1; half fog : TEXCOORD2; float3 ws : TEXCOORD3; };

            float Hash1(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float VNoise1(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash1(i), Hash1(i + float2(1, 0)), f.x), lerp(Hash1(i + float2(0, 1)), Hash1(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.pos);
                float size = i.uv2.x;
                o.ws = ws;
                if (size < 0)   // lake streak (user, 2026-11-14 night photos): a long thin band lying on the water from below
                {               // the light towards the camera, like a lamp's reflection broken up by ripples
                    float h = -size, wid = i.uv2.y;   // h: the light's height above the water
                    float2 d = _WorldSpaceCameraPos.xz - ws.xz;
                    float dl = max(length(d), 0.01);
                    d /= dl;
                    // a reflection only stretches into a long streak when it is seen at a low angle across the water;
                    // from high above (a drone, the Scene view) it is just a short smudge under the light
                    float camH = max(_WorldSpaceCameraPos.y - ws.y, 0.3);
                    float elev = camH / dl;
                    float grazing = saturate(1.0 - elev * 2.5);
                    // the mirror point of the light lies dl*c/(c+h) from the camera; ripples spread the glitter on both
                    // sides of it, so the streak runs from the shore well past that point towards the viewer (a fixed
                    // 16-48 m streak was a dot from across the lake, 15 Nov)
                    float spec = dl * camH / (camH + h);
                    float len = clamp((dl - spec) * 1.5, 4.0, dl * 0.85) * lerp(0.35, 1.0, grazing);
                    wid *= lerp(0.6, 1.0, grazing) * (1.0 + dl / 120.0);   // keeps a visible width from far away
                    i.col.a *= grazing;
                    float2 side = float2(-d.y, d.x);
                    ws.xz += side * (i.uv.x * 2 - 1) * wid + d * (i.uv.y * 1.1 - 0.1) * len;
                    o.ws = ws;
                    o.pos = TransformWorldToHClip(ws);
                    o.halo = 2;
                }
                else if (size > 0)   // billboard: spread the corners in view space round the bulb
                {
                    float3 vs = TransformWorldToView(ws);
                    // shrink it close up (22 Nov 2026: a lamp 6-8 m away showed a halo like a full moon); full size from ~25 m
                    size *= lerp(0.4, 1.0, saturate((-vs.z - 3.0) / 22.0));
                    vs.xy += (i.uv * 2 - 1) * size;
                    vs.z += size * 0.5;   // a little towards the camera, so the lamp head does not cut it
                    o.pos = TransformWViewToHClip(vs);
                }
                else o.pos = TransformWorldToHClip(ws);
                o.col = i.col;
                if (size > 0)   // (owner, 23 Nov: "tube lights / neon ka flicker") ~3% of the lights stutter in bursts
                {
                    float id = Hash1(floor(ws.xz * 2.0) + floor(ws.y * 2.0) * 0.37);
                    if (id < 0.03)
                    {
                        float t = _Time.y;
                        float burst = step(0.62, VNoise1(float2(t * 0.3, id * 977.0)));
                        float blink = step(0.4, VNoise1(float2(t * 11.0, id * 531.0)));
                        o.col.a *= lerp(1.0, 0.08 + 0.92 * blink, burst);
                    }
                }
                o.uv = i.uv;
                if (size >= 0) o.halo = size > 0 ? 1 : 0;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                if (i.halo > 1.5)   // lake streak
                {
                    float across = abs(i.uv.x * 2 - 1);
                    half core = pow(saturate(1 - across), 2.5);
                    half along = smoothstep(0.08, 0.16, i.uv.y) * pow(saturate(1 - i.uv.y), 1.2);   // bright from the shore, fading out
                    // ripples: bright dashes along the streak that drift and flicker
                    float t = _Time.y;
                    float2 p = i.ws.xz;
                    half rip = VNoise1(float2(p.x * 1.7 + t * 0.3, p.y * 1.7 - t * 0.5)) * VNoise1(float2(p.y * 3.1 + t * 0.8, p.x * 2.3));
                    rip = saturate(rip * 2.6 - 0.15);
                    half3 cs = i.col.rgb * (core * along * (0.35 + 1.4 * rip) * i.col.a * _NtLampGlow);
                    cs = MixFogColor(cs, half3(0, 0, 0), i.fog);
                    return half4(cs, 0);
                }
                float d = length(i.uv * 2 - 1);
                half f = saturate(1 - d);
                half a = i.halo > 0.5 ? f * f + 1.5 * pow(f, 10) : f * f * (3 - 2 * f) * 0.85;
                half3 c = i.col.rgb * (a * i.col.a * _NtLampGlow);
                c = MixFogColor(c, half3(0, 0, 0), i.fog);   // far lamps sink into the haze
                return half4(c, 0);
            }
            ENDHLSL
        }
    }
}
