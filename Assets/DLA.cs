using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections.Generic;

namespace DLA { 
    public class DLA : MonoBehaviour
    {
        public int resolution = 513;
        public bool[,] DLAmap;
        public Terrain terrain;
        float[,] heightMapData;
        List<Walker> walkers = new List<Walker>();
        public int walkerCount = 10000;
        public int maxWalkers = 200;
    
        public void StartDLA()
        {
            walkers = new List<Walker>();
            heightMapData = new float[resolution, resolution];
            DLAmap = new bool[resolution, resolution];
            StartCoroutine(initializeDLA());

        }
        public void StopDLA()
        {
            StopAllCoroutines();
            terrain.terrainData.SetHeights(0, 0, heightMapData);
            walkers = new List<Walker>();
            heightMapData = new float[resolution, resolution];
            DLAmap = new bool[resolution, resolution];
        }
        void InstantiateWalker()
        {
            walkers.Add(new Walker(DLAmap));
        }
        IEnumerator initializeDLA()
        {
            DLAmap[resolution/2,resolution/2] = true;
            int count = 0;
      
            for (int i = 0; i < walkerCount; i++)
            {
                InstantiateWalker();
            }
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
}