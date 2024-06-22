using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Camera))]
public class RayMarchCameraCompute : MonoBehaviour
{
    public ComputeShader rayMarchComputeShader;
    public Texture3D volumeTexture;
    public Vector4[] smokeVoxels;

    private RenderTexture resultTexture;
    private ComputeBuffer voxelBuffer;
    private int kernelHandle;

    private void Start()
    {
        InitializeResources();
    }

    private void InitializeResources()
    {
        // Initialize render texture for storing computed result
        resultTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        resultTexture.enableRandomWrite = true;
        resultTexture.Create();

        // Initialize compute buffer for voxel data
        InitializeVoxelBuffer();

        // Find the kernel handle for the compute shader
        kernelHandle = rayMarchComputeShader.FindKernel("CSMain");
    }

    private void InitializeVoxelBuffer()
    {
        if (smokeVoxels != null && smokeVoxels.Length > 0)
        {
            voxelBuffer = new ComputeBuffer(smokeVoxels.Length, sizeof(float) * 4);
            voxelBuffer.SetData(smokeVoxels);
        }
    }

    private void Update()
    {
        if (rayMarchComputeShader == null || resultTexture == null || volumeTexture == null || voxelBuffer == null)
            return;

        // Set compute shader parameters
        var fc = GetCameraFrustumCorners(Camera.main);
        rayMarchComputeShader.SetVector("_camTL", fc.topLeft);
        rayMarchComputeShader.SetVector("_camTR", fc.topRight);
        rayMarchComputeShader.SetVector("_camBL", fc.bottomLeft);
        rayMarchComputeShader.SetVector("_camBR", fc.bottomRight);
        rayMarchComputeShader.SetVector("_camPos", Camera.main.transform.position);
        rayMarchComputeShader.SetMatrix("_camToWorldMatrix", Camera.main.cameraToWorldMatrix);
        rayMarchComputeShader.SetTexture(kernelHandle, "_VolumeTex", volumeTexture);
        rayMarchComputeShader.SetBuffer(kernelHandle, "voxels", voxelBuffer);
        rayMarchComputeShader.SetInt("voxelCount", smokeVoxels.Length);

        // Set the output texture
        rayMarchComputeShader.SetTexture(kernelHandle, "OutputTexture", resultTexture);

        // Dispatch the compute shader
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        rayMarchComputeShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Copy the result to the destination render texture
        Graphics.Blit(resultTexture, destination);
    }

    private void OnDestroy()
    {
        if (voxelBuffer != null)
            voxelBuffer.Release();
    }

    // Find camera near plane corners in camera space
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
