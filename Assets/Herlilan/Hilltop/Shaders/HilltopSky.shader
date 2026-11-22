Shader "Herlilan/Hilltop/Soft Sky"
{
    Properties
    {
        _Horizon("Horizon", Color) = (0.84,0.88,0.76,1)
        _Zenith("Zenith", Color) = (0.43,0.66,0.72,1)
        _Ground("Below horizon", Color) = (0.66,0.76,0.63,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon, _Zenith, _Ground;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.direction=i.positionOS.xyz; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                half3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.7));
                c=lerp(c,_Ground.rgb,saturate(-d.y*2));
                float haze=pow(saturate(dot(d,normalize(float3(-.6,.4,-.5)))),14);
                return half4(c+haze*half3(.05,.034,.009),1);
            }
            ENDHLSL
        }
    }
}
