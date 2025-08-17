﻿Shader "Instanced/InstancedSurfaceShader" {
	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_Color("Color", Color) = (0,0,0,0)
	}
		SubShader{
		Tags{ "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
			// Physically based Standard lighting model
        #pragma surface surf Standard addshadow fullforwardshadows vertex:vert
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup
			
	#include "./Common/Defines.cginc"

		sampler2D _MainTex;
		float4 _Color;

		struct Input {
			float2 uv_MainTex;
		};

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    Buffer<int> Body;
    StructuredBuffer<float3> positions;
	StructuredBuffer<float4> particleColors;
    float Diameter;
    int numPart;
#endif

	void vert(inout appdata_full v, out Input o)
	{
		UNITY_INITIALIZE_OUTPUT(Input, o);

		float3 right =    normalize(UNITY_MATRIX_V._m00_m01_m02);
		float3 up =		  normalize(UNITY_MATRIX_V._m10_m11_m12);
		float3 forward = -normalize(UNITY_MATRIX_V._m20_m21_m22);

		float4x4 rotationMatrix = float4x4(right, 0,
			up, 0,
			forward, 0,
			0, 0, 0, 1);

		float offset = 1;
		v.vertex = mul(v.vertex + float4(0, offset, 0, 0), rotationMatrix) + float4(0, -offset, 0, 0);
		v.normal = mul(v.normal, rotationMatrix);
	}

	void setup()
	{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

		float3 pos = positions[unity_InstanceID];

        unity_ObjectToWorld._11_21_31_41 = float4(Diameter, 0, 0, 0);
        unity_ObjectToWorld._12_22_32_42 = float4(0, Diameter, 0, 0);
        unity_ObjectToWorld._13_23_33_43 = float4(0, 0, Diameter, 0);
        unity_ObjectToWorld._14_24_34_44 = float4(pos, 1);

        unity_WorldToObject = unity_ObjectToWorld;
        unity_WorldToObject._14_24_34 *= -1;
        unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
        
		_Color = particleColors[unity_InstanceID];
#endif
	}

	half _Glossiness;
	half _Metallic;


	void surf(Input IN, inout SurfaceOutputStandard o) {
		fixed4 c = tex2D(_MainTex, IN.uv_MainTex);

		float2 uv = IN.uv_MainTex.xy * 2.0 - 1.0; // Remap UVs from [0,1] to [-1,1]
		float magSq = dot(uv, uv);
		if (magSq > 1.0) {
			discard;
		}
		
		float3 normal = float3(uv.x, uv.y, sqrt(1.0 - magSq));
		o.Normal = normalize(normal);

		o.Albedo = c.rgb * _Color.rgb;
		o.Alpha = c.a * _Color.a;

		o.Smoothness = _Glossiness;
		o.Metallic = _Metallic;
	}
	ENDCG
	}
		FallBack "Diffuse"
}