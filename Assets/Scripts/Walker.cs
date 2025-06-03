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
        public bool StepWalker(out Vector2Int stuckPos, out Vector2Int dirToConnection, bool diagonal = false)
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
        public Vector2Int GetPos()
        {
            return pos;
        }
    }
}