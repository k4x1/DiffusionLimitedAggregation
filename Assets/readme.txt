readme heightmap

Unity editor version 6000.0.35f1

SampleScene Objects

TerrainGenerator

> Holds DLA generating script and the terrain
> Its children includes the terrain it generates
> Creates up to 2 children at runtime
>> HeightMap is the generated map as a texture and
>> NoiseField is the noise field that perlin noise guided walkers use

TerrainEvaluator
>  Holds the script that evaluates the terrain

Main Script - DLA 
DLAEditor buttons
> Run DLA - start generation
> Stop DLA - cancel the run
> Do post processing - apply blur/exposure and update the terrain

DLA script variables

> resolution - Map size
> diagonalWalk - Allow diagonal steps
> heightMultiplier - Scales the drawn terrain height only
> killWalkers - Respawn walkers after maxSteps step or on attach, doesn’t work too well
> maxSteps - Steps before a walker is reset
> noiseGuided - Walkers sample from noiseField (scaled by noiseFieldScale) using perlin noise
> convexHull - Keeps cluster within a convex hull, turns on walker respawning on attach, doesn’t work to well
> hullUpscale - Scale of the convex hull

> Base DLA
>> Spawns a bunch of walkers on start, on end just apply that map to the current map, recommend to turn on all post processing settings on
>> walkerCount - Start walkers, leave low if kill walkers is on
>> maxWalkers - How many walkers in cluster before finishing DLA

> Multi Resolution DLA
>> Starts with small map, keeps crisp map and blurred map, mixes data together when upscaling, I wrote more about how it works on the script
>> baseSize - starting size, make sure the resolution is divisible by this
>> fillFraction - how much percent the map has to be filled before upscaling
>> crispBlurRadius, crispBlurStandardDeviation - Blur settings for crisp map
>> blurryBlurRadius, blurryBlurStandardDeviation - Blur settings for blurry map
>> lerpAlpha - Weight of crisp data when blending

> Perlin Noise // Simplex Noise
>> Generates terrain using stacking perlin noise / open simplex noise
>> octaves - Sow many layers of noise
>> baseScale - Size of first noise layer
>> persistence - How fast each further layer shrinks
>> lacunarity - How much each layers detail increases
>> seed - RNG seed 

> Post Processing settings
>> autoExpose -Normalizes all the values from 0-1, good to use before comparing 
>> blur - Applies gaussian blur with radius and standard deviation
>> radius - Gaussian blur radius
>> standardDeviation - Gaussian blur standard deviation
>> weightFalloff - Works only for base dla, makes the heights go from tall in center to short)
>> smoothPower - Smoothing strength of the weight falloff

> Visual Settings
>> createHeightTexture - Creates height texture quad
>> drawWalkers - Draws walkers
>> drawConnections - Draws the tree as connections walker -> point it attached to
>> drawConvexHull - Draws convex hull


HistogramEvaluator script variables

> heightMapA, heightMapB - Compare heightmapA vs heightmapB manually 
> realHeightMapList - Height map list to compare average
> result - Last comparison result
> heightmapJson - Your last generated heightmap automatically gets saved here for easy comparison
> to compare heightmaps single as a texture you have to set read/write to true, compression to none and filter mode to point (no filter)
> Buttons
>> Compare chi average - debugs all the chi results and the average vs the current loaded map json
>>  Compare coefficient average - debugs all the coefficient results and the average vs the current loaded map json
>> Load json heightmap - reloads the json map, if comparison doesn’t work try this
>> Compare chi/coefficient single - debugs the result of heightmapA vs heightmapB

Make sure to have gizmos on

Imported the open simplex noise script from https://gist.github.com/digitalshadow/134a3a02b67cecd72181

Convex hull does speed regular dla considerably but its not very good for creating realistic maps

Terrain evaluator script doesn't do anything. I wasn't able to get it working as it requires a look up table that would be way outside of scope to calculate myself and I couldn't find one anywhere, sources removed it.



