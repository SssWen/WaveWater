Shader "Hidden/WaveEquationGen"
{
	Properties
	{
		_MainTex ("MainTexture", 2D) = "white" {}
		_Mask ("Mask", 2D) = "white" {}
		_PreTex("PreTex", 2D) = "white" {}
		_WaveParams("WaveParams", vector) = (0,0,0,0)
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc" 
			#include "Utils.cginc"

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

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			sampler2D _MainTex;
			sampler2D _PreTex;

			sampler2D _Mask;

			half4 _WaveParams;

			// 根据波动方程，重新计算深度之后，再次存储放在后续计算。
			fixed4 frag (v2f i) : SV_Target
			{
				// 当前顶点位移 cur = k1*z(i,j,k); k 对应当前_MainTex贴图
				float cur = _WaveParams.x*DecodeHeight(tex2D(_MainTex, i.uv));
				fixed mask = tex2D(_Mask, i.uv).r;

				// rg = k3(z(i+1,j,k)+z(i-1,j,k)+z(i,j+1,k),z(i,j-1,k))
				float rg = _WaveParams.z*(DecodeHeight(tex2D(_MainTex, i.uv + float2(_WaveParams.w, 0))) + DecodeHeight(tex2D(_MainTex, i.uv + float2(-_WaveParams.w,0)))
				+ DecodeHeight(tex2D(_MainTex, i.uv + float2(0, _WaveParams.w))) + DecodeHeight(tex2D(_MainTex, i.uv + float2(0,-_WaveParams.w))));

				// 上个时间段位移 pre = k2*z(i,j,k-1) k-1 对应 _PreTex贴图
				float pre = _WaveParams.y*DecodeHeight(tex2D(_PreTex, i.uv));
				
				cur += (rg + pre) * mask;

				cur *= 0.96*mask;


				return EncodeHeight(cur);
			}
			ENDCG
		}
	}
}
