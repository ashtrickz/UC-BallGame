Shader "BallGame/Prototypes/Cartoon Crystal"
{
    Properties
    {
        _BaseColor ("Candy crystal", Color) = (1, 0.36, 0.29, 1)
        _ShadowTint ("Colored facet shadow", Color) = (0.52, 0.29, 0.64, 1)
        _TipColor ("Lit crystal tip", Color) = (1, 0.79, 0.62, 1)
        _EdgeColor ("Fine illustrated edges", Color) = (1, 0.9, 0.73, 1)
        _EdgeWidth ("Edge width", Range(0.001, 0.04)) = 0.006
        _EdgeStrength ("Edge strength", Range(0, 1)) = 0.28
        _Brightness ("Color brightness", Range(0.5, 1.5)) = 1.05
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "CartoonFacets"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CrystalVertex
            #pragma fragment CrystalFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SlimeCartoonLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowTint, _TipColor, _EdgeColor;
                float _EdgeWidth, _EdgeStrength, _Brightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings CrystalVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(p);
                return output;
            }

            half4 CrystalFragment(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                Light light = GetMainLight(input.shadowCoord);
                half height = saturate(input.positionOS.y);
                half3 base = lerp(_BaseColor.rgb * 0.86h, _BaseColor.rgb, smoothstep(0, 0.55h, height));
                base = lerp(base, _TipColor.rgb, smoothstep(0.52h, 1.0h, height) * 0.48h);
                half3 color = SlimeCartoonDiffuse(base, _ShadowTint.rgb,
                    dot(n, light.direction), light.shadowAttenuation) * _Brightness;
                color *= 0.72h + 0.28h * smoothstep(-0.1h, 0.15h, dot(n, light.direction));

                // Lines follow the six real prism corners, avoiding diagonal triangle outlines.
                float2 centered = input.positionOS.xz - float2(0.08, -0.04) * saturate((height - 0.7) / 0.3);
                float angle = atan2(centered.y, centered.x) * (3.0 / PI);
                float sector = frac(angle);
                float distanceToCorner = min(sector, 1.0 - sector);
                float aa = max(fwidth(distanceToCorner), 0.002);
                half edge = 1.0h - smoothstep(_EdgeWidth, _EdgeWidth + aa, distanceToCorner);
                half shoulder = 1.0h - smoothstep(0.006, 0.006 + max(fwidth(height), 0.006), abs(height - 0.7));
                edge = max(edge, shoulder * 0.2h);
                color = lerp(color, _EdgeColor.rgb, edge * _EdgeStrength);
                return half4(color, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
