Shader "Herlilan/VisualLab/Landscape Look"
{
    Properties { _Look ("Look", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _Look;
            half3 ReadColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }
            half Lightness(half3 color) { return dot(color, half3(.2126,.7152,.0722)); }
            float BrushSeed(float2 cell)
            {
                float3 mixed = frac(cell.xyx * float3(.1031,.1030,.0973));
                mixed += dot(mixed, mixed.yzx + 33.33);
                return frac((mixed.x + mixed.y) * mixed.z);
            }
            half BrokenStroke(float2 paper, float2 spacing)
            {
                float2 grid = paper / spacing;
                grid.x += frac(floor(grid.y) * .5);
                float2 cell = floor(grid);
                float seed = BrushSeed(cell);
                float2 local = frac(grid) - .5;
                local.y += local.x * (seed - .5) * .55;
                float radius = length(local * float2(2.2, 2.9));
                return (seed * 2 - 1) * (1 - smoothstep(.30, 1., radius));
            }
            half HatchLine(float phase, float width)
            {
                float distance = abs(frac(phase + .5) - .5);
                float aa = clamp(fwidth(phase) * .5, .015, .18);
                return 1 - smoothstep(width - aa, width + aa, distance);
            }
            half4 PenAndInk(float2 uv, float2 pixel, float2 stepUV, half light)
            {
                float2 sheet = pixel / max(1., _BlitTexture_TexelSize.w / 700.);
                half tone = saturate(light);
                #ifndef UNITY_COLORSPACE_GAMMA
                    tone = sqrt(tone);
                #endif
                half left = Lightness(ReadColor(uv - float2(stepUV.x, 0)));
                half right = Lightness(ReadColor(uv + float2(stepUV.x, 0)));
                half below = Lightness(ReadColor(uv - float2(0, stepUV.y)));
                half above = Lightness(ReadColor(uv + float2(0, stepUV.y)));
                half contrast = max(abs(right - left), abs(above - below)) / (.10 + light);
                half contour = smoothstep(.10, .38, contrast);
                float bend = .62 * sin(dot(sheet, float2(.032, .019))) + .19 * sin(sheet.y * .10);
                half hatch = HatchLine((dot(sheet, float2(.86, -.51)) + bend) / 5.6, .088)
                           * (1 - smoothstep(.44, .69, tone));
                half crossHatch = HatchLine((dot(sheet, float2(.43, .90)) - bend) / 7.1, .072)
                                * (1 - smoothstep(.29, .44, tone));
                half pool = 1 - smoothstep(.245, .285, tone);
                half ink = max(pool, max(contour, max(hatch, crossHatch)));
                half grain = BrushSeed(floor(sheet));
                half printValue = lerp(.93 + .035 * grain, .009 + .019 * grain, ink);
                return half4(printValue.xxx, 1);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 color = ReadColor(uv);
                half light = Lightness(color);
                if (_Look < 1.5) return half4(light.xxx, 1);
                float2 stepUV = _BlitTexture_TexelSize.xy * max(1., _BlitTexture_TexelSize.w / 700.);
                if (_Look > 3.5) return PenAndInk(uv, input.positionCS.xy, stepUV, light);
                if (_Look < 2.5)
                {
                    float2 paper = input.positionCS.xy / max(1., _BlitTexture_TexelSize.w / 700.);
                    float2 axis = normalize(float2(1., .23 + .30 * sin(dot(paper, float2(.013,.008)))));
                    float2 across = float2(-axis.y, axis.x);
                    float2 field = float2(paper.x + .19 * paper.y, paper.y - .19 * paper.x);
                    half mark = BrokenStroke(field, float2(11, 5))
                              + .45 * BrokenStroke(field.yx + 17., float2(6, 3.5));
                    half3 wash = color * 2.;
                    half weight = 2.;
                    [unroll] for (int tap = 0; tap < 6; tap++)
                    {
                        float along = (tap - 2.5) * 1.5;
                        float lateral = (tap % 2 == 0 ? -.8 : .8) + mark;
                        half3 pigment = ReadColor(uv + (axis * along + across * lateral) * stepUV);
                        half affinity = exp2(-16 * length(pigment - color));
                        wash += pigment * affinity;
                        weight += affinity;
                    }
                    wash /= weight;
                    half sun = smoothstep(.17, .73, light);
                    half3 air = lerp(half3(.28,.29,.48), half3(1.,.88,.67), sun);
                    half3 paint = lerp(wash, air, lerp(.13,.19,sun));
                    half3 fleck = mark > 0 ? half3(.105,.061,.011) : half3(-.038,.041,.082);
                    paint += abs(mark) * fleck * (.4 + .6 * saturate(light));
                    return half4(max(paint + mark * .024, 0), 1);
                }
                half edge = max(abs(light - Lightness(ReadColor(uv + stepUV * float2(1,0)))),
                                abs(light - Lightness(ReadColor(uv + stepUV * float2(0,1)))));
                half band = floor(sqrt(saturate(light)) * 5 + .5) / 5;
                half3 cel = lerp(light.xxx, color, 1.16) * ((band * band + .025) / (light + .025));
                return half4(cel * (1 - .74 * smoothstep(.025,.12,edge)), 1);
            }
            ENDHLSL
        }
    }
}
