using System;
using System.Collections.Generic;
using UnityEngine;
namespace DLA
{
    public class Walker
    {
        private static readonly Vector2Int[] directions = new Vector2Int[]
{
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
};
        private static readonly Vector2Int[] diagonalDirections = new Vector2Int[]
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),

            new Vector2Int( 1,  1),
            new Vector2Int( 1, -1),
            new Vector2Int(-1,  1),
            new Vector2Int(-1, -1),
        };
        private static readonly object rndLock = new object();
        private static readonly System.Random globalRnd = new System.Random();
        private readonly System.Random rnd;
        // have to use system.rnd because unity doesnt support tasks

        Vector2Int pos = new Vector2Int(0, 0);
        bool[,] DLAMap;
        public bool inPos = false;
        public int maxSteps = 100;
        public int stepCount = 0;
        public Vector2Int directionToConnection;
        public Walker(bool[,] map, List<Vector2Int> hull = null)
        {   
            int seed;
            lock (rndLock)
            {
                seed = globalRnd.Next(); 
            }
            rnd = new System.Random(seed);
            DLAMap = map;
            int mapSize = DLAMap.GetLength(0);
            if (hull != null && hull.Count > 1)
            {
                int edgeIndex = rnd.Next(hull.Count);
                Vector2Int startPoint = hull[edgeIndex];
                Vector2Int endPoint = hull[(edgeIndex + 1) % hull.Count];
                
                // entire line
                Vector2Int edgeVector = new Vector2Int(endPoint.x - startPoint.x, endPoint.y - startPoint.y);
                
                //gets points in the line
                int steps = Utils.EuclidsAlgorithm(Mathf.Abs(edgeVector.x), Mathf.Abs(edgeVector.y));
                Vector2Int step = new Vector2Int(edgeVector.x / steps, edgeVector.y / steps);
                
                //pick random point in the edge
                int stepIndex = rnd.Next(0, steps + 1);
                Vector2Int spawn = new Vector2Int(startPoint.x + step.x * stepIndex, startPoint.y + step.y * stepIndex);


                pos = new Vector2Int(Mathf.Clamp(spawn.x, 0, mapSize - 1), Mathf.Clamp(spawn.y, 0, mapSize - 1));   
                   
            }
            else
            {
                pos = new Vector2Int(rnd.Next(0, mapSize), rnd.Next(0, mapSize));
            }
        }
        public bool StepWalker(out Vector2Int stuckPos, out Vector2Int dirToConnection, bool diagonal = true)
        {
            stuckPos = Vector2Int.zero;
            dirToConnection = Vector2Int.zero;
            int size = DLAMap.GetLength(0);
            Vector2Int[] choices = diagonal
            ? diagonalDirections
            : directions;

            Vector2Int offset = choices[rnd.Next(choices.Length)];

            Vector2Int newPos = pos + offset;

            if (newPos.x < 0 || newPos.x >= size || newPos.y < 0 || newPos.y >= size) return false;

            if (DLAMap[newPos.x, newPos.y])
            {
                stuckPos = pos;
                dirToConnection = offset;
                directionToConnection = offset;
                inPos = true;
                return true;
            }
            pos = newPos;
        
         /*   stepCount++;
            if (stepCount >= maxSteps)
            {
                stepCount = 0;
            }*/
            // in future can implement killing and respawning, not really needed right now
            return false;

        }
        public bool StepWalkerNoiseGuided(out Vector2Int stuckPos, out Vector2Int dirToConnection, float[,] noiseField, bool diagonal = true)
        {
            stuckPos = Vector2Int.zero;
            dirToConnection = Vector2Int.zero;
            int size = DLAMap.GetLength(0);

            Vector2Int[] choices = diagonal ? diagonalDirections : directions;
            int choiceCount = choices.Length;

            float totalWeight = 0f;
            float[] weights = new float[choiceCount];
            // noisefield weights

            for (int i = 0; i < choiceCount; i++)
            {
                Vector2Int offset = choices[i];
                
                int nx = pos.x + offset.x;
                int ny = pos.y + offset.y;

                // weight options from noise field
                bool outOfBounds = nx < 0 || nx >= size || ny < 0 || ny >= size;
                weights[i] =  outOfBounds ? 0 : noiseField[nx, ny];
      
                totalWeight += weights[i];
            }

            int selectedIndex;
            if (totalWeight <= 0f)
            {
                // pick random if somehow all weight is 0 
                selectedIndex = rnd.Next(choiceCount);
            }
            else
            {
                // pick a random number from 0 to max weight
                float r = (float)rnd.NextDouble() * totalWeight;
                float cumulative = 0f;
                selectedIndex = 0;

                for (int i = 0; i < choiceCount; i++)
                {
                    cumulative += weights[i];
                    if (r <= cumulative)
                    {
                        selectedIndex = i;
                        // select that direction 
                        break;
                    }
                }
            }

            Vector2Int chosenOffset = choices[selectedIndex];
            Vector2Int newPos = pos + chosenOffset;

            if (newPos.x < 0 || newPos.x >= size || newPos.y < 0 || newPos.y >= size)
            {
                return false;
            }

            // check map for stuck 
            if (DLAMap[newPos.x, newPos.y])
            {
                stuckPos = pos;
                dirToConnection = chosenOffset;
                directionToConnection = chosenOffset;
                inPos = true;
                return true;
            }

            // if not then step
            pos = newPos;
            stepCount++;

           /* if (stepCount >= maxSteps)
            {
                stepCount = 0;
            }*/
           //killing and respawning 

            return false;
        }

        public Vector2Int GetPos()
        {
            return pos;
        }
    }
}