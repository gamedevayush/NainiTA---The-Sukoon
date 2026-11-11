// The hills beyond the 4 km map and the snow range on the horizon (Tools/make_far_ridges.py, WorldBuilder.FarRidges.cs).
// A dome that travels with the camera, drawn after the skybox without writing depth: everything real stands in front of it
// (depth test), and the atmosphere pass hazes it as sky, thinning with the camera's height. Each vertex carries its azimuth and its elevation angle seen from two eye heights (the lake,
// the peaks): the camera's height picks between them, so climbing Naina Peak lifts the snow range over the nearer ridges.
Shader "Nainital/FarRidges"
{
    Properties
    {
        _Radius ("Distance, as a fraction of the far plane", Float) = 0.94
        _EyeLo ("World height of the low eye", Float) = 380
        _EyeHi ("World height of the high eye", Float) = 1080
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent-450" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "FarRidges"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Radius, _EyeLo, _EyeHi;
            CBUFFER_END

            struct Attributes { float3 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; half4 col : COLOR; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                float t = saturate((_WorldSpaceCameraPos.y - _EyeLo) / max(_EyeHi - _EyeLo, 1.0));
                float az = i.pos.x, el = lerp(i.pos.y, i.pos.z, t);
                float3 dir = float3(sin(az) * cos(el), sin(el), cos(az) * cos(el));
                float3 ws = _WorldSpaceCameraPos + dir * (_ProjectionParams.z * _Radius);
                o.pos = TransformWorldToHClip(ws);
                o.col = half4(i.col.rgb, i.uv.x);   // a: haze
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                Light sun = GetMainLight();
                half lit = saturate(dot(sun.color, half3(0.3, 0.59, 0.11)) * 0.8 + 0.1);
                half3 c = i.col.rgb * lit;
                c = lerp(c, unity_FogColor.rgb, i.col.a);   // aerial perspective by real distance
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
