Shader "OpenSourceTeam6/SkyOverlay" {
 Properties {_Night("Night",Range(0,1))=0 _Drift("Drift",Float)=0}
 SubShader {Tags {"Queue"="Transparent-200"} Cull Front ZWrite Off Blend One OneMinusSrcAlpha Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 float _Night,_Drift,_MoonRadius; float4 _MoonDirection,_SunDirection;
 struct V{float4 pos:SV_POSITION;float3 d:TEXCOORD0;};
 V vert(float4 v:POSITION){V o;o.pos=UnityObjectToClipPos(v);o.d=v.xyz;return o;}
 float hash(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
 float h3(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*f*(f*(f*6-15)+10);return lerp(lerp(lerp(h3(a),h3(a+float3(1,0,0)),f.x),lerp(h3(a+float3(0,1,0)),h3(a+float3(1,1,0)),f.x),f.y),lerp(lerp(h3(a+float3(0,0,1)),h3(a+float3(1,0,1)),f.x),lerp(h3(a+float3(0,1,1)),h3(a+1),f.x),f.y),f.z);}
 float fbm(float3 p){float v=0,w=.5;for(int k=0;k<5;k++){v+=noise(p)*w;p=p*2.03+7.1;w*=.5;}return v;}
 // Bounded 24-step cloud-layer integration gives density depth.
 float cloudDensity(float3 p){
 float height=(p.y-1)/.45;
 float profile=smoothstep(0,.18,height)*(1-smoothstep(.55,1,height));
 float3 q=p*float3(2.8,4.5,2.8);
 float shape=noise(q)*.65+noise(q*2.03+7.1)*.25+noise(q*4.1+19)*.10;
 float erosion=noise(q*9+31)*.045;
 return saturate((shape-.58-erosion)*7)*profile;
 }
 // Random positions and two populations avoid an evenly spaced dot pattern.
 float stars(float2 uv,float scale,float threshold){
 float2 g=uv*float2(scale,scale*.5),c=floor(g);
 float seed=hash(c);float2 center=float2(hash(c+17),hash(c+43))*.7+.15;
 float r=length(frac(g)-center);float aa=max(length(fwidth(g))*.45,.018);
 float core=1-smoothstep(0,aa,r);
 float glow=exp(-r*r/.006)*.12;
 return step(threshold,seed)*(core+glow)*lerp(.12,1.4,pow(hash(c+91),3));}
 half4 frag(V i):SV_Target {
 float3 d=normalize(i.d);
 float2 uv=float2(atan2(d.z,d.x)/6.283185+.5,.5-asin(clamp(d.y,-1,1))/3.141593);
 // Original spherical cloud scale, rigid rotation instead of morphing slices.
 float angle=_Drift*.225,s=sin(angle),c=cos(angle);
 float3 cd=float3(c*d.x-s*d.z,d.y,s*d.x+c*d.z);
 float transmittance=1;float3 accumulated=0;
 float3 lightDirection=normalize(_SunDirection.xyz+float3(0,.001,0));
 float entry=1/max(cd.y,.12);
 float stepLength=min(.45/max(cd.y,.12),2.5)/24;
 [loop] for(int sampleIndex=0;sampleIndex<24;sampleIndex++){
 float3 p=cd*(entry+(sampleIndex+.5)*stepLength);
 float density=cloudDensity(p);
 float shadow=cloudDensity(p+lightDirection*.065);
 float lighting=exp(-shadow*2.8);
 float3 sampleColor=lerp(float3(.43,.53,.65),float3(1.08,1.07,1.03),lighting);
 float opacity=1-exp(-density*stepLength*17);
 accumulated+=transmittance*opacity*sampleColor;
 transmittance*=1-opacity;
 }
 // Sparse high cloud streaks; independent from the cumulus density volume.
 float3 high=cd*float3(3,16,5)+17;
 float wisps=smoothstep(.55,.78,fbm(high))*.16;
 accumulated+=transmittance*wisps*float3(.93,.96,1);
 transmittance*=1-wisps;
 float alphaCloud=1-transmittance;
 float3 cloudColor=accumulated/max(alphaCloud,.0001);
 float star=stars(uv,1300,.965)*.65+stars(uv,500,.989)*2;
 float band=exp(-pow(dot(d,normalize(float3(.6,.25,-.7)))*4,2));
 float dust=fbm(d*12+21);
 float3 nightColor=float3(.0015,.003,.009)+band*dust*float3(.009,.014,.026);
 nightColor+=star*float3(.83,.89,1)*smoothstep(.45,1,saturate(_Night));
 float separation=atan2(length(cross(d,normalize(_MoonDirection.xyz))),dot(d,normalize(_MoonDirection.xyz)));
 float moonAA=max(fwidth(separation),.00015);
 float disk=1-smoothstep(_MoonRadius-moonAA,_MoonRadius+moonAA,separation);
 float moonVisibility=smoothstep(-.025,.04,_MoonDirection.y);
 float halo=(exp(-separation/.045)*.32+exp(-separation/.12)*.035)*moonVisibility;
 float3 moon=disk*float3(2.1,2.2,2.4)*moonVisibility+halo*float3(.55,.68,1);
 nightColor+=moon;
 float night=saturate(_Night);
 float horizon=smoothstep(.04,.20,d.y);
 alphaCloud*=horizon;
 float3 nightCloud=cloudColor*float3(.012,.018,.032)+halo*float3(.035,.045,.065);
 float3 litCloud=lerp(cloudColor,nightCloud,night);
 // Premultiplied composition keeps tenuous cloud edges from turning into
 // opaque white cutouts as the night background fades in.
 float skyMask=smoothstep(-.02,.08,d.y);
 return half4((litCloud*alphaCloud+nightColor*night*(1-alphaCloud))*skyMask,(alphaCloud+night*(1-alphaCloud))*skyMask);
 }
 ENDHLSL}}}
