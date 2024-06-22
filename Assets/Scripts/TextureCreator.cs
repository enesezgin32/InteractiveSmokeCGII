using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TextureCreator : MonoBehaviour
{
    public ComputeShader worleyNoiseCompute;
    public int size = 128;
    public float featurePointMultiplier = 5;



    [Range(0.0f, 1.0f)]
    public float perlinEffect = 0.2f;


    private Color[] colors;
    private void Start()
    {
        GenerateWorleyNoiseTexture3D();
    }

    void GenerateWorleyNoiseTexture3D()
    {
        int feature1 = 4;
        int feature2 = 8;
        int feature3 = 32;

        // Generate feature points
        Vector3[] featurePoints1 = GenerateFeaturePoints(size * feature1);
        Vector3[] featurePoints2 = GenerateFeaturePoints(size * feature2);
        Vector3[] featurePoints3 = GenerateFeaturePoints(size * feature3);


        colors = new Color[size*size*size];


        // Create a 3D texture
        Texture3D texture = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        // Create compute buffer
        ComputeBuffer buffer1 = new ComputeBuffer(featurePoints1.Length, 12);
        ComputeBuffer buffer2 = new ComputeBuffer(featurePoints2.Length, 12);
        ComputeBuffer buffer3 = new ComputeBuffer(featurePoints3.Length, 12);

        ComputeBuffer colorBuffer = new ComputeBuffer (colors.Length, 16);

        buffer1.SetData(featurePoints1);
        buffer2.SetData(featurePoints2);
        buffer3.SetData(featurePoints3);
        colorBuffer.SetData(colors);

        // Set buffer data to the compute shader
        worleyNoiseCompute.SetBuffer(0, "featurePoints1", buffer1);
        worleyNoiseCompute.SetBuffer(0, "featurePoints2", buffer2);
        worleyNoiseCompute.SetBuffer(0, "featurePoints3", buffer3);


        // Set texture and parameters
        worleyNoiseCompute.SetBuffer(0, "colors", colorBuffer);
        worleyNoiseCompute.SetInt("featurePointsCount1", featurePoints1.Length);
        worleyNoiseCompute.SetInt("featurePointsCount2", featurePoints2.Length);
        worleyNoiseCompute.SetInt("featurePointsCount3", featurePoints3.Length);
        worleyNoiseCompute.SetInt("size", size);
        worleyNoiseCompute.SetFloat("perlinEffect", perlinEffect);

        worleyNoiseCompute.SetFloat("featurePointMultiplier", featurePointMultiplier);



        // Dispatch the compute shader
        int threadGroups = Mathf.CeilToInt(size / 8.0f);
        worleyNoiseCompute.Dispatch(0, threadGroups, threadGroups, threadGroups);


        colorBuffer.GetData(colors);
        // Release buffers
        buffer1.Release();
        buffer2.Release();
        buffer3.Release();
        colorBuffer.Release();
        
        texture.SetPixels(colors);
        // Apply the texture and save it as an asset
        texture.Apply();

        AssetDatabase.CreateAsset(texture, $"Assets/Textures/WorleyNoiseTexture3D_{size}_{featurePointMultiplier}_{perlinEffect}.asset");
    }

    Vector3[] GenerateFeaturePoints(int count)
    {
        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = new Vector3(Random.value, Random.value, Random.value);
        }
        return points;
    }
}
