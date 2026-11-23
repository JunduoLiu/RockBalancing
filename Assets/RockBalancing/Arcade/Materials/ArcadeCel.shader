Shader "RockBalancing/Arcade Cel"
{
    Properties
    {
        [MainColor] _BaseColor ("Surface color", Color) = (1, 1, 1, 1)
        _FacetContrast ("Rock facet contrast", Range(0, 1)) = 0
        [Enum(Plain,0,Granite,1,Shale,2,Basalt,3,Crystal,4,Gemstone,5,Gold,6)] _StoneFinish ("Stone finish", Float) = 0
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
            half _StoneFinish;
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
                float3 positionOS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.facetTone = lerp(1.0h - _FacetContrast, 1.0h, input.color.r);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Fractional arithmetic keeps the grain stable on the moving object.
            float GrainHash(float3 cell)
            {
                cell = frac(cell * .1031);
                cell += dot(cell, cell.yzx + 33.33);
                return frac((cell.x + cell.y) * cell.z);
            }

            half3 StoneFinish(half3 lit, float3 local, half3 normal, half3 view, Light key)
            {
                if (_StoneFinish < 1.5h)
                {
                    float3 grain = local * 54;
                    float3 cell = floor(grain);
                    float seed = GrainHash(cell);
                    float3 center = .36 + .28 * float3(seed, GrainHash(cell + 7), GrainHash(cell + 13));
                    half fleck = 1 - smoothstep(.23h, .49h, length(frac(grain) - center));
                    half contrast = 1 - smoothstep(.35h, .78h, length(fwidth(grain)));
                    half dark = 1 - smoothstep(.13h, .32h, seed);
                    half bright = smoothstep(.66h, .86h, seed);
                    return lit * (1 + ((.32h * bright - .66h * dark) * fleck + .14h * (seed - .5h)) * contrast);
                }
                if (_StoneFinish < 2.5h)
                {
                    float layer = dot(local, float3(12, 98, 8)) + .12 * sin(local.x * 45 + local.z * 24);
                    half separation = 1 - smoothstep(.28h, .57h, fwidth(layer));
                    half seam = smoothstep(.08h, .37h, frac(layer));
                    return lit * (1 - .42h * (1 - seam) * separation);
                }
                if (_StoneFinish < 3.5h)
                {
                    half pores = GrainHash(floor(local * 164));
                    return lit * (.94h + .06h * pores);
                }

                half grazing = 1 - saturate(dot(normal, view));
                half3 reflected = reflect(-view, normal);
                // A strip reflection models the cabinet lighting even under the canopy.
                half strip = 1 - smoothstep(.06h, .18h, abs(reflected.y - .35h + .18h * reflected.x));
                half specular = smoothstep(.93h, .98h, dot(normal, normalize(view + key.direction)));
                specular *= key.shadowAttenuation;

                if (_StoneFinish < 4.5h)
                {
                    // Backscattering and a frosted rim suggest depth while preserving the
                    // opaque contact edges, depth buffer and shadow of the solid stone.
                    half transmission = saturate(-dot(normal, key.direction) * .65h + .5h);
                    half rim = smoothstep(.47h, .93h, grazing);
                    half vein = smoothstep(.16h, .81h, frac(dot(local, float3(5, 9, -5))));
                    half3 ice = lit * lerp(half3(.58h, .81h, 1.06h), half3(.98h, 1.04h, 1.04h), vein);
                    return ice + half3(.56h, .79h, .92h) * (.14h * transmission + .24h * rim + .12h * specular);
                }
                if (_StoneFinish < 5.5h)
                {
                    half glint = .19h * strip + .56h * specular + .14h * smoothstep(.64h, .9h, grazing);
                    return lit + half3(.95h, .64h, 1) * glint;
                }

                half reflectedSky = smoothstep(-.1h, .22h, reflected.y);
                half goldGlint = .59h * strip + .72h * specular;
                half3 skyTint = lerp(half3(.45h, .21h, .06h), half3(1.03h, .71h, .31h), reflectedSky);
                return lit * .64h + _BaseColor.rgb * skyTint * .48h + half3(1, .78h, .38h) * goldGlint;
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
                half3 color = _BaseColor.rgb * input.facetTone * (shadow + direct);
                if (_StoneFinish > .5h)
                    color = StoneFinish(color, input.positionOS, normal, GetWorldSpaceNormalizeViewDir(input.positionWS), key);
                return half4(color + _EmissionColor.rgb, 1);
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
