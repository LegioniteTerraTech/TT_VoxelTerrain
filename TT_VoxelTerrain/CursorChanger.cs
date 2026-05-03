using System;
using System.Collections.Generic;
using TerraTechETCUtil;
using UnityEngine;

namespace TT_VoxelTerrain
{
    public enum TerraformerCursorState
    {
        None,
        Leveling,
        Up,
        Default,
        Down
    }
    public class CursorChanger : MonoBehaviour
    {
        public static CursorChangeHelper.CursorChangeCache Cache;
        public static bool AddedNewCursors = false;
        public static CursorChangeHelper.CursorChangeCache CursorIndexCache => Cache.CursorIndexCache;

        public static void AddNewCursors()
        {
            if (AddedNewCursors)
                return;
            if (ResourcesHelper.TryGetModContainer("Voxel Terrain", out ModContainer MC))
            {
                Cache = CursorChangeHelper.GetCursorChangeCache("Voxel Terrain", "Terraformer_Icons", MC,
                    new KeyValuePair<string, bool>("TerrainToolLevel", false),
                    new KeyValuePair<string, bool>("TerrainToolUp", false),
                    new KeyValuePair<string, bool>("TerrainToolDefault", false),
                    new KeyValuePair<string, bool>("TerrainToolDown", false)
                    );
            }
            else
                DebugVoxel.Assert(true, "CursorChanger: AddNewCursors - Could not find ModContainer for Voxel Terrain!");

            AddedNewCursors = true;
        }
    }

}
