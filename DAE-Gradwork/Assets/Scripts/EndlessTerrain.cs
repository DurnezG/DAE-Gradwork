using Mono.Cecil;
using System;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.AdaptivePerformance.Provider;
using static FlowFieldGenerator;
using static MapData;

public class EndlessTerrain : MonoBehaviour
{

    public const float SCALE = 5f;

    const float _VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE = 25f;
    const float _SQR_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE = _VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE * _VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE;

    public LODInfo[] detailLevels;
    public static float maxViewDst;

    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    private Vector2 _viewerPositionOld;
    private static MapGenerator _mapGenerator;
    public int ChunkSize { get; private set; }
    private int _chunksVisibleInViewDst;

    private Dictionary<Vector2, TerrainChunk> _terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    private static List<TerrainChunk> _terrainChunksVisibleLastUpdate = new List<TerrainChunk>();


    [Header("DEBUGG REMOVE ME!!!!")]
    bool set = false;
    public Renderer chunk1; 
    public Renderer chunk2;
    public Border demoBorder;



    void Start()
    {
        _mapGenerator = FindFirstObjectByType<MapGenerator>();

        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        ChunkSize = MapGenerator.MAP_CHUNK_SIZE - 1;
        _chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / ChunkSize);

        UpdateVisibleChunks();
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / SCALE;

        if ((_viewerPositionOld - viewerPosition).sqrMagnitude > _SQR_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE)
        {
            _viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }

        if (set) return;

        Vector2 borderVector = new Vector2();

        switch(demoBorder)
        {
            case Border.North:
                borderVector = new Vector2(0, 1);
                break;
            case Border.South:
                borderVector = new Vector2(0, -1);
                break;
            case Border.East:
                borderVector = new Vector2(1, 0);
                break;
            case Border.West:
                borderVector = new Vector2(-1, 0);
                break;
        }

        if (!_terrainChunkDictionary.TryGetValue(new Vector2(0, 0), out TerrainChunk tc1)) return;
        if(!_terrainChunkDictionary.TryGetValue(borderVector, out TerrainChunk tc2)) return;

        if(tc1.MapData == null || tc2.MapData == null) return;

        var t1 = TextureGenerator.TextureFromHeightMap(tc1.MapData.HeightMap);
        var t2 = TextureGenerator.TextureFromHeightMap(tc2.MapData.HeightMap);

