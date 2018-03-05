// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "GridShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_CellsCount ("CellsCount", vector) = (10,10,1,1)
		_LineWidth ("Width", range(0.01, 0.5)) = 0.2
	}
	
	SubShader
	{
		Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Off Lighting Off ZWrite Off Fog { Mode Off }
		LOD 100
		
		Pass
    	{
			CGPROGRAM
			#pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
			
			struct appdata
			{
    			float4 vertex:	POSITION;
    			fixed4 color:	COLOR;
    			fixed2 texcoord:TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex:	POSITION;
    			fixed4 color:	COLOR;
				fixed2 texcoord:TEXCOORD0;
            };
        
            half4 _MainTex_ST;
        
            v2f vert(appdata i)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(i.vertex);
                o.color = i.color;
                o.texcoord = TRANSFORM_TEX(i.texcoord, _MainTex);
                return o;
            }

            //sampler2D _MainTex;
            float2 _CellsCount;
            float _LineWidth;
            fixed4 frag(v2f i): COLOR
            {
                if((abs(frac(i.texcoord.x * _CellsCount.x + _LineWidth / 2)) < _LineWidth) || (abs(frac(i.texcoord.y * _CellsCount.y + _LineWidth / 2)) < _LineWidth))
                    return float4(0, 0, 0, 0.75f);
                else
            	    return i.color;
            }

			ENDCG
		}
	}
}