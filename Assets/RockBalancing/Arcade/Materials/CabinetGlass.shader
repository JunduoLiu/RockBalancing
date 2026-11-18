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
                // Two short reflection bands at the upper corner leave the central sightline clear.
                float2 uv = input.uv;
                float diagonal = uv.x + .40 * uv.y;
                float aa = max(fwidth(diagonal), .0001);
                float streak = 1.0 - smoothstep(.007 - aa, .007 + aa, abs(diagonal - .466));
                streak += .45 * (1.0 - smoothstep(.002 - aa, .002 + aa, abs(diagonal - .483)));
                streak *= smoothstep(.69, .94, uv.y);
                float2 edge = min(uv, 1.0 - uv);
                float nearest = min(edge.x, edge.y);
                float rimAA = max(fwidth(nearest), .0001);
                float rim = 1.0 - smoothstep(.012 - rimAA, .012 + rimAA, nearest);
                return half4(lerp(_Tint.rgb, half3(.92, 1, .98), streak),
                             _Tint.a + .20 * streak + .055 * rim);
            }
            ENDHLSL
        }
    }
}
