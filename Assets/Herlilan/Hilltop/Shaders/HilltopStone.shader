Shader "Herlilan/Hilltop/Natural Stone"
{
    Properties
    {
        [MainColor] _BaseColor("Stone tint", Color) = (0.52,0.51,0.46,1)
        [MainTexture] _BaseMap("Base", 2D) = "white" {}
        _Grain("Mineral grain", Range(0,1)) = 0.35
        _Scale("Grain scale", Float) = 22
        _Smoothness("Smoothness", Range(0,1)) = 0.19
        _Cull("Cull", Float) = 2
        _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _Grain, _Scale, _Smoothness, _Cull, _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 local:TEXCOORD2; half fog:TEXCOORD3; };
            float Hash(float3 p) { p=frac(p*0.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float Noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.local=v.positionOS.xyz; o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 p=i.local*_Scale;
                float broad=Noise(p*0.72);
                float mineral=Noise(p*4.7);
                float grain=Noise(p*19);
                float band=sin((p.y+p.x*.23+Noise(p*.63)*1.8)*4.5)*.5+.5;
                float paleVein=pow(saturate(1-abs(band-.57)*20),3)*.07;
                half3 albedo=_BaseColor.rgb*(.91+_Grain*((broad-.5)*.48+(mineral-.5)*.25+(grain-.5)*.13));
                albedo+=paleVein*_Grain;
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo; surface.alpha=1; surface.metallic=0;
                surface.specular=half3(.04,.04,.04); surface.smoothness=_Smoothness;
                surface.normalTS=half3(0,0,1); surface.occlusion=1;
                InputData data=(InputData)0;
                data.positionWS=i.positionWS; data.positionCS=i.positionCS;
                data.normalWS=normalize(i.normalWS); data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                data.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                data.bakedGI=SampleSH(data.normalWS); data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                data.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(data,surface);
                color.rgb=MixFog(color.rgb,i.fog); return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
