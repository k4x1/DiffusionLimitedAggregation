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
using System.IO;
using System;

namespace DLA {

    public enum DLAMode
    {
        Basic,
        MultiResolution,
        NoiseGuided,
        ConvexHull
    }


    [ExecuteInEditMode]

    public class DLA : MonoBehaviour
    {

        [Header("DLA Mode")]
        public DLAMode mode = DLAMode.Basic;

        public Terrain terrain;
        [Header("DLA settings")]
        public int resolution = 513;
        public int walkerCount = 10000;
        public int maxWalkers = 200;

        [Header("post proccessing options")]
        public bool autoExpose = false;
        public bool blur = false;
        public bool weightFalloff = false;

        [Header("Blur settings")]
        public int radius = 30;
        public float standardDeviation = 20;

        [Header("Smoothing settings")]
        public float smoothPower = 0.5f;  // the lower this is the higher the lower bits are

        
        [HideInInspector] public bool[,] DLAMap;
        [HideInInspector] public Vector2Int[,] parentMap;
        [HideInInspector] Dictionary<Vector2Int, Vector2Int> parentDict;
        [HideInInspector] float[,] heightMapData;
        [HideInInspector] List<Walker> walkers = new List<Walker>();

        [HideInInspector] SynchronizationContext unityContext;
        [HideInInspector] CancellationTokenSource cts;
        [HideInInspector] object mapLock = new object();
        [HideInInspector] string dataPath { get { return Path.Combine(Application.persistentDataPath, "dlaData.bin"); } }





        public void StartTaskDLA()
        {

            StopDLA();
           

            Stopwatch stopwatch = Stopwatch.StartNew();

            Task.Run(() => {
                switch (mode)
                {
                    case DLAMode.Basic:
                        for (int i = 0; i < walkerCount; i++)
                        {
                            InstantiateWalker();
                        }
                        cts = new CancellationTokenSource();
                        RunDLA(cts.Token);
                        break;

                    case DLAMode.MultiResolution:
                        cts = new CancellationTokenSource();
                        //RunMultiResolutionDLA(cts.Token);
                        break;

                    case DLAMode.NoiseGuided:
                        cts = new CancellationTokenSource();
                        //RunNoiseGuidedDLA(cts.Token);
                        break;

                    case DLAMode.ConvexHull:
                        cts = new CancellationTokenSource();
                        //RunConvexHullDLA(cts.Token);
                        break;
                }
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
                SaveDLAData();

                PostProccessing();
            };
            #else

            #endif

        }

        public void PostProccessing()
        {
            if (heightMapData == null || heightMapData.Length == 0)
            {
                if (!LoadDLAData())
                {
                    Debug.LogError("no saved data");
                }
            }

            float[,] data = heightMapData;

            if (weightFalloff)
            {
                int[,] weightMap = Utils.CalculateWeights(DLAMap,parentDict);

                data = new float[resolution, resolution];
                data = Utils.ApplySmoothHeights(weightMap,smoothPower);
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

        public void SaveDLAData()
        {
            try
            {
                using (BinaryWriter bw = new BinaryWriter(File.Open(dataPath, FileMode.Create)))
                {
                    bw.Write(resolution);
                    for (int x = 0; x < resolution; x++)
                    {
                        for (int y = 0; y < resolution; y++)
                        {
                            bw.Write(DLAMap[x, y]);
                        }
                    }
                    bw.Write(parentDict.Count);
                    foreach (var kv in parentDict)
                    {
                        bw.Write(kv.Key.x); bw.Write(kv.Key.y);
                        bw.Write(kv.Value.x); bw.Write(kv.Value.y);
                    }
                    for (int x = 0; x < resolution; x++)
                    {
                        for (int y = 0; y < resolution; y++)
                        {
                            bw.Write(heightMapData[x, y]);
                        }
                    }
                }
                Debug.Log("saved DLA data to " + dataPath);
            }
            catch (Exception e)
            {
                Debug.LogError("error saving DLA data: " + e);
            }
        }

        public bool LoadDLAData()
        {
            if (!File.Exists(dataPath)) return false;

            try
            {
                using (BinaryReader br = new BinaryReader(File.Open(dataPath, FileMode.Open)))
                {
                    int fileRes = br.ReadInt32();
                    if (fileRes != resolution)
                    { 
                        Debug.LogWarning($"saved res {fileRes} != real res {resolution}"); 
                    }

                    DLAMap = new bool[resolution, resolution];
                    parentDict = new Dictionary<Vector2Int, Vector2Int>();
                    heightMapData = new float[resolution, resolution];

                    for (int x = 0; x < resolution; x++)
                    {
                        for (int y = 0; y < resolution; y++)
                        {
                            DLAMap[x, y] = br.ReadBoolean();
                        }
                    }
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        int kx = br.ReadInt32();
                        int ky = br.ReadInt32();

                        int vx = br.ReadInt32();
                        int vy = br.ReadInt32();

                        parentDict[new Vector2Int(kx, ky)] = new Vector2Int(vx, vy);
                    }
                    // read heightMapData
                    for (int x = 0; x < resolution; x++)
                    {
                        for (int y = 0; y < resolution; y++)
                        {
                            heightMapData[x, y] = br.ReadSingle();
                        }
                    }
                }
                Debug.Log("loaded DLA data from " + dataPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("error loading DLA data: " + e);
                return false;
            }
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