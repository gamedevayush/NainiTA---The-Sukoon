// Mall Road shop fronts (user, 2026-11-17: restaurants, shops, hotels, banks... each with its own room behind; "photo
// lgaaye", "crowd bhi photos me"). The glass (or the open front) of one shop: the room is a photo on a plane a couple
// of metres behind it (NainitalRooms.hlsl), a window also reflects the sky and the street. Built by
// WorldBuilder.MallFronts.cs with MeshBuilder.QuadTagged: uv = metres on the opening (x from its centre along
// cross(N, up), i.e. to the right of someone facing it; y up from the floor), vertex colour r = room index (0..31),
// g = 255 for an open shop (no glass).
// _EmissionColor is the town's night glow (DayNightCycle.emissiveMaterials), the shops' lights at night.
Shader "Nainital/ShopGlass"
{
    Properties
    {
        [NoScaleOffset] _RoomTex("Rooms (photo atlas)", 2D) = "black" {}
        [HDR] _EmissionColor("Night glow (set by DayNightCycle)", Color) = (0, 0, 0, 1)
        _Depth("(unused: set in Nainital > Fake Interior Look)", Float) = 3.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor;
            float _Depth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "NainitalRooms.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float2 tag        : TEXCOORD3;   // room index, open (float: a half 8 came out 7.9999 and flickered between tiles)
                half   fog        : TEXCOORD4;
                float  halfW      : TEXCOORD5;   // the opening's half width: every corner's |uv.x| is it, so it interpolates flat
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                o.halfW = abs(i.uv.x);
                o.tag = float2(round(i.color.r * 255.0), i.color.g);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float _NtGlassDebug;   // global, 1: flat colour per room tag (InteriorCost.GlassDebug), to find fighting quads

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (_NtGlassDebug > 0.5)
                    return half4(frac(i.tag.x * 0.37h), i.tag.y, frac(i.tag.x * 0.71h + 0.2h), 1.0h);
                float3 N = normalize(i.normalWS);
                float3 T = normalize(cross(N, float3(0.0, 1.0, 0.0)));   // to the right of someone facing the front
                // the owner's opening (InteriorLook: width share, sill, top): outside it, a painted panel (chosen at
                // the end, not returned early: the photo's derivative sample must not sit behind a per-pixel branch)
                bool panel = _NtLookSet > 0.5 && (abs(i.uv.x) > i.halfW * _NtMallOpenW + 1e-4 || i.uv.y < _NtMallSill || i.uv.y > _NtMallTop);
                float3 ray = i.positionWS - GetCameraPositionWS();
                bool open = i.tag.y > 0.5h;
                // framing and depths: the owner's InteriorLook, "Mall Road shops" (the material's _Depth is not read)
                NtFrame f = NtMallFrame();
                float2 plane = NtBackPlane(i.uv, ray, T, N, open ? f.depthB : f.depthA);
                half inside;
                float2 puv = NtPhotoUV(floor(i.tag.x + 0.5), plane, f, inside);   // whole room index after interpolation
                half3 photo = SAMPLE_TEXTURE2D(_RoomTex, sampler_RoomTex, puv).rgb * inside;
                // a shop keeps its tube lights on by day too (Mall Road in video 02): a steady glow under the night one
                half dl = NtShopDayLight();
                half3 col = NtRoomLight(photo, _EmissionColor.rgb * 0.7h + half3(dl, dl * 0.96h, dl * 0.88h));
                if (!open)
                {
                    // glass: the sky and the street in it, more at a slant (Schlick), a faint green-grey tint
                    half3 V = (half3)normalize(-ray);
                    half fres = 0.04h + 0.96h * pow(1.0h - saturate(dot((half3)N, V)), 5.0h);
                    // the sky cubemap URP keeps for Forward+ (unity_SpecCube0, which the 3-argument
                    // GlossyEnvironmentReflection reads, is not set up in Forward+: it gave a speckle of garbage, 17 Nov)
                    half4 enc = SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, reflect(-V, (half3)N), PerceptualRoughnessToMipmapLevel(0.06h));
                    half3 refl = DecodeHDREnvironment(enc, _GlossyEnvironmentCubeMap_HDR);
                    col = lerp(col * half3(0.92h, 0.96h, 0.95h), refl, fres * 0.85h);
                }
                if (panel)
                {
                    half sunP = dot(_MainLightColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                    half lit = 0.3h + 0.7h * saturate(dot((half3)N, (half3)_MainLightPosition.xyz)) * sunP;
                    col = _NtMallPanel.rgb * lit;
                }
                col = MixFog(col, i.fog);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = GetVertexPositionInputs(i.positionOS.xyz).positionCS;   // the forward pass's exact maths: depth priming tests Equal
                return o;
            }
            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = GetVertexPositionInputs(i.positionOS.xyz).positionCS;   // the forward pass's exact maths: depth priming tests Equal
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n) * 0.5 + 0.5;
                return half4(PackFloat2To888(oct), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}
