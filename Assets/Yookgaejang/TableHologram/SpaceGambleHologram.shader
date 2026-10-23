// SpaceGamble / Hologram  (URP, Unlit, Transparent)
//
// 기존 SoftHologram과 가장 큰 차이:
//   몸통 색(_FillColor)과 가장자리 색(_EdgeColor)을 완전히 분리했다.
//   몸통을 "어두운 연기 유리"로 두면 뒤의 밝은 우주 배경이 눌리면서
//   앞에 놓인 글자가 선명하게 읽힌다. 가장자리만 밝게 빛나므로
//   홀로그램 느낌은 그대로 유지된다.
//
// 글자가 묻히면 순서대로:
//   1) Fill Color를 더 어둡게        (가장 효과 큼)
//   2) Fill Opacity를 올린다          (배경을 더 많이 누름)
//   3) Edge Falloff를 올린다          (광을 테두리로 몰아냄)
//   4) 텍스트 머티리얼 Render Queue를 3100으로
//
// 세로 그라디언트와 스캔라인은 UV가 아니라 오브젝트 로컬 Y를 쓴다.
// 메시 UV가 엉망이어도 정상 동작한다. Gradient Height는 모델의
// 로컬 높이(스케일 적용 전)를 넣으면 된다.

Shader "SpaceGamble/Hologram"
{
    Properties
    {
        [Header(Content)][Space(4)]
        _BaseMap ("Content Texture", 2D) = "white" {}
        [Toggle(_USE_CONTENT)] _UseContent ("Use Content Texture", Float) = 0
        [HDR] _ContentTint ("Content Tint", Color) = (0.72, 0.95, 1.0, 1)

        [Header(Body)][Space(4)]
        [HDR] _FillColor ("Fill Color - keep this dark", Color) = (0.015, 0.06, 0.095, 1)
        _FillOpacity ("Fill Opacity", Range(0, 1)) = 0.34

        [Header(Edge)][Space(4)]
        [HDR] _EdgeColor ("Edge Color", Color) = (0.0, 0.78, 1.0, 1)
        _EdgeOpacity ("Edge Opacity", Range(0, 1)) = 0.55
        _EdgeGlow ("Edge Glow", Range(0, 4)) = 1.3
        _FresnelPower ("Edge Falloff - higher is tighter", Range(0.5, 8)) = 2.6

        [Header(Gradient)][Space(4)]
        _GradientStrength ("Gradient Strength", Range(0, 1)) = 0.3
        _GradientHeight ("Gradient Height - local units", Float) = 1.0
        _GradientOffset ("Gradient Offset - local Y", Float) = 0.0
        [Toggle] _GradientFlip ("Flip Gradient", Float) = 0

        [Header(Scanlines)][Space(4)]
        _ScanDensity ("Scanline Density", Range(1, 200)) = 60
        _ScanStrength ("Scanline Strength", Range(0, 0.6)) = 0.12
        _ScanSharpness ("Scanline Sharpness", Range(0.2, 6)) = 1.6
        _ScanSpeed ("Scanline Speed", Range(-3, 3)) = 0.15

        [Header(Sweep)][Space(4)]
        _SweepIntensity ("Sweep Intensity", Range(0, 2)) = 0.35
        _SweepWidth ("Sweep Width", Range(0.01, 0.5)) = 0.08
        _SweepSpeed ("Sweep Speed", Range(0, 2)) = 0.25

        [Header(Instability)][Space(4)]
        _FlickerAmount ("Flicker Amount", Range(0, 0.25)) = 0.02
        _FlickerSpeed ("Flicker Speed", Range(0, 30)) = 7
        _PulseAmount ("Pulse Amount", Range(0, 0.2)) = 0.02
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0.8
        _GlitchAmount ("Glitch Shift", Range(0, 0.04)) = 0.002
        _GlitchChance ("Glitch Line Chance", Range(0, 0.25)) = 0.02
        _GlitchDensity ("Glitch Line Density", Range(1, 200)) = 40
        _GlitchSpeed ("Glitch Speed", Range(0, 10)) = 1.5

        [Header(Faces)][Space(4)]
        _BackFaceDim ("Back Face Dim", Range(0, 1)) = 0.45
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HologramUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _USE_CONTENT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ContentTint;
                float4 _FillColor;
                float4 _EdgeColor;
                float  _UseContent;
                float  _FillOpacity;
                float  _EdgeOpacity;
                float  _EdgeGlow;
                float  _FresnelPower;
                float  _GradientStrength;
                float  _GradientHeight;
                float  _GradientOffset;
                float  _GradientFlip;
                float  _ScanDensity;
                float  _ScanStrength;
                float  _ScanSharpness;
                float  _ScanSpeed;
                float  _SweepIntensity;
                float  _SweepWidth;
                float  _SweepSpeed;
                float  _FlickerAmount;
                float  _FlickerSpeed;
                float  _PulseAmount;
                float  _PulseSpeed;
                float  _GlitchAmount;
                float  _GlitchChance;
                float  _GlitchDensity;
                float  _GlitchSpeed;
                float  _BackFaceDim;
                float  _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  localY      : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = pos.positionCS;
                output.normalWS    = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS   = GetWorldSpaceViewDir(pos.positionWS);
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);

                // 그라디언트/스캔라인 기준축. UV가 아니라 로컬 Y라서 메시 UV와 무관하다
                output.localY = input.positionOS.y;

                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // ── 세로 위치 0~1 ──────────────────────────────
                float h = saturate((input.localY - _GradientOffset) / max(_GradientHeight, 1e-4));
                h = lerp(h, 1.0 - h, saturate(_GradientFlip));

                // ── 글리치: 가로로 드문드문 밀림 ─────────────────
                float2 uv = input.uv;
                float row       = floor(input.localY * _GlitchDensity + _Time.y * _GlitchSpeed);
                float rowHash   = frac(sin(row * 127.1 + 311.7) * 43758.5453);
                float glitchOn  = step(1.0 - _GlitchChance, rowHash);
                uv.x = clamp(uv.x + (rowHash - 0.5) * _GlitchAmount * glitchOn, 0.001, 0.999);

                // ── 몸통 색 ───────────────────────────────────
                float3 fillColor   = _FillColor.rgb;
                float  fillOpacity = _FillOpacity;

                #ifdef _USE_CONTENT
                    half4 content = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                    // 텍스처가 있는 곳만 덮어쓴다. 빈 곳은 어두운 몸통 그대로 유지
                    fillColor   = lerp(fillColor, content.rgb * _ContentTint.rgb, content.a);
                    fillOpacity = lerp(fillOpacity, 1.0, content.a * _ContentTint.a);
                #endif

                // ── 가장자리(프레넬) ───────────────────────────
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS   = normalize(input.viewDirWS);
                float  facing   = saturate(abs(dot(normalWS, viewWS)));
                float  rim      = pow(saturate(1.0 - facing), max(_FresnelPower, 0.01));

                // ── 세로 그라디언트 ────────────────────────────
                float gradient = lerp(1.0, h, _GradientStrength);

                // ── 스캔라인 ──────────────────────────────────
                float phase    = input.localY * _ScanDensity + _Time.y * _ScanSpeed;
                float scanWave = 0.5 + 0.5 * sin(phase * 6.2831853);
                scanWave       = pow(scanWave, max(_ScanSharpness, 0.01));
                float scan     = 1.0 - _ScanStrength * scanWave;

                // ── 주기적으로 위로 훑고 지나가는 띠 ──────────────
                float sweepPos = frac(_Time.y * _SweepSpeed);
                float sweep    = _SweepIntensity *
                                 saturate(1.0 - abs(h - sweepPos) / max(_SweepWidth, 1e-4));

                // ── 불안정함 ──────────────────────────────────
                float flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed));
                float pulse   = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831853);

                // ── 뒷면은 어둡게 (Cull Off일 때 볼륨감) ──────────
                float faceMul = IS_FRONT_VFACE(cullFace, 1.0, _BackFaceDim);

                // ── 합성 ─────────────────────────────────────
                // 알파: 몸통이 배경을 누르고, 가장자리와 띠가 더해진다
                float alpha = fillOpacity * scan * gradient
                            + rim * _EdgeOpacity
                            + sweep * 0.5;
                alpha = saturate(alpha * flicker * pulse * faceMul);

                // 색: 몸통은 어둡게 깔리고, 빛은 가장자리와 띠에서만 나온다
                float3 color = fillColor * scan * gradient
                             + _EdgeColor.rgb * (rim * _EdgeGlow)
                             + _EdgeColor.rgb * sweep;
                color *= faceMul;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
