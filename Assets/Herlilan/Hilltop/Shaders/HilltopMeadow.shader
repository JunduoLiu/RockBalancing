Shader "Herlilan/Hilltop/Meadow Wind"
{
    Properties
    {
        [MainColor] _BaseColor("Root green", Color) = (0.26,0.40,0.12,1)
        _TipColor("Sunlit tips", Color) = (0.66,0.73,0.29,1)
        _Wind("Wind amplitude", Range(0,0.12)) = 0.025
        _Speed("Wind speed", Range(0,3)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _TipColor;
            float _Wind, _Speed;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; half fog:TEXCOORD3; };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            float3 p=TransformObjectToWorld(v.positionOS.xyz);
            float t=_Time.y*_Speed;
            float gust=sin(p.x*1.6+p.z*.9-t*1.5)*.6+sin(p.z*3.3-p.x*.5-t*2.3)*.4;
            p.xz+=float2(1,.43)*gust*_Wind*v.uv.y*v.uv.y;
            o.positionWS=p; o.positionCS=TransformWorldToHClip(p);
            o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.uv=v.uv; o.color=v.color; o.fog=ComputeFogFactor(o.positionCS.z); return o;
        }
        ENDHLSL
        Pass
        {
            Name "Meadow"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            half4 Frag(Varyings i):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 n=normalize(i.normalWS);
                half diffuse=.42+.58*saturate(dot(n,sun.direction)*.5+.5);
                half3 albedo=lerp(_BaseColor.rgb,_TipColor.rgb,pow(saturate(i.uv.y),.7));
                albedo*=lerp(.78,1.12,i.color.r);
                half3 light=SampleSH(half3(0,1,0))*.68+sun.color*diffuse*lerp(.48,1,sun.shadowAttenuation)*.67;
                half3 result=albedo*light;
                return half4(MixFog(result,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            half4 FragDepth(Varyings i):SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
