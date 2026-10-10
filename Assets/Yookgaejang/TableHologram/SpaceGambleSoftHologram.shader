Shader "SpaceGamble/SoftHologram"
{
    Properties
    {
        _BaseMap ("Image / Transparent Text / RenderTexture", 2D) = "white" {}
        [Toggle] _UseContent ("Use Content Texture", Float) = 0
        [HDR] _ContentTint ("Content Tint", Color) = (0.62, 0.90, 1.0, 1)
        [HDR] _Color ("Hologram Fill", Color) = (0.42, 0.78, 0.91, 1)
        [HDR] _EdgeColor ("Edge Tint", Color) = (0.52, 0.86, 0.96, 1)
        _Opacity ("Surface / Content Opacity", Range(0, 1)) = 0.18
        _EdgeOpacity ("Rim Opacity", Range(0, 1)) = 0.22
        _FresnelPower ("Rim Falloff", Range(0.5, 6)) = 2.2
        _Emission ("Soft Edge Emission", Range(0, 2)) = 0.45
        _ScanlineDensity ("Scanline Density", Range(1, 120)) = 42
        _ScanlineStrength ("Scanline Strength", Range(0, 0.5)) = 0.08
        _ScanSpeed ("Scan Speed", Range(-3, 3)) = 0.14
        _GlitchAmount ("Horizontal Glitch Amount", Range(0, 0.04)) = 0.002
        _GlitchChance ("Glitch Line Chance", Range(0, 0.25)) = 0.025
        _GlitchDensity ("Glitch Line Density", Range(1, 100)) = 26
        _GlitchSpeed ("Glitch Speed", Range(0, 10)) = 1.5
        _FlickerAmount ("Flicker Amount", Range(0, 0.25)) = 0.025
        _FlickerSpeed ("Flicker Speed", Range(0, 30)) = 7
        _PulseAmount ("Pulse Amount", Range(0, 0.2)) = 0.025
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0.8
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
            Name "SoftHologramUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ContentTint;
                float4 _Color;
                float4 _EdgeColor;
                float _UseContent;
                float _Opacity;
                float _EdgeOpacity;
                float _FresnelPower;
                float _Emission;
                float _ScanlineDensity;
                float _ScanlineStrength;
                float _ScanSpeed;
                float _GlitchAmount;
                float _GlitchChance;
                float _GlitchDensity;
                float _GlitchSpeed;
                float _FlickerAmount;
                float _FlickerSpeed;
                float _PulseAmount;
                float _PulseSpeed;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Sparse, small horizontal shifts affect the texture itself, including text.
                float row = floor(uv.y * _GlitchDensity + _Time.y * _GlitchSpeed);
                float rowHash = frac(sin(row * 127.1 + 311.7) * 43758.5453);
                float glitchLine = step(1.0 - _GlitchChance, rowHash);
                uv.x = clamp(uv.x + (rowHash - 0.5) * _GlitchAmount * glitchLine * _UseContent, 0.001, 0.999);

                half4 content = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float3 sourceColor = lerp(_Color.rgb, content.rgb * _ContentTint.rgb, saturate(_UseContent));
                float sourceAlpha = lerp(1.0, content.a, saturate(_UseContent));

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float facing = saturate(abs(dot(normalWS, viewDirWS)));
                float rim = pow(saturate(1.0 - facing), _FresnelPower);

                float phase = input.uv.y * _ScanlineDensity + _Time.y * _ScanSpeed;
                float scanWave = 0.5 + 0.5 * sin(phase * 6.2831853);
                float scan = 1.0 - _ScanlineStrength * scanWave;
                float flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed));
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831853);

                float alpha = saturate((sourceAlpha * _Opacity * scan + rim * _EdgeOpacity) * flicker * pulse);
                float3 color = sourceColor * scan + _EdgeColor.rgb * (rim * _Emission);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
