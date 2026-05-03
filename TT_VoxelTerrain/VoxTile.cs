using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TT_VoxelTerrain
{
    /// <summary>
    /// A Voxel tile that represents a cluster of VoxTerrain in relation to a WorldTile
    /// </summary>
    internal class VoxTile
    {
        private static Dictionary<IntVector2, VoxTile> Tiles = new Dictionary<IntVector2, VoxTile>();


        internal static void Add(Vector3 scenePos, VoxTerrain vox)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            vox.chunkCoord = WP.TileCoord;
            vox.inTilePos = WP.TileRelativePos;
            if (!Tiles.TryGetValue(WP.TileCoord, out var chunk))
            {
                chunk = new VoxTile(WP.TileCoord);
                Tiles.Add(WP.TileCoord, chunk);
            }
            chunk.AddVox(vox.inTilePos, vox);
        }
        internal static void Remove(Vector3 scenePos, VoxTerrain vox)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            if (Tiles.TryGetValue(vox.chunkCoord, out var chunk) &&
                chunk.RemoveVox(vox.inTilePos))
            {
                if (!chunk.blocks.Any())
                    Tiles.Remove(vox.chunkCoord);
                return;
            }
            DebugVoxel.Log("failed to get vox at [" + WP.TileCoord.x + ", " +
                WP.TileCoord.y + "], doing it the long way");
            if (Tiles.TryGetValue(vox.chunkCoord, out chunk))
            {
                chunk.RemoveVoxSLOW(vox);
                if (!chunk.blocks.Any())
                    Tiles.Remove(vox.chunkCoord);
            }
        }
        internal static VoxTerrain Lookup(Vector3 scenePos)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            if (Tiles.TryGetValue(WP.TileCoord, out var chunk))
                return chunk.LookupVox(WP.TileRelativePos);
            return null;
        }
        internal static VoxTerrain LookupOrCreate(Vector3 scenePos)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            if (!Tiles.TryGetValue(WP.TileCoord, out var chunk))
            {
                chunk = new VoxTile(WP.TileCoord);
                Tiles.Add(WP.TileCoord, chunk);
            }
            return chunk.LookupOrCreateVox(WP.TileRelativePos);
        }

        public readonly IntVector2 worldCoord;
        public Dictionary<IntVector3, VoxTerrain> blocks = new Dictionary<IntVector3, VoxTerrain>();
        public VoxTile(IntVector2 coordWorld)
        {
            worldCoord = coordWorld;
        }
        private void AddVox(Vector3 inPos, VoxTerrain vox) => blocks.Add(VoxelGlobals.ToIntVox(inPos), vox);
        private bool RemoveVox(Vector3 inPos) => blocks.Remove(VoxelGlobals.ToIntVox(inPos));
        private void RemoveVoxSLOW(VoxTerrain vox)
        {
            try
            {
                blocks.Remove(blocks.ElementAt(blocks.Values.ToList().IndexOf(vox)).Key);
            }
            catch
            {
                DebugVoxel.Assert("VoxelTerrain: fallback failed to remove vox from lookup!  We will now be out of sync!");
            }
        }
        private VoxTerrain LookupVox(Vector3 inPos)
        {
            blocks.TryGetValue(VoxelGlobals.ToIntVox(inPos), out VoxTerrain vox);
            return vox;
        }
        public VoxTerrain LookupOrCreateVox(Vector3 inPos)
        {
            IntVector3 pos = VoxelGlobals.ToIntVox(inPos);
            if (blocks.TryGetValue(pos, out VoxTerrain vox))
                return vox;
            vox = VoxGenerator.GenerateVoxPart(new WorldPosition(worldCoord, pos).ScenePosition);
            return vox;
        }
    }
}
