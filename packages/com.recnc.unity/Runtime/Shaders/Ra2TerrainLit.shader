Shader "ReCnc/Ra2TerrainLit"
{
    Properties
    {
        _PrimaryMap ("Primary Ground Albedo", 2D) = "white" {}
        _DirtSandMap ("Shore / Dirt Albedo", 2D) = "white" {}
        _RoadMap ("Road / Asphalt Albedo", 2D) = "gray" {}
        _RockMap ("Cliff / Rock Albedo", 2D) = "gray" {}
        _MacroTintA ("Macro Variation Light", Color) = (1.04, 1.03, 0.98, 1)
        _MacroTintB ("Macro Variation Dark", Color) = (0.86, 0.90, 0.84, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 250

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
                half4  color      : COLOR; // R = Road, G = Shore/Sand, B = CliffRock, A = Moisture/AO
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  splat      : COLOR;
            };

            TEXTURE2D(_PrimaryMap);  SAMPLER(sampler_PrimaryMap);
            TEXTURE2D(_DirtSandMap); SAMPLER(sampler_DirtSandMap);
            TEXTURE2D(_RoadMap);     SAMPLER(sampler_RoadMap);
            TEXTURE2D(_RockMap);     SAMPLER(sampler_RockMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _MacroTintA;
                half4 _MacroTintB;
            CBUFFER_END

            float2 RotateUv(float2 uv)
            {
                return float2(uv.x * 0.866 - uv.y * 0.5, uv.x * 0.5 + uv.y * 0.866);
            }

            half3 SampleAntiTile(TEXTURE2D_PARAM(tex, samp), float2 worldXZ, float scale)
            {
                float2 uvA = worldXZ * scale;
                float2 uvB = RotateUv(worldXZ * (scale * 0.37) + float2(13.7, 29.3));
                half3 a = SAMPLE_TEXTURE2D(tex, samp, uvA).rgb;
                half3 b = SAMPLE_TEXTURE2D(tex, samp, uvB).rgb;
                float blend = sin(worldXZ.x * 0.09) * cos(worldXZ.y * 0.08) * 0.5 + 0.5;
                return lerp(a, b, blend * 0.45);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.splat = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float2 xz = input.positionWS.xz;

                half3 colPrimary = SampleAntiTile(TEXTURE2D_ARGS(_PrimaryMap, sampler_PrimaryMap), xz, 0.13);
                half3 colSand    = SampleAntiTile(TEXTURE2D_ARGS(_DirtSandMap, sampler_DirtSandMap), xz, 0.15);
                half3 colRoad    = SAMPLE_TEXTURE2D(_RoadMap, sampler_RoadMap, xz * 0.18).rgb;

                // Triplanar-like sampling for steep cliff faces so vertical walls never stretch!
                float2 rockUv = float2(xz.x + xz.y, input.positionWS.y * 1.6) * 0.16;
                half3 colRock = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, rockUv).rgb;

                half wRoad  = saturate(input.splat.r);
                half wSand  = saturate(input.splat.g);
                half slopeRock = saturate((0.84h - normalWS.y) * 3.6h);
                half wRock  = saturate(max(input.splat.b, slopeRock));

                half3 albedo = colPrimary;
                albedo = lerp(albedo, colSand, wSand);
                albedo = lerp(albedo, colRoad, wRoad);
                albedo = lerp(albedo, colRock, wRock);

                // Large-scale natural landscape light/dark variation
                half macro = sin(xz.x * 0.055 + cos(xz.y * 0.048) * 1.8) * 0.5h + 0.5h;
                albedo *= lerp(_MacroTintB.rgb, _MacroTintA.rgb, macro);
                albedo *= lerp(0.82h, 1.04h, input.splat.a);

                // Subtle micro-bump from albedo luminance derivatives
                half lum = dot(albedo, half3(0.3h, 0.59h, 0.11h));
                float3 bumpN = normalize(normalWS + float3(-ddx(lum), 0.0, -ddy(lum)) * 1.35);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(bumpN, mainLight.direction));
                half shadow = lerp(0.48h, 1.0h, mainLight.shadowAttenuation);

                half3 skyAmb = half3(0.36h, 0.40h, 0.46h) * (0.5h + 0.5h * bumpN.y);
                half3 lit = albedo * (skyAmb + 0.78h * ndotl * shadow * mainLight.color);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
