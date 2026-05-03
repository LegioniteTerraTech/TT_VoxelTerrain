using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TT_VoxelTerrain
{
    public static class VoxelGlobals
    {
        public static LayerMask VoxelTerrainOnlyLayer = LayerMask.GetMask(LayerMask.LayerToName(Globals.inst.layerTerrain));
        /// <summary>
        /// Resolution of each voxel block.  This determines how many CloudPairs are within.
        /// </summary>
        public const float voxBlockResolution = 6;
        //3.0f; //! Half of a terrain vertex scale
        //public const float voxelSize = 8; //3.0f; //! Half of a terrain vertex scale

        /// <summary>
        /// Number of chunks to subdivide the terrain in to.  Higher number means finer accuracy
        /// </summary>
        internal const int voxChunksPerTile = 4;//8;
        // 4 - safest for fastest loading speed

        /// <summary>
        /// Size of each voxel.
        /// AUTO-SET in <see cref="FirstSetup"/>
        /// </summary>
        public static int voxBlockSize = -1;
        /// <summary>
        /// Size of a chunk in world meter units.
        /// AUTO-SET in <see cref="FirstSetup"/>
        /// </summary>
        public static int voxChunkSize = -1;
        /// <summary>
        /// The center of each voxel.
        /// AUTO-SET in <see cref="FirstSetup"/>
        /// </summary>
        public static Vector3 voxBlockCenterOffset = Vector3.zero;

        /// <summary>
        /// AUTO-SET in <see cref="FirstSetup"/>
        /// </summary>
        public static int BleedWrap = 2;

        public static ItemTypeInfo GetNewVoxelTypeInfo => new ItemTypeInfo(VoxGenerator.ObjectTypeVoxelChunk, 0);
        public static ItemTypeInfo GetDamageableObjectType = new ItemTypeInfo(ObjectTypes.Scenery, 0);

        internal static void FirstSetup()
        {
            voxChunkSize = Mathf.RoundToInt(ManWorld.inst.TileSize) / voxChunksPerTile;
            voxBlockSize = Mathf.RoundToInt(voxChunkSize / voxBlockResolution);
            voxBlockCenterOffset = Vector3.one * (voxChunkSize / 2f);
            BleedWrap = Mathf.RoundToInt(voxChunkSize / voxBlockResolution);
        }


        public static IntVector3 ToIntVox(Vector3 inPos)
        {
            return new IntVector3(
                Mathf.FloorToInt((inPos.x + 0.01f) / voxChunkSize) * voxChunkSize,
                Mathf.FloorToInt((inPos.y + 0.01f) / voxChunkSize) * voxChunkSize,
                Mathf.FloorToInt((inPos.z + 0.01f) / voxChunkSize) * voxChunkSize
                );
        }

        public static Vector3 GetVoxCenter(Vector3 ScenePos)
        {
            return new Vector3(Mathf.Floor(ScenePos.x / voxBlockSize) * voxBlockSize,
                Mathf.Floor(ScenePos.y / voxBlockSize) * voxBlockSize,
                Mathf.Floor(ScenePos.z / voxBlockSize) * voxBlockSize) + voxBlockCenterOffset;
        }
        public static bool Within(Vector3 min, Vector3 ScenePosTarget, float size)
        {
            Vector3 max = min + (Vector3.one * size);
            return min.x <= ScenePosTarget.x && max.x >= ScenePosTarget.x &&
                min.y <= ScenePosTarget.y && max.y >= ScenePosTarget.y &&
                min.z <= ScenePosTarget.z && max.z >= ScenePosTarget.z;
        }
        public static bool WithinVox(Vector3 ScenePos, Vector3 ScenePosTarget)
        {
            float minx = Mathf.Floor(ScenePos.x / voxBlockSize) * voxBlockSize;
            float miny = Mathf.Floor(ScenePos.y / voxBlockSize) * voxBlockSize;
            float minz = Mathf.Floor(ScenePos.z / voxBlockSize) * voxBlockSize;
            float maxx = minx + voxBlockSize;
            float maxy = miny + voxBlockSize;
            float maxz = minz + voxBlockSize;
            return minx <= ScenePosTarget.x && maxx >= ScenePosTarget.x &&
                miny <= ScenePosTarget.y && maxy >= ScenePosTarget.y &&
                minz <= ScenePosTarget.z && maxz >= ScenePosTarget.z;
        }
        public static bool WithinVoxWithRadius(Vector3 ScenePos, Vector3 ScenePosTarget, float estRadius)
        {
            float minx = Mathf.Floor(ScenePos.x / voxBlockSize) * voxBlockSize;
            float miny = Mathf.Floor(ScenePos.y / voxBlockSize) * voxBlockSize;
            float minz = Mathf.Floor(ScenePos.z / voxBlockSize) * voxBlockSize;
            float maxx = minx + voxBlockSize;
            float maxy = miny + voxBlockSize;
            float maxz = minz + voxBlockSize;
            return minx - estRadius <= ScenePosTarget.x && maxx + estRadius >= ScenePosTarget.x &&
                miny - estRadius <= ScenePosTarget.y && maxy + estRadius >= ScenePosTarget.y &&
                minz - estRadius <= ScenePosTarget.z && maxz + estRadius >= ScenePosTarget.z;
        }
        public static bool WithinVoxByOrigin(Vector3 voxOrigin, Vector3 ScenePosTarget)
        {
            Vector3 max = voxOrigin + (Vector3.one * voxBlockSize);
            return voxOrigin.x <= ScenePosTarget.x && max.x >= ScenePosTarget.x &&
                voxOrigin.y <= ScenePosTarget.y && max.y >= ScenePosTarget.y &&
                voxOrigin.z <= ScenePosTarget.z && max.z >= ScenePosTarget.z;
        }
        public static bool WithinVoxVertical(Vector3 ScenePos, Vector3 ScenePosTarget)
        {
            float minx = Mathf.Floor(ScenePos.x / voxBlockSize) * voxBlockSize;
            float minz = Mathf.Floor(ScenePos.z / voxBlockSize) * voxBlockSize;
            float maxx = minx + voxBlockSize;
            float maxz = minz + voxBlockSize;
            return minx <= ScenePosTarget.x && maxx >= ScenePosTarget.x &&
                minz <= ScenePosTarget.z && maxz >= ScenePosTarget.z;
        }
        public static bool WithinVoxVerticalWithRadius(Vector3 ScenePos, Vector3 ScenePosTarget, float estRadius)
        {
            float minx = Mathf.Floor(ScenePos.x / voxBlockSize) * voxBlockSize;
            float minz = Mathf.Floor(ScenePos.z / voxBlockSize) * voxBlockSize;
            float maxx = minx + voxBlockSize;
            float maxz = minz + voxBlockSize;
            return minx - estRadius <= ScenePosTarget.x && maxx + estRadius >= ScenePosTarget.x &&
                minz - estRadius <= ScenePosTarget.z && maxz + estRadius >= ScenePosTarget.z;
        }

    }
}
