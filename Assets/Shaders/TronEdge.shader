Shader "Custom/TronEdge"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.02, 0.04, 1)
        _EdgeColor ("Edge Color", Color) = (0.765, 0.906, 0.29, 1)
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.06
        _EmissionStrength ("Emission Strength", Float) = 3.0
        _FaceGlow ("Face Glow", Range(0, 0.3)) = 0.04
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
                float3 objPos : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            fixed4 _BaseColor;
            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _EmissionStrength;
            float _FaceGlow;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.objPos = v.vertex.xyz; // -0.5 to 0.5 for Unity cube
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Distance from each face boundary (0 at edge, 0.5 at center)
                float3 d = 0.5 - abs(i.objPos);

                // Sort to find the second-smallest distance
                // An edge is where 2+ axes are near their boundary
                float dmin = min(d.x, min(d.y, d.z));
                float dmax = max(d.x, max(d.y, d.z));
                float dmid = d.x + d.y + d.z - dmin - dmax;

                // Edge mask: bright at edges, dark on faces
                float edgeMask = 1.0 - smoothstep(0.0, _EdgeWidth, dmid);

                // Base color with subtle face glow + bright edges
                fixed4 baseCol = _BaseColor + _EdgeColor * _FaceGlow;
                fixed4 edgeCol = _EdgeColor * _EmissionStrength;
                fixed4 col = lerp(baseCol, edgeCol, edgeMask);
                col.a = 1.0;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
