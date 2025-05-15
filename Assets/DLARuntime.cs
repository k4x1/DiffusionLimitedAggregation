using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
namespace DLA
{
    public class DLARuntime : MonoBehaviour
    {
        public int resolution = 513;
        public int walkerCount = 10000;
        public int maxSticks = 10000;
        public Renderer displayRenderer;
        Texture2D texture;
        float[,] heightMapData;
        bool[,] DLAmap;
        Walker[] walkers;
        int count = 0;
        HashSet<Vector2Int> previousWalkerPositions = new HashSet<Vector2Int>();

        void Start()
        {
            StartCoroutine(GenerateDLA());
        }

        IEnumerator GenerateDLA()
        {

            texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            heightMapData = new float[resolution, resolution];
            DLAmap = new bool[resolution, resolution];
            DLAmap[resolution / 2, resolution / 2] = true;

            walkers = new Walker[walkerCount];
            for (int i = 0; i < walkers.Length; i++)
            {
                walkers[i] = new Walker(DLAmap);
            }

            while (count < maxSticks)
            {
                previousWalkerPositions.Clear();

                for (int j = 0; j < walkers.Length; j++)
                {
                    if (walkers[j].inPos) continue;

                    Vector2Int oldPos = walkers[j].GetPos();
                    previousWalkerPositions.Add(oldPos);

                    if (walkers[j].StepWalker())
                    {
                        var pos = walkers[j].GetPos();
                        DLAmap[pos.x, pos.y] = true;

                        float dist = Vector2Int.Distance(pos, new Vector2Int(resolution / 2, resolution / 2));
                        float strength = 1.0f - Mathf.Pow(dist / (resolution / 2), 2);
                        heightMapData[pos.x, pos.y] = strength;

                        texture.SetPixel(pos.x, pos.y, new Color(strength, strength, strength, 1));
                        count++;
                    }
                }

                foreach (var pos in previousWalkerPositions)
                {
                    if (!DLAmap[pos.x, pos.y])
                    {
                        float h = heightMapData[pos.x, pos.y];
                        texture.SetPixel(pos.x, pos.y, new Color(h, h, h, 1));
                    }
                }

                foreach (var walker in walkers)
                {
                    if (!walker.inPos)
                    {
                        var pos = walker.GetPos();
                        texture.SetPixel(pos.x, pos.y, Color.red);
                    }
                }

                texture.Apply();

                if (displayRenderer != null)
                    displayRenderer.material.mainTexture = texture;

                yield return null;
            }

            texture.Apply();
            SaveTexture(texture);
            Debug.Log("DLA generation complete");
        }

        void SaveTexture(Texture2D tex)
        {
            byte[] bytes = tex.EncodeToPNG();
            string path = Application.dataPath + "/GeneratedTextures/DLA_runtime.png";

            Directory.CreateDirectory(Application.dataPath + "/GeneratedTextures");
            File.WriteAllBytes(path, bytes);
            Debug.Log("Saved: " + path);
        }
    }
}