using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
//[ExecuteInEditMode]
public class RaymarchCamera : SceneViewFilter
{
    [SerializeField] private Shader _shader;
    [SerializeField] private Texture3D _volumeTexture;

    private Voxelizer _voxelizer;

    private Material RaymarchMaterial
    {
        get
        {
            if (!_raymarchMat && _shader)
            {
                _raymarchMat = new Material(_shader);
                _raymarchMat.hideFlags = HideFlags.HideAndDontSave;
            }
            return _raymarchMat;
        }
    }
    private Material _raymarchMat;

    private Camera Camera
    {
        get
        {
            if (!_cam)
            {
                _cam = GetComponent<Camera>();
            }
            return _cam;
        }
    }
    private Camera _cam;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int CamTL = Shader.PropertyToID("_camTL");
    private static readonly int CamTR = Shader.PropertyToID("_camTR");
    private static readonly int CamBL = Shader.PropertyToID("_camBL");
    private static readonly int CamBR = Shader.PropertyToID("_camBR");
    private static readonly int CamPos = Shader.PropertyToID("_camPos");
    private static readonly int CamToWorldMatrix = Shader.PropertyToID("_camToWorldMatrix");
    private static readonly int VolumeTex = Shader.PropertyToID("_VolumeTex");
    private static readonly int VoxelCount = Shader.PropertyToID("voxelCount");



    private float startTime;
    private void Start()
    {
        startTime = Time.time;
        _voxelizer = FindObjectOfType<Voxelizer>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(!RaymarchMaterial)
        {
            Graphics.Blit(source, destination);
            return;
        }

        //set shader variables
        var fc = GetCameraFrustumCorners(Camera);
        RaymarchMaterial.SetVector(CamTL, fc.topLeft);
        RaymarchMaterial.SetVector(CamTR, fc.topRight);
        RaymarchMaterial.SetVector(CamBL, fc.bottomLeft);
        RaymarchMaterial.SetVector(CamBR, fc.bottomRight);
        RaymarchMaterial.SetVector(CamPos, Camera.transform.position);
        RaymarchMaterial.SetMatrix(CamToWorldMatrix, Camera.cameraToWorldMatrix);
        RaymarchMaterial.SetTexture(VolumeTex, _volumeTexture);


        RaymarchMaterial.SetVector("gridSize", new Vector3(_voxelizer.gridSize, _voxelizer.gridSize, _voxelizer.gridSize));
        RaymarchMaterial.SetVector("centerPosition", _voxelizer.centerPosition);
        RaymarchMaterial.SetFloat("voxelSize", _voxelizer.voxelSize);
        RaymarchMaterial.SetFloat("smokeRadius", _voxelizer.smokeRadius);
        RaymarchMaterial.SetVector("smokeCenter", _voxelizer.smokeCenter);

        _voxelizer.tempMapVoxelInfoBuffer.SetData(_voxelizer.tempMapVoxelInfo);
        RaymarchMaterial.SetBuffer("tempMapVoxelInfo", _voxelizer.tempMapVoxelInfoBuffer);

        //int countOfTwos = 0;
        //foreach (int value in _voxelizer.tempMapVoxelInfo)
        //{
        //    if (value == 2)
        //    {
        //        countOfTwos++;
        //    }
        //}

        //Debug.Log("Count of 2s: " + countOfTwos);



        //Vector4[] smokeVoxels = new Vector4[_voxelizer.smokeVoxels.Length];
        //int j = 0;
        //for (int i = 0; i < _voxelizer.smokeVoxels.Length; i++)
        //{
        //    var p = _voxelizer.smokeVoxels[i].position;
        //    if(p != Vector3.zero)
        //        smokeVoxels[j++] = new Vector4(p.x,p.y, p.z, _voxelizer.voxelSize);
        //}
        //if(Time.time - startTime < 10)
        //{
        //    RaymarchMaterial.SetFloat(VoxelCount, j);
        //    if(j>0)
        //        RaymarchMaterial.SetVectorArray("voxels", smokeVoxels);
        //}

        //set render target and draw a fullscreen quad
        RenderTexture.active = destination;
        RaymarchMaterial.SetTexture(MainTex, source);
        //push current used mvp matrix to allow as to draw fullscreen quad
        GL.PushMatrix();
        GL.LoadOrtho();
        //Active the shader pass numbered 0 (we only have zero)
        RaymarchMaterial.SetPass(0);
        //draw a quad
        GL.Begin(GL.QUADS);
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 0.0f);
        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 0.0f);
        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 0.0f);
        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);
        GL.End();
        //get back the old mvp matrix
        GL.PopMatrix();
    }

    //find camera near plane corners in camera space
    private FrustumCorners GetCameraFrustumCorners(Camera cam)
    {
        float fov = cam.fieldOfView;
        float aspect = cam.aspect;

        float halfHeight = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        Vector3 toTop = Vector3.up * halfHeight;
        Vector3 toRight = Vector3.right * halfHeight * aspect;

        Vector3 topLeft = -Vector3.forward + toTop - toRight;
        Vector3 topRight = -Vector3.forward + toTop + toRight;
        Vector3 bottomLeft = -Vector3.forward - toTop - toRight;
        Vector3 bottomRight = -Vector3.forward - toTop + toRight;
        
        return new FrustumCorners(topLeft, topRight, bottomLeft, bottomRight); 
    }
    
    private class FrustumCorners
    {
        public Vector3 topLeft;
        public Vector3 topRight;
        public Vector3 bottomLeft;
        public Vector3 bottomRight;
        public FrustumCorners(Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br) 
        {
            topLeft = tl;
            topRight = tr;
            bottomLeft = bl;
            bottomRight = br;
        }
    }
}
