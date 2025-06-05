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

        Vector2Int pos = new Vector2Int(0, 0);
        bool[,] DLAmap;
        public bool inPos = false;
        public int maxSteps = 100;
        public int stepCount = 0;
        public Vector2Int directionToConnection;

        public Walker(bool[,] _map)
        {
            int seed;
            lock (rndLock)
            {
                seed = globalRnd.Next(); 
            }
            rnd = new System.Random(seed);
            DLAmap = _map;
          //  pos = new Vector2Int(Random.Range(0, DLAmap.GetLength(0)), Random.Range(0, DLAmap.GetLength(1)));
            pos = new Vector2Int(rnd.Next(0,DLAmap.GetLength(0)), rnd.Next(0, DLAmap.GetLength(1)));
        }
        public bool StepWalker(out Vector2Int stuckPos, out Vector2Int dirToConnection, bool diagonal = true)
        {
            stuckPos = Vector2Int.zero;
            dirToConnection = Vector2Int.zero;
            int width = DLAmap.GetLength(0);
            int height = DLAmap.GetLength(1);
            Vector2Int[] choices = diagonal
            ? diagonalDirections
            : directions;

            Vector2Int offset = choices[rnd.Next(choices.Length)];

            Vector2Int newPos = pos + offset;

            if (newPos.x < 0 || newPos.x >= width || newPos.y < 0 || newPos.y >= height) return false;

            if (DLAmap[newPos.x, newPos.y])
            {
                stuckPos = pos;
                dirToConnection = offset;
                directionToConnection = offset;
                inPos = true;
                return true;
            }
            pos = newPos;
        
            stepCount++;
            if (stepCount >= maxSteps)
            {
                // pos = new Vector2Int(Random.Range(0, DLAmap.GetLength(0)), Random.Range(0, DLAmap.GetLength(1)));
                stepCount = 0;
            }
            return false;

        }
        public bool StepWalkerNoiseGuided(out Vector2Int stuckPos, out Vector2Int dirToConnection, float[,] noiseField, bool diagonal = true)
        {
            stuckPos = Vector2Int.zero;
            dirToConnection = Vector2Int.zero;
            int width = DLAmap.GetLength(0);
            int height = DLAmap.GetLength(1);

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
                bool outOfBounds = nx < 0 || nx >= width || ny < 0 || ny >= height;
                weights[i] = ( outOfBounds ?0 : weights[i] = noiseField[nx, ny]);
      
                totalWeight += weights[i];
            }

            int selectedIndex;
            if (totalWeight <= 0f)
            {
                // If all weights are zero (e.g. edge or noise = 0), pick uniformly
                selectedIndex = rnd.Next(choiceCount);
            }
            else
            {
                // Pick a random number in [0, totalWeight)
                float r = (float)rnd.NextDouble() * totalWeight;
                float cumulative = 0f;
                selectedIndex = 0;

                for (int i = 0; i < choiceCount; i++)
                {
                    cumulative += weights[i];
                    if (r <= cumulative)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            Vector2Int chosenOffset = choices[selectedIndex];
            Vector2Int newPos = pos + chosenOffset;

            // If out of bounds, do nothing this step
            if (newPos.x < 0 || newPos.x >= width || newPos.y < 0 || newPos.y >= height)
            {
                return false;
            }

            // Check for sticking
            if (DLAmap[newPos.x, newPos.y])
            {
                stuckPos = pos;
                dirToConnection = chosenOffset;
                directionToConnection = chosenOffset;
                inPos = true;
                return true;
            }

            // Otherwise, move into that cell
            pos = newPos;
            stepCount++;

            if (stepCount >= maxSteps)
            {
                stepCount = 0;
            }

            return false;
        }

        public Vector2Int GetPos()
        {
            return pos;
        }
    }
}