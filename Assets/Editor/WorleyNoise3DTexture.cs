using UnityEditor;
using UnityEngine;

public class WorleyNoise3DTexture : MonoBehaviour
{

    public static int worlyNoiseFeaturePointCount2D = 256;
    public static int worlyNoiseFeaturePointCount3D = 64;
    public static float featurePointMultiplier = 3f;

    [MenuItem("CreateExamples2/3DNoiseTexture")]
    static void CreateTexture3D()
    {
        // Configure the texture
        int size = 128;
        TextureFormat format = TextureFormat.RGBA32;
        TextureWrapMode wrapMode = TextureWrapMode.Clamp;

        // Create the texture and apply the configuration
        Texture3D texture = new Texture3D(size, size, size, format, false);
        texture.wrapMode = wrapMode;

        // Create a 3-dimensional array to store color data
        Color[] colors = new Color[size * size * size];

        //create feature points
        Vector3[] featurePoints = new Vector3[size * worlyNoiseFeaturePointCount3D];

        // Generate feature points
        for (int i = 0; i < size * worlyNoiseFeaturePointCount3D; i++)
        {
            featurePoints[i] = new Vector3(Random.value, Random.value, Random.value);
        }

        // Populate the array with Worley noise values
        float inverseResolution = 1.0f / (size - 1.0f);
        for (int z = 0; z < size; z++)
        {
            int zOffset = z * size * size;
            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    float noiseValue = WorleyNoise(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints);
                    colors[x + yOffset + zOffset] = new Color(noiseValue, noiseValue, noiseValue, 1.0f);
                }
            }
        }

        // Copy the color values to the texture
        texture.SetPixels(colors);

        // Apply the changes to the texture and upload the updated texture to the GPU
        texture.Apply();

        // Save the texture to your Unity Project
        AssetDatabase.CreateAsset(texture, $"Assets/Example3DTextureNoise{worlyNoiseFeaturePointCount3D}_{featurePointMultiplier}.asset");
    }

    static float WorleyNoise(float x, float y, float z, Vector3[] featurePoints)
    {

        // Calculate the minimum distance to the feature points
        float minDist = float.MaxValue;
        foreach (var point in featurePoints)
        {
            float dist = Vector3.Distance(new Vector3(x, y, z), point);
            if (dist < minDist)
            {
                minDist = dist;
            }
        }

        // Normalize the distance to [0, 1] range
        return Mathf.Clamp01(minDist * featurePointMultiplier);
    }



    [MenuItem("CreateExamples2/2DTexture")]
    static void CreateTexture2D()
    {
        // Configure the texture
        int size = 256;
        TextureFormat format = TextureFormat.RGBA32;
        TextureWrapMode wrapMode = TextureWrapMode.Clamp;

        // Create the texture and apply the configuration
        Texture2D texture = new Texture2D(size, size, format, false);
        texture.wrapMode = wrapMode;

        // Create a 2-dimensional array to store color data
        Color[] colors = new Color[size * size];
        Vector2[] featurePoints = new Vector2[worlyNoiseFeaturePointCount2D];

        // Generate feature points
        for (int i = 0; i < worlyNoiseFeaturePointCount2D; i++)
        {
            featurePoints[i] = new Vector2(Random.value, Random.value);
        }
        // Populate the array with Worley noise values
        float inverseResolution = 1.0f / (size - 1.0f);
        for (int y = 0; y < size; y++)
        {
            int yOffset = y * size;
            for (int x = 0; x < size; x++)
            {
                float noiseValue = WorleyNoise2D(x * inverseResolution, y * inverseResolution, featurePoints);
                colors[x + yOffset] = new Color(noiseValue, noiseValue, noiseValue, 1.0f);
            }
        }

        // Copy the color values to the texture
        texture.SetPixels(colors);

        // Apply the changes to the texture and upload the updated texture to the GPU
        texture.Apply();

        // Save the texture to your Unity Project
        AssetDatabase.CreateAsset(texture, $"Assets/Example2DTextureNoise{worlyNoiseFeaturePointCount2D}_{featurePointMultiplier}.asset");
    }


    static float WorleyNoise2D(float x, float y, Vector2[] featurePoints)
    {
        // Calculate the minimum distance to the feature points
        float minDist = float.MaxValue;
        foreach (var point in featurePoints)
        {
            float dist = Vector2.Distance(new Vector2(x, y), point);
            if (dist < minDist)
            {
                minDist = dist;
            }
        }

        // Normalize the distance to [0, 1] range
        return Mathf.Clamp01(minDist* featurePointMultiplier);
    }
}
