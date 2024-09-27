using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TT_VoxelTerrain;
using UnityEngine;

// Original source: http://paulbourke.net/geometry/polygonise/marchingsource.cpp

public struct CloudPair
{
    public sbyte Density;
    public byte Terrain;
    public float DensityFloat => (Density - 0.5f) / 128f;

    public CloudPair(sbyte Density, byte Terrain)
    {
        this.Density = Density;
        this.Terrain = Terrain;
    }

    public CloudPair(float Density, byte Terrain)
    {
        this.Density = (sbyte)Mathf.Clamp(Density * 128f + 0.5f, -128, 127);
        this.Terrain = Terrain;
    }

    public CloudPair AddDensityAndSeepTerrain(float Change, byte Terrain)
    {
        return new CloudPair((Density - 0.5f) / 128f + Change, this.Density <= 0 ? Terrain : this.Terrain);
    }
    /// <summary>
    /// Was AddDensity
    /// </summary>
    public CloudPair SubDensity(float Change)
    {
        return new CloudPair((Density - 0.5f) / 128f + Change, Terrain);
    }
}

public class MarchingCubes
{
    public struct ReadPair
    {
        public ReadPair(float Height, byte TopTerrain, byte AltTerrain)
        {
            this.Height = Height;
            this.TopTerrain = TopTerrain;
            this.AltTerrain = AltTerrain;
        }
        public float Height;
        public byte TopTerrain;
        public byte AltTerrain;
    }

    internal static Vector3 IV3MF(IntVector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);

    public List<Vector3> _vertices = new List<Vector3>();
    public List<Vector2> _uvs = new List<Vector2>();
    public Dictionary<byte, List<int>> _indices = new Dictionary<byte, List<int>>();
    public Dictionary<Vector3, List<int>> edgeIndices = new Dictionary<Vector3, List<int>>();
    int _currentIndex = 0;
    // outside the function for better performance
    CloudPair[] caCubeValue = new CloudPair[8];
    //byte[] caCubeTerra = new byte[8];
    HashSet<byte> caCubeTerra = new HashSet<byte>();

    public delegate ReadPair SampleDelegate(Vector2 position);
    public SampleDelegate sampleProc;

    // set to true for smoother mesh
    public bool interpolate = false;

    public static float DefaultSampleHeight = 100;

    public MarchingCubes()
    {
        sampleProc = (Vector2 p) => { return new ReadPair(DefaultSampleHeight, 127, 128); };
        edgeIndices.Add(Vector3.up, new List<int>());
        edgeIndices.Add(Vector3.down, new List<int>());
        edgeIndices.Add(Vector3.left, new List<int>());
        edgeIndices.Add(Vector3.right, new List<int>());
        edgeIndices.Add(Vector3.forward, new List<int>());
        edgeIndices.Add(Vector3.back, new List<int>());
    }

    public void PartialReset()
    {
        foreach (var item in _indices)
        {
            item.Value.Clear();
            _listCache.Push(item.Value);
        }
        _indices.Clear();
        _uvs.Clear();
    }
    public void FullReset()
    {
        PartialReset();
        _vertices.Clear();
        indicesCache.Clear();
        //_colors.Clear();
        _currentIndex = 0;
        foreach (var item in edgeIndices)
        {
            item.Value.Clear();
        }
    }

    public List<Vector3> GetVertices() => _vertices;

    public List<int> indicesCache = new List<int>();
    public List<int> GetIndices()
    {
        if (indicesCache.Any())
            return indicesCache;
        int count = 0;
        foreach (var i in _indices)
        {
            count += i.Value.Count;
        }
        indicesCache.Clear();
        foreach (var i in _indices)
        {
            for (int j = 0; j < i.Value.Count; j++)
            {
                indicesCache.Add(i.Value[j]);
            }
        }
        return indicesCache;
    }

    //public Color32[] GetColors()
    //{
    //    return _colors.ToArray();
    //}

