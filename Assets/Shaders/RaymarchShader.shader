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

            uniform float3 bulletOrigins[100];
            uniform float3 bulletDirections[100];
            uniform float bulletSizes[100];
            uniform int bulletCount;

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

            int posToIndex(float3 position)
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
            float sdCylinder(float3 p, float3 a, float3 b, float r)
            {
                float3  ba = b - a;
                float3  pa = p - a;
                float baba = dot(ba,ba);
                float paba = dot(pa,ba);
                float x = length(pa*baba-ba*paba) - r*baba;
                float y = abs(paba-baba*0.5)-baba*0.5;
                float x2 = x*x;
                float y2 = y*y*baba;
                
                float d = (max(x,y)<0.0)?-min(x2,y2):(((x>0.0)?x2:0.0)+((y>0.0)?y2:0.0));
                
                return sign(d)*sqrt(abs(d))/baba;
            }
            float sdRoundCone( float3 p, float3 a, float3 b, float r1, float r2 )
            {
              // sampling independent computations (only depend on shape)
              float3  ba = b - a;
              float l2 = dot(ba,ba);
              float rr = r1 - r2;
              float a2 = l2 - rr*rr;
              float il2 = 1.0/l2;
    
              // sampling dependant computations
              float3 pa = p - a;
              float y = dot(pa,ba);
              float z = y - l2;
              float3 xDot = pa * l2 - ba * y;
              float x2 = dot(xDot, xDot);
              float y2 = y*y*l2;
              float z2 = z*z*l2;

              // single square root!
              float k = sign(rr)*rr*rr*x2;
              if( sign(z)*a2*z2>k ) return  sqrt(x2 + z2)        *il2 - r2;
              if( sign(y)*a2*y2<k ) return  sqrt(x2 + y2)        *il2 - r1;
                                    return (sqrt(x2*a2*il2)+y*rr)*il2 - r1;
            }

            float2 densitySun(float3 ro, float3 rd)
            {
                float res = 0.0f;
                float totalDist = 0.0;
                float stepDist = 0.4;
                float hitDist = -1;
                float3 p = ro;
                while( totalDist < 10)
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

                        dist = smoothstep(0.45f, 0.75f, dist);

                        float falloff = min(1.0f, dist);

                        res += saturate(stepDist * (1 - falloff)) ; 

                        if(hitDist == -1)
                            hitDist = totalDist;
                    }
                }



                return float2(res, hitDist); 
            }


            float bullethole(float3 p, float n)
            {
                float res = 1000000;
                for(int i = 0; i< bulletCount; i++)
                {
                    //res = min(res, sdCylinder(p, bulletOrigins[i], bulletOrigins[i] + bulletDirections[i] * 100, bulletSizes[i]));
                    
                    
                    // float distV = sdCylinder(p, bulletOrigins[i], bulletOrigins[i] + bulletDirections[i] * 100, bulletSizes[i]);

                    // float dist = max(1.0f, distV);
                    // float n = tex3D(_VolumeTex, p * 0.1 + float3( _Time.y * 0.1f, 0, 0)).r;
                    // dist = smoothstep(0.65f, 1.0f, dist);

                    // float falloff = min(1.0f, dist + n);
                    
                    float3 a = bulletOrigins[i];
                    float3 b = bulletOrigins[i] + bulletDirections[i] * 20;

                    float r1 = bulletSizes[i];
                    float r2 = bulletSizes[i] * 0.01f;

                    float distance = sdRoundCone(p, a, b, r1, r2);

                    float dist = min(1.0f, distance + n);


                    dist = smoothstep(clamp(bulletSizes[i],0.0,0.65), 1.0f, dist);

                    res = min(res, dist);

                    // if (dist <= 0) 
                    // {                   
                    //     dist = min(1.0f, dist * -1  + n * 0.4);
                    //     res = min(res, (1-smoothstep(0.75f, 1.0f, dist)));
                    // }
                    // else 
                    // {
                    //     res = res;   
                    // }


                }

                return saturate(res);
            }

            float3 density(float3 ro, float3 rd)
            {
                float res = 0.0f;
                float sunDes = 1.0f;
                float totalDist = 0.0;
                float stepDist = 0.075;
                float hitDist = -1;
                float3 p = ro;
                while( totalDist < 20)
                {
                    totalDist += stepDist;

                    p += rd * stepDist;
                    int voxelStep = tempMapVoxelInfo[posToIndex(p)];
                    float n = tex3D(_VolumeTex, p * 0.1 + float3( _Time.y * 0.1f, 0, 0)).r;
                    float bulletVal = bullethole(p, n);
                    if (posToIndex(p) != -1 && voxelStep != 0)
                    {
                        // res += stepDist * tex3D(_VolumeTex, p * 0.1 + float3( _Time.y * 0.1f, 0, 0)).r / ((float)voxelStep / 6 + length(p - smokeCenter)20)*20;

                        

                        float distE = min(1.0f, length(p-smokeCenter) / smokeRadius);

                        float distV = min(1.0f,(voxelStep)/maxStepCount +5);
                        float dist;

                        // if(distV > distE)
                        //     dist = distE*0.8f + distV * 0.2f;
                        // else
                        //     dist = distE;


                        dist = smoothstep(0.65f, 1.0f, distE);

                        float falloff = min(1.0f, dist + n);

                        float additionRes = stepDist * (1 - falloff) /* * (1-densitySunVal) */;

                        
                        if (bulletVal <= 1)
                        {
                            additionRes  *= bulletVal; 
                        }
                        
                        res += saturate(additionRes); 

                        sunDes -= densitySun(p, (_sunPos - p)).x;
                        

                        if(hitDist == -1)
                            hitDist = totalDist;
                    }
                }



                return float3(res, hitDist, saturate(sunDes)); 
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 res = fixed4(tex2D(_MainTex, i.uv).xyz, 1);
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                depth *= length(i.nearPlanePoint - _camPos.xyz);
                
                float3 dir = normalize(i.nearPlanePoint - _camPos.xyz);
                
                float3 dens = density(_camPos.xyz , dir);


                if(dens.y == -1 || dens.y > depth)
                    return res;

                
                

                float rayleighFactor = 5.5;
                float ABSORPTION = 0.6f;
                
                float totalScattering = ABSORPTION + rayleighFactor;

                float4 color1 = lerp(res,fixed4(0,0.2f,0.0,1.0), 1 - exp(-dens.x * (totalScattering)));


                return color1 + fixed4(0,1.0,0.0,1.0) * 0.1f * dens.z * dens.x;


                    

            }
            ENDCG
        }
    }
}
