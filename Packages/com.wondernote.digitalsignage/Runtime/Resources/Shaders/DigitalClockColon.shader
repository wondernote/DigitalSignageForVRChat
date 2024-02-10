Shader "Custom/DigitalClockColon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Udon_BlinkState ("Blink State", Float) = 1.0
        _CustomLightColor ("Light Color", Color) = (1, 1, 1, 1)
        _CustomBgColor ("Background Color", Color) = (0.03, 0.03, 0.03, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            uniform float4 _CustomLightColor;
            uniform float4 _CustomBgColor;
            float4 _MainTex_ST;

            float _Udon_BlinkState;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _CustomBgColor;

                if (_Udon_BlinkState > 0.5 && i.uv.x >= 0.75 && i.uv.x <= 1.0 && i.uv.y >= 0.5 && i.uv.y <= 1.0)
                {
                    return _CustomLightColor;
                }
                return col;
            }
            ENDCG
        }
    }
}