    internal const float heightDefaultOffset = -50;
    internal const float heightDefault = 100;
    internal static float heightSize = -200;
    internal static float heightScale => heightSize / heightDefault;
    internal static float TileYOffsetDelta => (heightDefaultOffset * heightScale) - heightDefaultOffset;

    static Dictionary<Biome, byte> BiomeMapInvLookup => ManVoxelTerrain.BiomeMapInvLookup;
    const int CellsInTileIndexer = 129;
    static float tilePosToTileScaleG => ManWorld.inst.TileSize / (Mathf.Max(1, 2 >> QualitySettingsExtended.ReducedHeightmapDetail) * ManWorld.inst.CellsPerTileEdge);
    private static float[,] heightsCached = new float[CellsInTileIndexer, CellsInTileIndexer];
    internal static float[,] GetRealHeights(WorldTile tile)
    {
        float tilePosToTileScale = tilePosToTileScaleG;
        float height = tile.Terrain.terrainData.size.y;
        Vector2 delta = tile.WorldOrigin.ToVector2XZ();
        Terrain terra = tile.Terrain;
        for (int x = 0; x < CellsInTileIndexer; x++)
            for (int y = 0; y < CellsInTileIndexer; y++)
                heightsCached[x, y] = terra.SampleHeight((delta + new Vector2(x * tilePosToTileScale, y * tilePosToTileScale)
                    ).ToVector3XZ()) / height;
        return heightsCached;
    }

    /// <summary>
    /// This SETS the terrain heights INSIDE THE TILE
    /// </summary>
    public static void SetBufferFromTerrain(CloudPair[,,] sampleBuffer, float[,] terrainDataFast, WorldTile tile, Vector3 sceneOffset, 
        int size, float scale, out int CountBelow, out int CountAbove)
    {
        CountBelow = 0; CountAbove = 0;
        //TerrainData terrainData = null;
        int sizep1 = size + 1;
        float tileSize = ManWorld.inst.TileSize;
        int xTileOffset = (int)(sceneOffset.x / scale);
        int yTileOffset = (int)(sceneOffset.z / scale);
        float sizeScale = size * scale;
        float deltaScale = scale * 2f;//1.5f;// 0.75f// - looks cool
        // So what we do here is that we iterate each voxelable position and then obtain the heights for each,
        //   filling each voxel entirely that is below our height until we hit the top.
        for (int xVox = 0; xVox < sizep1; xVox++)
        {
            for (int zVox = 0; zVox < sizep1; zVox++)
            {
                try
                {
                    byte biomeID = 20;

                    int xTile = xVox + xTileOffset;
                    int yTile = zVox + yTileOffset;
                    int xTileInv = (xVox - size) + xTileOffset;
                    int yTileInv = (zVox - size) + yTileOffset;

                    // The new WorldTiles after the SetPieces update have x and y switched for some reason...
                    //   the reason for this change absolutely eludes me
                    //float heightTile = terrainData.GetHeight(xTile * 2, yTile * 2);
                    float heightTile = terrainDataFast[yTile * 2, xTile * 2] * heightSize;
                    int cb = Mathf.FloorToInt((heightTile - sceneOffset.y - scale) / sizeScale);
                    int ca = Mathf.CeilToInt((heightTile - sceneOffset.y + scale) / sizeScale);
                    if (cb < CountBelow)
                        CountBelow = cb;
                    if (ca > CountAbove)
                        CountAbove = ca;
                    //if (x % 2 == 1)
                    //{
                    //    proc += data.GetHeight(x / 2 + 1, z / 2);
                    //    if (z % 2 == 1)
                    //    {
                    //        proc += data.GetHeight(x / 2, z / 2 + 1) + data.GetHeight(x / 2 + 1, z / 2 + 1);
                    //        proc /= 4f;
                    //    }
                    //    else
                    //    {
                    //        proc /= 2f;
                    //    }
                    //}
                    //else if (z % 2 == 1)
                    //{
                    //    proc += data.GetHeight(x / 2, z / 2 + 1);
                    //    proc /= 2f;
                    //}

                    //var proc = data.GetInterpolatedHeight((float)i / (size * subCount) + offset.x / tileSize, (float)k / (size * subCount) + offset.z / tileSize);

                    //Another point of failiure
                    int error = 0;
                    try
                    {
                        if (1 == 0 && TT_VoxelTerrain.ManVoxelTerrain.isBiomeInjectorPresent)
                        {
                            error = 99;
                        }
                        else
                        {
                            // The new WorldTiles after the SetPieces update have x and y switched for some reason...
                            //   the reason for this change absolutely eludes me
                            var bc = new ManWorld.CachedBiomeBlendWeights(tile.BiomeMapData.cells[xTile, yTile]);
                            error = 1;
                            Biome heaviestBiome = null;
                            float h = 0f;
                            for (int m = 0; m < bc.NumWeights; m++)
                            {
                                Biome biome = bc.Biome(m);
                                float weight = bc.Weight(m);
                                error++;
                                if (biome != null && weight > h)
                                {
                                    h = weight;
                                    heaviestBiome = biome;
                                }
                            }
                            error = 2000;
                            if (heaviestBiome != null)
                            {
                                error = 10000;
                                biomeID = (byte)(BiomeMapInvLookup[heaviestBiome] * 2);
                            }
                            error = 13000;

                            for (int yVox = 0; yVox < sizep1; yVox++)
                            {
                                float d = heightTile - (yVox * scale + sceneOffset.y);
                                error++;
                                sampleBuffer[xVox, yVox, zVox] = new CloudPair(d / scale, Mathf.Abs(d) > deltaScale ? (byte)(biomeID + 1) : biomeID);
                            }
                            error = 14000;
                        }
                    }
                    catch (Exception E)
                    {
                        //DebugVoxel.Log($"Could not properly handle tile information at [{xVox}, {zVox}], tile coord [{xTile}, {yTile}] " +
                        //    $" case {error.ToString()} - {E.ToString()}");
                    }

                }
                catch (Exception E)
                {
                    //DebugVoxel.Log($"Critical error on handled tile!  at {xVox}, {zVox}   {E.ToString()}");
                }
            }
        }
    }

