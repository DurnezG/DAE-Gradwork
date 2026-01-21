using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Rendering;
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
    public FlowDirection[,] debugDirection;
    public float[,] debugAccumulation;
    //bool set = false;
    //public Renderer chunk1; 
    //public Renderer chunk2;
    //public Border demoBorder;

    private List<float> _borderTimings= new List<float>();

    void Start()
    {
        _mapGenerator = FindFirstObjectByType<MapGenerator>();

        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        ChunkSize = MapGenerator.MAP_CHUNK_SIZE - 1;
        _chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / ChunkSize);

        UpdateVisibleChunks();
    }

    private void OnDestroy()
    {
        if (_borderTimings == null || _borderTimings.Count == 0)
            return;

        // Remove outliers
        List<float> filtered = RemoveOutliersIQR(_borderTimings);

        float min = filtered.Min();
        float max = filtered.Max();
        float avg = filtered.Average();

        using (StreamWriter writer = new StreamWriter($"{Application.persistentDataPath + "/border_timings.csv"}", false))
        {
            writer.WriteLine(string.Join(";", _borderTimings));
            writer.WriteLine($"{min};{max};{avg}");
        }
    }

    private List<float> RemoveOutliersIQR(List<float> values)
    {
        List<float> sorted = values.OrderBy(v => v).ToList();

        float q1 = Percentile(sorted, 25);
        float q3 = Percentile(sorted, 75);
        float iqr = q3 - q1;

        float lowerBound = q1 - 1.5f * iqr;
        float upperBound = q3 + 1.5f * iqr;

        return sorted.Where(v => v >= lowerBound && v <= upperBound).ToList();
    }

    private float Percentile(List<float> sorted, float percentile)
    {
        float index = (percentile / 100f) * (sorted.Count - 1);
        int lower = Mathf.FloorToInt(index);
        int upper = Mathf.CeilToInt(index);

        if (lower == upper)
            return sorted[lower];

        return Mathf.Lerp(sorted[lower], sorted[upper], index - lower);
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / SCALE;

        if ((_viewerPositionOld - viewerPosition).sqrMagnitude > _SQR_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE)
        {
            _viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }

        //if (set) return;

        //Vector2 borderVector = new Vector2();

        //switch(demoBorder)
        //{
        //    case Border.North:
        //        borderVector = new Vector2(0, 1);
        //        break;
        //    case Border.South:
        //        borderVector = new Vector2(0, -1);
        //        break;
        //    case Border.East:
        //        borderVector = new Vector2(1, 0);
        //        break;
        //    case Border.West:
        //        borderVector = new Vector2(-1, 0);
        //        break;
        //}

        //if (!_terrainChunkDictionary.TryGetValue(new Vector2(0, 0), out TerrainChunk tc1)) return;
        //if(!_terrainChunkDictionary.TryGetValue(borderVector, out TerrainChunk tc2)) return;

        //if(tc1.MapData == null || tc2.MapData == null) return;

        //var t1 = TextureGenerator.TextureFromHeightMap(tc1.MapData.HeightMap);
        //var t2 = TextureGenerator.TextureFromHeightMap(tc2.MapData.HeightMap);

        //chunk1.sharedMaterial.mainTexture = t1;
        //chunk1.transform.localScale = new Vector3(t1.width, 1, t1.height);
        //chunk2.sharedMaterial.mainTexture = t2;
        //chunk2.transform.localScale = new Vector3(t2.width, 1, t2.height);
        //set = true;
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
                    var swBorderTimer = Stopwatch.StartNew();
                    if (ResolveBorder(chunk, neighbour, border))
                    {
                        chunk.MapData.bordersMapped[i] = true;
                        neighbour.MapData.bordersMapped[(int)Opposite(border)] = true;

                        UpdateAccumulationFromChunk(chunk, neighbour, border, 2);

                        chunk.MapData.RiverPaths = RiverGenerator.GenerateRivers(chunk.MapData.FlowField, _mapGenerator.RiverThreshold);
                        neighbour.MapData.RiverPaths = RiverGenerator.GenerateRivers(neighbour.MapData.FlowField, _mapGenerator.RiverThreshold);

                        swBorderTimer.Stop();
                        _borderTimings.Add(swBorderTimer.ElapsedMilliseconds);
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

        int edge = width - 1;

        switch (border)
        {
            case Border.North:
                for (int x = 1; x <= edge; x++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, x, 0, x, innerMaxY, border);
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, x, height - 1, x, innerMinY, border);
                }
                break;

            case Border.South:
                for (int x = 1; x <= edge; x++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, x, height - 1, x, innerMinY, border);
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, x, 0, x, innerMaxY, border);
                }
                break;

            case Border.East:
                for (int y = 1; y <= edge; y++)
                {
                    ResolveCellWithNeighbour(sourceChunk, neighbourChunk, width - 1, y, innerMinX, y, border);
                    ResolveCellWithNeighbour(neighbourChunk, sourceChunk, 0, y, innerMaxX, y, border);
                }
                break;

            case Border.West:
                for (int y = 1; y <= edge; y++)
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

        //Debug.Log($"Resolving cell {sourceX},{sourceY} with neighbour cell {neighbourXhm},{neighbourYhm} | Current Height: {currentHeight} -> {SampleHeight(hmNeighbour, neighbourXhm, neighbourYhm, neighbourChunk.FlowField)}");

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

    int FlipY(int y, int height) => height - 1 - y;

    struct CellKey : IEquatable<CellKey>
    {
        public Vector2 ChunkCoord;
        public int X;
        public int Y;

        public CellKey(Vector2 chunkCoord, int x, int y)
        {
            ChunkCoord = chunkCoord;
            X = x;
            Y = y;
        }

        public bool Equals(CellKey other)
            => ChunkCoord.Equals(other.ChunkCoord) && X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is CellKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + ChunkCoord.GetHashCode();
                h = h * 31 + X;
                h = h * 31 + Y;
                return h;
            }
        }
    }

    TerrainChunk GetChunkByCoord(Vector2 coord)
    {
        _terrainChunkDictionary.TryGetValue(coord, out TerrainChunk c);
        return c;
    }

    bool TryGetCell(CellKey key, out TerrainChunk chunk, out FlowFieldData flow)
    {
        chunk = GetChunkByCoord(key.ChunkCoord);
        if (chunk == null || chunk.MapData == null)
        {
            flow = default;
            return false;
        }

        flow = chunk.MapData.FlowField;
        return true;
    }

    bool TryStep(CellKey from, out CellKey to)
    {
        to = default;

        if (!TryGetCell(from, out TerrainChunk chunk, out FlowFieldData flow))
            return false;

        FlowDirection dir = flow.DirectionMap[from.X, from.Y];
        if (dir == FlowDirection.Still)
            return false;

        Vector2Int d = FlowFieldGenerator.GetDirectionVector(dir);
        int nx = from.X + d.x;
        int ny = from.Y + d.y;

        int w = flow.Width;
        int h = flow.Height;

        if (nx >= 0 && ny >= 0 && nx < w && ny < h)
        {
            to = new CellKey(from.ChunkCoord, nx, ny);
            return true;
        }

        Border exitBorder;
        if (nx < 0) exitBorder = Border.West;
        else if (nx >= w) exitBorder = Border.East;
        else if (ny < 0) exitBorder = Border.North;
        else exitBorder = Border.South;

        Vector2 neighCoord = GetNeighbourCoord(from.ChunkCoord, exitBorder);
        TerrainChunk neigh = GetChunkByCoord(neighCoord);
        if (neigh == null || neigh.MapData == null)
            return false;

        if (nx < 0) nx += w;
        else if (nx >= w) nx -= w;

        if (ny < 0) ny += h;
        else if (ny >= h) ny -= h;

        to = new CellKey(neighCoord, nx, ny);
        return true;
    }

    IEnumerable<CellKey> GetNeighbour8(CellKey c)
    {
        if (!TryGetCell(c, out TerrainChunk chunk, out FlowFieldData flow))
            yield break;

        int w = flow.Width;
        int h = flow.Height;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = c.X + dx;
                int ny = c.Y + dy;
                Vector2 nChunk = c.ChunkCoord;

                if (nx >= 0 && ny >= 0 && nx < w && ny < h)
                {
                    yield return new CellKey(nChunk, nx, ny);
                    continue;
                }

                Vector2 cc = nChunk;

                if (nx < 0)
                {
                    cc = GetNeighbourCoord(cc, Border.West);
                    nx += w;
                }
                else if (nx >= w)
                {
                    cc = GetNeighbourCoord(cc, Border.East);
                    nx -= w;
                }

                if (ny < 0)
                {
                    cc = GetNeighbourCoord(cc, Border.North);
                    ny += h;
                }
                else if (ny >= h)
                {
                    cc = GetNeighbourCoord(cc, Border.South);
                    ny -= h;
                }

                var ch = GetChunkByCoord(cc);
                if (ch == null || ch.MapData == null) continue;

                yield return new CellKey(cc, nx, ny);
            }
        }
    }

    bool FlowsInto(CellKey candidate, CellKey target)
    {
        if (!TryStep(candidate, out CellKey down))
            return false;
        return down.Equals(target);
    }

    IEnumerable<CellKey> GetUpstream(CellKey cell)
    {
        foreach (var n in GetNeighbour8(cell))
            if (FlowsInto(n, cell))
                yield return n;
    }

    IEnumerable<CellKey> GetBorderSeeds(TerrainChunk a, TerrainChunk b, Border border, int overlap)
    {
        var flow = a.MapData.FlowField;
        int w = flow.Width;
        int h = flow.Height;

        Vector2 aCoord = GetChunkVectorAtPosition(a.WorldPosition);
        Vector2 bCoord = GetChunkVectorAtPosition(b.WorldPosition);

        overlap = Mathf.Max(1, overlap);
        overlap = Mathf.Min(overlap, Mathf.Min(w, h));

        switch (border)
        {
            case Border.North:
                for (int x = 0; x < w; x++)
                {
                    for (int k = 0; k < overlap; k++)
                    {
                        yield return new CellKey(aCoord, x, k);
                        yield return new CellKey(bCoord, x, (h - overlap) + k);
                    }

                    yield return new CellKey(aCoord, x, overlap);
                    yield return new CellKey(bCoord, x, h - overlap - 1);
                }
                break;

            case Border.South:
                for (int x = 0; x < w; x++)
                {
                    for (int k = 0; k < overlap; k++)
                    {
                        yield return new CellKey(aCoord, x, (h - overlap) + k);
                        yield return new CellKey(bCoord, x, k);
                    }

                    yield return new CellKey(aCoord, x, h - overlap - 1);
                    yield return new CellKey(bCoord, x, overlap);
                }
                break;

            case Border.East:
                for (int y = 0; y < h; y++)
                {
                    for (int k = 0; k < overlap; k++)
                    {
                        yield return new CellKey(aCoord, (w - overlap) + k, y);
                        yield return new CellKey(bCoord, k, y);
                    }

                    yield return new CellKey(aCoord, w - overlap - 1, y);
                    yield return new CellKey(bCoord, overlap, y);
                }
                break;

            case Border.West:
                for (int y = 0; y < h; y++)
                {
                    for (int k = 0; k < overlap; k++)
                    {
                        yield return new CellKey(aCoord, k, y);
                        yield return new CellKey(bCoord, (w - overlap) + k, y);
                    }

                    yield return new CellKey(aCoord, overlap, y);
                    yield return new CellKey(bCoord, w - overlap - 1, y);
                }
                break;
        }
    }

    void WriteAccumulation(CellKey key, float value)
    {
        if (!TryGetCell(key, out TerrainChunk chunk, out FlowFieldData flow))
            return;

        chunk.MapData.FlowField.AccumulationMap[key.X, key.Y] = value;
    }

    float ReadAccumulation(CellKey key)
    {
        if (!TryGetCell(key, out TerrainChunk chunk, out FlowFieldData flow))
            return 1f;

        return chunk.MapData.FlowField.AccumulationMap[key.X, key.Y];
    }

    FlowDirection ReadDirection(CellKey key)
    {
        if (!TryGetCell(key, out TerrainChunk chunk, out FlowFieldData flow))
            return FlowDirection.Still;

        return chunk.MapData.FlowField.DirectionMap[key.X, key.Y];
    }


    void UpdateAccumulationFromChunk(TerrainChunk source, TerrainChunk neighbour, Border border, int overlap = 2)
    {
        if (source == null || neighbour == null || source.MapData == null || neighbour.MapData == null)
            return;

        overlap = Mathf.Max(1, overlap);

        var affected = new HashSet<CellKey>();
        var q = new Queue<CellKey>();

        foreach (var seed in GetBorderSeeds(source, neighbour, border, overlap))
            q.Enqueue(seed);

        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (affected.Contains(c))
                continue;

            if (!TryGetCell(c, out _, out _))
                continue;

            affected.Add(c);

            if (TryStep(c, out var down)) 
                q.Enqueue(down);

            foreach (var up in GetUpstream(c))
                q.Enqueue(up);
        }

        if (affected.Count == 0)
            return;

        var acc = new Dictionary<CellKey, float>(affected.Count);
        var indeg = new Dictionary<CellKey, int>(affected.Count);

        foreach (var c in affected)
        {
            acc[c] = 1f;
            indeg[c] = 0;
        }

        foreach (var c in affected)
        {
            if (ReadDirection(c) == FlowDirection.Still)
                continue;

            if (TryStep(c, out var down) && affected.Contains(down))
                indeg[down] = indeg[down] + 1;
        }

        var topo = new Queue<CellKey>();
        foreach (var c in affected)
            if (indeg[c] == 0)
                topo.Enqueue(c);

        while (topo.Count > 0)
        {
            var c = topo.Dequeue();

            if (ReadDirection(c) == FlowDirection.Still)
                continue;

            if (!TryStep(c, out var down))
                continue;

            if (!affected.Contains(down))
                continue;

            acc[down] += acc[c];

            indeg[down] = indeg[down] - 1;
            if (indeg[down] == 0)
                topo.Enqueue(down);
        }

        foreach (var c in affected)
            WriteAccumulation(c, acc[c]);
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