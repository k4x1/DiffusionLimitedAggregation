#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System;
using System.Security.Cryptography;

namespace DLA {

    public enum TerrainMode
    {
        Basic,
        MultiResolution,
        NoiseGuided,
        ConvexHull,
        PerlinNoise
    }


    [ExecuteInEditMode]

    public class DLA : MonoBehaviour
    {

        

        public Terrain terrain;
        [Header("DLA settings")]
        public int resolution = 513;
        public bool diagonalWalk = false;
        public float heightMultiplier = 100f;
        //basic
        [Header("Basic dla Settings")]
        [HideInInspector] public int walkerCount = 50000;
        [HideInInspector] public int maxWalkers = 50000;
        //multires
        [Header("Multires dla Settings")]
        [HideInInspector] public int baseSize = 64;
        [HideInInspector] public float fillFraction = 0.5f;
         public int jitterRange = 2;

        [Header("Perlin noise settings")]
        [HideInInspector] public int perlinOctaves = 4;
        [HideInInspector] public float perlinBaseScale = 0.005f;
        [HideInInspector] public float perlinPersistence = 0.5f;
        [HideInInspector] public float perlinLacunarity = 2.0f;
        [HideInInspector] public int perlinSeed = 42;

        [Header("Post proccessing options")]
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

        [Header("DLA Mode")]
        public TerrainMode mode = TerrainMode.Basic;

