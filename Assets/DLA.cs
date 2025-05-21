#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;
using System.Linq.Expressions;
using System.Globalization;
using Debug = UnityEngine.Debug;
using System.Xml.Schema;
using NUnit.Framework.Constraints;
using UnityEngine.UIElements;

namespace DLA {
    [ExecuteInEditMode]
    public class DLA : MonoBehaviour
    {
        public int resolution = 513;
        public int walkerCount = 10000;
        public int maxWalkers = 200;
        public Terrain terrain;

        public int  radius=30;
        public float standardDeviation = 20;
        public bool[,] DLAmap;
        float[,] heightMapData;
        List<Walker> walkers = new List<Walker>();
        
        private SynchronizationContext unityContext;
        private CancellationTokenSource cts;
        private object mapLock = new object();
        public void StartCoroutineDLA()
        {
            StopDLA();
            walkers = new List<Walker>();
            heightMapData = new float[resolution, resolution];
            DLAmap = new bool[resolution, resolution];
            DLAmap[resolution / 2, resolution / 2] = true;
            StartCoroutine(initializeDLA());
        }
        public void StartTaskDLA()
        {

            StopDLA();
           
            for (int i = 0; i < walkerCount; i++)
            {
                InstantiateWalker();
            }
            cts = new CancellationTokenSource();

            Stopwatch stopwatch = Stopwatch.StartNew();

            Task.Run(() => {
                RunDLA(cts.Token);
                stopwatch.Stop();
#if UNITY_EDITOR
                EditorApplication.delayCall += () =>
                {
                    Debug.Log($"DLA has taken {stopwatch.Elapsed.TotalSeconds:F3} time to run | resolution {resolution} | maxWalkers {maxWalkers} | walkerCount {walkerCount}");
                };
#else
                Debug.Log($"DLA has taken {stopwatch.Elapsed.TotalSeconds:F3} time to run | resolution {resolution} | maxCalkers {maxWalkers} | walkerCount {walkerCount}");
#endif
            },
            cts.Token);

        }
        public void StopDLA()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
            StopAllCoroutines();
            walkers = new List<Walker>();
            heightMapData = new float[resolution, resolution];
            DLAmap = new bool[resolution, resolution];
       
            DLAmap[resolution / 2, resolution / 2] = true;
        }
        void InstantiateWalker()
        {
            walkers.Add(new Walker(DLAmap));
        }
        private void RunDLA(CancellationToken token)
        {
            int stuckCount = 0;
            int centerX = resolution / 2;
            int centerY = resolution / 2;
            float maxDist = Mathf.Sqrt(centerX * centerX + centerY * centerY);

            while (stuckCount < maxWalkers && !token.IsCancellationRequested)
            {
                foreach(Walker walker in walkers) {
                    if (token.IsCancellationRequested) break;
                    if (walker.inPos) continue; // gotta update this to kill the walker at some point

                    if (walker.StepWalker())
                    {
                        Vector2Int walkerPos = walker.GetPos();
                        float dist = Vector2Int.Distance(walkerPos, new Vector2Int(centerX, centerY));
                        float strength = Mathf.Exp(-2f * (dist / maxDist));
                        lock (mapLock) {
                            DLAmap[walkerPos.x, walkerPos.y] = true;
                            heightMapData[walkerPos.x, walkerPos.y] = strength;
                        }
                        stuckCount++;
                        if(stuckCount >= maxWalkers)
                        {
                            break;
                        }
                    }
                } 
            }
            if (token.IsCancellationRequested)
            {
                Debug.Log("DLA canceled");
                return;
            }

            #if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                float[,] blurredData = Blur.GaussianBlur(heightMapData, radius, standardDeviation);
                blurredData = AddMultidimensionalFloats(blurredData, Blur.GaussianBlur(heightMapData,Mathf.CeilToInt(radius*0.5f), standardDeviation*0.5f), 0.5f);
                blurredData = AddMultidimensionalFloats(blurredData, Blur.GaussianBlur(heightMapData, Mathf.CeilToInt(radius * 0.25f), standardDeviation * 0.25f), 0.3f);
                blurredData = MultiplyMultidimensionalFloats(blurredData, 0.33333f);
                terrain.terrainData.SetHeights(0, 0, blurredData);
                EditorUtility.SetDirty(terrain.terrainData);
                Debug.Log("done normal tasks");
            };
            #else

