Shader "Herlilan/VisualLab/White Veil"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half opacity : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.opacity = input.color.a;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 pixels = rcp(max(fwidth(input.uv), .00001));
                float shortSide = min(pixels.x, pixels.y);
                float feather = clamp(shortSide * .13, 1.5, 14);
                float radius = max(shortSide * .5 - feather, 0);
                float2 local = abs((input.uv - .5) * pixels) - (pixels * .5 - feather - radius);
                float edge = length(max(local, 0)) + min(max(local.x, local.y), 0) - radius;
                half fill = 1 - smoothstep(-feather * .24, feather * .24, edge);
                half halo = exp2(-5 * pow(max(edge, 0) / feather, 2)) * (1 - fill);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 stepUV = clamp(shortSide * .09, 2, 9) / _ScaledScreenParams.xy;
                half3 blur = SampleSceneColor(uv) * .20;
                blur += (SampleSceneColor(uv + stepUV * float2(1,0)) + SampleSceneColor(uv - stepUV * float2(1,0))
                       + SampleSceneColor(uv + stepUV * float2(0,1)) + SampleSceneColor(uv - stepUV * float2(0,1))) * .12;
                blur += (SampleSceneColor(uv + stepUV * 1.45) + SampleSceneColor(uv - stepUV * 1.45)
                       + SampleSceneColor(uv + stepUV * float2(1.45,-1.45)) + SampleSceneColor(uv - stepUV * float2(1.45,-1.45))) * .08;
                half body = fill * saturate(input.opacity * 1.7);
                half glow = halo * input.opacity * .42;
                half3 milk = lerp(blur, half3(1,1,1), input.opacity * .83);
                return half4(milk * body + glow, body);
            }
            ENDHLSL
        }
    }
}
