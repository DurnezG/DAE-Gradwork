using System.Collections.Generic;
using UnityEngine;

public class RiverGenerator
{
    public struct RiverPath
    {
        public List<Vector2Int> Cells;
    }

    public static List<RiverPath> GenerateRivers(FlowFieldGenerator.FlowFieldData flowField, float riverThreshold)
    {
        int width = flowField.Width;
        int height = flowField.Height;

        bool[,] isRiver = new bool[width, height];

        // Mark river cells
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (flowField.AccumulationMap[x, y] >= riverThreshold)
                    isRiver[x, y] = true;
            }
        }

        // Find river sources
        List<RiverPath> rivers = new List<RiverPath>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!isRiver[x, y])
                    continue;

                if (HasUpstreamRiver(x, y, flowField, isRiver))
                    continue; // not a source

                RiverPath path = TraceRiverFrom(x, y, flowField, isRiver);
                if (path.Cells.Count > 1)
                    rivers.Add(path);
            }
        }

        return rivers;
    }

    private static bool HasUpstreamRiver( int x, int y, FlowFieldGenerator.FlowFieldData flowField, bool[,] isRiver)
    {
        int width = flowField.Width;
        int height = flowField.Height;

        for (int i = 0; i < 8; i++)
        {
            Vector2Int dir = FlowFieldGenerator.GetDirectionVector((FlowFieldGenerator.FlowDirection)i);

            int nx = x - dir.x;
            int ny = y - dir.y;

            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                continue;

            if (!isRiver[nx, ny])
                continue;

            // Check if neighbour flows into me
            var neighbourDir = flowField.DirectionMap[nx, ny];
            Vector2Int neighbourFlow = FlowFieldGenerator.GetDirectionVector(neighbourDir);

            if (nx + neighbourFlow.x == x && ny + neighbourFlow.y == y)
                return true;
        }

        return false;
    }

    private static RiverPath TraceRiverFrom(int startX, int startY, FlowFieldGenerator.FlowFieldData flowField, bool[,] isRiver)
    {
        RiverPath path = new RiverPath();
        path.Cells = new List<Vector2Int>();

        int x = startX;
        int y = startY;

        int width = flowField.Width;
        int height = flowField.Height;

        while (true)
        {
            path.Cells.Add(new Vector2Int(x, y));

            var dir = flowField.DirectionMap[x, y];
            if (dir == FlowFieldGenerator.FlowDirection.Still)
                break;

            Vector2Int d = FlowFieldGenerator.GetDirectionVector(dir);

            int nx = x + d.x;
            int ny = y + d.y;

            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                break;

            if (!isRiver[nx, ny])
                break;

            x = nx;
            y = ny;
        }

        return path;
    }
}
