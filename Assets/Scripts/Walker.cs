using UnityEngine;
namespace DLA
{
    public class Walker
    {

        private static readonly object rndLock = new object();
        private static readonly System.Random globalRnd = new System.Random();
        private readonly System.Random rnd;

        Vector2Int pos = new Vector2Int(0, 0);
        bool[,] DLAmap;
        public bool inPos = false;
        public int maxSteps = 100;
        public int stepCount = 0;
        public Vector2Int dirToConnection;
        public Walker(bool[,] _map)
        {
            int seed;
            lock (rndLock)
            {
                seed = globalRnd.Next(); 
            }
            rnd = new System.Random();
            DLAmap = _map;
          //  pos = new Vector2Int(Random.Range(0, DLAmap.GetLength(0)), Random.Range(0, DLAmap.GetLength(1)));
            pos = new Vector2Int(rnd.Next(0,DLAmap.GetLength(0)), rnd.Next(0, DLAmap.GetLength(1)));
        }
        public bool StepWalker()
        {
            int width = DLAmap.GetLength(0);
            int height = DLAmap.GetLength(1);
            Vector2Int offset = new Vector2Int(pos.x, pos.y);
            do
            {
                int dx = rnd.Next(-1, 2);
                int dy = rnd.Next(-1, 2);
                //offset = new Vector2Int(Random.Range(-1, 2), Random.Range(-1, 2));
                offset = new Vector2Int(dx, dy);
            }
            while (offset == Vector2Int.zero);

            Vector2Int newPos = pos + offset;

            if (newPos.x < 0 || newPos.x >= width || newPos.y < 0 || newPos.y >= height)
                return false;

            if (DLAmap[newPos.x, newPos.y])
            {
                dirToConnection = offset;
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