    public void SetBuffer(CloudPair[,,] sampleBuffer, Vector3 origin, int size, float scale)
    {
        int sizep1 = size + 1;
        for (int i = 0; i < sizep1; i++)
            for (int k = 0; k < sizep1; k++)
            {
                IntVector2 offset = new Vector2(i * scale + origin.x, k * scale + origin.z);
                var proc = sampleProc(offset);
                for (int j = 0; j < sizep1; j++)
                {
                    var d = proc.Height - j * scale - origin.y;
                    sampleBuffer[i, j, k] = new CloudPair(d / scale, Mathf.Abs(d) > scale*1.5f ? proc.AltTerrain : proc.TopTerrain);
                }
            }
    }
    public static CloudPair[,,] CreateNewBuffer(int size)
    {
        int sizep1 = size + 1;
        return new CloudPair[sizep1, sizep1, sizep1];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="origin">World space origin position</param>
    /// <param name="size">Buffer size</param>
    /// <param name="scale">Size of each voxel</param>
    /// <param name="_sampleBuffer"></param>
    /// <returns></returns>
    public void MarchChunk(IntVector3 origin, int size, float scale, float scaleUV, CloudPair[,,] sampleBuffer)
    {
        FullReset();
        int flagIndex;
        
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                for (int k = 0; k < size; k++)
                {
                    caCubeValue[0] = sampleBuffer[i, j, k];
                    caCubeValue[1] = sampleBuffer[i + 1, j, k];
                    caCubeValue[2] = sampleBuffer[i + 1, j + 1, k];
                    caCubeValue[3] = sampleBuffer[i, j + 1, k];
                    caCubeValue[4] = sampleBuffer[i, j, k + 1];
                    caCubeValue[5] = sampleBuffer[i + 1, j, k + 1];
                    caCubeValue[6] = sampleBuffer[i + 1, j + 1, k + 1];
                    caCubeValue[7] = sampleBuffer[i, j + 1, k + 1];

                    caCubeTerra.Clear();
                    caCubeTerra.Add(caCubeValue[0].Terrain);
                    caCubeTerra.Add(caCubeValue[1].Terrain);
                    caCubeTerra.Add(caCubeValue[2].Terrain);
                    caCubeTerra.Add(caCubeValue[3].Terrain);
                    caCubeTerra.Add(caCubeValue[4].Terrain);
                    caCubeTerra.Add(caCubeValue[5].Terrain);
                    caCubeTerra.Add(caCubeValue[6].Terrain);
                    caCubeTerra.Add(caCubeValue[7].Terrain);

                    foreach (byte currentTerrain in caCubeTerra)
                    {
                        // corner bitfield
                        flagIndex = 0;
                        for (int vtest = 0; vtest < 8; vtest++)
                        {
                            if (caCubeValue[vtest].Density <= 0/* || caCubeValue[vtest].Terrain != currentTerrain*/)
                                flagIndex |= 1 << vtest;
                        }

                        // early out if all corners same
                        if (flagIndex == 0x00 || flagIndex == 0xFF)
                            continue;

                        // voxel world offset
                        Vector3 offset = new Vector3(i * scale, j * scale, k * scale);
                        // generate triangles
                        for (int tri = 0; tri < 5; tri++)
                        {
                            int edgeIndex = a2iTriangleConnectionTable[flagIndex, 3 * tri];
                            if (edgeIndex < 0)
                                break;

                            byte t = sampleBuffer[i, j, k].Terrain;
                            for (int triCorner = 0; triCorner < 3; triCorner++)
                            {
                                edgeIndex = a2iTriangleConnectionTable[flagIndex, 3 * tri + triCorner];

                                IntVector3 edge1I = edgeVertexOffsets[edgeIndex, 0];
                                IntVector3 edge2I = edgeVertexOffsets[edgeIndex, 1];
                                Vector3 edge1 = IV3MF(edge1I, scale);
                                Vector3 edge2 = IV3MF(edge2I, scale);

                                Vector3 middlePoint;

                                CloudPair p1 = sampleBuffer[i + edge1I.x, j + edge1I.y, k + edge1I.z], p2 = sampleBuffer[i + edge2I.x, j + edge2I.y, k + edge2I.z];

                                float ofst = 0.5f;
                                float s1, delta, cofst = 0f;

                                if (p1.Terrain == currentTerrain)
                                {
                                    s1 = p1.Density;
                                }
                                else
                                {
                                    s1 = p1.Density;//Mathf.Min(p1.Density, 0);
                                    cofst += 0.25f;
                                }

                                if (p2.Terrain == currentTerrain)
                                {
                                    delta = s1 - p2.Density;
                                }
                                else
                                {
                                    delta = s1 - p2.Density;//Mathf.Min(p2.Density, 0);
                                    cofst -= 0.25f;
                                }

                                if (delta != 0.0f)
                                    ofst = s1 / delta;
                                middlePoint = edge1 + (ofst + cofst) * (edge2 - edge1);

                                _vertices.Add(offset + middlePoint);
                                _uvs.Add(new Vector2(
                                    ((middlePoint.x / scaleUV) + i +
                                    Mathf.Sin((((middlePoint.y / scaleUV) + j) / size) * Mathf.Deg2Rad * 720f)) / 
                                    size, 
                                    ((middlePoint.z / scaleUV) + k + 
                                    Mathf.Cos((((middlePoint.y / scaleUV) + j) / size) * Mathf.Deg2Rad * 720f)) /
                                    size
                                    ));
                                if (i == 0)
                                    edgeIndices[Vector3.left].Add(_currentIndex);
                                else if (i == size - 1)
                                    edgeIndices[Vector3.right].Add(_currentIndex);
                                if (j == 0)
                                    edgeIndices[Vector3.down].Add(_currentIndex);
                                else if (j == size - 1)
                                    edgeIndices[Vector3.up].Add(_currentIndex);
                                if (k == 0)
                                    edgeIndices[Vector3.back].Add(_currentIndex);
                                else if (k == size - 1)
                                    edgeIndices[Vector3.forward].Add(_currentIndex);
                                /*
                                if (i == 0)
                                    AddToIndices(126, _currentIndex++);
                                else if (k == 0 && i % 2 == 1)
                                    AddToIndices(127, _currentIndex++);
                                else // */
                                    AddToIndices(currentTerrain, _currentIndex++);
                            }
                        }
                    }
                }
        //return sampleBuffer;
    }

    private Stack<List<int>> _listCache = new Stack<List<int>>();
    private void AddToIndices(byte Type, int Value)
    {
        if (!_indices.ContainsKey(Type))
        {
            if (_listCache.Any())
                _indices.Add(Type, _listCache.Pop());
            else
                _indices.Add(Type, new List<int>());
        }
        _indices[Type].Add(Value);
    }

    // offsets from the minimal corner to other corners
    static readonly IntVector3[] cornerOffsets = new IntVector3[8]
    {
        new IntVector3(0, 0, 0),
        new IntVector3(1, 0, 0),
        new IntVector3(1, 1, 0),
        new IntVector3(0, 1, 0),
        new IntVector3(0, 0, 1),
        new IntVector3(1, 0, 1),
        new IntVector3(1, 1, 1),
        new IntVector3(0, 1, 1)
    };

    // offsets from the minimal corner to 2 ends of the edges
    static readonly IntVector3[,] edgeVertexOffsets = new IntVector3[12, 2]
    {
        { new IntVector3(0, 0, 0), new IntVector3(1, 0, 0) },
        { new IntVector3(1, 0, 0), new IntVector3(1, 1, 0) },
        { new IntVector3(0, 1, 0), new IntVector3(1, 1, 0) },
        { new IntVector3(0, 0, 0), new IntVector3(0, 1, 0) },
        { new IntVector3(0, 0, 1), new IntVector3(1, 0, 1) },
        { new IntVector3(1, 0, 1), new IntVector3(1, 1, 1) },
        { new IntVector3(0, 1, 1), new IntVector3(1, 1, 1) },
        { new IntVector3(0, 0, 1), new IntVector3(0, 1, 1) },
        { new IntVector3(0, 0, 0), new IntVector3(0, 0, 1) },
        { new IntVector3(1, 0, 0), new IntVector3(1, 0, 1) },
        { new IntVector3(1, 1, 0), new IntVector3(1, 1, 1) },
        { new IntVector3(0, 1, 0), new IntVector3(0, 1, 1) }
    };

    static readonly int[,] a2iTriangleConnectionTable = new int[,]
    {
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
        {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
        {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
        {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
        {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
        {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
        {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
        {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
        {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
        {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
        {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
        {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
        {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
        {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
        {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
        {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
        {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
        {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
        {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
        {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
        {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
        {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
        {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
        {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
        {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
        {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
        {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
        {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
        {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
        {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
        {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
        {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
        {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
        {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
        {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
        {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
        {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
        {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
        {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
        {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
        {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
        {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
        {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
        {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
        {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
        {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
        {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
        {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
        {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
        {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
        {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
        {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
        {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
        {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
        {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
        {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
        {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
        {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
        {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
        {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
        {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
        {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
        {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
        {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
        {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
        {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
        {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
        {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
        {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
        {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
        {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
        {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
        {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
        {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
        {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
        {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
        {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
        {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
        {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
        {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
        {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
        {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
        {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
        {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
        {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
        {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
        {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
        {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
        {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
        {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
        {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
        {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
        {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
        {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
        {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
        {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
        {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
    };
}