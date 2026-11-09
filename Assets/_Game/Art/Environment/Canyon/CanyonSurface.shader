Shader "BallGame/Environment/Canyon Surface"
{
    Properties
    {
        _BaseColor ("Palette tint", Color) = (1,1,1,1)
        _ShadowTint ("Cool shadow tint", Color) = (0.64,0.72,0.84,1)
        _Brightness ("Brightness", Range(0.5,1.5)) = 1
        _Variation ("Broad surface variation", Range(0,0.1)) = 0.025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "CanyonForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowTint;
                float _Brightness, _Variation;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float4 shadowCoord : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.shadowCoord = GetShadowCoord(p);
                o.color = input.color;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                // A large ground polygon can span several cascades: choose the cascade per pixel.
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light key = GetMainLight(shadowCoord);
                half illumination = saturate(dot(n,key.direction)*0.5h+0.5h);
                half band = 0.42h + 0.30h*smoothstep(0.28h,0.38h,illumination)
                    + 0.28h*smoothstep(0.64h,0.76h,illumination);
                band *= lerp(0.45h,1.0h,key.shadowAttenuation);
                half3 baseColor = input.color.rgb * _BaseColor.rgb;
                float mottling = sin(input.positionWS.x*1.13 + sin(input.positionWS.z*0.61))
                    * sin(input.positionWS.z*1.47 + input.positionWS.x*0.23);
                half3 color = lerp(baseColor*_ShadowTint.rgb,baseColor,band);
                color *= _Brightness * (1.0h + mottling*_Variation);
                return half4(color,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
