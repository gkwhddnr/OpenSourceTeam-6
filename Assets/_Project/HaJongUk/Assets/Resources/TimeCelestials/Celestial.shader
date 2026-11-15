Shader "OpenSourceTeam6/Celestial"
{
 Properties { _MainTex("Surface", 2D) = "white" {} _SunDirection("Sun direction", Vector) = (0,1,0,0) _IsSun("Self illumination", Float) = 0 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Opaque" }
 Pass {
 ZWrite Off ZTest LEqual Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_ST; float4 _SunDirection; float _IsSun;
 CBUFFER_END
 struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
 struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
 V vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; return o; }
 half4 frag(V i):SV_Target {
 // A luminous disk: no crater texture or surface-lighting variation.
 return half4(1.35,1.4,1.5,1);
 }
 ENDHLSL
 }
 }
}
