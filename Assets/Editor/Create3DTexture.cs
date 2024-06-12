using UnityEngine;
using UnityEditor;

public class WorleyNoise3DGenerator : EditorWindow
{
    private int textureSize = 16;
    private float scale = 1.0f;

    [MenuItem("Tools/Worley Noise 3D Generator")]
    public static void ShowWindow()
    {
        GetWindow<WorleyNoise3DGenerator>("Worley Noise 3D Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Worley Noise 3D Texture Generator", EditorStyles.boldLabel);
        textureSize = EditorGUILayout.IntField("Texture Size", textureSize);
        scale = EditorGUILayout.FloatField("Scale", scale);

        if (GUILayout.Button("Generate and Save Texture3D"))
        {
            Texture3D texture = GenerateWorleyNoise3D(textureSize, scale);
            SaveTexture3DAsset(texture);
        }
    }

    private Texture3D GenerateWorleyNoise3D(int size, float scale)
    {
        Texture3D texture = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size * size];

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    float nx = x / (float)size * scale;
                    float ny = y / (float)size * scale;
                    float nz = z / (float)size * scale;

                    float distance = WorleyNoise(nx, ny, nz);
                    Color color = new Color(distance, distance, distance, 1.0f);
                    colors[x + size * (y + size * z)] = color;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    private float WorleyNoise(float x, float y, float z)
    {
        int K = 2; // Number of cells to consider
        float minDist = float.MaxValue;

        for (int i = -K; i <= K; i++)
        {
            for (int j = -K; j <= K; j++)
            {
                for (int k = -K; k <= K; k++)
                {
                    Vector3 cell = new Vector3(Mathf.Floor(x) + i, Mathf.Floor(y) + j, Mathf.Floor(z) + k);
                    Vector3 cellOffset = new Vector3(Random(cell.x), Random(cell.y), Random(cell.z));

                    Vector3 point = cell + cellOffset;
                    float dist = Vector3.Distance(new Vector3(x, y, z), point);
                    if (dist < minDist)
                    {
                        minDist = dist;
                    }
                }
            }
        }
        return minDist;
    }

    private float Random(float x)
    {
        return Mathf.Sin(x * 127.1f + 311.7f) * 43758.5453f % 1.0f;
    }

    private void SaveTexture3DAsset(Texture3D texture)
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Texture3D", "NewWorleyNoiseTexture", "asset", "Save Texture3D");
        if (path.Length > 0)
        {
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.SaveAssets();
        }
    }
}
