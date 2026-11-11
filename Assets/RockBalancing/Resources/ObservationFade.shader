Shader "RockBalancing/ObservationFade"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Opacity ("Visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Smooth alpha, with no depth writes: obscured stones stay visible
                // through the base and the ground disappears completely below it.
                clip(_Opacity - .0001);
                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half3 illumination = SampleSH(normal) + light.color * saturate(dot(normal,light.direction));
                return half4(_BaseColor.rgb * illumination,_Opacity);
            }
            ENDHLSL
        }
    }
}
