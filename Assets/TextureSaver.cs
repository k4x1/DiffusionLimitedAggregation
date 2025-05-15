using UnityEngine;
using UnityEditor;
using System.IO;
namespace DLA { 
public class TextureSaver
{

    [MenuItem("Tools/Generate DLA")]

    public static void GenerateDLA()
    {
        Debug.Log("generating");
        int resolution = 513;
        bool[,] DLAmap;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        DLAmap = new bool[resolution, resolution];
        float[,] heightMapData = new float[513, 513];
        DLAmap[resolution / 2, resolution / 2] = true;
        int count = 0;
        Walker[] walkers = new Walker[100000];
        for (int i = 0; i < walkers.Length; i++)
        {
            walkers[i] = new Walker( DLAmap);
        }
        while(count<100000)
        {
            for (int j = 0; j < walkers.Length; j++)
            {
                if (walkers[j].inPos)
                {
                    continue;
                }
                if (walkers[j].StepWalker())
                {
                    Debug.Log("hit");
                    count++;
                    DLAmap[walkers[j].GetPos().x, walkers[j].GetPos().y] = true;
                    heightMapData[walkers[j].GetPos().x, walkers[j].GetPos().y] = 1;
                }
            }

        }


        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Color newCol = new Color(heightMapData[x, y], heightMapData[x, y], heightMapData[x, y], 1);
                texture.SetPixel(x, y, newCol);
            }
        }

        texture.Apply();

        byte[] bytes = texture.EncodeToPNG();
        string path = "Assets/GeneratedTextures/DLA.png";

        Directory.CreateDirectory("Assets/GeneratedTextures"); 
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

    }
}
}