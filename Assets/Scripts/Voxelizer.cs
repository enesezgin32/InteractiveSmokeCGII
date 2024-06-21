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

    public int smokeWidth = 10;

    private ComputeBuffer voxelBuffer;

    private ComputeBuffer argsBuffer;
    private ComputeBuffer meshVertexBuffer;
    private ComputeBuffer meshIndexBuffer;
    private ComputeBuffer mapVoxelInfoBuffer;
    private Voxel[] mapVoxels;

    private ComputeBuffer smokeVoxelBuffer;
    private Voxel[] smokeVoxels;

    private Bounds bounds;

    private int[] mapVoxelInfo;
    [SerializeField] private Transform staticObjects;


    int voxelizeKernel;
    int createSmokeKernel;





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


    float smokeStartTime = 0;
    bool isSmokeExpanding = false;
    float smokeRadius = 5.0f;
    Vector3 smokeCenter = Vector3.zero; 

    void Start()
    {
        

        voxelizeKernel = voxelComputeShader.FindKernel("CSVoxelizeMap");
        createSmokeKernel = voxelComputeShader.FindKernel("CSCreateSmokeVoxels");

        InitializeVoxels();
        
        /*ComputeVoxels(Vector3.zero);*/ // Initialize voxels' positions in the compute shader
        VoxelizeMesh();


    }



    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;
                mapVoxelInfo = new int[gridSize * gridSize * gridSize];
                smokeCenter = hitPoint;

                //optimizasyon bukucu bunu silmek lazim
                VoxelizeMesh();
                //CreateSmoke(hitPoint);

                isSmokeExpanding = true;
                smokeStartTime = Time.time; 
            }
        }


        if(isSmokeExpanding)
        {
            var val = EaseFunction(Time.time - smokeStartTime);
            mapVoxelInfo = new int[gridSize * gridSize * gridSize];
            

            //optimizasyon bukucu bunu silmek lazim
            VoxelizeMesh();
            //CreateSmoke(hitPoint);
            voxelComputeShader.SetFloat("smokeRadius", smokeRadius * val);
            CreateSmoke(smokeCenter);

            if (val >= 1) 
            {
                isSmokeExpanding = false;
                smokeStartTime = 0;
            }
        }


        if(Input.GetKeyDown(KeyCode.X))
        {
            int countOnes = mapVoxelInfo.AsParallel().Count(value => value == 1);
            Debug.Log($"NUMBER OF OCCUPIED VOXEL IS {countOnes}");

            int countTwos = mapVoxelInfo.AsParallel().Count(value => value == 2);
            Debug.Log($"NUMBER OF SMOKED VOXEL IS {countTwos}");
        }

        

        //test amacli duruyor duzelt
        VoxelRenderMaterial.SetBuffer("voxels", smokeVoxelBuffer);
        //VoxelRenderMaterial.SetBuffer("voxels", voxelBuffer);

        VoxelRenderMaterial.SetFloat("_VoxelSize", voxelSize);
        Graphics.DrawMeshInstancedIndirect(voxelMesh, 0, VoxelRenderMaterial, bounds, argsBuffer);


        
    }

    private void InitializeVoxels()
    {
        mapVoxels = new Voxel[gridSize * gridSize * gridSize];
        smokeVoxels = new Voxel[smokeWidth * smokeWidth * smokeWidth];
        mapVoxelInfo = new int[gridSize * gridSize * gridSize];

        int colorSize = sizeof(float) * 4;
        int vector3Size = sizeof(float) * 3;
        int totalVoxelDataSize = colorSize + vector3Size;

        voxelBuffer = new ComputeBuffer(mapVoxels.Length, totalVoxelDataSize);
        smokeVoxelBuffer = new ComputeBuffer(smokeVoxels.Length, totalVoxelDataSize);

        mapVoxelInfoBuffer = new ComputeBuffer(mapVoxelInfo.Length, sizeof(int));
        

        // Initialize the argument buffer
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)voxelMesh.GetIndexCount(0);
        args[1] = (uint)mapVoxels.Length;
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

    private void CreateSmoke(Vector3 center)
    {

        smokeVoxels = new Voxel[smokeWidth * smokeWidth * smokeWidth];
        

        mapVoxelInfoBuffer.SetData(mapVoxelInfo);
        smokeVoxelBuffer.SetData(smokeVoxels);

        voxelComputeShader.SetVector("smokeCenter", center);
        voxelComputeShader.SetInt("smokeWidth", smokeWidth);
        voxelComputeShader.SetInt("smokeHeight", smokeWidth);

        voxelComputeShader.SetBuffer(createSmokeKernel, "smokeVoxels", smokeVoxelBuffer);
        voxelComputeShader.SetBuffer(createSmokeKernel, "mapVoxelInfo", mapVoxelInfoBuffer);

        //voxelComputeShader.Dispatch(createSmokeKernel, Mathf.CeilToInt(smokeWidth / 10.0f), Mathf.CeilToInt(smokeWidth / 10.0f), 1);
        voxelComputeShader.Dispatch(createSmokeKernel, 1, 1, 1);

        smokeVoxelBuffer.GetData(smokeVoxels);
        mapVoxelInfoBuffer.GetData(mapVoxelInfo);
    }

    private void VoxelizeMesh()
    {
        
        // Set buffers and parameters for the compute shader
        
        voxelComputeShader.SetVector("centerPosition", new Vector3(0,0, 0));
        voxelComputeShader.SetBuffer(voxelizeKernel, "mapVoxels", voxelBuffer);
        voxelComputeShader.SetBuffer(voxelizeKernel, "mapVoxelInfo", mapVoxelInfoBuffer);

        voxelComputeShader.SetBuffer(voxelizeKernel, "meshVertices", meshVertexBuffer);
        voxelComputeShader.SetBuffer(voxelizeKernel, "meshIndices", meshIndexBuffer);

        voxelComputeShader.SetInt("meshIndiceCount", meshIndexBuffer.count);
        Debug.Log($"{meshIndexBuffer.count} -> meshIndexBuffer.count");

        voxelComputeShader.SetFloat("voxelSize", voxelSize);
        voxelComputeShader.SetInts("gridSize", new int[] { gridSize, gridSize, gridSize });
        voxelComputeShader.Dispatch(voxelizeKernel, Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f));

        mapVoxelInfoBuffer.GetData(mapVoxelInfo);
        voxelBuffer.GetData(mapVoxels);
    }


    private float EaseFunction(float time)
    {
        
        time = Mathf.Clamp01(time);

        // Ease-in-out quadratic function
        if (time < 0.5f)
        {
            // Ease in
            return 2 * time * time;
        }
        else
        {
            // Ease out
            //return -1 + (4 - 2 * time) * time;
            return 2 * time * time;
        }
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
