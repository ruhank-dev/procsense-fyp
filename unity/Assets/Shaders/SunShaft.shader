// Soft atmospheric sunshaft approximation (desktop and Quest; the host object's DesktopOnly switch keeps it on Android unless a diagnostic toggle hides it).
// Stereo-safe for single-pass instanced / Vulkan multiview (P3-PRF-11): the instance id and the eye index are carried, so each eye sees the beam at its own position.
// Several overlapping prisms per window opening (irregular widths, lengths and strengths, strength in the vertex colour). Each is additive: the silhouette fades out by view angle (no hard boundary),
// the shaft fades along its travel, and a low-frequency noise stretched ALONG the sun direction breaks the air into irregular, softly separated shafts; it drifts extremely slowly.
// The noise is a function of world position only, so overlapping prisms agree and the floor/wall lighting (baked) is never touched. No depth texture is needed: the beam ends where the
// opaque surface it lights writes depth. Dust motes are a separate particle system (Sun Dust Mote shader).
Shader "ProcSense/Sun Shaft"
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
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shaft"
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _Intensity; half _EdgePower; half _NoiseScale; half _NoiseAmount; half _FadeStart; float4 _ShaftDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float strength : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.uv = i.uv; o.strength = i.color.r; return o;
            }

            float hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i), n100 = hash13(i + float3(1, 0, 0)), n010 = hash13(i + float3(0, 1, 0)), n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1)), n101 = hash13(i + float3(1, 0, 1)), n011 = hash13(i + float3(0, 1, 1)), n111 = hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                                              // GetCameraPositionWS() is this eye's position
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float edge = pow(saturate(abs(dot(normalize(i.normalWS), V))), _EdgePower);            // silhouette fades smoothly, never a hard line
                float along = smoothstep(0.0, _FadeStart + 0.001, i.uv.y) * pow(saturate(1.0 - i.uv.y), 1.5);   // a little brighter near the window, fading with distance
                float3 d = normalize(_ShaftDir.xyz); float3 pp = i.positionWS * _NoiseScale; float al = dot(pp, d); float3 perp = pp - al * d;
                float t = _Time.y;                                                                       // drift is a few millimetres a second: the haze changes, it does not move or pulse
                float n1 = vnoise(perp * 2.1 + d * al * 0.22 + float3(t * 0.006, t * 0.004, t * 0.005));
                float n2 = vnoise(perp * 4.7 - d * al * 0.35 + float3(-t * 0.004, t * 0.007, t * 0.003) + 17.3);
                float n = n1 * 0.65 + n2 * 0.35;
                float dens = lerp(1.0, smoothstep(0.28, 0.82, n) * 1.35 + 0.05, _NoiseAmount);
                float nearFade = smoothstep(0.8, 2.6, distance(GetCameraPositionWS(), i.positionWS));      // prism walls close to the eye (close-ups, head movement) dissolve instead of showing as bars
                float3 col = _Tint.rgb * _Intensity * i.strength * dens * edge * along * nearFade;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
