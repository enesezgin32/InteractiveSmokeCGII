Shader "Custom/VoxelRenderShader"
{
    Properties
    {
        _VoxelSize("Voxel Size", Float) = 1.0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Voxel
            {
                float3 position;
                float4 color;
            };

            StructuredBuffer<Voxel> voxels;

            float _VoxelSize;

            struct appdata
            {
                uint instanceID : SV_InstanceID;
                float3 position : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                Voxel voxel = voxels[v.instanceID];
                o.pos = UnityObjectToClipPos(float4(voxel.position + v.position, 1.0));
                o.color = voxel.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
