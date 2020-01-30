// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/VertexColorUnlit" 
{       
    SubShader 
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass 
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
           
            #include "UnityCG.cginc"
 
            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };
 
            struct v2f {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };
           
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
           
            fixed4 frag (v2f i) : COLOR
            {
                i.color.a = step(i.uv.y / i.uv.w, i.color.a);
                return i.color;
            }
            
            ENDCG
        }
    }
}