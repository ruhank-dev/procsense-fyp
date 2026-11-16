// Quest exterior surface: one texture sample times the baked vertex shading and a tint. No lights, no shadows, no normal or metal maps, no keywords, so a window pixel that shows the city costs
// a fraction of a URP Lit pixel (the headset is GPU bound). The sun/shade difference between facade sides and the darker street canyon is computed once per VERTEX from the normal and the height (constants below, set by ExteriorImpostorBuild), never per pixel.
// Optional alpha test (_Cutoff above 0) for leaf cards. SRP Batcher compatible; stereo safe for single-pass instanced / Vulkan multiview.
Shader "ProcSense/Exterior Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff (0 = off)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _SunDir ("Direction towards the sun", Vector) = (-0.88, 0.2, 0.43, 0)
        _Shade ("Ambient, sun, canyon dark", Vector) = (0.6, 0.55, 0.55, 0)
        _CanyonY ("Canyon bottom y, top y", Vector) = (-48, 20, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ExteriorUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half _Cutoff; float4 _SunDir; float4 _Shade; float4 _CanyonY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 shade : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pw = TransformObjectToWorld(i.positionOS.xyz); float3 nw = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(pw); o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                float lit = _Shade.x + _Shade.y * saturate(dot(nw, normalize(_SunDir.xyz)));
                float canyon = lerp(_Shade.z, 1.0, saturate((pw.y - _CanyonY.x) / max(_CanyonY.y - _CanyonY.x, 0.01)));
                o.shade = (half3)(lit * canyon).xxx;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                if (_Cutoff > 0) clip(c.a - _Cutoff);
                return half4(c.rgb * _BaseColor.rgb * i.shade, 1);
            }
            ENDHLSL
        }
    }
}
