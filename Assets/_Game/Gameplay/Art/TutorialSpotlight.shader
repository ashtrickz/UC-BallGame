// Full-screen UI dimmer with soft rounded cut-outs ("spotlights") and a pulsing ring around each hole.
// Hole rectangles are given in the root canvas space (the space UI vertices are batched in).
Shader "BallGame/UI/Tutorial Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Dim colour", Color) = (0.02, 0.05, 0.07, 0.8)
        _RingColor ("Ring colour", Color) = (0.88, 1, 0.53, 1)
        _Softness ("Edge softness (canvas units)", Float) = 22
        _RingWidth ("Ring width (canvas units)", Float) = 9
        _PulseSpeed ("Ring pulse speed", Float) = 4

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            Name "Spotlight"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define MAX_HOLES 6

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 canvasPos : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _RingColor;
            float _Softness;
            float _RingWidth;
            float _PulseSpeed;
            float _HoleCount;
            // xy = centre, zw = half size (canvas units)
            float4 _Holes[MAX_HOLES];
            // x = corner radius, y = ring strength 0..1
            float4 _HoleStyles[MAX_HOLES];

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.canvasPos = v.vertex.xy;
                return o;
            }

            float RoundBox (float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float distance = 100000.0;
                float ring = 0.0;
                for (int k = 0; k < MAX_HOLES; k++)
                {
                    if (k >= (int)_HoleCount) break;
                    float4 hole = _Holes[k];
                    float radius = min(_HoleStyles[k].x, min(hole.z, hole.w));
                    float d = RoundBox(i.canvasPos - hole.xy, hole.zw, radius);
                    distance = min(distance, d);
                    ring = max(ring, _HoleStyles[k].y * saturate(1.0 - abs(d - _RingWidth) / _RingWidth));
                }
                float dim = _Color.a * smoothstep(0.0, _Softness, distance);
                float pulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed);
                float ringAlpha = saturate(ring * pulse * _RingColor.a);
                fixed3 rgb = lerp(_Color.rgb, _RingColor.rgb, ringAlpha);
                return fixed4(rgb, max(dim, ringAlpha) * i.color.a);
            }
            ENDCG
        }
    }
}
