// The phone's frosted glass (owner, 22 Nov 2026: "crazy glassmorphism"). uGUI cannot blur what is behind it, so the glass
// samples a blurred copy of the phone's wallpaper (_NtPhoneBlur, set by Phone.cs) at the pixel's place on the phone
// screen (_NtPhoneRect: the screen's rect in the root canvas' space, the same space the UI vertices come in), then mixes
// in the tint (Image.color rgb, by the material's _Tint; Image.color a and any CanvasGroup fade it as usual; the sprite's
// alpha gives the shape) and a faint grain. Stencil / clip rect as UI/Default, so it works inside the screen's rounded Mask.
Shader "Nainital/UIGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Shape (alpha)", 2D) = "white" {}
        _Lift ("Lift", Range(0, 1)) = 0.08
        _Tint ("Tint amount", Range(0, 1)) = 0.22
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Glass"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            sampler2D _NtPhoneBlur;
            float4 _NtPhoneRect;   // x0, y0, x1, y1
            float4 _ClipRect;
            float _Lift, _Tint;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 size = max(_NtPhoneRect.zw - _NtPhoneRect.xy, float2(1, 1));
                float2 suv = saturate((IN.worldPosition.xy - _NtPhoneRect.xy) / size);
                half3 behind = tex2D(_NtPhoneBlur, suv).rgb;
                behind = behind + _Lift * (1 - behind);                 // frosted: lifted towards white
                half3 col = lerp(behind, IN.color.rgb, _Tint);           // the tint
                half grain = frac(sin(dot(IN.worldPosition.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                col += grain * 0.025;
                half a = tex2D(_MainTex, IN.texcoord).a * IN.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(a - 0.001);
                #endif
                return fixed4(col, a);
            }
        ENDCG
        }
    }
}
