Shader "Herlilan/ZenTriptych/Stone" {
Properties { _BaseColor("Stone",Color)=(.6,.58,.52,1) _Rim("Cyan edge",Float)=0 }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass { Tags {"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _BaseColor; float _Rim;
CBUFFER_END
struct A {float4 p:POSITION;float3 n:NORMAL;};
struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
half4 frag(V i):SV_Target {
float3 n=normalize(i.n);Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
float diffuse=saturate(dot(n,l.direction));float3 ambient=SampleSH(n);
float3 c=_BaseColor.rgb*(ambient*.8+l.color*diffuse*lerp(.45,1,l.shadowAttenuation));
float rim=pow(1-saturate(dot(n,normalize(GetWorldSpaceViewDir(i.w)))),7);
c+=float3(.18,.8,1)*rim*_Rim*.85;return half4(c,1);}
ENDHLSL
}
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
UsePass "Universal Render Pipeline/Lit/DepthOnly"
UsePass "Universal Render Pipeline/Lit/DepthNormals"
}
}
