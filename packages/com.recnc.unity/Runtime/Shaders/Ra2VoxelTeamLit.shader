Shader "ReCnc/Ra2VoxelTeamLit"
{
    Properties
    {
        _MainTex ("Palette LUT (256x2)", 2D) = "white" {}
        _TeamColor ("Faction Team Color", Color) = (0.22, 0.55, 1.0, 1.0)
        _SelectedBoost ("Selection Rim Boost", Range(0, 1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TeamColor;
                half _SelectedBoost;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Row 0 (top in PNG -> v = 0.75) = diffuse/luminance, Row 1 (bottom -> v = 0.25) = team mask
                half3 baseRgb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(input.uv.x, 0.75)).rgb;
                half teamMask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(input.uv.x, 0.25)).r;

                half3 albedo = lerp(baseRgb, baseRgb * _TeamColor.rgb * 1.35h, step(0.5h, teamMask));

                float3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half shadow = lerp(0.55h, 1.0h, mainLight.shadowAttenuation);
                half3 diffuse = albedo * (0.42h + 0.72h * ndotl * shadow) * mainLight.color;

                // Subtle specular sheen on metallic voxel armor
                float3 viewDir = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = SafeNormalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(normalWS, halfDir)), 24.0h) * 0.18h * shadow;

                half3 finalCol = diffuse + spec + albedo * (_SelectedBoost * 0.28h);
                return half4(finalCol, 1.0h);
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
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
