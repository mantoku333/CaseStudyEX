Shader "UI/PlayerGameOverFogReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Reveal ("Reveal", Float) = -0.18
        _Softness ("Softness", Range(0.01, 0.4)) = 0.18
        _NoiseScale ("Noise Scale", Range(1, 32)) = 9
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.17
        _Center ("Reveal Center", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Reveal;
            float _Softness;
            float _NoiseScale;
            float _NoiseStrength;
            float4 _Center;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                float x1 = lerp(a, b, local.x);
                float x2 = lerp(c, d, local.x);
                return lerp(x1, x2, local.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float2x2 rotation = float2x2(1.6, -1.2, 1.2, 1.6);

                value += amplitude * ValueNoise(p);
                p = mul(rotation, p * 1.9);
                amplitude *= 0.5;

                value += amplitude * ValueNoise(p);
                p = mul(rotation, p * 2.1);
                amplitude *= 0.5;

                value += amplitude * ValueNoise(p);
                return saturate(value * 1.6);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                float2 centeredUv = i.uv - _Center.xy;
                float radial = length(centeredUv) / 0.70710678;
                float noise = Fbm(i.uv * _NoiseScale);
                float fogMask = radial + (noise - 0.5) * _NoiseStrength;
                // Invert the threshold so the fog itself grows outward and stays on screen.
                float fogAlpha = 1.0 - smoothstep(_Reveal, _Reveal + _Softness, fogMask);

                col.a *= fogAlpha;
                return col;
            }
            ENDCG
        }
    }
}
