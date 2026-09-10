Shader "GameName/FX/Graceful Ribbon"
{
    Properties
    {
        _MainTex ("Pastel Spectrum", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half _Brightness;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float along = saturate(input.uv.x);
                float across = input.uv.y * 2.0 - 1.0;
                float fold = sin(along * 5.8 - 0.9);
                float edge = 1.0 - smoothstep(0.45, 1.0, abs(across));
                // Two soft reflections trade brightness across a translucent folded sheet.
                float frontDistance = (across - fold * 0.58) * 3.2;
                float backDistance = (across + fold * 0.5) * 3.8;
                float frontLobe = sin(along * 3.8 + 0.2);
                float backLobe = sin(along * 3.8 + 1.8);
                float front = exp(-frontDistance * frontDistance) * (0.2 + 0.8 * frontLobe * frontLobe);
                float back = exp(-backDistance * backDistance) * (0.15 + 0.6 * backLobe * backLobe);
                float veil = 0.1 * (1.0 - across * across);
                float silk = (veil + front * 0.55 + back * 0.38) * edge;
                half3 spectrum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(along, 0.5)).rgb;
                half3 iridescence = (half3(0.78, 0.92, 1.0) * front + half3(1.0, 0.82, 0.93) * back +
                    half3(0.95, 0.93, 1.0) * veil) / max(front + back + veil, 0.0001);
                half3 color = iridescence * spectrum;
                return half4(color * input.color.rgb * _Brightness, silk * input.color.a);
            }
            ENDHLSL
        }
    }
}
