// Dust mote billboard for the desktop sunbeam (additive, soft round sprite, per-particle colour and alpha). Motes are emitted only inside the sunlit volume, so they are only ever lit there.
Shader "ProcSense/Sun Dust Mote"
{
    Properties
    {
        _MainTex ("Soft dot", 2D) = "white" {}
        _Gain ("Gain", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+7" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mote"
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half _Gain;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.color = i.color; return o; }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                return half4(i.color.rgb * a * _Gain, 1);
            }
            ENDHLSL
        }
    }
}
