Shader "ReCnc/Ra2WaterLit"
{
    Properties
    {
        _DeepColor ("Deep Water Color", Color) = (0.06, 0.24, 0.44, 0.88)
        _ShallowColor ("Shallow Highlight Color", Color) = (0.16, 0.50, 0.72, 0.84)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float t = _Time.y;

                // Dual scrolling wave ripples
                float w1 = sin(p.x * 1.35 + t * 1.6) * cos(p.y * 1.15 - t * 1.2);
                float w2 = sin((p.x + p.y) * 2.4 - t * 2.1) * 0.5;
                float wave = (w1 + w2) * 0.5;

                float3 normalWS = normalize(float3(
                    cos(p.x * 1.35 + t * 1.6) * 0.14 + cos((p.x + p.y) * 2.4 - t * 2.1) * 0.08,
                    1.0,
                    -sin(p.y * 1.15 - t * 1.2) * 0.14 + cos((p.x + p.y) * 2.4 - t * 2.1) * 0.08));

                Light mainLight = GetMainLight();
                float3 viewDir = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = SafeNormalize(mainLight.direction + viewDir);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), 3.0h);
                half spec = pow(saturate(dot(normalWS, halfDir)), 64.0h) * 0.85h;

                half3 waterCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(wave * 0.5h + 0.5h));
                waterCol = lerp(waterCol, half3(0.58h, 0.74h, 0.90h), fresnel * 0.45h) + spec * mainLight.color;
                return half4(waterCol, _DeepColor.a);
            }
            ENDHLSL
        }
    }
}
