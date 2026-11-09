Shader "Game/UI/Star Shimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ShimmerColor ("Highlight Color", Color) = (1, 0.98, 0.86, 1)
        _ShimmerStrength ("Highlight Strength", Range(0, 1)) = 0.7
        _ShimmerWidth ("Highlight Width", Range(0.02, 0.5)) = 0.2
        _ShimmerAngle ("Tilt From Vertical (Degrees)", Range(-30, 30)) = 12
        _SweepDuration ("Sweep Duration (Seconds)", Float) = 0.9
        _PauseDuration ("Pause Between Sweeps (Seconds)", Float) = 2.1
        _PhaseOffset ("Cycle Offset", Range(0, 1)) = 0

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
            Name "StarShimmer"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 localUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 localUV : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _ShimmerColor;
            float4 _ClipRect;
            float _ShimmerStrength;
            float _ShimmerWidth;
            float _ShimmerAngle;
            float _SweepDuration;
            float _PauseDuration;
            float _PhaseOffset;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.localUV = v.localUV;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float sweep = max(_SweepDuration, 0.01);
                float cycle = sweep + max(_PauseDuration, 0.0);
                float phase = frac(_Time.y / cycle + _PhaseOffset) * cycle / sweep;
                float tilt = tan(radians(_ShimmerAngle));
                float padding = _ShimmerWidth + abs(tilt) * 0.5;
                float center = lerp(-padding, 1.0 + padding, saturate(phase));
                float distanceToBand = abs(i.localUV.x - (i.localUV.y - 0.5) * tilt - center);
                float band = 1.0 - smoothstep(0.0, _ShimmerWidth, distanceToBand);
                band *= step(phase, 1.0) * _ShimmerStrength * _ShimmerColor.a;
                color.rgb = lerp(color.rgb, _ShimmerColor.rgb * i.color.rgb, band);

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