            #endif

         

        }
        float[,] AddMultidimensionalFloats(float[,] a, float[,] b, float weight = 1) 
        {
            int width = a.GetLength(0);
            int height = a.GetLength(1);
            float[,] combined = new float[width,height];
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    combined[i, j] = a[i,j] + b[i,j]* weight; 
                }
            }
            return combined;
        }
        float[,] MultiplyMultidimensionalFloats(float[,] a, float b)
        {
            int width = a.GetLength(0);
            int height = a.GetLength(1);
            float[,] combined = new float[width, height];
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    combined[i, j] = a[i, j] * b;
                }
            }
            return combined;
        }
        IEnumerator initializeDLA()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            int count = 0;
      
    
            while (count < maxWalkers)
            {
                for (int j = 0; j < walkers.Count; j++)
                {
                    if(walkers[j].inPos)
                    {
                        continue;
                    }
                    if (walkers[j].StepWalker())
                    {
                       
                        count++;
                        int walkerPosX = walkers[j].GetPos().x;
                        int walkerPosY = walkers[j].GetPos().y;
                        DLAmap[walkerPosX, walkerPosY] = true;
                        int centerX = resolution / 2;
                        int centerY = resolution / 2;

                        float dist = Vector2Int.Distance(new Vector2Int(walkerPosX, walkerPosY), new Vector2Int(centerX, centerY));
                        float maxDist = Mathf.Sqrt(centerX * centerX + centerY * centerY);
                        float strength = Mathf.Exp(-5.0f * (dist / maxDist));

                
                        heightMapData[walkerPosX, walkerPosY] = strength * 0.2f;
                    }
                }
                yield return null;
            }
            stopwatch.Stop();
            Debug.Log($"DLA courutine has taken {stopwatch.Elapsed.TotalSeconds:F3} time to run | resolution {resolution} | maxCalkers {maxWalkers} | walkerCount {walkerCount}");
            terrain.terrainData.SetHeights(0, 0, heightMapData);

            Debug.Log("done");

        }
        private void OnDrawGizmos()
        {
            if (walkers.Count == 0) return;
            foreach (Walker walker in walkers) {
                if (walker == null) continue;
                Gizmos.color = walker.inPos ? new Color(0,1,0,0.5f) : new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(new Vector3(walker.GetPos().x,30, walker.GetPos().y) , Vector3.one);
            }
        }
    }
    static class Blur { 
        public static float[,] GaussianBlur(float[,] toBlur, int radius, float standardDeviation)
        {
            int width = toBlur.GetLength(0);
            int height = toBlur.GetLength(1);
            float[] kernel = CalculateKernel(radius, standardDeviation);

            float[,] temp = new float[width,height];
            //horizontal pass
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;

                    for (int i = -radius; i <= radius; i++)
                    {
                        int sampleX = Mathf.Clamp(x + i, 0, width - 1);
                        sum += toBlur[sampleX, y] * kernel[i + radius];
                    }
                    temp[x, y] = sum;   
                }
            }
            //vertical pass
            float[,] blurredResult = new float[width,height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;

                    for (int i = -radius; i <= radius; i++)
                    {
                        int sampleY = Mathf.Clamp(y + i, 0, height - 1);
                        sum += temp[x, sampleY] * kernel[i + radius];
                    }
                    blurredResult[x, y] = sum;
                    if (blurredResult[x, y] == 0f) continue;
                  //  Debug.Log($"blurring ({toBlur[x, y]}) into ({blurredResult[x,y]}) ({sum})");
                }
            }
            return blurredResult;

        }
        public static float[] CalculateKernel(int radius, float standardDeviation)
        {
            int kernelSize = 2 * radius + 1;
            float[] kernel = new float[kernelSize];
            float twoDeviationSquared = 2 * standardDeviation * standardDeviation;
            float inverseDeviationRoot = 1 / Mathf.Sqrt(twoDeviationSquared * Mathf.PI);
            float total = 0f;

            for (int i = -radius; i <= radius; i++)
            {
                float dist = i * i;
                int idx = i + radius;
                kernel[idx] = inverseDeviationRoot * Mathf.Exp(-dist / twoDeviationSquared);
                total += kernel[idx];
            }
            for (int i = 0; i < kernelSize; i++)
            {
                kernel[i] /= total;
               // Debug.Log($"kernel at {i} = {kernel[i]}");
            }
            return kernel;
        }

    }
}