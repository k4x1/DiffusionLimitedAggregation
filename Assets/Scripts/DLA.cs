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
using System;


namespace DLA {

    public enum TerrainMode
    {
        BasicDLA,
        MultiResolutionDLA,
        PerlinNoise,
        SimplexNoise
    }

    [Serializable]
    public class ParentCell
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class DLADataJson
    {
        public int size;
        public bool[] DLAMap;
        public ParentCell[] parentMap;
        public float[] heightMapData;
    }

    [ExecuteInEditMode]

    public class DLA : MonoBehaviour
    {

        

        public Terrain terrain;
        [Header("DLA settings")]
        public int resolution = 512;
        public bool diagonalWalk = false;
        public float heightMultiplier = 100f; // scales only the drawn map

        public bool killWalkers = false;
        public int maxSteps = 200;

        public bool noiseGuided = false;
        public float[,] noiseField;
        public float noiseFieldScale = 1;


        public bool convexHull = false;
        public float maxHullEdgeGap = 12;


        //basic
        [Header("Basic dla Settings")]
        [HideInInspector] public int walkerCount = 10000; // walkers spawned at start
        [HideInInspector] public int maxWalkers = 50000; // how many walkers can spawn before it stops

        [Header("Multiresolution DLA Settings")]
        [HideInInspector] public int baseSize = 64; //starting grid size, this will scale up
        [HideInInspector] public float fillFraction = 0.1f; //how much percentwise the grid needs to be filled before upscaling
        [HideInInspector] public int crispBlurRadius = 3; // gaussian blur radius for upscaling crisp map
        [HideInInspector] public int crispBlurStandardDeviation = 1; // gaussian blur standard deviation for upscaling crisp map
        [HideInInspector] public int blurryBlurRadius = 3; // gaussian blur radius for upscaling blurry map
        [HideInInspector] public int blurryBlurStandardDeviation = 1; // gaussian blur standard deviation for upscaling blurry map
        [HideInInspector] public float lerpAlpha = 0.3f; // when adding the crisp to blurry this determines how much weight the crisp has

        [Header("Perlin noise settings")]
        [HideInInspector] public int perlinOctaves = 4;
        [HideInInspector] public float perlinBaseScale = 0.005f;
        [HideInInspector] public float perlinPersistence = 0.5f;
        [HideInInspector] public float perlinLacunarity = 2.0f;
        [HideInInspector] public int perlinSeed = 6767;

        [Header("Simplex noise settings")]
        [HideInInspector] public int simplexOctaves = 4;
        [HideInInspector] public float simplexBaseScale = 0.005f;
        [HideInInspector] public float simplexPersistence = 0.5f;
        [HideInInspector] public float simplexLacunarity = 2.0f;
        [HideInInspector] public int simplexSeed = 6767;


        [Header("Post processing options")]
        public bool autoExpose = false;
        public bool blur = false;
        public bool weightFalloff = false;

        [Header("Blur settings")]
        public int radius = 30;
        public float standardDeviation = 20;

        [Header("Smoothing settings")]
        public float smoothPower = 0.5f;  // the lower this is the higher the lower bits are



        [Header("Visual debugging")]
        public bool createHeightTexture = true;
        public bool drawWalkers = true;
        public bool drawConnections = true;
        public bool drawConvexHull = true;

        // height stuff
        [HideInInspector] Texture2D heightTex;
        [HideInInspector] GameObject heightQuad;
        
        //noise field stuff
        [HideInInspector] Texture2D noiseFieldTex;
        [HideInInspector] GameObject noiseFieldQuad;

        // important stuff
        [HideInInspector] public bool[,] DLAMap;
        [HideInInspector] public Vector2Int[,] parentMap;
        [HideInInspector] float[,] heightMapData;
        [HideInInspector] List<Walker> walkers = new List<Walker>();
        [HideInInspector] SynchronizationContext unityContext;
        [HideInInspector] CancellationTokenSource cts; 
        [HideInInspector] List<Vector2Int> clusterPoints = new List<Vector2Int>();
        [HideInInspector] List<Vector2Int> hullPoints = new List<Vector2Int>();

        [HideInInspector] string dataPath
        {
            get
            {
                string folder = Path.Combine(Application.dataPath, "dla data");
                return Path.Combine(folder, "dlaData.json");
            }
        }
        [HideInInspector] object mapLock = new object();

