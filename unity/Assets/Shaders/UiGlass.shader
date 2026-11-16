// P3-UIX-03: the glass of every ProcSense panel. URP, unlit, transparent, one pass, premultiplied alpha.
// A rounded-rectangle signed distance from the quad's UVs (the panel size and corner radius come from GlassPanel in uv1), a tint, a frosted
// gradient blended with the environment reflection cube at a high mip (looks blurred, one cube sample), a Fresnel rim brighter at the top,
// a soft specular streak that moves with the head, and fine noise against banding. No grab pass, no depth read, and on the headset no opaque texture (the desktop-only blur variant of P3-UIX-09 reads it).
// Stereo: instancing and stereo macros for single-pass instanced and multiview. Works under RectMask2D (clip rect) and Mask (stencil) like UI/Default.
Shader "ProcSense/UI Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused, uGUI needs it)", 2D) = "white" {}
        _Color ("Tint", Color) = (0.059, 0.102, 0.141, 0.85)
        _EdgeColor ("Edge", Color) = (1, 1, 1, 0.35)
        _EdgeWidth ("Edge width (canvas units)", Float) = 1.5
        _Frost ("Frost (environment amount)", Range(0, 1)) = 0.35
        _FrostMip ("Frost blur (cube mip)", Range(0, 10)) = 6
        _Streak ("Streak strength", Range(0, 1)) = 0.08
        _Noise ("Noise against banding", Range(0, 0.05)) = 0.012

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [Toggle(UNITY_UI_CLIP_RECT)] _UseClipRect ("Use Clip Rect", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" "RenderPipeline" = "UniversalPipeline" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UiGlass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            // P3-UIX-09: desktop only, real blur of what is behind the panel from the camera's opaque texture. GlassBlur turns the keyword on off-Android; the Android
            // build strips every variant with it (GlassBlurStripper), so the headset never compiles or samples the opaque texture.
            #pragma multi_compile _ PS_GLASS_BLUR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #if defined(PS_GLASS_BLUR)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;      // 0..1 across the panel
                float4 shape : TEXCOORD1;   // x, y: size in canvas units; z: corner radius; w: unused
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 positionOS : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                float _EdgeWidth;
                half _Frost;
                half _FrostMip;
                half _Streak;
                half _Noise;
                float4 _ClipRect;
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = normalize(TransformObjectToWorldDir(float3(0, 0, -1)));   // a canvas faces -Z of its transform
                o.color = v.color;
                o.uv = v.uv;
                o.shape = v.shape;
                o.positionOS = v.positionOS;
                return o;
            }

            // signed distance to a rounded rectangle centred at 0 with half size b and radius r (negative inside)
            float RoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            #if defined(PS_GLASS_BLUR)
            // a 3 x 3 box of taps spread 6 pixels apart over the (half-resolution, bilinear) opaque texture: a soft blur of the room behind the panel
            half3 BlurBehind(float2 uv)
            {
                float2 px = _ScreenSize.zw * 6;
                half3 s = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                    [unroll] for (int x = -1; x <= 1; x++)
                        s += SampleSceneColor(uv + float2(x, y) * px);
                return s / 9;
            }
            #endif

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 size = max(i.shape.xy, 1);
                float radius = min(i.shape.z, 0.5 * min(size.x, size.y));
                float2 p = (i.uv - 0.5) * size;                                   // canvas units from the centre
                float d = RoundedBox(p, 0.5 * size, radius);
                float aa = max(fwidth(d), 1e-4);
                half inside = saturate(0.5 - d / aa);                              // smooth rounded edge, one pixel wide at any distance
                if (inside <= 0) discard;

                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 n = i.normalWS;
                n = dot(n, view) < 0 ? -n : n;                                     // both faces light the same

                // frost: a soft top-to-bottom gradient blended with the environment cube at a high mip (blurred reflection of the room)
                half3 gradient = lerp(half3(0.02, 0.03, 0.05), half3(0.20, 0.26, 0.32), saturate(i.uv.y));
                float3 r = reflect(-view, n);
                half4 env = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, r, _FrostMip);
                half3 envColor = DecodeHDREnvironment(env, unity_SpecCube0_HDR);
                half3 frost = lerp(gradient, envColor, 0.6h);

                half3 colour = lerp(_Color.rgb, frost, _Frost);
                half alpha = _Color.a;

                // rim: a thin bright edge, brighter along the top; plus a Fresnel lift at grazing angles
                half edge = saturate(1 - abs(d) / max(_EdgeWidth, 0.01)) * step(d, 0.5);
                half top = lerp(0.6h, 1.4h, saturate(i.uv.y));
                half fresnel = pow(1 - saturate(dot(n, view)), 3) * 0.35h;
                half rim = saturate(edge * top * _EdgeColor.a + fresnel);
                colour = lerp(colour, _EdgeColor.rgb, rim);
                alpha = max(alpha, rim);

                // streak: a soft diagonal highlight whose position follows the view direction
                float streakPos = i.uv.x + i.uv.y * 0.6 + dot(view, float3(0.5, 0.3, 0)) * 0.8;
                half streak = exp(-pow((frac(streakPos * 0.5) - 0.5) * 14, 2)) * _Streak;   // a narrow band: a highlight that does not grey the panel
                colour += streak;

                #if defined(PS_GLASS_BLUR)
                    // the panel draws the blurred room itself, then its glass over it at the same opacity as the plain blend, so the text contrast is unchanged
                    colour = lerp(BlurBehind(GetNormalizedScreenSpaceUV(i.positionCS)), colour, alpha);
                    alpha = 1;
                #endif

                colour += (Hash(i.uv * size + _Time.y) - 0.5) * _Noise;          // breaks gradient banding on the headset's 8-bit swapchain

                half a = saturate(alpha * i.color.a * inside);
                colour *= i.color.rgb;

                #ifdef UNITY_UI_CLIP_RECT
                    float2 inRect = step(_ClipRect.xy, i.positionOS.xy) * step(i.positionOS.xy, _ClipRect.zw);
                    a *= inRect.x * inRect.y;
                #endif
                return half4(colour * a, a);                                       // premultiplied
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
