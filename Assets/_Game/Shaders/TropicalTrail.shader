Shader "BananaRun/TropicalTrail"
{
    Properties
    {
        _DirtColor ("Warm Dirt", Color) = (0.65,0.39,0.16,1)
        _GrassColor ("Grass", Color) = (0.29,0.48,0.09,1)
        _HalfWidth ("Trail Half Width", Float) = 4.7
        _EdgeBlend ("Grass Transition", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DirtColor, _GrassColor;
                float _HalfWidth, _EdgeBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p=i.positionWS.xz;
                float edge=(noise(float2(p.y*.16,3))-.5)*.65 + (noise(p*1.4)-.5)*.2;
                float grass=smoothstep(_HalfWidth+edge,_HalfWidth+_EdgeBlend+edge,abs(p.x));
                float flecks=noise(p*8);
                float variation=(noise(p*.55)-.5)*.22 + (flecks-.5)*.06;
                half3 albedo=lerp(_DirtColor.rgb,_GrassColor.rgb,grass)*(1+variation);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 ambient=SampleSH(half3(0,1,0));
                half3 color=albedo*(ambient + sun.color * saturate(sun.direction.y) * sun.shadowAttenuation);
                // Evaluate fog per pixel: this surface spans the whole course, so vertex fog
                // would incorrectly wash out even the ground immediately under the camera.
                half fog = ComputeFogFactor(TransformWorldToHClip(i.positionWS).z);
                return half4(MixFog(color,fog),1);
            }
            ENDHLSL
        }
    }
}
