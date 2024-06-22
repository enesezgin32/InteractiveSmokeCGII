
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public class WorleyNoise3DTexture : MonoBehaviour
{

    public static int worlyNoiseFeaturePointCount2D = 32;
    public static int worlyNoiseFeaturePointCount3D = 16;
    public static float featurePointMultiplier = 7f;

    public static int size = 64;

    public static Color[] WorleyNoiseTexture3D()
    {
        
        int feature1 = 8;
        int feature2 = 32;
        int feature3 = 64;

        // Create a 3-dimensional array to store color data
        Color[] colors = new Color[size * size * size];

        // Create feature points
        Vector3[] featurePoints1 = new Vector3[size * feature1];
        Vector3[] featurePoints2 = new Vector3[size * feature2];
        Vector3[] featurePoints3 = new Vector3[size * feature3];

        // Generate feature points
        GenerateFeaturePoints(featurePoints1);
        GenerateFeaturePoints(featurePoints2);
        GenerateFeaturePoints(featurePoints3);

        // Populate the array with Worley noise values using parallel processing
        float inverseResolution = 1.0f / (size - 1.0f);

        Parallel.For(0, size, z =>
        {
            int zOffset = z * size * size;
            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    float noiseValue = WorleyNoise3D(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints3) * 0.3f +
                                       WorleyNoise3D(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints2) * 0.3f +
                                       WorleyNoise3D(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints1) * 0.4f;

                    colors[x + yOffset + zOffset] = new Color(noiseValue, noiseValue, noiseValue, 1.0f);
                }
            }
        });

        return colors;
    }

    private static void GenerateFeaturePoints(Vector3[] featurePoints)
    {
        for (int i = 0; i < featurePoints.Length; i++)
        {
            featurePoints[i] = new Vector3(Random.value, Random.value, Random.value);
        }
    }

    [MenuItem("CreateExamples2/3DNoiseTexture")]
    static void CreateTexture3D()
    {
        // Configure the texture
        
        TextureFormat format = TextureFormat.RGBA32;
        TextureWrapMode wrapMode = TextureWrapMode.Clamp;

        // Create the texture and apply the configuration
        Texture3D texture = new Texture3D(size, size, size, format, false);
        texture.wrapMode = wrapMode;

        // Generate the colors using Worley noise
        Color[] colors = WorleyNoiseTexture3D();

        // Copy the color values to the texture
        texture.SetPixels(colors);

        // Apply the changes to the texture and upload the updated texture to the GPU
        texture.Apply();

        // Save the texture to your Unity Project
        AssetDatabase.CreateAsset(texture, $"Assets/Textures/Example3DTextureNoise_{featurePointMultiplier}_average.asset");
    }

    static float WorleyNoise3D(float x, float y, float z, Vector3[] featurePoints)
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
        return 1 - Mathf.Clamp01(minDist * featurePointMultiplier);
    }



    [MenuItem("CreateExamples2/2DTexture")]
    static void CreateTexture2D()
    {
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


        colors = WorleyNoiseTexture2D();

        // Copy the color values to the texture
        texture.SetPixels(colors);

        // Apply the changes to the texture and upload the updated texture to the GPU
        texture.Apply();

        // Save the texture to your Unity Project
        AssetDatabase.CreateAsset(texture, $"Assets/Textures/Example2DTextureNoise{featurePointMultiplier}_average5.asset");
        //AssetDatabase.CreateAsset(texture, $"Assets/Textures/Example2DTextureNoise{worlyNoiseFeaturePointCount2D}_{featurePointMultiplier}.asset");
    }


    static float WorleyNoise2D(float x, float y, Vector2[] featurePoints)
    {
        // Calculate the minimum distance to the feature points
        float minDist = float.MaxValue;
        List<float> minDistances  = new List<float>();
        minDistances.Add(minDist);
        minDistances.Add(minDist);
        minDistances.Add(minDist);

        foreach (var point in featurePoints)
        {
            float dist = Vector2.Distance(new Vector2(x, y), point);

            for (int i = 0; i < minDistances.Count; i++)
            {
                if (minDistances[i] > dist)
                {
                    minDistances.Insert(i,dist);
                    break;
                }
            }
        }
        minDist = (minDistances[0] + minDistances[1] + minDistances[2]) / 3;
        minDist = minDistances[0];
        // Normalize the distance to [0, 1] range
        return 1 - Mathf.Clamp01(minDist* featurePointMultiplier);
    }


    public static Color[] WorleyNoiseTexture2D()
    {
        int feature1 = 16;
        int feature2 = 64;
        int feature3 = 256;

        // Create a 2-dimensional array to store color data
        Color[] colors = new Color[size * size];
        Vector2[] featurePoints1 = new Vector2[feature1];
        Vector2[] featurePoints2 = new Vector2[feature2];
        Vector2[] featurePoints3 = new Vector2[feature3];

        // Generate feature points
        for (int i = 0; i < feature1; i++)
        {
            featurePoints1[i] = new Vector2(Random.value, Random.value);
        }
        // Generate feature points
        for (int i = 0; i < feature2; i++)
        {
            featurePoints2[i] = new Vector2(Random.value, Random.value);
        }
        // Generate feature points
        for (int i = 0; i < feature3; i++)
        {
            featurePoints3[i] = new Vector2(Random.value, Random.value);
        }

        // Populate the array with Worley noise values
        float inverseResolution = 1.0f / (size - 1.0f);
        for (int y = 0; y < size; y++)
        {
            int yOffset = y * size;
            for (int x = 0; x < size; x++)
            {
                float noiseValue = WorleyNoise2D(x * inverseResolution, y * inverseResolution, featurePoints3) * 0.3f + 
                    WorleyNoise2D(x * inverseResolution, y * inverseResolution, featurePoints2) * 0.3f + 
                    WorleyNoise2D(x * inverseResolution, y * inverseResolution, featurePoints1) * 0.4f;


                colors[x + yOffset] = new Color(noiseValue, noiseValue, noiseValue, 1.0f);
            }
        }
        return colors;
    }


    //public static Color[] WorleyNoiseTexture3D()
    //{
    //    int size = 256;
    //    int feature1 = 4;
    //    int feature2 = 16;
    //    int feature3 = 32;

    //    // Create a 3-dimensional array to store color data
    //    Color[] colors = new Color[size * size * size];

    //    //create feature points
    //    Vector3[] featurePoints1 = new Vector3[size * feature1];
    //    Vector3[] featurePoints2 = new Vector3[size * feature2];
    //    Vector3[] featurePoints3 = new Vector3[size * feature3];

    //    // Generate feature points
    //    for (int i = 0; i < size * feature1; i++)
    //    {
    //        featurePoints1[i] = new Vector3(Random.value, Random.value, Random.value);
    //    }
    //    for (int i = 0; i < size * feature2; i++)
    //    {
    //        featurePoints2[i] = new Vector3(Random.value, Random.value, Random.value);
    //    }
    //    for (int i = 0; i < size * feature3; i++)
    //    {
    //        featurePoints3[i] = new Vector3(Random.value, Random.value, Random.value);
    //    }

    //    // Populate the array with Worley noise values
    //    float inverseResolution = 1.0f / (size - 1.0f);
    //    for (int z = 0; z < size; z++)
    //    {
    //        int zOffset = z * size * size;
    //        for (int y = 0; y < size; y++)
    //        {
    //            int yOffset = y * size;
    //            for (int x = 0; x < size; x++)
    //            {
    //                float noiseValue = WorleyNoise3D(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints3) * 0.3f +
    //                    WorleyNoise3D(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints2) * 0.3f +
    //                    WorleyNoise3D(x * inverseResolution, y * inverseResolution, z * inverseResolution, featurePoints1) * 0.4f;


    //                colors[x + yOffset] = new Color(noiseValue, noiseValue, noiseValue, 1.0f);
    //            }
    //        }
    //    }
    //    return colors;
    //}


}