        public void StartTaskDLA()
        {

            StopDLA();
            cts = new CancellationTokenSource();

            Stopwatch stopwatch = Stopwatch.StartNew();

            Task.Run(() => {
                switch (mode)
                {
                    case TerrainMode.Basic:
                     
                        RunDLA(cts.Token);
                        break;

                    case TerrainMode.MultiResolution:
                        RunMultiResolutionDLA(cts.Token);
                        break;

                    case TerrainMode.NoiseGuided:
                        //RunNoiseGuidedDLA(cts.Token);
                        break;

                    case TerrainMode.ConvexHull:
                        //RunConvexHullDLA(cts.Token);
                        break;
                    case TerrainMode.PerlinNoise:
                        RunPerlinNoise(cts.Token);
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
            Utils.jitterOffsets.Clear();


        }

        private void RunDLA(CancellationToken token)
        {
            walkers = new List<Walker>();

            Vector2Int root = new Vector2Int(resolution / 2, resolution / 2);

            parentMap = new Vector2Int[resolution,resolution];
            heightMapData = new float[resolution, resolution];

            parentDict = new Dictionary<Vector2Int, Vector2Int>();

            DLAMap = new bool[resolution, resolution];
            DLAMap[root.x, root.y] = true;

            int stuckCount = 0;
            int centerX = resolution / 2;
            int centerY = resolution / 2;
            float maxDist = Mathf.Sqrt(centerX * centerX + centerY * centerY);
            for (int i = 0; i < walkerCount; i++)
            {
                walkers.Add(new Walker(DLAMap));
            }
            while (stuckCount < maxWalkers && !token.IsCancellationRequested)
            {
                foreach(Walker walker in walkers) {
                    if (token.IsCancellationRequested) break;
                    if (walker.inPos) continue;

                    if (walker.StepWalker(out Vector2Int walkerPos, out Vector2Int walkerStuckDir, diagonalWalk))
                    {
                        lock (mapLock) {
                            parentMap[walkerPos.x,walkerPos.y] = walkerStuckDir;
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
        private void RunMultiResolutionDLA(CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int[] levels = Utils.ComputeLevels(baseSize, resolution);
            int currentSize = levels[0];

            walkers = new List<Walker>();
            Vector2Int root = new Vector2Int(baseSize / 2, baseSize / 2);
            heightMapData = new float[baseSize, baseSize];
            parentMap = new Vector2Int[baseSize, baseSize];
            heightMapData[root.x, root.y] = 1f;
            for (int x = 0; x < baseSize; x++)
            {
                for (int y = 0; y < baseSize; y++)
                {
                    parentMap[x, y] = Utils.SENTINEL;
                }
            }
            parentMap[baseSize / 2, baseSize / 2] = new Vector2Int(0, 0);

            parentDict = new Dictionary<Vector2Int, Vector2Int>();
            DLAMap = new bool[baseSize, baseSize];
            DLAMap[root.x, root.y] = true;

            for (int i = 0; i < levels.Length - 1; i++)
            {
                if (token.IsCancellationRequested) return;

                int size = levels[i];
                int nextSize = levels[i + 1];

                int walkersToAdd = Mathf.FloorToInt(fillFraction * size * size);
                RunDLALevel(size, walkersToAdd, token);

                Vector2Int[,] upscaledDir = Utils.UpscaleDirectionMap(parentMap, nextSize, jitterRange);
                bool[,] crispUpscale = Utils.BuildMapFromDirections(upscaledDir);

                int[,] weightMap = Utils.CalculateWeights(upscaledDir);
                float[,] crispHeight = Utils.ApplySmoothHeights(weightMap, smoothPower);

                float[,] skeletonUps = Utils.UpscaleBilinear(heightMapData, nextSize);

                float[,] blurredSkeletonUps = Utils.GaussianBlur(skeletonUps, /*radius=*/6, /*stdDev=*/2);

                float[,] crispHeightNext = Utils.UpscaleBilinear(crispHeight, nextSize);

                float[,] merged = new float[nextSize, nextSize];
                for (int x = 0; x < nextSize; x++)
                {
                    for (int y = 0; y < nextSize; y++)
                    {
                        float rawDelta = crispHeightNext[x, y] - skeletonUps[x, y];
                        float falloff = Mathf.SmoothStep(0, 1, skeletonUps[x, y]);
                        merged[x, y] = blurredSkeletonUps[x, y] + rawDelta * falloff;
                    }
                }

                currentSize = nextSize;
                DLAMap = crispUpscale;
                heightMapData = crispHeightNext;
                parentMap = upscaledDir;

            }

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                SaveDLAData();
                PostProccessing();
                stopwatch.Stop();
                Debug.Log($"Multires DLA has taken {stopwatch.Elapsed.TotalSeconds:F3} time to run | resolution {resolution} | baseSize {baseSize} | fillFraction {fillFraction}");
            };
#else
            stopwatch.Stop();
            Debug.Log($"[MultiRes DLA] Total Time: {stopwatch.Elapsed.TotalSeconds:F3}s | final res {resolution}");
#endif
        }
        private void RunDLALevel(int size, int walkersToAdd, CancellationToken token)
        {
            int stuckCount = 0;
            walkers = new List<Walker>();

            for (int i = 0; i < walkersToAdd; i++)
            {
                walkers.Add(new Walker(DLAMap)); 
            }

            while (stuckCount < walkersToAdd && !token.IsCancellationRequested)
            {
                foreach (Walker walker in walkers)
                {
                    if (walker.inPos) continue;
                    if (walker.StepWalker(out Vector2Int walkerPos, out Vector2Int walkerStuckDir,diagonalWalk))
                    {
                        lock (mapLock)
                        {
                            parentMap[walkerPos.x, walkerPos.y] = walkerStuckDir;
                            DLAMap[walkerPos.x, walkerPos.y] = true;
                            heightMapData[walkerPos.x, walkerPos.y] = 1f;
                        }
                        stuckCount++;
                        if (stuckCount >= walkersToAdd)
                        { 
                            break; 
                        }
                    }
                }
            }
        }
        private void RunPerlinNoise(CancellationToken token)
        {
            int res = resolution;
            heightMapData = new float[res, res];

            System.Random perlinRnd = new System.Random(perlinSeed);
            Vector2[] octaveOffsets = new Vector2[perlinOctaves];
            for (int i = 0; i < perlinOctaves; i++)
            {
                float offsetX = perlinRnd.Next(-100000, 100000);
                float offsetY = perlinRnd.Next(-100000, 100000);
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }

            float maxPossibleHeight = 0;
            float amplitude = 1;
            for (int i = 0; i < perlinOctaves; i++)
            {
                maxPossibleHeight += amplitude;
                amplitude *= perlinPersistence;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int y = 0; y < res; y++)
            {
                if (token.IsCancellationRequested) return;
                for (int x = 0; x < res; x++)
                {
                    float noiseHeight = 0f;
                    float frequency = perlinBaseScale;
                    amplitude = 1f;

                    for (int i = 0; i < perlinOctaves; i++)
                    {
                        float sampleX = (x + octaveOffsets[i].x) * frequency;
                        float sampleY = (y + octaveOffsets[i].y) * frequency;

                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= perlinPersistence;
                        frequency *= perlinLacunarity;
                    }

                    float normalizedHeight = (noiseHeight / maxPossibleHeight + 1f) / 2f;
                    heightMapData[y, x] = normalizedHeight;
                }
            }

            stopwatch.Stop();

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                TerrainData tData = terrain.terrainData;
                tData.heightmapResolution = res;
                tData.size = new Vector3(res, heightMultiplier, res);

                tData.SetHeights(0, 0, heightMapData);

                EditorUtility.SetDirty(tData);

                Debug.Log($"Perlin noise has taken {stopwatch.Elapsed.TotalSeconds:F3}s | resolution {res}");
            };
#else
            TerrainData tData = terrain.terrainData;
            tData.heightmapResolution = res;
            tData.size = new Vector3(res, perlinHeightMultiplier, res);
            tData.SetHeights(0, 0, heightMapData);
            Debug.Log($"[PerlinNoise] Generation time: {stopwatch.Elapsed.TotalSeconds:F3}s | resolution {res}");
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
                int[,] weightMap = Utils.CalculateWeights(parentMap);

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



            TerrainData tData = terrain.terrainData;
            tData.heightmapResolution = resolution;
            tData.size = new Vector3(resolution, heightMultiplier, resolution);
            tData.SetHeights(0, 0, data);
            EditorUtility.SetDirty(terrain.terrainData);
            Debug.Log("done normal tasks");
        }

        public void SaveDLAData()
        {
            try
            {
                using (BinaryWriter bw = new BinaryWriter(File.Open(dataPath, FileMode.Create)))
                {
                    int res = DLAMap.GetLength(0);
                    bw.Write(res);
                    for (int x = 0; x < res; x++)
                    {
                        for (int y = 0; y < res; y++)
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
                    for (int x = 0; x < res; x++)
                    {
                        for (int y = 0; y < res; y++)
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
            if (DLAMap != null)
            {
                int size = DLAMap.GetLength(0);

                Gizmos.color = new Color(1, 0, 0, 0.5f); ;
                float cubeSize = 1f;
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        if (DLAMap[x, y])
                        {
                            Vector3 worldPos = new Vector3(x, 100f, y);
                            Gizmos.DrawCube(worldPos, Vector3.one * cubeSize);
                        }
                    }
                }
                if(parentMap!= null)
                {
                    Gizmos.color = Color.cyan;
                    for(int x = 0; x < size; x++) 
                    {
                        for (int y = 0; y < size; y++)
                        {
                            if (parentMap[x, y] == Utils.SENTINEL) continue;
                            Vector3 worldPos = new Vector3(x, 101f, y);
                            Vector3 endPos = new Vector3(x + parentMap[x, y].x, 101f, y + parentMap[x, y].y);
                            Gizmos.DrawLine(worldPos, endPos);
                        }
                    }
                }
                if (walkers.Count == 0) return;
                foreach (Walker walker in walkers)
                {
                    if (walker == null|| walker.inPos) continue;
                    Gizmos.color =new Color(0, 1, 0, 0.5f);
                    Gizmos.DrawCube(new Vector3(walker.GetPos().x, 100, walker.GetPos().y), Vector3.one);
                }
            }
         
        }
    }
   
}