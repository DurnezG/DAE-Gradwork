using System;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using static RiverGenerator;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode { NoiseMap, ColourMap, Mesh, FalloffMap, None };
    
    [Header("Map Settings")]
    public DrawMode drawMode;

    public Noise.NormalizeMode normalizeMode;
    public TerrainType[] regions;

    public const int MAP_CHUNK_SIZE = 239;
    public float noiseScale;

    public int octaves;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    [Header("Flow Field Settings")]
    public int AccumulationIterations = 25;
    public float AccumulationStartValue = 1f;
    public int sampleStep = 1;

    [Header("River generator")]
    public float RiverThreshold = 20f;

    [Header("Falloff map")]
    public bool useFalloff;

    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    [Header("Editor")]

    public bool autoUpdate;

    [Range(0, 6)]
    public int editorPreviewLOD;

    private float[,] _falloffMap;

    private Queue<MapThreadInfo<MapData>> _mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    private Queue<MapThreadInfo<MeshData>> _meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    void Awake()
    {
        _falloffMap = FalloffGenerator.GenerateFalloffMap(MAP_CHUNK_SIZE);
    }

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);

        MapDisplay display = FindFirstObjectByType<MapDisplay>();

        switch (drawMode)
        {
            case DrawMode.NoiseMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.HeightMap));
                break;
            case DrawMode.ColourMap:
                display.DrawTexture(TextureGenerator.TextureFromColourMap(mapData.ColourMap, MAP_CHUNK_SIZE, MAP_CHUNK_SIZE));
                break;
            case DrawMode.Mesh:
                display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.HeightMap, meshHeightMultiplier, meshHeightCurve, editorPreviewLOD), TextureGenerator.TextureFromColourMap(mapData.ColourMap, MAP_CHUNK_SIZE, MAP_CHUNK_SIZE));
                break;
            case DrawMode.FalloffMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(MAP_CHUNK_SIZE)));
                break;
            default:
            case DrawMode.None:
                break;
        }
    }

    public void RequestMapData(Vector2 centre, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate {
            MapDataThread(centre, callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 centre, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);
        lock (_mapDataThreadInfoQueue)
        {
            _mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.HeightMap, meshHeightMultiplier, meshHeightCurve, lod);
        lock (_meshDataThreadInfoQueue)
        {
            _meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void Update()
    {
        if (_mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < _mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = _mapDataThreadInfoQueue.Dequeue();
                threadInfo.Callback(threadInfo.Parameter);
            }
        }

        if (_meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < _meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = _meshDataThreadInfoQueue.Dequeue();
                threadInfo.Callback(threadInfo.Parameter);
            }
        }
    }

    MapData GenerateMapData(Vector2 centre)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(MAP_CHUNK_SIZE + 2, MAP_CHUNK_SIZE + 2, seed, noiseScale, octaves, persistance, lacunarity, centre + offset, normalizeMode);

        Color[] colourMap = new Color[MAP_CHUNK_SIZE * MAP_CHUNK_SIZE];
        for (int y = 0; y < MAP_CHUNK_SIZE; y++)
        {
            for (int x = 0; x < MAP_CHUNK_SIZE; x++)
            {
                if (useFalloff)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - _falloffMap[x, y]);
                }
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colourMap[y * MAP_CHUNK_SIZE + x] = regions[i].colour;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        FlowFieldGenerator.FlowFieldData flowField = FlowFieldGenerator.GenerateFlowField(noiseMap, meshHeightCurve, meshHeightMultiplier, sampleStep);
        List<RiverPath> rivers = RiverGenerator.GenerateRivers(flowField, RiverThreshold);

        return new MapData(noiseMap, colourMap, flowField, rivers);
    }

    void OnValidate()
    {
        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
        if (octaves < 0)
        {
            octaves = 0;
        }
        if (sampleStep < 1)
        {
            sampleStep = 1;
        }

        _falloffMap = FalloffGenerator.GenerateFalloffMap(MAP_CHUNK_SIZE);
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> Callback;
        public readonly T Parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.Callback = callback;
            this.Parameter = parameter;
        }

    }

}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color colour;
}

public class MapData
{
    public float[,] HeightMap;
    public Color[] ColourMap;
    public FlowFieldGenerator.FlowFieldData FlowField;
    public List<RiverPath> RiverPaths;

    public enum Border { North = 0, East = 1, South = 2, West = 3 }
    public bool[] bordersMapped = new bool[4];

    public bool IsFullyMapped
    {
        get
        {
            foreach (bool mapped in bordersMapped)
            {
                if (!mapped)
                    return false;
            }
            return true;
        }
    }

    public MapData(float[,] heightMap, Color[] colourMap, FlowFieldGenerator.FlowFieldData flowField, List<RiverPath> riverPaths)
    {
        this.HeightMap = heightMap;
        this.ColourMap = colourMap;
        this.FlowField = flowField;
        this.RiverPaths = riverPaths;
        this.bordersMapped = new bool[4] { false, false, false, false };
    }

    public static Vector2 GetNeighbourCoord(Vector2 coord, Border border)
    {
        switch (border)
        {
            case Border.North: return coord + new Vector2(0, -1);
            case Border.East: return coord + new Vector2(1, 0);
            case Border.South: return coord + new Vector2(0, 1);
            case Border.West: return coord + new Vector2(-1, 0);
            default: return coord;
        }
    }

    public static Border Opposite(Border b)
    {
        switch (b)
        {
            case Border.North: return Border.South;
            case Border.South: return Border.North;
            case Border.East: return Border.West;
            case Border.West: return Border.East;
            default: return b;
        }
    }
}