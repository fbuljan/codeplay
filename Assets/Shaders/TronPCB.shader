Shader "Custom/TronPCB"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.07, 0.07, 0.11, 1)
        _LineColor ("Line Color", Color) = (0.765, 0.906, 0.29, 1)
        _TraceSpacing ("Trace Spacing", Float) = 0.4
        _TraceWidth ("Trace Width", Range(0.005, 0.15)) = 0.03
        _EmissionStrength ("Emission Strength", Float) = 2.0
        _SegmentLength ("Segment Length", Float) = 2.0
        _WobbleAmount ("Wobble Amount", Range(0, 0.4)) = 0.2
        _PadSize ("Pad Size", Range(0, 0.15)) = 0.06
        _PadChance ("Pad Chance", Range(0, 1)) = 0.3
        _CrossoverChance ("Crossover Chance", Range(0, 1)) = 0.25
        _CrossoverWidth ("Crossover Width", Range(0.005, 0.1)) = 0.02
        _BrightnessVariation ("Brightness Variation", Range(0, 1)) = 0.4
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
            float _TraceSpacing;
            float _TraceWidth;
            float _EmissionStrength;
            float _SegmentLength;
            float _WobbleAmount;
            float _PadSize;
            float _PadChance;
            float _CrossoverChance;
            float _CrossoverWidth;
            float _BrightnessVariation;

            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float hash1(float p)
            {
                return hash(float2(p, p * 0.7123));
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
                float wx = i.worldPos.x;
                float wz = i.worldPos.z;

                // Which trace channel and Z segment are we in
                float traceIdx = floor(wx / _TraceSpacing + 0.5);
                float zSeg = floor(wz / _SegmentLength);

                // Each trace wobbles left/right per Z segment
                // Current and next segment offsets for smooth transition
                float wobbleCur = (hash(float2(traceIdx, zSeg)) - 0.5) * _WobbleAmount * _TraceSpacing;
                float wobbleNext = (hash(float2(traceIdx, zSeg + 1.0)) - 0.5) * _WobbleAmount * _TraceSpacing;

                // Smooth interpolation within segment (ease in/out at ends)
                float zFrac = frac(wz / _SegmentLength);
                // Short diagonal transition in the middle 20% of each segment
                float transitionStart = 0.4;
                float transitionEnd = 0.6;
                float t = saturate((zFrac - transitionStart) / (transitionEnd - transitionStart));
                t = t * t * (3.0 - 2.0 * t); // smoothstep
                float wobble = lerp(wobbleCur, wobbleNext, t);

                // Trace center X position
                float traceCenterX = traceIdx * _TraceSpacing + wobble;

                // Distance from fragment to trace
                float distToTrace = abs(wx - traceCenterX);

                // Anti-aliased trace line
                float fwx = fwidth(wx);
                float traceMask = 1.0 - smoothstep(0.0, fwx + _TraceWidth, distToTrace);

                // Per-trace brightness
                float traceBrightness = lerp(1.0 - _BrightnessVariation, 1.0, hash1(traceIdx));

                // Pads: bright dots at segment boundaries on some traces
                float padMask = 0.0;
                float segBoundaryDist = abs(frac(wz / _SegmentLength) - 0.5) * _SegmentLength;
                float segStartDist = min(frac(wz / _SegmentLength), 1.0 - frac(wz / _SegmentLength)) * _SegmentLength;
                if (hash(float2(traceIdx * 3.7, zSeg * 2.3)) < _PadChance)
                {
                    float2 padCenter = float2(traceCenterX, (zSeg + 0.5) * _SegmentLength);
                    // Use wobble at the midpoint for pad position
                    float padWobble = (hash(float2(traceIdx, zSeg)) - 0.5) * _WobbleAmount * _TraceSpacing;
                    padCenter.x = traceIdx * _TraceSpacing + padWobble;
                    float padDist = length(float2(wx - padCenter.x, wz - padCenter.y));
                    padMask = 1.0 - smoothstep(0.0, fwx + _PadSize, padDist);
                }

                // Horizontal crossovers: short horizontal segments connecting adjacent traces
                float crossMask = 0.0;
                float crossZ = (zSeg + 0.5) * _SegmentLength; // midpoint of segment
                float distToCrossZ = abs(wz - crossZ);
                float fwz = fwidth(wz);
                if (hash(float2(traceIdx * 1.3, zSeg * 4.7)) < _CrossoverChance)
                {
                    float crossHoriz = 1.0 - smoothstep(0.0, fwz + _CrossoverWidth, distToCrossZ);
                    // Only between this trace and the next
                    float nextTraceX = (traceIdx + 1.0) * _TraceSpacing +
                        (hash(float2(traceIdx + 1.0, zSeg)) - 0.5) * _WobbleAmount * _TraceSpacing;
                    float minX = min(traceCenterX, nextTraceX);
                    float maxX = max(traceCenterX, nextTraceX);
                    // Use the midpoint wobble for crossover position
                    float midWobbleCur = (hash(float2(traceIdx, zSeg)) - 0.5) * _WobbleAmount * _TraceSpacing;
                    minX = traceIdx * _TraceSpacing + midWobbleCur;
                    float midWobbleNext = (hash(float2(traceIdx + 1.0, zSeg)) - 0.5) * _WobbleAmount * _TraceSpacing;
                    maxX = (traceIdx + 1.0) * _TraceSpacing + midWobbleNext;
                    if (minX > maxX) { float tmp = minX; minX = maxX; maxX = tmp; }
                    float inBand = step(minX - _TraceWidth, wx) * step(wx, maxX + _TraceWidth);
                    crossMask = crossHoriz * inBand;
                }

                // Combine all masks
                float totalMask = saturate(traceMask + padMask * 1.5 + crossMask);

                float brightness = _EmissionStrength * traceBrightness;
                fixed4 col = lerp(_BaseColor, _LineColor * brightness, totalMask);
                col.a = 1.0;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
