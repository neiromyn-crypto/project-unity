Shader "Blackwood/Coastal Water"
{
    Properties {_Deep("Deep water",Color)=(.035,.18,.24,1) _Shallow("Shallow water",Color)=(.10,.48,.52,1)}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Deep,_Shallow;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;};
            V vert(A a){V o;o.p=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.p);return o;}
            half4 frag(V i):SV_Target
            {
                float x=i.p.x,z=i.p.z,t=_Time.y;float shore=-1.4+sin(x*.27)*.43+sin(x*.81)*.15;
                float dist=max(0,shore-z);float waves=sin(z*4.2+sin(x*.8+t*.15)*1.2+t*.7);
                half3 color=lerp(_Shallow.rgb,_Deep.rgb,saturate(dist/8));color+=smoothstep(.94,1,waves)*(.025+.02*sin(x*3+z));
                float foam=(1-smoothstep(.06,.28,abs(dist-.22-.08*sin(x*5+t*2))))*.75;
                float lace=smoothstep(.88,1,sin(dist*10+sin(x*3)*.7-t))*smoothstep(-.2,.8,sin(x*4.5+t*.2))*.14;
                color=lerp(color,half3(.73,.91,.84),saturate(foam+lace*exp(-dist*.5)));return half4(color,1);
            }
            ENDHLSL
        }
    }
}
