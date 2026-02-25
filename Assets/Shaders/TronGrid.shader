Shader "Custom/TronGrid"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.06, 0.06, 0.1, 1)
        _LineColor ("Line Color", Color) = (0.765, 0.906, 0.29, 1)
        _GridDensity ("Grid Density", Float) = 0.4
        _LineWidth ("Line Width", Range(0.01, 0.5)) = 0.05
        _EmissionStrength ("Emission Strength", Float) = 2.0
        _PulseSpeed ("Pulse Speed", Float) = 0.0
        _PulseMin ("Pulse Min Brightness", Range(0, 1)) = 0.7
        _Distortion ("Line Distortion", Range(0, 0.3)) = 0.08
        _BrightnessVariation ("Brightness Variation", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            fixed4 _BaseColor;
            fixed4 _LineColor;
            float _GridDensity;
            float _LineWidth;
            float _EmissionStrength;
            float _PulseSpeed;
            float _PulseMin;
            float _Distortion;
            float _BrightnessVariation;

            // Simple hash for per-cell randomness
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 worldUV = i.worldPos.xz * _GridDensity;

                // Cell ID for per-cell randomness
                float2 cellID = floor(worldUV);

                // Distort grid lines: offset each line by a hash of its cell
                float2 distortX = float2(_Distortion * (hash(float2(cellID.x, 0.0)) - 0.5), 0.0);
                float2 distortZ = float2(0.0, _Distortion * (hash(float2(0.0, cellID.y)) - 0.5));
                float2 distortedUV = worldUV + distortX + distortZ;

                // Distance to nearest grid line
                float2 grid = abs(frac(distortedUV - 0.5) - 0.5);

                // Anti-aliased lines
                float2 fw = fwidth(worldUV);
                float2 lineFade = smoothstep(0.0, fw * _LineWidth, grid);
                float gridMask = 1.0 - min(lineFade.x, lineFade.y);

                // Per-cell brightness variation: some lines brighter, some dimmer
                float cellBrightness = lerp(1.0 - _BrightnessVariation, 1.0, hash(cellID));

                // Optional pulse
                float pulse = 1.0;
                if (_PulseSpeed > 0.0)
                    pulse = lerp(_PulseMin, 1.0, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed));

                // Blend
                float brightness = _EmissionStrength * cellBrightness * pulse;
                fixed4 col = lerp(_BaseColor, _LineColor * brightness, gridMask);
                col.a = 1.0;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
