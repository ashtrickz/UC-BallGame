Shader "Game/UI/Crystal Transition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Silhouette", Color) = (0, 0, 0, 1)
        _CrystalAtlas ("Gameplay Crystal Atlas (4 x 2)", 2D) = "black" {}
        _Coverage ("Coverage", Range(0, 1)) = 1
        _Columns ("Crystal Columns", Range(4, 20)) = 9
        _Cascade ("Cascade Spread", Range(0, 0.9)) = 0.65
        _CrystalScale ("Crystal Packing", Range(2, 5)) = 3.4
        _ReverseCascade ("Reverse Cascade", Float) = 0
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 screenPosition : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _CrystalAtlas;
            float4 _CrystalAtlas_TexelSize;
            fixed4 _Color;
            float _Coverage, _Columns, _Cascade, _CrystalScale, _ReverseCascade;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPosition = ComputeScreenPos(o.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_Coverage <= 0.0) return fixed4(0, 0, 0, 0);
                float2 uv = i.screenPosition.xy / i.screenPosition.w;
                // The atlas is rendered from the exact meshes/materials used in CrystalFieldView.
                float2 grid = float2(_Columns, _Columns * _ScreenParams.y / _ScreenParams.x);
                float2 position = uv * grid;
                float2 cellId = floor(position);
                float crystalAlpha = 0;
                // Neighbours let silhouettes cross cell boundaries without rectangular clipping.
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 id = cellId + float2(x, y);
                    float noise = frac(sin(dot(id, float2(127.1, 311.7))) * 43758.5453);
                    float random = frac(noise * 17.17);
                    float diagonal = dot(float2(id.x, grid.y - 1.0 - id.y) / max(grid - 1.0, 1.0), float2(0.5, 0.5));
                    float delay = saturate(diagonal * 0.9 + noise * 0.1);
                    delay = lerp(delay, 1.0 - delay, _ReverseCascade);
                    float t = saturate((_Coverage - delay * _Cascade) / max(0.01, 1.0 - _Cascade));
                    if (t <= 0.0) continue;
                    float growth = smoothstep(0.0, 0.85, t);
                    float size = _CrystalScale * (0.08 + growth * 0.92 + sin(t * UNITY_PI) * 0.1);
                    float2 center = id + 0.5 + float2(frac(id.y * 0.5) * 0.45 - 0.225, (random - 0.5) * 0.18);
                    // The pieces settle into a dense wall; reversing the same motion scatters them.
                    center += float2(random - 0.5, noise - 0.5) * (1.0 - growth) * 0.65;
                    float2 local = position - center;
                    float angle = (random - 0.5) * 0.55 + (1.0 - growth) * (noise - 0.5) * 1.1;
                    float sine, cosine;
                    sincos(angle, sine, cosine);
                    float2 portraitUV = float2(cosine * local.x - sine * local.y, sine * local.x + cosine * local.y) / size + 0.5;
                    if (any(portraitUV < 0.0) || any(portraitUV > 1.0)) continue;
                    float portrait = floor(noise * 8.0);
                    float2 tile = float2(fmod(portrait, 4.0), floor(portrait / 4.0));
                    // Stay inside a portrait's transparent padding, including at lower mip levels.
                    float2 inset = _CrystalAtlas_TexelSize.xy * float2(4, 2) * 2.0;
                    portraitUV = clamp(portraitUV, inset, 1.0 - inset);
                    // Cell IDs/rotations jump at boundaries: implicit derivatives would sample
                    // neighbouring atlas portraits there and draw thin rectangular seams.
                    float pixelsAcross = max(1.0, _ScreenParams.x / _Columns * size);
                    float mip = clamp(log2(_CrystalAtlas_TexelSize.z / (4.0 * pixelsAcross)), 0.0, 3.0);
                    float4 crystal = tex2Dlod(_CrystalAtlas, float4((portraitUV + tile) / float2(4, 2), 0, mip));
                    float alpha = crystal.a * smoothstep(0.0, 0.12, t);
                    crystalAlpha = alpha + crystalAlpha * (1.0 - alpha);
                }
                // Endpoint guarantees prevent an atlas filtering edge from leaking a pixel during loading.
                if (_Coverage >= 1.0) crystalAlpha = 1.0;
                fixed4 color = fixed4(_Color.rgb, crystalAlpha * _Color.a) * i.color;
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
