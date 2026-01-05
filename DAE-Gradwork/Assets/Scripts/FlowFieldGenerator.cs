using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class FlowFieldGenerator
{
    public enum FlowDirection { N, NE, E, SE, S, SW, W, NW, Still }

    private static readonly Vector2Int[] DIRECTIONS = {
        new Vector2Int(0, 1),  new Vector2Int(1, 1),
        new Vector2Int(1, 0),  new Vector2Int(1, -1),
        new Vector2Int(0, -1), new Vector2Int(-1, -1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1)
    };

    public static Vector2Int GetDirectionVector(FlowDirection dir)
    {
        if (dir == FlowDirection.Still) return Vector2Int.zero;
        return DIRECTIONS[(int)dir];
    }

    public struct FlowFieldData
    {
        public FlowDirection[,] DirectionMap;
        public float[,] AccumulationMap;
        public int Width;
        public int Height;
    }

    public static FlowFieldData GenerateFlowField(float[,] noiseMap, AnimationCurve heightCurve, float heightMultiplier, int itterations = 20, float accumulationStart = 1f)
    {
        int width = noiseMap.GetLength(0);
        int height = noiseMap.GetLength(1);

        FlowFieldData data = new FlowFieldData();
        data.Width = width;
        data.Height = height;
        data.DirectionMap = new FlowDirection[width, height];
        data.AccumulationMap = new float[width, height];

        AssignFlowDirections(noiseMap, heightCurve, heightMultiplier, data.DirectionMap);
        //ComputeAccumulation(data.DirectionMap, data.AccumulationMap, itterations, accumulationStart);

        return data;
    }

    private static void AssignFlowDirections(float[,] map, AnimationCurve _heightCurve, float heightMultiplier, FlowDirection[,] directionMap)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                //float cell = map[x, y]; //heightCurve.Evaluate(map[x, y]) * heightMultiplier;
                float cell = heightCurve.Evaluate(map[x, height - y - 1]) * heightMultiplier * EndlessTerrain.SCALE;
                float lowest = cell;
                int flowDir = -1;

                for (int i = 0; i < DIRECTIONS.Length; i++)
                {
                    int nx = x + DIRECTIONS[i].x;
                    int ny = y + DIRECTIONS[i].y;

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    //float nh = map[nx, ny]; //heightCurve.Evaluate(map[nx, ny]) * heightMultiplier;
                    float nh = heightCurve.Evaluate(map[nx, height - ny - 1]) * heightMultiplier * EndlessTerrain.SCALE;
                    if (nh < lowest)
                    {
                        lowest = nh;
                        flowDir = i;
                    }
                }

                directionMap[x, y] = flowDir == -1 ? FlowDirection.Still : (FlowDirection)flowDir;
            }
        }
    }

    private static void ComputeAccumulation(FlowDirection[,] directionMap, float[,] accumulationMap, int iterations, float startValue)
    {
        int width = directionMap.GetLength(0);
        int height = directionMap.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                accumulationMap[x, y] = startValue;
            }
        }

        for (int itteration = 0; itteration < iterations; itteration++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    FlowDirection dir = directionMap[x, y];
                    if (dir == FlowDirection.Still) continue;

                    Vector2Int offset = DIRECTIONS[(int)dir];
                    int nx = x + offset.x;
                    int ny = y + offset.y;

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;

                    accumulationMap[nx, ny] += accumulationMap[x, y];
                }
            }
        }
    }
}
