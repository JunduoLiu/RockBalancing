Shader "RockBalancing/Arcade Cel"
{
    Properties
    {
        [MainColor] _BaseColor ("Surface color", Color) = (1, 1, 1, 1)
        _FacetContrast ("Rock facet contrast", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Light strips", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            half _FacetContrast;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half facetTone : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.facetTone = lerp(1.0h - _FacetContrast, 1.0h, input.color.r);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light key = GetMainLight(shadowCoord);
                half3 normal = normalize(input.normalWS);
                half incidence = dot(normal, key.direction);
                half band = .21h * step(.16h, incidence) + .27h * step(.65h, incidence);

                // Bounce light separates differently oriented facets even beneath the roof.
                half bounce = dot(normal, half3(.78h, .56h, -.28h));
                half fill = .55h + .16h * bounce;
                half3 shadow = half3(.90h, .98h, 1.04h) * fill;
                half3 direct = (.13h + band) * key.color * key.shadowAttenuation;
                return half4(_BaseColor.rgb * input.facetTone * (shadow + direct) + _EmissionColor.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
