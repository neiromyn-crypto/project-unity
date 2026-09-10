Shader "Blackwood/Clearing Ground"
{
    Properties
    {
        _BaseMap("User ground detail",2D)="white" {}
        _ForestColor("Forest floor",Color)=(0.24,0.29,0.20,1)
        _ClearingColor("Work area",Color)=(0.38,0.36,0.29,1)
        _TextureStrength("Detail contrast",Range(0,1))=0.16
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Ground" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _ForestColor,_ClearingColor;
                half _TextureStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.positionWS.xz;
                float irregular=sin(p.x*1.37+sin(p.y*1.1))*.11+sin(p.y*2.1-p.x*.7)*.08;
                float central=length((p-float2(8,5.6))/float2(3.8,3.0));
                float camp=length((p-float2(4.1,4.9))/float2(2.0,1.9));
                float power=length((p-float2(11.9,4.9))/float2(2.0,1.9));
                float clearing=1-smoothstep(.76,1.13,min(central,min(camp,power))+irregular);
                float northPath=(1-smoothstep(.42,.90,abs(p.x-7.6)+irregular))*(1-smoothstep(9.7,11.0,p.y))*smoothstep(5.4,7.0,p.y);
                float approach=(1-smoothstep(.65,1.05,abs(p.x-8.0)+irregular))*(1-smoothstep(3.0,4.0,p.y))*smoothstep(-2.5,-.4,p.y);
                half detail=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb,half3(.2126,.7152,.0722));
                half3 albedo=lerp(_ForestColor.rgb,_ClearingColor.rgb,saturate(max(clearing,max(northPath,approach))*.87));
                albedo*=1+(detail-.45)*_TextureStrength;
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normalize(input.normalWS),sun.direction));
                half3 light=SampleSH(normalize(input.normalWS))+sun.color*diffuse*sun.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                uint count=GetAdditionalLightsCount();
                for(uint i=0;i<count;i++)
                {
                    Light lamp=GetAdditionalLight(i,input.positionWS);
                    light+=lamp.color*saturate(dot(normalize(input.normalWS),lamp.direction))*lamp.distanceAttenuation;
                }
                #endif
                return half4(albedo*light,1);
            }
            ENDHLSL
        }
    }
}
