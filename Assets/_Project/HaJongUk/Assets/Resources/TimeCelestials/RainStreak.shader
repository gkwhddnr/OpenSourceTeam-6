Shader "OpenSourceTeam6/RainStreak"
{
    Properties { _Tint("Tint", Color) = (.65,.73,.82,.48) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color * _Tint;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 edge = abs(i.uv * 2 - 1);
                float mask = pow(saturate(1-edge.x*edge.x),2) * saturate(1-edge.y*edge.y);
                return half4(i.color.rgb, i.color.a * mask);
            }
            ENDHLSL
        }
    }
}
