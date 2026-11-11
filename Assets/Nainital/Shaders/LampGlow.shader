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
            struct Varyings { float4 pos : SV_POSITION; half4 col : COLOR; float2 uv : TEXCOORD0; half halo : TEXCOORD1; half fog : TEXCOORD2; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.pos);
                float size = i.uv2.x;
                if (size > 0)   // billboard: spread the corners in view space round the bulb
                {
                    float3 vs = TransformWorldToView(ws);
                    vs.xy += (i.uv * 2 - 1) * size;
                    vs.z += size * 0.5;   // a little towards the camera, so the lamp head does not cut it
                    o.pos = TransformWViewToHClip(vs);
                }
                else o.pos = TransformWorldToHClip(ws);
                o.col = i.col;
                o.uv = i.uv;
                o.halo = size > 0 ? 1 : 0;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
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
