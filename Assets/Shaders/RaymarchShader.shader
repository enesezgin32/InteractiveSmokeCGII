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
            sampler2D _CameraDepthTexture;
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
                float3 nearPlanePoint : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                
                float3 rightVec = _camTR.xyz - _camTL.xyz;
                float3 upVec = _camTL.xyz - _camBL.xyz;
                o.nearPlanePoint = rightVec * v.vertex.x + upVec * v.vertex.y + _camBL;
                o.nearPlanePoint /= abs(o.nearPlanePoint.z);
                o.nearPlanePoint = mul(_camToWorldMatrix, float4(o.nearPlanePoint, 1));
                
                v.vertex.z = 0;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv.xy;
                return o;
            }

            float sdSphere(float3 pos, float rad, float3 p)
            {
                return length(p - pos) - rad;
            }
            float scene(float3 p)
            {
                return sdSphere(float3(0, 0, 0), 1, p);
            }
            fixed4 raymarching(float3 ro, float3 rd, v2f i)
            {
                fixed4 res = fixed4(tex2D(_MainTex, i.uv).xyz, 1);
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                depth *= length(i.nearPlanePoint - _camPos.xyz);
                float t = 0.0;
                for (int i = 0; i < 256; i++)
                {
                    if (t >= depth)
                    {
                        break;
                    }
                    float3 p = ro + rd * t;
                    float d = scene(p);
                    if (d < 0.001)
                    {
                        res = fixed4(1, 0, 0, 1);
                        break;
                    }
                    t += d;
                }
                return res;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.nearPlanePoint - _camPos.xyz);
                return raymarching(_camPos.xyz, dir, i);
            }
            ENDCG
        }
    }
}
