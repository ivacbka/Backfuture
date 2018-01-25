// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Transparent/BlendedTextureColor-QueT100"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
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

            sampler2D _MainTex;
                
            fixed4 frag(v2f i): COLOR
            {
            	return tex2D(_MainTex, i.texcoord) * i.color;
            }

			ENDCG
		}
	}
}