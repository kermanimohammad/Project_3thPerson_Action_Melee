Shader "Custom/UIBlur_Fixed"
{
    Properties
    {
        _BlurSize ("Blur Size", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float _BlurSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);

                // 👇 تبدیل به Screen UV
                float4 screenPos = ComputeScreenPos(o.positionHCS);
                o.screenUV = screenPos.xy / screenPos.w;

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.screenUV;
                float2 offset = _BlurSize / _ScreenParams.xy;

                half4 col = 0;

                col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-offset.x, -offset.y));
                col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( offset.x, -offset.y));
                col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-offset.x,  offset.y));
                col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( offset.x,  offset.y));

                return col * 0.25;
            }

            ENDHLSL
        }
    }
}