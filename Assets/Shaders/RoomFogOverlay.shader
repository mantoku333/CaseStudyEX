Shader "CaseStudy/RoomFogOverlay"
{
    Properties
    {
        _MaskTex ("Reveal Mask", 2D) = "black" {}
        _EntranceMaskTex ("Portal Entrance Mask", 2D) = "black" {}
        _FogColor ("Fog Color", Color) = (0, 0, 0, 1)
        _FogAlpha ("Fog Alpha", Range(0, 1)) = 1
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.22
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.18
        _NoiseScale ("Noise Scale", Float) = 0.32
        _RevealFront ("Reveal Front", Range(-0.12, 1.12)) = -0.12
        _ConcealFront ("Conceal Front", Range(-0.12, 1.12)) = -0.12
        _PreviousActive ("Previous Room Active", Range(0, 1)) = 0
        _PreviousPortalStrength ("Previous Portal Strength", Range(0, 1)) = 0
        _ContinuityFront ("Continuity Front", Range(-0.12, 1.12)) = 1.12
        _ContinuityStrength ("Continuity Strength", Range(0, 1)) = 0
        _WorldMin ("World Min", Vector) = (0, 0, 0, 0)
        _WorldSize ("World Size", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MaskTex;
            sampler2D _EntranceMaskTex;
            fixed4 _FogColor;
            float _FogAlpha;
            float _EdgeSoftness;
            float _NoiseStrength;
            float _NoiseScale;
            float _RevealFront;
            float _ConcealFront;
            float _PreviousActive;
            float _PreviousPortalStrength;
            float _ContinuityFront;
            float _ContinuityStrength;
            float4 _WorldMin;
            float4 _WorldSize;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 maskUv = (i.worldPos.xy - _WorldMin.xy) / max(_WorldSize.xy, float2(0.001, 0.001));
                float3 encodedDistances = tex2D(_MaskTex, maskUv).rgb;
                float3 validDistances = 1.0 - step(254.5 / 255.0, encodedDistances);
                float3 roomDistances = lerp(
                    float3(-0.25, -0.25, -0.25),
                    float3(1.25, 1.25, 1.25),
                    saturate(encodedDistances * (255.0 / 254.0)));
                float2 roomRevealed = validDistances.rg * step(
                    roomDistances.rg,
                    float2(_RevealFront, _ConcealFront));
                roomRevealed.g *= _PreviousActive;
                float continuityRoomRevealed = validDistances.b * step(
                    roomDistances.b,
                    _ContinuityFront);
                float3 entranceMasks = tex2D(_EntranceMaskTex, maskUv).rgb;
                float entranceRevealed = max(
                    entranceMasks.r,
                    entranceMasks.g * _PreviousPortalStrength);
                float revealed = max(
                    max(roomRevealed.r, roomRevealed.g),
                    entranceRevealed);
                float hidden = 1.0 - smoothstep(0.01, max(_EdgeSoftness, 0.011), revealed);
                float continuityCoverage = max(continuityRoomRevealed, entranceMasks.b);
                hidden *= 1.0 - saturate(continuityCoverage * _ContinuityStrength);

                if (hidden <= 0.0)
                {
                    discard;
                }

                float noise = ValueNoise(i.worldPos.xy * _NoiseScale + _Time.y * 0.04);
                fixed4 color = _FogColor;
                color.rgb *= saturate(1.0 + (noise - 0.5) * _NoiseStrength);
                color.a *= _FogAlpha * hidden;
                return color;
            }
            ENDCG
        }
    }
}
