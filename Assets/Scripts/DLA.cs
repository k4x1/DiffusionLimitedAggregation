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
        public bool[,] DLAMap;
        public Vector2Int[,] parentMap;
        private Dictionary<Vector2Int, Vector2Int> parentDict;
        float[,] heightMapData;
        List<Walker> walkers = new List<Walker>();

        private SynchronizationContext unityContext;
        private CancellationTokenSource cts;
        private object mapLock = new object();

        [Header("settings")]
        public bool autoExpose = false;
        public bool blur = false;
        public bool weightFalloff = false;



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
            walkers = new List<Walker>();

            Vector2Int root = new Vector2Int(resolution / 2, resolution / 2);

            heightMapData = new float[resolution, resolution];
            
            parentDict = new Dictionary<Vector2Int, Vector2Int>();

            DLAMap = new bool[resolution, resolution];
            DLAMap[root.x, root.y] = true;

        }
        void InstantiateWalker()
        {
            walkers.Add(new Walker(DLAMap));
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
                    if (walker.inPos) continue; 

                    if (walker.StepWalker(out Vector2Int walkerPos, out Vector2Int walkerStuckDir))
                    {
                        lock (mapLock) {
                            parentDict[walkerPos] = walkerPos + walkerStuckDir;
                            DLAMap[walkerPos.x, walkerPos.y] = true;
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
                RandomUtil();
            };
            #else

            #endif

         

        }

        public void RandomUtil()
        {
            float[,] data = heightMapData;

            if (weightFalloff)
            {
                int[,] weightMap = Utils.CalculateWeights(DLAMap,parentDict);

                data = new float[resolution, resolution];
                data = Utils.ApplySmoothHeights(weightMap);
            }

            if (blur)
            {
                data = Utils.GaussianBlur(data, radius, standardDeviation);
            }
            if (autoExpose)
            {
                data = Utils.AutoExpose(data);
            }
            terrain.terrainData.SetHeights(0, 0, data);
            EditorUtility.SetDirty(terrain.terrainData);
            Debug.Log("done normal tasks");
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