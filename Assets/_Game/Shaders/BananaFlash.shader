Shader "BananaRun/PickupFlash"
{
    Properties { _StarStrength ("Star strength", Range(0, 1)) = 0.4 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _StarStrength;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p = abs(input.uv * 2 - 1);
                float glow = pow(saturate(1 - length(p)), 3);
                float star = pow(saturate(1 - min(p.x, p.y) * 14), 3)
                    * pow(saturate(1 - max(p.x, p.y)), 2);
                return half4(input.color.rgb * 2, input.color.a * saturate(glow + star * _StarStrength));
            }
            ENDHLSL
        }
    }
}