        chunk1.sharedMaterial.mainTexture = t1;
        chunk1.transform.localScale = new Vector3(t1.width, 1, t1.height);
        chunk2.sharedMaterial.mainTexture = t2;
        chunk2.transform.localScale = new Vector3(t2.width, 1, t2.height);
        set = true;
    }

    void UpdateVisibleChunks()
    {

        for (int i = 0; i < _terrainChunksVisibleLastUpdate.Count; i++)
        {
            _terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        _terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / ChunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / ChunkSize);

        for (int yOffset = -_chunksVisibleInViewDst; yOffset <= _chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -_chunksVisibleInViewDst; xOffset <= _chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (_terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    _terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    _terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, ChunkSize, detailLevels, transform, mapMaterial, CheckForBorderUpdate));
                }
            }
        }
    }

    public TerrainChunk GetChunkAtPosition(Vector3 worldPos)
    {
        Vector2 chunkPos = new Vector2(worldPos.x, worldPos.z) / SCALE;

        int currentChunkCoordX = Mathf.RoundToInt(chunkPos.x / ChunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(chunkPos.y / ChunkSize);

        Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX, currentChunkCoordY);

        if (_terrainChunkDictionary.TryGetValue(viewedChunkCoord, out TerrainChunk chunk))
        {
            return chunk;
        }

        return null;
    }

    public Vector2 GetChunkVectorAtPosition(Vector3 worldPos)
    {
        Vector2 chunkPos = new Vector2(worldPos.x, worldPos.z) / SCALE;

        int currentChunkCoordX = Mathf.RoundToInt(chunkPos.x / ChunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(chunkPos.y / ChunkSize);

        return new Vector2(currentChunkCoordX, currentChunkCoordY);
    }

    public bool TryGetChunk(Vector2 neighbourCoord, out TerrainChunk neighbourChunk)
    {
        return _terrainChunkDictionary.TryGetValue(neighbourCoord, out neighbourChunk);
    }

    private void CheckForBorderUpdate(TerrainChunk chunk)
    {
        if (!chunk.MapData.IsFullyMapped)
        {
            for (int i = 0; i < 4; i++)
            {
                if (chunk.MapData.bordersMapped[i])
                    continue;

                Border border = (Border)i;

                Vector2 neighbourCoord = GetNeighbourCoord(GetChunkVectorAtPosition(chunk.WorldPosition), border);

                if (_terrainChunkDictionary.TryGetValue(neighbourCoord, out TerrainChunk neighbour))
                {
                    if(ResolveBorder(chunk, neighbour, border))
                    {
                        chunk.MapData.bordersMapped[i] = true;
                        neighbour.MapData.bordersMapped[(int)Opposite(border)] = true;
                    }
                }
            }
        }
    }


    bool ResolveBorder(TerrainChunk sourceChunk, TerrainChunk neighbourChunk, Border border)
    {
        if (neighbourChunk == null || neighbourChunk.MapData == null || sourceChunk == null || sourceChunk.MapData == null)
            return false;

        var flowA = sourceChunk.MapData.FlowField;

        int width = flowA.Width;
        int height = flowA.Height;

        // Overlap counter
        int overlap = 2;

        int borderI = overlap;
        int innerMinX = borderI;
        int innerMaxX = width - borderI - 1;

        int innerMinY = borderI;
        int innerMaxY = height - borderI - 1;

        switch (border)
        {
            case Border.North:
                for (int x = innerMinX; x <= innerMaxX; x++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, x, height - 1, x, innerMaxY, border);
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, x, 0, x, innerMinY, border);
                }
                break;

            case Border.South:
                for (int x = innerMinX; x <= innerMaxX; x++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, x, 0, x, innerMinY, border); 
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, x, height - 1, x, innerMaxY, border);
                }
                break;

            case Border.East:
                for (int y = innerMinY; y <= innerMaxY; y++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, width -1, y, innerMinX, y, border);
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, 0, y, innerMaxX, y, border);
                }
                break;

            case Border.West:
                for (int y = innerMinY; y <= innerMaxY; y++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, 0, y, innerMaxX, y, border);
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, width - 1, y, innerMinX, y, border);
                }
                break;
        }

        return true;
    }

    void ResolveCellWithNeighbour(TerrainChunk sourceChunk, TerrainChunk neighbourChunk, int sourceX, int sourceY, int neighbourXhm, int neighbourYhm, Border border)
    {
        var flow = sourceChunk.MapData.FlowField;

        var hmSource = sourceChunk.MapData.HeightMap;
        var hmNeighbour = neighbourChunk.MapData.HeightMap;

        int width = hmSource.GetLength(0);
        int height = hmSource.GetLength(1);

        int step = flow.StepSize;

        int hx = sourceX * step;
        int hy = sourceY * step;

        float currentHeight = FlowFieldGenerator.SampleHeight(hmSource, hx, hy, flow);

        if (border == Border.North)
        { }
        //else if (border == Border.South)
        //else if (border == Border.East)
        else if (border == Border.West)
                    Debug.Log($"Resolving cell {sourceX},{sourceY} with neighbour cell {neighbourXhm},{neighbourYhm} | Current Height: {currentHeight} -> {SampleHeight(hmNeighbour, neighbourXhm, neighbourYhm, neighbourChunk.FlowField)}");

        float lowest = currentHeight;
        int bestDir = -1;

        for (int i = 0; i < FlowFieldGenerator.DIRECTIONS.Length; i++)
        {
            Vector2Int dir = FlowFieldGenerator.DIRECTIONS[i];

            int rawNx = hx + dir.x * step;
            int rawNy = hy + dir.y * step;

            float neighbourHeight;

            if (rawNx >= 0 && rawNy >= 0 && rawNx < width && rawNy < height)
            {
                neighbourHeight = FlowFieldGenerator.SampleHeight(hmSource, rawNx, rawNy, flow);
            }
            else
            {
                //int nHx = neighbourX * step;
                //int nHy = neighbourY * step;
                int nHx = (neighbourXhm + dir.x) * step;
                int nHy = (neighbourYhm + dir.y) * step;

                if (nHx < 0 || nHy < 0 || nHx >= width || nHy >= height) continue;

                neighbourHeight = FlowFieldGenerator.SampleHeight(hmNeighbour, nHx, nHy, flow);
            }

            if (neighbourHeight < lowest)
            {
                lowest = neighbourHeight;
                bestDir = i;
            }
        }

        flow.DirectionMap[sourceX, sourceY] = bestDir == -1 ? FlowDirection.Still : (FlowDirection)bestDir;
    }


    public class TerrainChunk
    {
        private GameObject _meshObject;
        private Vector2 _position;
        private Bounds _bounds;

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;

        private LODInfo[] _detailLevels;
        private LODMesh[] _lodMeshes;

        public MapData MapData { get; private set; }
        private bool _mapDataReceived;
        private int _previousLODIndex = -1;

        public Action<TerrainChunk> NeighbourghCallback = null;

        public Vector3 WorldPosition => _meshObject.transform.position;

        public FlowFieldGenerator.FlowFieldData FlowField;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material, Action<TerrainChunk> neighbourghCallback = null)
        {
            this._detailLevels = detailLevels;
            this.NeighbourghCallback = neighbourghCallback;

            _position = coord * size;
            _bounds = new Bounds(_position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(_position.x, 0, _position.y);

            _meshObject = new GameObject("Terrain Chunk");
            _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            _meshFilter = _meshObject.AddComponent<MeshFilter>();
            _meshRenderer.material = material;

            _meshObject.transform.position = positionV3 * SCALE;
            _meshObject.transform.parent = parent;
            _meshObject.transform.localScale = Vector3.one * SCALE;
            SetVisible(false);

            _lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                _lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }

            _mapGenerator.RequestMapData(_position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            this.MapData = mapData;
            _mapDataReceived = true;
            
            FlowField = mapData.FlowField;

            Texture2D texture = TextureGenerator.TextureFromColourMap(mapData.ColourMap, MapGenerator.MAP_CHUNK_SIZE, MapGenerator.MAP_CHUNK_SIZE);
            _meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
            NeighbourghCallback.Invoke(this);
        }



        public void UpdateTerrainChunk()
        {
            if (_mapDataReceived)
            {
                float viewerDstFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
                bool visible = viewerDstFromNearestEdge <= maxViewDst;

                if (visible)
                {
                    int lodIndex = 0;

                    for (int i = 0; i < _detailLevels.Length - 1; i++)
                    {
                        if (viewerDstFromNearestEdge > _detailLevels[i].visibleDstThreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lodIndex != _previousLODIndex)
                    {
                        LODMesh lodMesh = _lodMeshes[lodIndex];
                        if (lodMesh.HasMesh)
                        {
                            _previousLODIndex = lodIndex;
                            _meshFilter.mesh = lodMesh.Mesh;
                        }
                        else if (!lodMesh.HasRequestedMesh)
                        {
                            lodMesh.RequestMesh(MapData);
                        }
                    }

                    _terrainChunksVisibleLastUpdate.Add(this);
                }

                SetVisible(visible);
            }
        }

        public void SetVisible(bool visible)
        {
            _meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return _meshObject.activeSelf;
        }

    }

    class LODMesh
    {

        public Mesh Mesh;
        public bool HasRequestedMesh;
        public bool HasMesh;
        private int _lod;
        private System.Action _updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            this._lod = lod;
            this._updateCallback = updateCallback;
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            Mesh = meshData.CreateMesh();
            HasMesh = true;

            _updateCallback();
        }

        public void RequestMesh(MapData mapData)
        {
            HasRequestedMesh = true;
            _mapGenerator.RequestMeshData(mapData, _lod, OnMeshDataReceived);
        }

    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDstThreshold;
    }

}