        [Header("DLA Mode")]
        public TerrainMode mode = TerrainMode.BasicDLA;

        public void StartTaskDLA()
        {

            StopDLA();
            cts = new CancellationTokenSource();

            Stopwatch stopwatch = Stopwatch.StartNew();
            Debug.Log($"running {mode.ToString()}");

            if (noiseGuided)
            {
                noiseField = Utils.InitializeNoiseField(resolution, noiseFieldScale);
                CreateHeightMapQuad(noiseField, -resolution * 0.5f, resolution * 0.5f, ref noiseFieldQuad, ref noiseFieldTex);
            }

            Task.Run(() => {

                DLAMap = new bool[resolution, resolution];
                parentMap = new Vector2Int[resolution, resolution];
                for (int i = 0; i < resolution; i++)
                {
                    for (int j = 0; j < resolution; j++)
                    {
                        parentMap[i, j] = Utils.SENTINEL;
                    }
                }

                switch (mode)
                {
                    case TerrainMode.BasicDLA:
                        RunDLA(cts.Token);
                        break;

                    case TerrainMode.MultiResolutionDLA:
                        RunMultiResolutionDLA(cts.Token);
                        break;
                    case TerrainMode.PerlinNoise:
                        RunPerlinNoise(cts.Token);
                        break;
                    case TerrainMode.SimplexNoise:
                        RunSimplexNoise(cts.Token);
                        break;
                }
                stopwatch.Stop();
#if UNITY_EDITOR
                EditorApplication.delayCall += () =>
                {
                    Debug.Log($"{mode.ToString()} has taken {stopwatch.Elapsed.TotalSeconds:F3} time to run | resolution({resolution}), diagonalWalk({diagonalWalk}), noiseGuided({noiseGuided})");
                };
#else
                Debug.Log($"{mode.ToString()} has taken {stopwatch.Elapsed.TotalSeconds:F3} time to run. resolution({resolution}), diagonalWalk({diagonalWalk}), noiseGuided({noiseGuided})");
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

            heightMapData = new float[resolution, resolution];
            DLAMap[root.x, root.y] = true;

            clusterPoints = new List<Vector2Int>();
            hullPoints = new List<Vector2Int>();
            clusterPoints.Add(root);
            hullPoints.Add(root);


            int stuckCount = 0;
            int centerX = resolution / 2;
            int centerY = resolution / 2;
            float maxDist = Mathf.Sqrt(centerX * centerX + centerY * centerY);
            for (int i = 0; i < walkerCount; i++)
            {
                walkers.Add(InstantiateWalker());
            }
            while (stuckCount < maxWalkers && !token.IsCancellationRequested)
            {
                for (int i = 0; i < walkers.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    Walker walker = walkers[i];
                    if (walker.inPos) continue;

                    if (killWalkers &&  walker.stepCount >= maxSteps)
                    {
                        walkers[i] = InstantiateWalker();
                        // kill walkers when max steps reached
                        continue;
                    }

                    if (Step(walker, out Vector2Int walkerPos, out Vector2Int walkerStuckDir))
                    {
                        lock (mapLock)
                        {
                            if(killWalkers || convexHull) { 
                                walkers[i] = InstantiateWalker();
                            }
                            parentMap[walkerPos.x, walkerPos.y] = walkerStuckDir;
                            DLAMap[walkerPos.x, walkerPos.y] = true;
                            heightMapData[walkerPos.x, walkerPos.y] = 1;
                            if (convexHull) AddClusterPoint(walkerPos);
                        }
                        stuckCount++;
                        if (stuckCount >= maxWalkers) break;
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
                PostProcessing();
                Debug.Log($"Settings: resolution({resolution}), maxWalkers(maxWalkers), walkerCount(walkerCount)");
            };
            #else

            #endif

        }

        private void AddClusterPoint(Vector2Int walkerPos)
        {
            clusterPoints.Add(walkerPos);
            if (convexHull && clusterPoints.Count > 2)
            {
                List<Vector2Int> hull = Utils.ConvexHull(clusterPoints);
                hullPoints = Utils.RefineHull(hull, clusterPoints, maxHullEdgeGap);
                //hullPoints = Utils.ScalePolygon(hullPoints, 1f);
            }
        }
        Walker InstantiateWalker()
        {
            List<Vector2Int> useHull = (convexHull && hullPoints.Count > 1) ? hullPoints : null;

            return new Walker(DLAMap, useHull);
        }
        private void RunMultiResolutionDLA(CancellationToken token)
        {

            /*
            get level sizes
            initiate values
            heightMapData is the blurred map
            parent map holds all of the connections
            depth map helps find the leaves and calculate weight map
            DLAMap is the bool map that the walkers and drawing uses
            run dla untill threshold

            while running dla calculate parent, DLAMap and heightmapdata
            do a blur and upscale using NN and blur it a bit using gaussian blurs
            store this blurred map in old blurred map 
            upscale the crisp map 
            
            --
            run dla on this upscaled crisp map untill threshhold 
            when this threshold is reached 
            calculate the weight map by finding the leaf nodes and stepping up them using BFS
            normalize that data to be from 0-1
            blur this data depending on how deep we are 

            finally add this data to the old blurred map we stored
            then blur and upscalled the old map
            and upscale the crisp map
            --
            repeat this section untill done 

            */
            List<int> levelSizes = Utils.ComputeLevels(baseSize, resolution);
            if (levelSizes.Count == 0 || levelSizes[levelSizes.Count - 1] != resolution)
            {
                Debug.LogError($" cannot reach resolution {resolution} from baseSize {baseSize} by doubling {levelSizes.Count}");
                return;
            }

            Vector2Int root = new Vector2Int(baseSize / 2, baseSize / 2);

            heightMapData = new float[baseSize, baseSize];  // blurry map holder
            parentMap = new Vector2Int[baseSize, baseSize]; // holds directions to parent
            DLAMap = new bool[baseSize, baseSize];          // crisp map holder
            int[,] depthMap = new int[baseSize, baseSize];  // depth map holder
            for (int x = 0; x < baseSize; x++)
            {
                for (int y = 0; y < baseSize; y++)
                {
                    depthMap[x, y] = -1;
                    parentMap[x, y] = Utils.SENTINEL;
                    DLAMap[x, y] = false;
                }
            }
            depthMap[root.x, root.y] = 0;
            parentMap[root.x, root.x] = new Vector2Int(0, 0);
            DLAMap[root.x, root.y] = true; 

            for (int i = 0; i < levelSizes.Count-1; i++)
            {
             
                int res = levelSizes[i];
                if (token.IsCancellationRequested)
                {
                    Debug.Log("MultiResolution DLA canceled");
                    return;
                }

              
                root = new Vector2Int(res / 2, res / 2);

                // run dla proccess
                int thresholdCount = (int)Math.Floor((res * res) * fillFraction);
                RunDLALevel(res, thresholdCount, depthMap, token);
                if (i == 0)
                {
                    //first step only 
                    for (int x = 0; x < res; x++)
                    {
                        for (int y = 0; y < res; y++)
                        {
                            heightMapData[x,y] = DLAMap[x, y] ? 1f : 0; // man cant cast bools to numbers in c#
                            //first pass only calcualate base blurry map 
                        } 
                    }
                    heightMapData = Utils.UpscaleAndBlur(heightMapData, blurryBlurRadius, blurryBlurStandardDeviation);
                }
                // upscaling crisp step now 
                Vector2Int[,] newParent;
                int[,] newDepth;
                bool[,] crispMap;
                Utils.UpscaleCrisp(parentMap, depthMap, DLAMap, out newParent, out newDepth, out crispMap);
                parentMap = newParent;
                depthMap = newDepth;
                DLAMap = crispMap;

                if (i != 0)
                {
                    // if its not first pass calculate weight map and add it to the heightmap
                    // calculate weightmap 
                    int[,] rawWeightMap = Utils.CalculateWeights(parentMap);
                    float[,] crispWeightMap = Utils.ApplySmoothHeights(rawWeightMap, smoothPower, true);
                    int blurAmount = crispBlurRadius * i; // scaled to depth
                    int sigmaAmount = crispBlurStandardDeviation * i; // scaled to depth
                    float[,] blurredWeightMap = Utils.GaussianBlur(crispWeightMap, blurAmount, sigmaAmount); // recomended standard deviation is ~ 1/3 of radius


                    //initial concept for jiggling wasnt that good, this will just end up being unjiggled unfortunally  
                    heightMapData = Utils.UpscaleAndBlur(heightMapData, blurryBlurRadius, blurryBlurStandardDeviation);
                    heightMapData = Utils.LerpMaps(heightMapData, blurredWeightMap, lerpAlpha);
                }

            }


#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                SaveDLAData();
                PostProcessing();
                Debug.Log($"Settings: baseSize({baseSize}), fillFraction({fillFraction}), " +
                    $"crispBlurRadius({crispBlurRadius}), crispBlurStandardDeviation({crispBlurStandardDeviation})," +
                    $"blurryBlurRadius({blurryBlurRadius}),(blurryBlurStandardDeviation({blurryBlurStandardDeviation})," +
                    $"lerpAlpha({lerpAlpha})");
            };
#else
           
#endif
        }
        void RunDLALevel(int size, int walkersToAdd, int[,] depthMap, CancellationToken token)
        {
            int stuckCount = 0;
            walkers = new List<Walker>();

            clusterPoints = new List<Vector2Int>();
            int mapSize = DLAMap.GetLength(0);

            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (DLAMap[x, y]) clusterPoints.Add(new Vector2Int(x, y));
                }
            }

            hullPoints = Utils.ConvexHull(clusterPoints);

            for (int i = 0; i < walkersToAdd; i++)
            {
                walkers.Add(InstantiateWalker());
            }

            while (stuckCount < walkersToAdd && !token.IsCancellationRequested)
            {
                for (int i = 0; i < walkers.Count; i++)
                {
                    if (token.IsCancellationRequested) break;
                    Walker walker = walkers[i];
                    if (walker.inPos) continue;

                    if (killWalkers && walker.stepCount >= walker.maxSteps)
                    {
                        walkers[i] = new Walker(DLAMap, convexHull ? hullPoints : null);
                        continue;
                    }
                    if (Step(walker,out Vector2Int walkerPos, out Vector2Int walkerStuckDir))
                    {
                        lock (mapLock)
                        {
                            if (killWalkers || convexHull)
                            {
                                walkers[i] = InstantiateWalker();
                            }
                            depthMap[walkerPos.x, walkerPos.y] = depthMap[walkerPos.x + walkerStuckDir.x, walkerPos.y + walkerStuckDir.y] + 1;
                            parentMap[walkerPos.x, walkerPos.y] = walkerStuckDir; // direction to parent
                            DLAMap[walkerPos.x, walkerPos.y] = true;  // current map, we reinitialize at each step
                            if (convexHull) AddClusterPoint(walkerPos);
                        }
                        stuckCount++;
                        if (stuckCount >= walkersToAdd) break;
                    }
                }
            }
        }
        private void RunPerlinNoise(CancellationToken token)
        {
            int size = resolution;
            heightMapData = new float[size, size];

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

            for (int y = 0; y < size; y++)
            {
                if (token.IsCancellationRequested) return;
                for (int x = 0; x < size; x++)
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
                SaveDLAData();
                PostProcessing();
                Debug.Log($"Settings: perlinOctaves({perlinOctaves}), perlinBaseScale({perlinBaseScale})," +
                    $" perlinPersistence({perlinPersistence}), perlinLacunarity({perlinLacunarity}), perlinSeed({perlinSeed}),");
            };
#else

#endif
        }
        private void RunSimplexNoise(CancellationToken token)
        {
            int size = resolution;
            heightMapData = new float[size, size];
            NoiseTest.OpenSimplexNoise openSimplexNoise = new NoiseTest.OpenSimplexNoise(simplexSeed);

            System.Random simplexRnd = new System.Random(simplexSeed);
            Vector2[] octaveOffsets = new Vector2[simplexOctaves];
            for (int i = 0; i < simplexOctaves; i++)
            {
                float offsetX = simplexRnd.Next(-100000, 100000);
                float offsetY = simplexRnd.Next(-100000, 100000);
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }

            float maxPossibleHeight = 0;
            float amplitude = 1;
            for (int i = 0; i < simplexOctaves; i++)
            {
                maxPossibleHeight += amplitude;
                amplitude *= simplexPersistence;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            float heightMax = float.MaxValue;
            float heightMin = float.MinValue;
            for (int y = 0; y < size; y++)
            {
                if (token.IsCancellationRequested) return;
                for (int x = 0; x < size; x++)
                {
                    float noiseHeight = 0f;
                    float frequency = simplexBaseScale;
                    amplitude = 1f;

                    for (int i = 0; i < simplexOctaves; i++)
                    {
                        float sampleX = (x + octaveOffsets[i].x) * frequency;
                        float sampleY = (y + octaveOffsets[i].y) * frequency;

                        float simplexValue = (float)openSimplexNoise.Evaluate(sampleX, sampleY) * 2f - 1f;
                        noiseHeight += simplexValue * amplitude;

                        amplitude *= simplexPersistence;
                        frequency *= simplexLacunarity;
                    }

                    //float normalizedHeight = (noiseHeight / maxPossibleHeight + 1f) / 2f;
                    heightMapData[y, x] = noiseHeight;
                    heightMin = Mathf.Min(heightMax, noiseHeight);
                    heightMax = Mathf.Max(heightMax, noiseHeight);
                }
            }
            // normalize to 0-1 
            float invRange = 1f / (heightMax - heightMin);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) { 
                    heightMapData[y, x] = (heightMapData[y, x] - heightMin) * invRange;
                }
            }

            stopwatch.Stop();

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                SaveDLAData();
                PostProcessing();
                Debug.Log($"Settings: simplexOctaves({simplexOctaves}), simplexBaseScale({simplexBaseScale})," +
                    $" simplexPersistence({simplexPersistence}), simplexLacunarity({simplexLacunarity}), simplexSeed({simplexSeed})");
            };
#else

#endif
        }
        public void PostProcessing()
        {
            if (heightMapData == null || heightMapData.Length == 0)
            {
                if (!LoadDLAData())
                {
                    Debug.LogError("no saved data");
                    return;
                }
            }

            float[,] data = heightMapData;

            if (weightFalloff )
            {
                if (parentMap == null || parentMap.Length == 0)
                {
                    Debug.Log("no parentMap set");
                }
                else { 
                    int[,] weightMap = Utils.CalculateWeights(parentMap);
                    heightMapData = new float[resolution, resolution];
                    heightMapData = Utils.ApplySmoothHeights(weightMap,smoothPower);
                }
            }

            if (blur)
            {
                heightMapData = Utils.GaussianBlur(heightMapData, radius, standardDeviation);
            }
            if (autoExpose)
            {
                heightMapData = Utils.AutoExpose(heightMapData);
            }

            if (createHeightTexture)
            {
                CreateHeightMapQuad(heightMapData,resolution*1.5f,resolution*0.5f, ref heightQuad, ref heightTex);
            }

            TerrainData tData = terrain.terrainData;
            tData.heightmapResolution = resolution;
            tData.size = new Vector3(resolution, heightMultiplier, resolution);
            tData.SetHeights(0, 0, heightMapData);
            EditorUtility.SetDirty(terrain.terrainData);
        }
        private bool Step(Walker walker, out Vector2Int stuckPos, out Vector2Int dirToConnection)
        {
            if (noiseGuided)
            {
                return walker.StepWalkerNoiseGuided(
                    out stuckPos,
                    out dirToConnection,
                    noiseField,
                    diagonalWalk
                );
            }
            else
            {
                return walker.StepWalker(
                    out stuckPos,
                    out dirToConnection,
                    diagonalWalk
                );
            }
        }
      
