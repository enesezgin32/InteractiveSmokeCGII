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
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles
// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members ray)
#pragma exclude_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            
            StructuredBuffer<uint> tempMapVoxelInfo;

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            sampler3D _VolumeTex;

            uniform float voxelCount = 0;
            // uniform float4 voxels[1000];
            uniform float4 _camPos;
            uniform float4 _camTL;
            uniform float4 _camTR;
            uniform float4 _camBL;
            uniform float4 _camBR;
            uniform float4x4 _camToWorldMatrix;

            float3 _sunPos;
            float4 _sunColor;

            float3 gridSize;
            float3 centerPosition;
            float3 smokeCenter;
            float smokeRadius;

            float voxelSize;

            int maxStepCount;
            
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

            //Vertex Shader
            v2f vert (appdata v)
            {
                //for every vertex, calculate its position relative to camera
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
            uint3 indexToCord(uint index)
            {
                uint z = index / (gridSize.x * gridSize.y);
                uint y = (index % (gridSize.x * gridSize.y)) / gridSize.x;
                uint x = index % gridSize.x;
    
                return uint3(x, y, z);
            }

            uint cordToIndex(uint3 cord)
            {
                return cord.x + gridSize.x * (cord.y + gridSize.y * cord.z);
            }

            uint posToIndex(float3 position)
            {


                float3 centerOffset = centerPosition - float3(voxelSize * gridSize.x * 0.5f, 0, voxelSize * gridSize.z * 0.5f) + float3(1.0f, 0.0f, 1.0f) * voxelSize * 0.5f;
                uint3 id = (position - centerOffset) / voxelSize; // find x y z in float values

                float3 idcheck = (position - centerOffset) / voxelSize;

                if(idcheck.x < 0 || idcheck.x > gridSize.x || idcheck.y < 0 || idcheck.y > gridSize.y || idcheck.z < 0 || idcheck.z > gridSize.z)
                    return -1;

                uint index = ( id.x + gridSize.x * ( id.y + gridSize.y * id.z));


                return index;
            }

            uint3 posToCord(float3 position)
            {
                float3 centerOffset = centerPosition - float3(voxelSize * gridSize.x * 0.5f, 0, voxelSize * gridSize.z * 0.5f) + float3(1.0f, 0.0f, 1.0f) * voxelSize * 0.5f;
                float3 id = (position - centerOffset) / voxelSize; // find x y z in float values
                uint index = ((uint) id.x + gridSize.x * ((uint) id.y + gridSize.y * (uint) id.z));
                return indexToCord(index);
            }

            float3 indexToPos(uint index)
            {
                uint3 id = indexToCord(index);

                float3 centerOffset = centerPosition - float3(voxelSize * gridSize.x * 0.5f, 0, voxelSize * gridSize.z * 0.5f) + float3(1.0f, 0.0f, 1.0f) * voxelSize * 0.5f;
                float3 position = float3(id.x, id.y, id.z) * voxelSize + centerOffset;

                return position;
            }

            float3 cordToPos(uint3 cord)
            {
                float3 centerOffset = centerPosition - float3(voxelSize * gridSize.x * 0.5f, 0, voxelSize * gridSize.z * 0.5f) + float3(1.0f, 0.0f, 1.0f) * voxelSize * 0.5f;
                float3 position = float3(cord.x, cord.y, cord.z) * voxelSize + centerOffset;
                return position;
            }



// SDF FUCNTIONS
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

            float2 density(float3 ro, float3 rd)
            {
                float res = 0.0f;
                float totalDist = 0.0;
                float stepDist = 0.1;
                float hitDist = -1;
                float3 p = ro;
                while( totalDist < 30)
                {
                    totalDist += stepDist;

                    p += rd * stepDist;
                    int voxelStep = tempMapVoxelInfo[posToIndex(p)];
                    if (posToIndex(p) != -1 && tempMapVoxelInfo[posToIndex(p)] != 0)
                    {
                        // res += stepDist * tex3D(_VolumeTex, p * 0.1 + float3( _Time.y * 0.1f, 0, 0)).r / ((float)voxelStep / 6 + length(p - smokeCenter)20)*20;

                        float n = tex3D(_VolumeTex, p * 0.1 + float3( _Time.y * 0.1f, 0, 0)).r;

                        float distE = min(1.0f, length(p-smokeCenter) / smokeRadius);

                        float distV = min(1.0f,(voxelStep)/maxStepCount);

                        float dist = max(distE, distV);

                        dist = smoothstep(0.65f, 1.0f, dist);

                        float falloff = min(1.0f, dist + n);

                        res += saturate(stepDist * (1 - falloff)); 

                        if(hitDist == -1)
                            hitDist = totalDist;
                    }
                }
                return float2(res, hitDist); 
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 res = fixed4(tex2D(_MainTex, i.uv).xyz, 1);
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                depth *= length(i.nearPlanePoint - _camPos.xyz);
                
                float3 dir = normalize(i.nearPlanePoint - _camPos.xyz);
                
                float2 dens = density(_camPos.xyz , dir);


                // Add sun source density calculation
                float3 dirToSun = normalize(i.nearPlanePoint - _sunPos.xyz);
                float2 densToSun = density(_sunPos.xyz, dirToSun);


                if(dens.y == -1 || dens.y > depth)
                    return res;

                dens.x  = dens.x * (1-densToSun.x);
                float ABSORPTION = 1;
                return lerp(res * exp(-dens.x * ABSORPTION), _sunColor *0.5f + fixed4(0,0.5f,0.0,0.0), 1 - exp(-dens.x * ABSORPTION));
            }
            ENDCG
        }
    }
}
