Shader "RockBalancing/Arcade Cabinet Glass"
{
    Properties
    {
        _Tint ("Glass tint", Color) = (0.62, 0.87, 0.85, 0.07)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Reflections taper out at the corners, leaving the stacking sightline clear.
                float2 uv = input.uv;
                float diagonal = uv.x + 0.40 * uv.y;
                float streak = 1.0 - smoothstep(0.003, 0.013, abs(diagonal - 0.466));
                streak += 0.48 * (1.0 - smoothstep(0.002, 0.006, abs(diagonal - 0.485)));
                streak *= smoothstep(0.60, 0.95, uv.y);
                float2 edge = min(uv, 1.0 - uv);
                float rim = 1.0 - smoothstep(0.0, 0.027, min(edge.x, edge.y));
                return half4(lerp(_Tint.rgb, half3(0.92, 1.0, 0.98), streak),
                             _Tint.a + 0.25 * streak + 0.084 * rim);
            }
            ENDHLSL
        }
    }
}
