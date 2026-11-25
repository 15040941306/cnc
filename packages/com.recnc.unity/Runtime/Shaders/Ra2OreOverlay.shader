Shader "ReCnc/Ra2OreOverlay"
{
    Properties
    {
        _MainTex ("Ore / Gem / Drill Atlas", 2D) = "white" {}
        _SparkleStrength ("Crystal Sparkle Intensity", Range(0, 1)) = 0.0
        _TotalFrames ("Animated Horizontal Frames", Float) = 1.0
        _AnimFps ("Animation FPS", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardOre"
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _SparkleStrength;
                float _TotalFrames;
                float _AnimFps;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = normInputs.normalWS;

                float2 uv = input.uv;
                if (_TotalFrames > 1.5)
                {
                    float frame = floor(fmod(_Time.y * _AnimFps, _TotalFrames));
                    uv.x = (uv.x + frame) / _TotalFrames;
                }
                o.uv = uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (tex.a < 0.04h) discard;

                // Shadow pixels have alpha ~0.42 and RGB=0; mineral pixels have alpha=1
                if (tex.a < 0.85h)
                {
                    return tex;
                }

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(0.58h, 1.0h, mainLight.shadowAttenuation);
                half3 lit = tex.rgb * shadow;

                // Periodic RA2 gem sparkle glint
                if (_SparkleStrength > 0.01)
                {
                    float phase = frac(sin(dot(floor(input.positionWS.xz * 2.5), float2(12.9898, 78.233))) * 43758.5453);
                    float glint = pow(saturate(sin(_Time.y * 3.8 + phase * 6.2831)), 18.0);
                    lit += half3(0.95h, 0.85h, 1.0h) * (half)(glint * _SparkleStrength);
                }

                return half4(lit, tex.a);
            }
            ENDHLSL
        }
    }
}
