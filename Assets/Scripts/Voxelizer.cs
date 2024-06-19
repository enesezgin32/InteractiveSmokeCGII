using System.Collections.Generic;
using UnityEngine;

public class Voxelizer : MonoBehaviour
{
    public ComputeShader voxelComputeShader;
    public Shader voxelRenderShader;

    public Material voxelMaterial;
    public Mesh voxelMesh;

    public int gridSize = 10;
    public float voxelSize = 0.5f;

    private ComputeBuffer voxelBuffer;
    private ComputeBuffer argsBuffer;
    private Voxel[] voxels;

    private Bounds bounds;

    public struct Voxel
    {
        public Vector3 position;
        public Color color;
    }

    private Material VoxelRenderMaterial
    {
        get
        {
            if (!voxelRenderMaterial && voxelRenderShader)
            {
                voxelRenderMaterial = new Material(voxelRenderShader);
                voxelRenderMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return voxelRenderMaterial;
        }
    }
    private Material voxelRenderMaterial;

    private void InitializeVoxels()
    {
        voxels = new Voxel[gridSize * gridSize];
        int colorSize = sizeof(float) * 4;
        int vector3Size = sizeof(float) * 3;
        int totalVoxelDataSize = colorSize + vector3Size;
        voxelBuffer = new ComputeBuffer(voxels.Length, totalVoxelDataSize);

        // Initialize the argument buffer
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)voxelMesh.GetIndexCount(0);
        args[1] = (uint)voxels.Length;
        args[2] = (uint)voxelMesh.GetIndexStart(0);
        args[3] = (uint)voxelMesh.GetBaseVertex(0);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Set bounds for drawing
        bounds = new Bounds(Vector3.zero, new Vector3(gridSize, gridSize, gridSize) * voxelSize);
    }

    private void ComputeVoxels(Vector3 center)
    {
        voxelComputeShader.SetBuffer(0, "voxels", voxelBuffer);
        voxelComputeShader.SetInt("resolution", gridSize);
        voxelComputeShader.SetVector("centerPosition", center);
        voxelComputeShader.Dispatch(0, Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f), 1);

        voxelBuffer.GetData(voxels);
    }

    void Start()
    {
        InitializeVoxels();
        ComputeVoxels(Vector3.zero); // Initialize voxels' positions in the compute shader
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;
                ComputeVoxels(hitPoint);
            }
        }

        VoxelRenderMaterial.SetBuffer("voxels", voxelBuffer);
        VoxelRenderMaterial.SetFloat("_VoxelSize", voxelSize);

        Graphics.DrawMeshInstancedIndirect(voxelMesh, 0, VoxelRenderMaterial, bounds, argsBuffer);
    }

    void OnDestroy()
    {
        if (voxelBuffer != null)
        {
            voxelBuffer.Release();
        }
        if (argsBuffer != null)
        {
            argsBuffer.Release();
        }
    }
}
