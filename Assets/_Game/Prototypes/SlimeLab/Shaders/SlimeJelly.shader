Shader "BallGame/Prototypes/Slime Jelly"
{
    Properties
    {
        _BaseColor ("Cartoon jelly / lower color", Color) = (0.34, 0.94, 0.27, 1)
        _TopColor ("Cartoon jelly / upper color", Color) = (0.08, 0.88, 0.69, 1)
        _ShadowTint ("Colored shadow", Color) = (0.24, 0.76, 0.67, 1)
        _RimColor ("Lime transmitted edge", Color) = (0.88, 1, 0.45, 1)
        [HDR] _HighlightColor ("Cream highlight", Color) = (1.6, 1.6, 1.4, 1)
        _Brightness ("Color brightness", Range(0.5, 1.5)) = 1.05
        _RimStrength ("Transmitted edge strength", Range(0, 1)) = 0.9
        _BubbleStrength ("Subtle internal bubbles", Range(0, 1)) = 0.22
        _Gloss ("Toon specular size", Range(8, 160)) = 48
        _Wobble ("Surface wobble", Range(0, 0.08)) = 0.018
        _WobbleSpeed ("Wobble speed", Range(0, 12)) = 3.5
        _Impact ("Impact ripple", Range(0, 1)) = 0
        _Pull ("Pull towards drop", Range(0, 0.7)) = 0
        _PullDirection ("Pull direction (object space)", Vector) = (1, 0, 0, 0)
        _Phase ("Motion phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TopColor;
            half4 _ShadowTint;
            half4 _RimColor;
            half4 _HighlightColor;
            float4 _PullDirection;
            float _Gloss, _Wobble, _WobbleSpeed, _Impact, _Pull, _Phase;
            float _Brightness, _RimStrength, _BubbleStrength;
        CBUFFER_END

        float3 Deform(float3 p)
        {
            float3 n = normalize(p + float3(0, 0.00001, 0));
            float t = _Time.y * _WobbleSpeed + _Phase;
            float wave = sin(n.x * 4.2 + t) * sin(n.z * 3.6 - t * 0.8);
            wave += 0.5 * sin(n.y * 5.1 + t * 1.35);
            float ripple = sin(n.y * 11.0 - _Time.y * 20.0) * _Impact * 0.025;
            p += n * (wave * _Wobble + ripple);
            float3 direction = normalize(_PullDirection.xyz + float3(0.00001, 0, 0));
            float mask = pow(saturate(dot(n, direction)), 3.5);
            p += direction * (mask * _Pull);
            return p;
        }

        void DeformedSurface(float3 positionOS, float3 normalOS, out float3 p, out float3 n)
        {
            p = Deform(positionOS);
            n = normalOS;
            // The bridge is an ordinary tube with wobble/pull disabled. Keep its mesh normals.
            if (_Wobble + _Pull + _Impact > 0.0001)
            {
                float3 sphereN = normalize(positionOS);
                float3 axis = abs(sphereN.y) < 0.95 ? float3(0,1,0) : float3(1,0,0);
                float3 tangent = normalize(cross(axis, sphereN));
                float3 bitangent = cross(sphereN, tangent);
                float radius = length(positionOS);
                float3 pt = normalize(positionOS + tangent * 0.004) * radius;
                float3 pb = normalize(positionOS + bitangent * 0.004) * radius;
                n = normalize(cross(Deform(pt) - p, Deform(pb) - p));
            }
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float4 shadowCoord : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings JellyVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 p, n;
            DeformedSurface(input.positionOS.xyz, input.normalOS, p, n);
            VertexPositionInputs positions = GetVertexPositionInputs(p);
            output.positionCS = positions.positionCS;
            output.positionWS = positions.positionWS;
            output.normalWS = TransformObjectToWorldNormal(n);
            output.shadowCoord = GetShadowCoord(positions);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "JellyForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex JellyVertex
            #pragma fragment JellyFragment
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "SlimeCartoonLighting.hlsl"

            half4 JellyFragment(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight(input.shadowCoord);
                half ndv = saturate(dot(n, v));
                half top = smoothstep(-0.65h, 0.75h, n.y);
                half3 jelly = lerp(_BaseColor.rgb, _TopColor.rgb, top * 0.85h);
                half3 color = SlimeCartoonDiffuse(jelly, _ShadowTint.rgb,
                    dot(n, light.direction), light.shadowAttenuation) * _Brightness;
                color *= 0.66h + 0.34h * sqrt(ndv);
                color += jelly * pow(ndv, 3.0h) * 0.10h;
                // Broad inner light and a pale contour sell jelly without a scene-color copy.
                half fresnel = 1.0h - ndv;
                half rim = smoothstep(0.62h, 0.97h, fresnel);
                half3 edgeColor = lerp(_RimColor.rgb, _HighlightColor.rgb, smoothstep(-0.05h, 0.65h, n.y));
                color += _BaseColor.rgb * pow(fresnel, 2.5h) * 0.23h;
                color = lerp(color, edgeColor, rim * _RimStrength);
                half lowerGlow = pow(saturate(-n.y * 0.65h + 0.35h), 3.0h);
                color += _RimColor.rgb * lowerGlow * 0.18h;

                half3 normalVS = mul((half3x3)UNITY_MATRIX_V, n);
                half front = smoothstep(0.1h, 0.45h, normalVS.z);
                half card = SlimeCartoonSpot(normalVS.xy, float2(-0.34, 0.57), float2(0.24, 0.095));
                half smallCard = SlimeCartoonSpot(normalVS.xy, float2(0.47, 0.43), float2(0.065, 0.13));
                half sideCard = SlimeCartoonSpot(normalVS.xy, float2(-0.76, 0.02), float2(0.055, 0.12));
                half highlights = max(card, max(smallCard, sideCard * 0.75h)) * front;
                half3 h = SafeNormalize(light.direction + v);
                half specular = pow(saturate(dot(n, h)), _Gloss * 0.5h);
                color += _HighlightColor.rgb * specular * 0.10h;
                color = lerp(color, _HighlightColor.rgb, highlights * 0.96h);

                float2 bubbleUV = normalVS.xy;
                half bubble1 = abs(length((bubbleUV - float2(-0.26, -0.36)) / 0.085) - 1.0);
                half bubble2 = abs(length((bubbleUV - float2(0.08, -0.52)) / 0.045) - 1.0);
                half bubbles = 1.0h - smoothstep(0.06h, 0.17h, min(bubble1, bubble2));
                color = lerp(color, _RimColor.rgb, bubbles * front * _BubbleStrength);
                return half4(color, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                float3 p, n;
                DeformedSurface(input.positionOS.xyz, input.normalOS, p, n);
                float3 positionWS = TransformObjectToWorld(p);
                float3 normalWS = TransformObjectToWorldNormal(n);
                float3 lightDirection = _LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    lightDirection = normalize(_LightPosition - positionWS);
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #endif
                return positionCS;
            }
            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex JellyVertex
            #pragma fragment DepthFragment
            half4 DepthFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
