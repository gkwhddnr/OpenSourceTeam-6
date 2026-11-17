Shader "OpenSourceTeam6/RainSplash"
{
    Properties { _Tint("Tint", Color) = (.65,.73,.82,.48) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            struct Attributes { float4 position:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(i.position);
                o.uv = i.uv;
                o.color = i.color * _Tint;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                float mask = (1-smoothstep(.35, 1, r)) * (.4+.6*exp(-dot(p,p)*12));
                return half4(i.color.rgb, i.color.a * mask);
            }
            ENDHLSL
        }
    }
}
