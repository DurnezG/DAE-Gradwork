using UnityEngine;
using static EndlessTerrain;

public class FlowFieldDebugger : MonoBehaviour
{
    [Header("References")]
    public EndlessTerrain terrain;
    public Transform viewer;

    [Header("Visual Settings")]
    public float arrowLength = 0.35f;
    public float arrowHeadSize = 0.15f;
    public float yOffset = 0.2f;

    private readonly Color[] directionColors = {
        new Color(0.0f, 1f, 1f),  // N  cyan
        new Color(0.3f, 0.7f, 1f),// NE blue-ish
        new Color(0.1f, 0.5f, 1f),// E
        new Color(0.0f, 0.3f, 0.8f),// SE
        new Color(0.0f, 0.1f, 0.7f),// S
        new Color(0.2f, 0.0f, 0.4f),// SW violet
        new Color(0.3f, 0.3f, 1f),// W
        new Color(0.5f, 0.7f, 1f),// NW light blue
        Color.gray                      // Still (unused)
    };

    void OnDrawGizmos()
    {
        if (terrain == null || viewer == null) return;

        var chunk = terrain.GetChunkAtPosition(viewer.position);
        if (chunk == null || chunk.FlowField.DirectionMap == null) return;

        DrawFlow(chunk);
    }

    void DrawFlow(TerrainChunk chunk)
    {
        var flow = chunk.FlowField;

        Vector3 chunkCenter = chunk.WorldPosition;
        float half = (terrain.ChunkSize * SCALE) / 2f;
        Vector3 origin = chunkCenter - new Vector3(half, 0, half);
        float cellSize = terrain.ChunkSize / (float)(flow.Width) * EndlessTerrain.SCALE;

        for (int x = 0; x < flow.Width; x++)
        {
            for (int y = 0; y < flow.Height; y++)
            {
                var dir = flow.DirectionMap[x, y];
                if (dir == FlowFieldGenerator.FlowDirection.Still)
                    continue;

                Vector2Int offset = FlowFieldGenerator.GetDirectionVector(dir);

                Vector3 start = origin + new Vector3(x * cellSize, yOffset, y * cellSize);
                Vector3 end = start + new Vector3(offset.x, 0, offset.y) * (cellSize * arrowLength);


                // Color per direction
                Gizmos.color = directionColors[(int)dir];
                Gizmos.DrawLine(start, end);
                DrawArrowHead(end, offset);
            }
        }
    }

    void DrawArrowHead(Vector3 tip, Vector2Int dir)
    {
        // perpendicular vector
        Vector3 perpendicular = new Vector3(-dir.y, 0, dir.x).normalized;
        Vector3 baseLeft = tip - (new Vector3(dir.x, 0, dir.y).normalized * 0.1f) + perpendicular * arrowHeadSize;
        Vector3 baseRight = tip - (new Vector3(dir.x, 0, dir.y).normalized * 0.1f) - perpendicular * arrowHeadSize;

        Gizmos.DrawLine(tip, baseLeft);
        Gizmos.DrawLine(tip, baseRight);
    }
}
