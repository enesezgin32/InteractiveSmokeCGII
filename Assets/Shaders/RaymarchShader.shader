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
            sampler3D _VolumeTex;
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
            float opSmoothUnion( float d1, float d2, float k )
            {
                float h = clamp( 0.5 + 0.5*(d2-d1)/k, 0.0, 1.0 );
                return lerp( d2, d1, h ) - k*h*(1.0-h);
            }
            float sdSphere(float3 pos, float rad, float3 p)
            {
                return length(p - pos) - rad;
            }
            float sdBox( float3 pos, float3 b, float3 p )
            {
              float3 q = abs(pos - p) - b;
              return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
            }
            float scene(float3 p)
            {
                float x = opSmoothUnion(sdSphere(float3(0, 0, 0), 1, p), sdSphere(float3(0, 1.5, 0), 1, p), 0.7);
                
                return opSmoothUnion(x, sdBox(float3(3, 0, 0), float3(1, 1, 1), p), 0.1);
            }
            float density(float3 ro, float3 rd)
            {
                float totalDist = 0.0;
                float stepDist = 0.05;
                float3 p = ro;
                int maxIT = 512;
                do
                {
                    p += rd * stepDist;
                    totalDist += stepDist; 
                }
                while (scene(p) < 0 && maxIT-- > 0);
                totalDist -= scene(p);
                int sampleCount = 40;
                p = ro;
                float res = 0.0f;
                for(int i=0;i<sampleCount;i++)
                {
                    float stepLen = totalDist/sampleCount;
                    p += rd * stepLen;
                    res+= stepLen * tex3D(_VolumeTex, p * 0.25 + float3( _Time.y * 0.1f, 0, 0)).r;
                }
                return res;
            }
            float2 raymarchDist(float3 ro, float3 rd, float depth)
            {
                float t = 0.0;
                for (int i = 0; i < 512; i++)
                {
                    if (t >= depth)
                    {
                        return -1;
                    }
                    float3 p = ro + rd * t;
                    float d = scene(p);
                    if (d < 0.001)
                    {
                        return t;
                    }
                    t += d;
                }
                return -1;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 res = fixed4(tex2D(_MainTex, i.uv).xyz, 1);
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                depth *= length(i.nearPlanePoint - _camPos.xyz);
                
                float3 dir = normalize(i.nearPlanePoint - _camPos.xyz);
                float surfaceDist = raymarchDist(_camPos.xyz, dir, depth);
                
                float dens = density(_camPos.xyz + dir * surfaceDist, dir); 
                if(surfaceDist == -1)
                    return res;
                float ABSORPTION = 15;
                return lerp(res * exp(-dens * ABSORPTION), fixed4(0, 1,0 , 1), 1 - exp(-dens * ABSORPTION));
            }
            ENDCG
        }
    }
}
