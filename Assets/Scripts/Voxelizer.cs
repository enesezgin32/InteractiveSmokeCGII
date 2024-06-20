using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Voxelizer : MonoBehaviour
{
    public ComputeShader voxelComputeShader;
    public Shader voxelRenderShader;

    public Material voxelMaterial;
    public Mesh voxelMesh;

    public int gridSize = 10;
    public float voxelSize = 1f;

    private ComputeBuffer voxelBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer meshVertexBuffer;
    private ComputeBuffer meshIndexBuffer;
    private ComputeBuffer mapVoxelInfoBuffer;
    private Voxel[] voxels;

    private Bounds bounds;

    public int[] occupiedplaces;
    [SerializeField] private Transform staticObjects;


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





    void Start()
    {
        occupiedplaces = new int[gridSize* gridSize * gridSize];

        InitializeVoxels();
        
        /*ComputeVoxels(Vector3.zero);*/ // Initialize voxels' positions in the compute shader
        VoxelizeMesh();
        Debug.Log(voxelMesh.bounds);

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
        Debug.Log(voxelMesh.bounds);
        Graphics.DrawMeshInstancedIndirect(voxelMesh, 0, VoxelRenderMaterial, bounds, argsBuffer);
    }

    private void InitializeVoxels()
    {
        voxels = new Voxel[gridSize * gridSize * gridSize];
        int colorSize = sizeof(float) * 4;
        int vector3Size = sizeof(float) * 3;
        int totalVoxelDataSize = colorSize + vector3Size;

        voxelBuffer = new ComputeBuffer(voxels.Length, totalVoxelDataSize);
        mapVoxelInfoBuffer = new ComputeBuffer(voxels.Length, sizeof(int));

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



        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        // Iterate through all MeshFilter components in the staticObjects GameObject
        foreach (MeshFilter targetMeshFilter in staticObjects.GetComponentsInChildren<MeshFilter>())
        {
            // Get the sharedMesh of each MeshFilter
            Mesh mesh = targetMeshFilter.sharedMesh;

            // Ensure the mesh is valid
            if (mesh != null)
            {
                // Get the local-to-world matrix for the current MeshFilter
                Matrix4x4 localToWorld = targetMeshFilter.transform.localToWorldMatrix;

                // Accumulate vertices, transforming them to world space
                foreach (Vector3 vertex in mesh.vertices)
                {
                    vertices.Add(localToWorld.MultiplyPoint3x4(vertex));
                }

                // Accumulate indices, adjusting them for the overall vertex offset
                int vertexOffset = vertices.Count - mesh.vertexCount;
                foreach (int index in mesh.triangles)
                {
                    indices.Add(index + vertexOffset);
                }
            }
        }

        Debug.Log("Total vertices count: " + vertices.Count);   
        Debug.Log("Total indices count: " + indices.Count);

        // Create and set up compute buffers
        meshVertexBuffer = new ComputeBuffer(vertices.Count, sizeof(float) * 3);
        meshIndexBuffer = new ComputeBuffer(indices.Count, sizeof(int));

        meshVertexBuffer.SetData(vertices.ToArray());
        meshIndexBuffer.SetData(indices.ToArray());


    }

    private void ComputeVoxels(Vector3 center)
    {
        voxelComputeShader.SetBuffer(0, "voxels", voxelBuffer);
        voxelComputeShader.SetInt("resolution", gridSize);
        voxelComputeShader.SetVector("centerPosition", center);
        voxelComputeShader.SetInts("gridSize", new int[] { gridSize, gridSize, gridSize });
        voxelComputeShader.Dispatch(0, Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 1.0f));

        voxelBuffer.GetData(voxels);
    }

    private void VoxelizeMesh()
    {
        // Set buffers and parameters for the compute shader
        voxelComputeShader.SetVector("centerPosition", new Vector3(0,0, 0));
        voxelComputeShader.SetBuffer(0, "voxels", voxelBuffer);
        voxelComputeShader.SetBuffer(0, "vertices", meshVertexBuffer);
        voxelComputeShader.SetBuffer(0, "indices", meshIndexBuffer);
        voxelComputeShader.SetFloat("voxelSize", voxelSize);
        voxelComputeShader.SetInts("gridSize", new int[] { gridSize, gridSize, gridSize });
        voxelComputeShader.Dispatch(0, Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f), 1);

        voxelComputeShader.SetBuffer(1, "voxels", voxelBuffer);
        voxelComputeShader.SetBuffer(1, "meshVertices", meshVertexBuffer);
        voxelComputeShader.SetBuffer(1, "meshIndices", meshIndexBuffer);
        voxelComputeShader.SetBuffer(1, "mapVoxelInfo", mapVoxelInfoBuffer);
        voxelComputeShader.SetInt("meshIndiceCount", meshIndexBuffer.count);
        Debug.Log($"{meshIndexBuffer.count} -> meshIndexBuffer.count");

        voxelComputeShader.SetFloat("voxelSize", voxelSize);
        voxelComputeShader.SetInts("gridSize", new int[] { gridSize, gridSize, gridSize });
        voxelComputeShader.Dispatch(1, Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f));

        mapVoxelInfoBuffer.GetData(occupiedplaces);
        voxelBuffer.GetData(voxels);
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
