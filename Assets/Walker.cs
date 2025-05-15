using UnityEngine;
namespace DLA
{
    public class Walker
    {
        public Walker(bool[,] _map)
        {
            DLAmap = _map;
            pos = new Vector2Int(Random.Range(0, DLAmap.GetLength(0)), Random.Range(0, DLAmap.GetLength(1)));
        }
        Vector2Int pos = new Vector2Int(0, 0);
        bool[,] DLAmap;
        public bool inPos = false;
        public int maxSteps = 100;
        public int stepCount = 0;

        public bool StepWalker()
        {
            int width = DLAmap.GetLength(0);
            int height = DLAmap.GetLength(1);
            Vector2Int offset = new Vector2Int(pos.x, pos.y);
            do
            {
                offset = new Vector2Int(Random.Range(-1, 2), Random.Range(-1, 2));
            }
            while (offset == Vector2Int.zero);

            Vector2Int newPos = pos + offset;

            if (newPos.x < 0 || newPos.x >= width || newPos.y < 0 || newPos.y >= height)
                return false;

            if (DLAmap[newPos.x, newPos.y])
            {
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