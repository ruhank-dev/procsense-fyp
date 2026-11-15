// Cheap sunshaft for Quest (overhaul, Agent B). Same look as "ProcSense/Sun Shaft" without the per-pixel noise maths: the irregular air density comes from ONE sample of a small tileable
// noise texture (Assets/Textures/Effects/sunshaft_noise.png) projected perpendicular to the sun direction, back faces are culled (about half the additive layers), and everything
// is half precision. Same property names, so the existing materials switch shader without being re-authored. Stereo safe (instance id and eye index carried).
Shader "ProcSense/Sun Shaft Baked"
{
    Properties
    {
        _Tint ("Warm-white tint", Color) = (1, 0.965, 0.9, 1)
        _Intensity ("Intensity", Range(0, 0.3)) = 0.06
        _EdgePower ("Edge softness", Range(0.5, 4)) = 2.4
        _NoiseScale ("Noise scale (1/m)", Range(0.1, 3)) = 0.9
        _NoiseAmount ("Noise amount", Range(0, 1)) = 0.85
        _FadeStart ("Brightest near the window (0-1)", Range(0, 0.5)) = 0.1
        _ShaftDir ("Sun travel direction", Vector) = (0.88, -0.2, -0.43, 0)
        [NoScaleOffset] _NoiseTex ("Tileable noise (R)", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shaft"
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _Intensity; half _EdgePower; half _NoiseScale; half _NoiseAmount; half _FadeStart; float4 _ShaftDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half2 uv : TEXCOORD2; half strength : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = (half3)normalize(TransformObjectToWorldNormal(i.normalOS)); o.uv = (half2)i.uv; o.strength = i.color.r; return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 toCam = GetCameraPositionWS() - i.positionWS;
                float camDist = length(toCam);
                half edge = pow(saturate(abs(dot(i.normalWS, (half3)(toCam / camDist)))), _EdgePower);
                half along = smoothstep(0.0h, _FadeStart + 0.001h, i.uv.y) * (saturate(1.0h - i.uv.y) * sqrt(saturate(1.0h - i.uv.y)));
                half nearFade = smoothstep(0.8h, 2.6h, (half)camDist);
                half cheap = _Intensity * i.strength * edge * along * nearFade;
                if (cheap < 0.00003h) return half4(0, 0, 0, 1);
                float3 d = normalize(_ShaftDir.xyz);
                float3 right = normalize(cross(d, float3(0, 1, 0)));
                float3 up = cross(d, right);
                float3 pp = i.positionWS * _NoiseScale;
                float al = dot(pp, d);
                float2 nuv = float2(dot(pp, right), dot(pp, up)) * 0.62 + al * float2(0.07, 0.05) + _Time.y * 0.004;
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r;
                half dens = lerp(1.0h, smoothstep(0.28h, 0.82h, n) * 1.35h + 0.05h, _NoiseAmount);
                return half4(_Tint.rgb * (cheap * dens), 1);
            }
            ENDHLSL
        }
    }
}
