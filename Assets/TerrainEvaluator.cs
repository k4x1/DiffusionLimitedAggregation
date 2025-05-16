using UnityEngine;
public enum Geomorphon
{
    flat, peak, ridge, shoulder, pit, slope, spur, valley, footslope, hollow
}

public class TerrainEvaluator : MonoBehaviour
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

    (actually 4x4 just drawing it like that to make it easier to see, top down view)

    look at multidimensional floats and determine the geomorphs in it, then get it as a percentage, can also render it as a gizmo

    determine what pixel size and real height will be making sure it scales properly with the studies numbers (1pixel = 800m^2) 
    
    then use this function 
    PTRM = (-38.02+3.55 * pit + 1.75 * peak + 25.12 * flat + 9.61 * valley + 7.59 * ridge + 6.71 * hollow + 9.02 * spur + 7.31 * shoulder + 28.95 * slope + 7.63 * footslope)/69.96
    and get its PTRM rating
    then compare it with other heightmaps (gather online and maybe implement perlin myself)


    source https://dl.acm.org/doi/fullHtml/10.1145/3514244#sec-8

    then to make sure there are more than 1 source for accuracy as the above example doesnt acdtually consider
    gather elevation histograms and compare with my result using the chi squared test



    */

    

    static Vector2Int[] directions = {new(1,0)}
    float[,] input; 
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
