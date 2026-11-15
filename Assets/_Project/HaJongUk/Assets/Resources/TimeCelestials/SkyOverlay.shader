Shader "OpenSourceTeam6/SkyOverlay" {
 Properties {_Night("Night",Range(0,1))=0 _Drift("Drift",Float)=0}
 SubShader {Tags {"Queue"="Transparent-200"} Cull Front ZWrite Off Blend SrcAlpha OneMinusSrcAlpha Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Night,_Drift;float4 _MoonDirection; struct V{float4 pos:SV_POSITION;float3 d:TEXCOORD0;};
 V vert(float4 v:POSITION){V o;o.pos=UnityObjectToClipPos(v);o.d=v.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);float n=dot(a,float3(1,57,113));return lerp(lerp(lerp(frac(sin(n)*43758.5),frac(sin(n+1)*43758.5),f.x),lerp(frac(sin(n+57)*43758.5),frac(sin(n+58)*43758.5),f.x),f.y),lerp(lerp(frac(sin(n+113)*43758.5),frac(sin(n+114)*43758.5),f.x),lerp(frac(sin(n+170)*43758.5),frac(sin(n+171)*43758.5),f.x),f.y),f.z);}
 float fbm(float3 p){float v=0,w=.5;for(int k=0;k<5;k++){v+=noise(p)*w;p=p*2.03+7.1;w*=.5;}return v;}
 half4 frag(V i):SV_Target {float3 d=normalize(i.d);float2 uv=float2(atan2(d.z,d.x)/6.283,.5-asin(d.y)/3.14159);
 float2 g=uv*float2(500,250);float star=step(.996,hash(floor(g)))*(1-smoothstep(.05,.25,length(frac(g)-.5)));
 float density=fbm(d*7+float3(_Drift,0,_Drift*.35));
 float clouds=smoothstep(.45,.68,density);
 float alpha=lerp(clouds*.75,_Night,_Night)*smoothstep(-.02,.12,d.y);
 float separation=acos(clamp(dot(d,normalize(_MoonDirection.xyz)),-1,1));
 float halo=exp(-separation/.055)*.32*smoothstep(0,.08,_MoonDirection.y)*_Night;
 return half4(lerp(half3(.85,.89,.95),half3(.006,.012,.035)+star*1.5+halo*half3(.55,.68,1),_Night),alpha);}
 ENDHLSL}}}