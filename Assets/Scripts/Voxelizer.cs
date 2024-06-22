using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Voxelizer : MonoBehaviour
{


    //SHADERS and MESH
    public ComputeShader voxelComputeShader;
    public Shader voxelRenderShader;

    public Material voxelMaterial;
    public Mesh voxelMesh;


    //MAP CONFIGURATION
    public int gridSize = 10;
    public float voxelSize = 1f;


    //RENDER ARGS
    private ComputeBuffer argsBuffer;
    private Bounds bounds;


    //MAP BUFFERS AND VARIABLES
    private ComputeBuffer voxelBuffer;
    private Voxel[] mapVoxels;

    private ComputeBuffer meshVertexBuffer;
    private ComputeBuffer meshIndexBuffer;

    private ComputeBuffer mapVoxelInfoBuffer;
    private int[] mapVoxelInfo;

    [SerializeField] private Transform staticObjects;
    Vector3 smokeCenter = Vector3.zero;

    //SMOKE BUFFER AND VARIABLES
    private int smokeArraySize = 20; // -> SMOKE CAPACITY 20*20*20 -> 8000 tane smoke cube yapabiliyoz max

    private ComputeBuffer smokeVoxelBuffer;
    public Voxel[] smokeVoxels;

    private ComputeBuffer tempMapVoxelInfoBuffer;
    private int[] tempMapVoxelInfo;

    private ComputeBuffer queueFillBuffer;
    private Vector4[] queueFill;

    float smokeStartTime = 0;
    bool isSmokeExpanding = false;
    public float smokeRadius = 5.0f;


    //KERNELS
    int voxelizeKernel;
    int createSmokeKernel;




    // SOME GENERAL STUFFS BRUH
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
        
        //KERNELLARIN IDLERI CEKIYOR
        voxelizeKernel = voxelComputeShader.FindKernel("CSVoxelizeMap");
        createSmokeKernel = voxelComputeShader.FindKernel("CSCreateSmokeVoxels");

        //INITIALIZE EVERYTHING 
        InitializeVoxels();

        //MESH INFOSUNU SHADERA GONDERIYOR ELLEME BURAYI DORUDUR BURASI
        StaticMeshInitialization();

        //INITIALIZE MAP
        VoxelizeMesh();


    }


    void Update()
    {
        //SMOKE SPAMLAMAK ICIN SOL TIK
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;
                smokeCenter = hitPoint;

                isSmokeExpanding = true; // ANIMASYON CALISIYOR MU CHECK ETMEK ICIN
                smokeStartTime = Time.time;  // O ANKI ZAMANI TUTUP EASE FUNCTIONDA KULLANMAK ICIN
            }
        }

        //ANIMASYON AKTIFSE
        if(isSmokeExpanding)
        {
            var val = EaseFunction(Time.time - smokeStartTime);

            //RADIUSU EASE FUNCTIONDAN GELEN DEGERE GORE SETLE VE SMOKELARI HESAPLA
            voxelComputeShader.SetFloat("smokeRadius", smokeRadius * val);
            CreateSmoke(smokeCenter);

            //BITINCE KAPAT
            if (val >= 1) 
            {
                isSmokeExpanding = false;
                smokeStartTime = 0;
            }
        }

        VoxelRenderMaterial.SetBuffer("voxels", smokeVoxelBuffer); // smokelari renderlemak icin
        //VoxelRenderMaterial.SetBuffer("voxels", voxelBuffer); -> mapi gormek icin yorumdan cikarabilirsin enesim maviler bos kirmizilar static mesh demek

        VoxelRenderMaterial.SetFloat("_VoxelSize", voxelSize); // voxel size renderlarken size ayarlasin diye
        //Graphics.DrawMeshInstancedIndirect(voxelMesh, 0, VoxelRenderMaterial, bounds, argsBuffer); // render fonsiyonu
    }

    private void InitializeVoxels()
    {

        // SIZE HESAPLAMASI
        int colorSize = sizeof(float) * 4;
        int vector3Size = sizeof(float) * 3;
        int totalVoxelDataSize = colorSize + vector3Size;

        // mapi gormek amacli olmasa renderda bunu secersen mapi renderliyor basta
        mapVoxels = new Voxel[gridSize * gridSize * gridSize]; 
        voxelBuffer = new ComputeBuffer(mapVoxels.Length, totalVoxelDataSize);

        //bizim smokelarin renderi icin voxel array tutuyor 
        smokeVoxels = new Voxel[smokeArraySize * smokeArraySize * smokeArraySize];
        smokeVoxelBuffer = new ComputeBuffer(smokeVoxels.Length, totalVoxelDataSize);

        //haritadaki meshlerin yerini tutuyor
        mapVoxelInfo = new int[gridSize * gridSize * gridSize];
        mapVoxelInfoBuffer = new ComputeBuffer(mapVoxelInfo.Length, sizeof(int));

        //compute shaderda smoke genislerken eski smoke olan yerleri isaretlemek icin temp map arrayi
        tempMapVoxelInfo = new int[gridSize * gridSize * gridSize];
        tempMapVoxelInfoBuffer = new ComputeBuffer(tempMapVoxelInfo.Length, sizeof(int));

        //breadth first search icin recursion yerine queue ile yaptim shaderda array size yememek icin kullanacagi queueyu ben gonderiyom
        queueFill = new Vector4[5000]; // queue icin yeterli gibi allaha emanet
        queueFillBuffer = new ComputeBuffer(queueFill.Length, sizeof(int) * 4);



        // Initialize the argument buffer salla burayi bosver
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)voxelMesh.GetIndexCount(0);

        args[1] = (uint)mapVoxels.Length; // CIZILECEK INSTANCE SAYISI 

        args[2] = (uint)voxelMesh.GetIndexStart(0); 
        args[3] = (uint)voxelMesh.GetBaseVertex(0);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Set bounds for drawing BU SINIRLARDA CIZIYOR
        bounds = new Bounds(Vector3.zero, new Vector3(gridSize, gridSize, gridSize) * voxelSize);


    }

    private void CreateSmoke(Vector3 center)
    {
        //MEMORY LEAK OLABILIR
        smokeVoxels = new Voxel[smokeArraySize * smokeArraySize * smokeArraySize]; // renderlanacak smoke icin arrayi bosaltiyoz
        tempMapVoxelInfo = new int[gridSize * gridSize * gridSize]; // temp mapi de ayni sekil nolur nolmaz

        
        //bufferlara degerleri atiyoz
        queueFillBuffer.SetData(queueFill);
        tempMapVoxelInfoBuffer.SetData(tempMapVoxelInfo);
        smokeVoxelBuffer.SetData(smokeVoxels);
        //smoke merkezini setliyoz
        voxelComputeShader.SetVector("smokeCenter", center);

        //bufferlari gonderiyoz shadera
        voxelComputeShader.SetBuffer(createSmokeKernel, "smokeVoxels", smokeVoxelBuffer);
        voxelComputeShader.SetBuffer(createSmokeKernel, "mapVoxelInfo", mapVoxelInfoBuffer);
        voxelComputeShader.SetBuffer(createSmokeKernel, "tempMapVoxelInfo", tempMapVoxelInfoBuffer);
        voxelComputeShader.SetBuffer(createSmokeKernel, "queueFill", queueFillBuffer);

        //fonksiyonu calistiriyor multi thread yok yersen gpuda yapiyoz momento :D burayi duzeltek
        voxelComputeShader.Dispatch(createSmokeKernel, 1, 1, 1);
        //smokelari bufferdan okuyoz sonra renderlamak icin
        smokeVoxelBuffer.GetData(smokeVoxels);
    }

    private void VoxelizeMesh()
    {
        // Set buffers and parameters for the compute shader
        
        
        voxelComputeShader.SetVector("centerPosition", new Vector3(0,0, 0)); // mapin merkezi
        voxelComputeShader.SetBuffer(voxelizeKernel, "mapVoxels", voxelBuffer); // mapi renderlamak icin voxel arrayi 
        voxelComputeShader.SetBuffer(voxelizeKernel, "mapVoxelInfo", mapVoxelInfoBuffer); // mapteki static meshlerin infosu

        //mapteki meshlerin infosunu gonderiyoz
        voxelComputeShader.SetBuffer(voxelizeKernel, "meshVertices", meshVertexBuffer); 
        voxelComputeShader.SetBuffer(voxelizeKernel, "meshIndices", meshIndexBuffer);

        voxelComputeShader.SetInt("meshIndiceCount", meshIndexBuffer.count);
        Debug.Log($"{meshIndexBuffer.count} -> meshIndexBuffer.count");

        //ivir zivir
        voxelComputeShader.SetFloat("voxelSize", voxelSize);
        voxelComputeShader.SetInts("gridSize", new int[] { gridSize, gridSize, gridSize });

        //fonksiyonu cagiriyoz multi thread bu sefer tek olsa allana kavusur valla 40x40x40 lik grid suan 
        voxelComputeShader.Dispatch(voxelizeKernel, Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f), Mathf.CeilToInt(gridSize / 10.0f));

        //shaderda info aliyoz
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
        if (voxelBuffer != null) voxelBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (smokeVoxelBuffer != null) smokeVoxelBuffer.Release();
        if (tempMapVoxelInfoBuffer != null) tempMapVoxelInfoBuffer.Release();
        if (mapVoxelInfoBuffer != null) mapVoxelInfoBuffer.Release();
        if (queueFillBuffer != null) queueFillBuffer.Release();
        if (meshVertexBuffer != null) meshVertexBuffer.Release();
        if (meshIndexBuffer != null) meshIndexBuffer.Release();
    }



    private void StaticMeshInitialization()
    {
        //STATIC MAP INITIALIZATION ASAGISINI SALLA BURASI DORU CALISIYOR
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
}
