Shader "GameName/FX/Graceful Particle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 4)) = 1.3
        _Saturation ("Texture Saturation", Range(0, 1)) = 0.15
        _Halo ("Soft Edge Glow", Range(0, 1)) = 0
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
                half _Saturation;
                half _Halo;
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
                half4 particle = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(particle.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 pearl = lerp(luminance.xxx, particle.rgb, _Saturation);
                half halo = 0;
                if (_Halo > 0)
                {
                    // A small multi-radius kernel avoids four displaced silhouette outlines.
                    for (int ring = 1; ring <= 3; ring++)
                    {
                        float2 radius = float2(0.009 * ring, 0);
                        half weight = (4.0 - ring) / 24.0;
                        halo += weight * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + radius.xy).a;
                        halo += weight * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - radius.xy).a;
                        halo += weight * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + radius.yx).a;
                        halo += weight * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - radius.yx).a;
                    }
                    halo *= _Halo * (1.0 - particle.a);
                    pearl = lerp(pearl, half3(1, 0.98, 1), 0.55);
                }
                half alpha = particle.a + halo;
                half3 light = (pearl * particle.a + half3(1, 0.97, 1) * halo) / max(alpha, 0.0001);
                return half4(light * saturate(input.color.rgb) * _Brightness, alpha * saturate(input.color.a));
            }
            ENDHLSL
        }
    }
}
