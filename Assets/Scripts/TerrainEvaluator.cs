using UnityEngine;
public enum Geomorphon
{
    flat, peak, ridge, shoulder, pit, slope, spur, valley, footslope, hollow, none
}

public class TerrainEvaluator
{
    /*
    so first we must define the 10 geomorphons
    
    flat    peak     ridge    shoulder    pit    
    0 0 0   0 0 0    0 1 0    0 0 0       1 1 1
    0 0 0   0 1 0    0 1 0    1 1 1       1 0 1
    0 0 0   0 0 0    0 1 0    1 1 1       1 1 1

    slope   spur     valley   footslope   hollow   
    0 0 0   0 0 0    1 0 1    0 0 0       0 0 0
    0 0 0   0 1 0    1 0 1    0 0 0       1 0 1
    1 1 1   1 1 1    1 0 1    1 1 1       1 1 1


    look at multidimensional floats and determine the geomorphons in it, then get it as a percentage, can also render it as a gizmo

    determine what pixel size and real height will be making sure it scales properly with the studies numbers (1pixel = 800m^2) 
    
    then use this function 
    PTRM = (-38.02+3.55 * pit + 1.75 * peak + 25.12 * flat + 9.61 * valley + 7.59 * ridge + 6.71 * hollow + 9.02 * spur + 7.31 * shoulder + 28.95 * slope + 7.63 * footslope)/69.96
    and get its PTRM rating
    then compare it with other heightmaps (gather online and maybe implement perlin myself)

    source https://dl.acm.org/doi/fullHtml/10.1145/3514244#sec-8

    this is actually way harder than I thought because I have to find this lookup file to generare the geomorphons
    https://grass.osgeo.org/grass-stable/manuals/r.geomorphon.html
    I spent like 3 hours trying to find this file, went through all of the source code history and downloaded the entire program to try to figure out where to get that lookup table for now 
    I will just do the second method

    then to make sure there are more than 1 source for accuracy as the above example doesnt actually consider
    gather elevation histograms and compare with my result using the chi squared test



    /*

    // E N W S 
    Vector2Int[] directions = { 
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1), 
        new(-1, 0), new(-1, -1), new(0, -1), new(1, -1), 
    };
    Geomorphon[,] GeomorphonMap;
    float[] percentages;
    float[,] heightMap;
    float[] GetGeomorphonPercentages(float[,] input)
    {
        heightMap = input;


        return new float[8];
    }

    Geomorphon GetGeomorphonType(Vector2Int center, int radius = 2, float flatThreshold = 0.01f)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        GeomorphonMap = new Geomorphon[width, height];
        float inputCenter = heightMap[center.x, center.y];

        int[] touple = new int[8];

        for (int i = 0; i < 8; i++)
        {
            Vector2Int dir = directions[i];


        }



        return Geomorphon.none;
    }

/*    Geomorphon CalculateFromTouple(int[] touple)
    {

    }*/

    
}
