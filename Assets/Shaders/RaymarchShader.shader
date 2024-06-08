Shader "Hidden/RaymarchShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members ray)
#pragma exclude_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            uniform float4 _camPos;
            uniform float4 _camTL;
            uniform float4 _camTR;
            uniform float4 _camBL;
            uniform float4 _camBR;
            uniform float4x4 _camToWorldMatrix;
            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ray : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                
                float3 rightVec = _camTR.xyz - _camTL.xyz;
                float3 upVec = _camTL.xyz - _camBL.xyz;
                o.ray = rightVec * v.vertex.x + upVec * v.vertex.y + _camBL;
                o.ray = mul(_camToWorldMatrix, float4(o.ray, 1));
                
                v.vertex.z = 0;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 raymarching(float3 ro, float3 rd)
            {
                //Sphere marching on the origin with radius 1
                float3 spherePos = float3(0, 0, 0);
                float sphereRadius = 1.0;
                float3 oc = ro - spherePos;
                float b = dot(oc, rd);
                float c = dot(oc, oc) - sphereRadius * sphereRadius;
                float discriminant = b * b - c;
                if (discriminant > 0)
                {
                    float t = -b - sqrt(discriminant);
                    if (t > 0)
                    {
                        return fixed4(1, 1, 1, 1);
                    }
                }
                return fixed4(0, 0, 0, 1);
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.ray - _camPos.xyz);
                return raymarching(_camPos.xyz, dir);
            }
            ENDCG
        }
    }
}
