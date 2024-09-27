
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TT_VoxelTerrain
{
#if STEAM
    public class KickStartVoxelTerrain : ModBase
    {

        bool isInit = false;
        bool firstInit = false;
        public override bool HasEarlyInit()
        {
            DebugVoxel.Log("Voxel Terrain: CALLED");
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            DebugVoxel.Log("Voxel Terrain: CALLED EARLYINIT");
            if (isInit)
                return;
            try
            {
                TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Voxel Terrain", ManVoxelTerrain.Init);
            }
            catch { }
            isInit = true;
        }
        public override void Init()
        {
            DebugVoxel.Log("Voxel Terrain: CALLED INIT");
            if (isInit)
                return;
            try
            {
                TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Voxel Terrain", ManVoxelTerrain.Init);
            }
            catch { }
            isInit = true;
        }
        public override void DeInit()
        {
            if (!isInit)
                return;
            //isInit = false;
        }
    }
#endif

}
