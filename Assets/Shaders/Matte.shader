Shader "Custom/URPShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.8
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // 👇 مهم!
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

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
                float4 shadowCoord : TEXCOORD1;
            };

            float4 _ShadowColor;
            float _ShadowStrength;

            Varyings vert (Attributes v)
            {
                Varyings o;

                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);

                o.shadowCoord = TransformWorldToShadowCoord(o.positionWS);

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                Light mainLight = GetMainLight(i.shadowCoord);

                float shadow = mainLight.shadowAttenuation;

                float shadowFactor = (1.0 - shadow) * _ShadowStrength;

                return float4(_ShadowColor.rgb, shadowFactor * _ShadowColor.a);
            }

            ENDHLSL
        }
    }
}