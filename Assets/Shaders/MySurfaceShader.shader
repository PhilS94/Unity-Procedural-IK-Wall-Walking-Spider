Shader "Custom/MySurfaceShader" {
	Properties{
		_OutlineColor("Outline Color", Color) = (1,1,1,1)
		_BodyColor("Body Color", Color) = (1,1,1,1)
		_MainTex("Base layer (RGB)", 2D) = "white" {}

		_SizeOutline("SizeOutline Pulse", Range(0, 0.0005)) = 0.0001
		_SizeBody("SizeBody Pulse", Range(0, 0.0005)) = 0.0001
		_Speed("Speed",Range(0,5)) = 1
		_MinOutlineSize("Minimal Outline",Range(0,0.0001)) = 0.00005
	}

		SubShader{

			/// first pass
			Tags{"IgnoreProjector" = "True" "RenderType" = "Opaque" }
			Lighting Off
			ZWrite On
			ZTest LEqual
			cull Front


			CGPROGRAM

			#pragma surface surf StandardSpecular fullforwardshadows addshadow vertex:vert
			#pragma target 3.0
			#include "UnityCG.cginc"


			float4 _OutlineColor;
			float _MinOutlineSize;
			float _Speed;
			float _SizeOutline;


		struct Input {
			float2 uv_MainTex;
		};

		void vert(inout appdata_full v) {
			v.vertex.xyz += float3(v.normal.xyz) * (_MinOutlineSize + _SizeOutline * pow(sin(_Speed*_Time.z),2));
		}

		void surf(Input i, inout SurfaceOutputStandardSpecular o) {
			o.Albedo = _OutlineColor.rgb;
		}
		ENDCG
		//// end first pass


	/// second pass
	Tags{ "RenderType" = "Opaque" }
	Blend Off
	Lighting Off
	ZWrite On
	Cull Back


	CGPROGRAM

	#pragma surface surf StandardSpecular vertex:vert
	#pragma target 3.0
	#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _BodyColor;
			float _SizeBody;
			float _Speed;


			struct Input {
				float2 uv_MainTex;
				float3 norm :  TEXCOORD1;
			};

			void vert(inout appdata_full v) {
				v.vertex.xyz += -float3(v.normal.xyz) * (_SizeBody * pow(sin(sqrt(2.0)*_Speed*_Time.z), 2));
				v.vertex.xyz += float3(0,1,0)* 0.005*sin(_Speed*_Time.z);
			}

			void surf(Input i, inout SurfaceOutputStandardSpecular o) {
				fixed4 tex = tex2D(_MainTex, i.uv_MainTex);
				o.Albedo = tex *_BodyColor.rgb;
			}
			ENDCG
	// end second pass

		} //subshader
}//shader
