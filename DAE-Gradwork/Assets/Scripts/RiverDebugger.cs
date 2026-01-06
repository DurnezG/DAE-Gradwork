using System.Collections.Generic;
using UnityEngine;

using static EndlessTerrain;
using static RiverGenerator;

public class RiverDebugger : MonoBehaviour
{
    public enum DebugDrawMode { flat, terrainHeight }

    [Header("References")]
    public EndlessTerrain terrain;
    public Transform viewer;
    public MapGenerator mapGenerator;

    [Header("River Settings")]
    public float riverThreshold = 20f;

    [Header("Visual Settings")]
    public float yOffset = 0.2f;
    public float lineWidth = 0.1f;
    public Color riverColor = Color.cyan;

    [Header("Drawing Options")]
    public DebugDrawMode drawMode = DebugDrawMode.flat;

    void OnDrawGizmos()
    {
        if (terrain == null || viewer == null || mapGenerator == null)
            return;

        var chunk = terrain.GetChunkAtPosition(viewer.position);
        if (chunk == null || chunk.FlowField.DirectionMap == null)
            return;

        DrawRivers(chunk);
    }

    void DrawRivers(TerrainChunk chunk)
    {
        var flow = chunk.FlowField;
        if (flow.AccumulationMap == null)
            return;

        List<RiverPath> rivers = RiverGenerator.GenerateRivers(flow, riverThreshold);
        if (rivers == null || rivers.Count == 0)
            return;

        AnimationCurve heightCurve = new AnimationCurve(mapGenerator.meshHeightCurve.keys);

        Vector3 chunkCenter = chunk.WorldPosition;
        float half = (terrain.ChunkSize * SCALE) / 2f;
        Vector3 origin = chunkCenter - new Vector3(half, 0, half);

        float cellSize = terrain.ChunkSize / (float)flow.Width * SCALE;

        Gizmos.color = riverColor;

        foreach (var river in rivers)
        {
            for (int i = 0; i < river.Cells.Count - 1; i++)
            {
                Vector2Int a = river.Cells[i];
                Vector2Int b = river.Cells[i + 1];

                Vector3 worldA = origin + new Vector3(a.x * cellSize, 0, a.y * cellSize);
                Vector3 worldB = origin + new Vector3(b.x * cellSize, 0, b.y * cellSize);

                if (drawMode == DebugDrawMode.terrainHeight)
                {
                    ApplyTerrainHeight(chunk, heightCurve, ref worldA, a);
                    ApplyTerrainHeight(chunk, heightCurve, ref worldB, b);
                }
                else
                {
                    worldA.y = yOffset;
                    worldB.y = yOffset;
                }

                Gizmos.DrawLine(worldA, worldB);
            }
        }
    }

    void ApplyTerrainHeight(TerrainChunk chunk, AnimationCurve curve, ref Vector3 worldPos, Vector2Int cell)
    {
        if (chunk.MapData.HeightMap == null)
            return;

        int mapWidth = chunk.MapData.HeightMap.GetLength(0);
        int mapHeight = chunk.MapData.HeightMap.GetLength(1);

        int hx = Mathf.Clamp(cell.x, 0, mapWidth - 1);
        int hy = Mathf.Clamp(cell.y, 0, mapHeight - 1);

        float heightValue = curve.Evaluate( chunk.MapData.HeightMap[hx, mapHeight - hy - 1] ) * mapGenerator.meshHeightMultiplier * SCALE;

        worldPos.y = heightValue + yOffset;
    }
}

