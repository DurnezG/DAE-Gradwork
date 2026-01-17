using UnityEditor;
using UnityEngine;
using static EndlessTerrain;

public class FlowFieldDebugger : MonoBehaviour
{
    public enum DebugDrawMode { flat, terrainHeight }
    public enum ColorMode { direction, accumulation }

    public Vector3 ArrowOffset;

    [Header("References")]
    public EndlessTerrain terrain;
    public Transform viewer;
    public MapGenerator mapGenerator;

    [Header("Visual Settings")]
    public float arrowLength = 0.35f;
    public float arrowHeadSize = 0.15f;
    public float yOffset = 0.2f;

    [Header("Drawing options")]
    public DebugDrawMode drawMode = DebugDrawMode.flat;
    public ColorMode colorMode = ColorMode.direction;

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

    [Header("Value debugging")]
    public Vector2 activeChunk;
    public bool showAccumulationValues = false;


    void OnDrawGizmos()
    {
        if (terrain == null || viewer == null) return;

        var chunk = terrain.GetChunkAtPosition(viewer.position);
        if (chunk == null || chunk.MapData == null) return;

        activeChunk = terrain.GetChunkVectorAtPosition(chunk.WorldPosition);

        DrawFlow(chunk);
    }

    void DrawFlow(TerrainChunk chunk)
    {
        //AnimationCurve curve = new AnimationCurve(mapGenerator.meshHeightCurve.keys);

        //Vector3 chunkCenter = chunk.WorldPosition;
        //float halfw = (terrain.debugDirection.GetLength(0) * SCALE) / 2f;
        //float halfh = (terrain.debugDirection.GetLength(1) * SCALE) / 2f;
        //Vector3 origin = chunkCenter - new Vector3(halfw, 0, halfh);
        //float cellSize = terrain.ChunkSize / (float)(terrain.debugDirection.GetLength(0)) * EndlessTerrain.SCALE;

        //float minAccHeight, maxAccHeight;
        //GetMinMax(chunk.MapData.HeightMap, out minAccHeight, out maxAccHeight);

        //for (int x = 0; x < terrain.debugDirection.GetLength(0); x++)
        //{
        //    for (int y = 0; y < terrain.debugDirection.GetLength(1); y++)
        //    //for (int y = 0; y < flow.Height; y += flow.Height - 1)
        //    {
        //        var dir = terrain.debugDirection[x, y];
        //        if (dir == FlowFieldGenerator.FlowDirection.Still)
        //            continue;

        //        Vector2Int offset = FlowFieldGenerator.GetDirectionVector(dir);

        //        Vector3 start = ArrowOffset + origin + new Vector3(x * cellSize, yOffset, y * cellSize);
        //        Vector3 end = start + new Vector3(offset.x, 0, offset.y) * (cellSize * arrowLength);

        //        // Color per direction
        //        switch (colorMode)
        //        {
        //            case ColorMode.direction:
        //                Gizmos.color = directionColors[(int)dir];
        //                break;
        //            case ColorMode.accumulation:
        //                float acc = terrain.debugAccumulation[x, y];
        //                Gizmos.color = Color.Lerp(Color.green, Color.red, acc / 10f); // assuming max accumulation ~10 for color scaling
        //                break;
        //        }
        //        //Gizmos.color = directionColors[(int)dir];
        //        Gizmos.DrawLine(start, end);
        //        DrawArrowHead(end, offset);
        //    }
        //}


        var flow = chunk.MapData.FlowField;

        AnimationCurve curve = new AnimationCurve(mapGenerator.meshHeightCurve.keys);

        Vector3 chunkCenter = chunk.WorldPosition;
        float half = (terrain.ChunkSize * SCALE) / 2f;
        Vector3 origin = chunkCenter - new Vector3(half, 0, half);
        float cellSize = terrain.ChunkSize / (float)(flow.Width) * EndlessTerrain.SCALE;

        float minAccHeight, maxAccHeight;
        GetMinMax(chunk.MapData.HeightMap, out minAccHeight, out maxAccHeight);

        for (int x = 0; x < flow.Width; x++)
        {
            for (int y = 0; y < flow.Height; y++)
            //for (int y = 0; y < flow.Height; y += flow.Height - 1)
            {
                var dir = flow.DirectionMap[x, y];
                if (dir == FlowFieldGenerator.FlowDirection.Still)
                    continue;

                Vector2Int offset = FlowFieldGenerator.GetDirectionVector(dir);

                Vector3 start = ArrowOffset + origin + new Vector3(x * cellSize, yOffset, y * cellSize);
                Vector3 end = start + new Vector3(offset.x, 0, offset.y) * (cellSize * arrowLength);

                if (drawMode == DebugDrawMode.terrainHeight)
                {
                    if (chunk.MapData.HeightMap == null) continue;

                    int hx = Mathf.Clamp(x * flow.StepSize, 0, chunk.MapData.HeightMap.GetLength(0) + 1);
                    int hy = Mathf.Clamp(y * flow.StepSize, 0, chunk.MapData.HeightMap.GetLength(1) + 1);

                    float startHeight = curve.Evaluate(chunk.MapData.HeightMap[hx, chunk.MapData.HeightMap.GetLength(0) - hy - 1]) * mapGenerator.meshHeightMultiplier * SCALE;
                    start.y = startHeight + yOffset;
                    end.y = startHeight + (yOffset - 1f);
                }

                // Color per direction
                switch (colorMode)
                {
                    case ColorMode.direction:
                        Gizmos.color = directionColors[(int)dir];
                        break;
                    case ColorMode.accumulation:
                        float acc = flow.AccumulationMap[x, y];
                        Gizmos.color = Color.Lerp(Color.green, Color.red, acc / 10f); // assuming max accumulation ~10 for color scaling
                        break;
                }
                //Gizmos.color = directionColors[(int)dir];
                Gizmos.DrawLine(start, end);
                DrawArrowHead(end, offset);

                if (colorMode == ColorMode.accumulation && showAccumulationValues)
                {
                    float acc = flow.AccumulationMap[x, y];
                    Vector3 labelPos = end + Vector3.up * 0.15f;

                    Handles.color = Color.white;
                    Handles.Label(labelPos, acc.ToString("0"));
                }
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

    public static void GetMinMax(float[,] data, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;

        int width = data.GetLength(0);
        int height = data.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float v = data[x, y];
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
    }
}
