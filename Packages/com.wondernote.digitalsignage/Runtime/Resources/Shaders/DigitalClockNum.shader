Shader "Custom/DigitalClockNum"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Udon_TopSegment ("Top Segment", Float) = 0
        _Udon_TopRightSegment ("Top Right Segment", Float) = 0
        _Udon_BottomRightSegment ("Bottom Right Segment", Float) = 0
        _Udon_BottomSegment ("Bottom Segment", Float) = 0
        _Udon_BottomLeftSegment ("Bottom Left Segment", Float) = 0
        _Udon_TopLeftSegment ("Top Left Segment", Float) = 0
        _Udon_MiddleSegment ("Middle Segment", Float) = 0
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

            float _Udon_TopSegment;
            float _Udon_TopRightSegment;
            float _Udon_BottomRightSegment;
            float _Udon_BottomSegment;
            float _Udon_BottomLeftSegment;
            float _Udon_TopLeftSegment;
            float _Udon_MiddleSegment;

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

                if (i.uv.x > 0.0 && i.uv.x < 0.125 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                    col = _Udon_TopSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                else if (i.uv.x > 0.125 && i.uv.x < 0.25 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                    col = _Udon_TopRightSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                else if (i.uv.x > 0.25 && i.uv.x < 0.375 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                    col = _Udon_BottomRightSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                else if (i.uv.x > 0.375 && i.uv.x < 0.5 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                    col = _Udon_BottomSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                else if (i.uv.x > 0.5 && i.uv.x < 0.625 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                    col = _Udon_BottomLeftSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                else if (i.uv.x > 0.625 && i.uv.x < 0.75 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                    col = _Udon_TopLeftSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                else if (i.uv.x > 0.75 && i.uv.x < 0.875 && i.uv.y > 0.0 && i.uv.y < 0.5)
                {
                col = _Udon_MiddleSegment > 0.5 ? _CustomLightColor : _CustomBgColor;
                }
                return col;
            }
            ENDCG
        }
    }
}