        public void SaveDLAData()
        {
            try
            {
                int size = DLAMap.GetLength(0);

                string folder = Path.GetDirectoryName(dataPath);
                if (!Directory.Exists(folder))
                { 
                    Directory.CreateDirectory(folder); 
                }

                bool[] flatBool = new bool[size * size];
                ParentCell[] flatParent = new ParentCell[size * size];
                float[] flatFloat = new float[size * size];

                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        int i = x * size + y;
                        flatBool[i] = DLAMap[x, y];
                        flatParent[i] = new ParentCell
                        {
                            x = parentMap[x, y].x,
                            y = parentMap[x, y].y
                        };
                        flatFloat[i] = heightMapData[x, y];
                    }
                }

                DLADataJson container = new DLADataJson
                {
                    size = size,
                    DLAMap = flatBool,
                    parentMap = flatParent,
                    heightMapData = flatFloat
                };

                string json = JsonUtility.ToJson(container, true);
                File.WriteAllText(dataPath, json);

                Debug.Log($"Saved DLA data to {dataPath}");
            }
            catch (Exception e)
            {
                Debug.LogError("error saving DLA data: " + e);
            }
        }

        public bool LoadDLAData()
        {
            if (!File.Exists(dataPath))
            {
                Debug.LogError($"no file found at {dataPath}");
                return false;
            }
            try
            {
                string json = File.ReadAllText(dataPath);
                DLADataJson container = JsonUtility.FromJson<DLADataJson>(json);

                int size = container.size;
                DLAMap = new bool[size, size];
                parentMap = new Vector2Int[size, size];
                heightMapData = new float[size, size];

                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        int i = x * size + y;
                        DLAMap[x, y] = container.DLAMap[i];
                        parentMap[x, y] = new Vector2Int(
                            container.parentMap[i].x,
                            container.parentMap[i].y
                        );
                        heightMapData[x, y] = container.heightMapData[i];
                    }
                }

                Debug.Log($"Loaded DLA data from {dataPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("error loading DLA data: " + e);
                return false;
            }
        }
        void CreateHeightMapQuad(float[,] data, float posX, float posZ, ref GameObject quadPrefab, ref Texture2D quadTexture)
        {
            if (data == null || data.Length == 0) return;

            int size = data.GetLength(0);

            if (quadTexture == null || quadTexture.width != size)
            {
                quadTexture = new Texture2D(size, size, TextureFormat.RGB24, false);
                quadTexture.wrapMode = TextureWrapMode.Clamp;
                quadTexture.filterMode = FilterMode.Point;
            }

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float height = Mathf.Clamp01(data[x, y]);
                    quadTexture.SetPixel(x, y, new Color(height, height, height, 1f));
                }
            }
            quadTexture.Apply();

            if (quadPrefab == null)
            {
                quadPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadPrefab.name = "MapQuad";

                MeshRenderer mr = quadPrefab.GetComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find("Unlit/Texture"));
            }

            quadPrefab.transform.position = new Vector3(posX, 0f, posZ);
            quadPrefab.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float worldSize = size;
            quadPrefab.transform.localScale = new Vector3(worldSize, worldSize, 1f);

            MeshRenderer renderer = quadPrefab.GetComponent<MeshRenderer>();
            renderer.sharedMaterial.mainTexture = quadTexture;
        }
        private void OnDrawGizmos()
        {
            if (DLAMap != null)
            {
                int size = DLAMap.GetLength(0);
                if (drawWalkers)
                {
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
                }
                if (parentMap != null && drawConnections)
                {
                    Gizmos.color = Color.cyan;
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = 0; y < size; y++)
                        {
                            if (parentMap[x, y] == Utils.SENTINEL) continue;
                            Vector3 startPoint = new Vector3(x, 101f, y);
                            Vector3 endPoint = new Vector3(x + parentMap[x, y].x, 101f, y + parentMap[x, y].y);
                            Gizmos.DrawLine(startPoint, endPoint);
                        }
                    }
                }
                if (drawWalkers && walkers != null && walkers.Count > 0 )
                {
                     Walker[] snapshot = walkers.ToArray();

                    foreach (Walker walker in snapshot)
                    {
                        if (walker == null || walker.inPos) continue;

                        Gizmos.color = new Color(0, 1, 0, 0.5f);
                        Vector3 worldPos = new Vector3(walker.GetPos().x, 100, walker.GetPos().y);
                        Gizmos.DrawCube(worldPos, Vector3.one);
                    }
                }
                if (drawConvexHull && hullPoints != null && hullPoints.Count>0 )
                {
                    Vector2Int[] snapshot = hullPoints.ToArray();

                    for(int i  = 0; i < snapshot.Length; i++) 
                    {
                        Gizmos.color = Color.yellow;
                        Vector3 startPoint = new Vector3(snapshot[i].x, 102f, snapshot[i].y);
                        int next = i != snapshot.Length-1 ? i + 1 : 0;
                        Vector3 endPoint = new Vector3(snapshot[next].x, 102f, snapshot[next].y);
                        Gizmos.DrawLine(startPoint, endPoint);
                    }
                }
                /* if (heightMapData != null && drawHeightMapData)
                 {
                     for (int x = 0; x < size; x++)
                     {
                         for (int y = 0; y < size; y++)  
                         {
                             Gizmos.color = Color.Lerp(Color.black, Color.white, heightMapData[x, y]);
                             Gizmos.DrawCube(new Vector3(x,200,y), Vector3.one);
                             // please forgive me for my laggy crimes    
                         }

                     }
                 }*/
            }
         
        }
    }
   
}