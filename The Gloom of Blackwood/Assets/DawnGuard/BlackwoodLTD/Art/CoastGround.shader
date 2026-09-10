Shader "Blackwood/Coastal Ground"
{
    Properties { _Grass("Grass",Color)=(.25,.32,.15,1) _Earth("Earth",Color)=(.35,.29,.16,1) _Sand("Sand",Color)=(.62,.49,.30,1) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Grass,_Earth,_Sand;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float4 shadow:TEXCOORD1;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            V vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.shadow=TransformWorldToShadowCoord(o.world);return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;float n=noise(p*1.4),n2=noise(p*7);
                float lane=min(abs(p.x-14+.35*sin(p.y*.6)),abs(p.y-13.5+.4*sin(p.x*.7)));
                float track=1-smoothstep(.8,2.2,lane+n*.6);
                float camp=1-smoothstep(5.5,9,p.y);
                float shore=1-smoothstep(.8,3.3,p.y+sin(p.x*.38)*.5);
                half3 col=lerp(_Grass.rgb,_Earth.rgb,saturate(track*.7+camp*.8));
                col=lerp(col,_Sand.rgb,shore);col*=.85+n*.22+n2*.10;
                Light l=GetMainLight(i.shadow);float diffuse=saturate(l.direction.y)*l.shadowAttenuation;
                col*=SampleSH(float3(0,1,0))*.85+l.color*(.23+diffuse*.77);
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount=GetAdditionalLightsCount();
                for(uint lightIndex=0;lightIndex<lightCount;lightIndex++){Light localLight=GetAdditionalLight(lightIndex,i.world);col+=_Sand.rgb*localLight.color*localLight.distanceAttenuation*saturate(localLight.direction.y)*.6;}
                #endif
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
