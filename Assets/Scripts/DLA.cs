#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DLA {
    [ExecuteInEditMode]
    public class DLA : MonoBehaviour
    {
        public int resolution = 513;
        public int walkerCount = 10000;
        public int maxWalkers = 200;
        public Terrain terrain;

        public int radius = 30;
        public float standardDeviation = 20;
        public bool[,] DLAmap;
        float[,] heightMapData;
        List<Walker> walkers = new List<Walker>();

        private SynchronizationContext unityContext;
        private CancellationTokenSource cts;
        private object mapLock = new object();

        [Header("settings")]
        public bool autoExpose = false;
        public bool blur = false;
        public bool weightFalloff = false;
        public bool smoothHeights = false;

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
                            heightMapData[walkerPos.x, walkerPos.y] =  1;
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
                float[,] data = heightMapData;

                if (weightFalloff)
                {
                    int[,] weightMap = Utils.ComputeWeightMap(DLAmap);

                    if (smoothHeights)
                    {
                        data = new float[resolution, resolution];
                        data = Utils.ApplySmoothHeights(weightMap);
                    }
                }
                
                if (blur)
                {
                    data = Utils.GaussianBlur(data, radius, standardDeviation);
                }
                /* data = Utils.AddMultidimensionalFloats(data, Utils.GaussianBlur(heightMapData,Mathf.CeilToInt(radius*0.5f), standardDeviation*0.5f), 0.5f);
                 data = Utils.AddMultidimensionalFloats(data, Utils.GaussianBlur(heightMapData, Mathf.CeilToInt(radius * 0.25f), standardDeviation * 0.25f), 0.3f);
                data = Utils.MultiplyMultidimensionalFloats(data, 0.33333f);*/
                if (autoExpose)
                {
                    data = Utils.AutoExpose(data);
                }
                terrain.terrainData.SetHeights(0, 0, data);
                EditorUtility.SetDirty(terrain.terrainData);
                Debug.Log("done normal tasks");
            };
            #else

            #endif

         

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
   
}