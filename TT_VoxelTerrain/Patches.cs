using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using UnityEngine.Networking;


namespace TT_VoxelTerrain
{
    public static class Patches
    {
        [HarmonyPatch(typeof(WorldTile), "AddVisible")]
        private static class EnforceNotActuallyScenery
        {
            private static bool Prefix(Visible visible)
            {
                return !visible.GetComponent<VoxTerrain>();
            }
        }

        [HarmonyPatch(typeof(ManSaveGame.StoredTile), "SetSceneryAwake")]
        private static class NoResdispBecauseNotActuallyScenery
        {
            private static bool Prefix(Dictionary<int, Visible>[] visibles, bool awake)
            {
                foreach (Visible visible in visibles[3].Values)
                {
                    if (visible.resdisp != null)
                        visible.resdisp.SetAwake(awake);
                }
                return false;
            }
        }


        [HarmonyPatch(typeof(Visible), "OnPool")]
        private static class VisibleIsBeingStubborn
        {
            private static void Prefix(ref Visible __instance)
            {
                if (__instance.GetComponent<VoxTerrain>())
                {
                    __instance.m_ItemType = new ItemTypeInfo(VoxGenerator.ObjectTypeVoxelChunk, 0);
                }
            }
        }

        [HarmonyPatch(typeof(Visible), "MoveAboveGround")]
        private static class DenySurfTeleports
        {
            private static void Prefix(ref Visible __instance)
            {
                if (__instance.type == ObjectTypes.Vehicle)
                {
                    return; //DENY THE TELEPORT TO SURFACE WHEN BUILDING!!!
                            // If we fall down into void then let KillVolumeCheck do the dirty work.
                }
            }
        }


        /*
        [HarmonyPatch(typeof(ManDamage), "DamageableType")]
        private static class ForceAddTerrainType
        {
            private static void Prefix(ref ManDamage.DamageableType __instance)
            {
                //if (__instance.name == "VoxTerrainChunk")
                //{
                __instance = new (TerrainGenerator.ObjectTypeVoxelChunk, 0);
                //}
            }
        }
        */

        [HarmonyPatch(typeof(Visible), "OnSpawn")]
        private static class VisibleIsBeingReallyStubborn
        {
            private static void Prefix(ref Visible __instance)
            {
                if (__instance.GetComponent<VoxTerrain>())
                {
                    __instance.m_ItemType = new ItemTypeInfo(VoxGenerator.ObjectTypeVoxelChunk, 0);
                }
            }
        }

        [HarmonyPatch(typeof(TileManager), "SetTileCache")]
        private static class PleaseStopRemovingMyChunks
        {
            private static bool Prefix(Visible visible, WorldTile newTile, ref bool __result)
            {
                __result = false;
                if (visible.GetComponent<VoxTerrain>() && newTile != null)
                {
                    //newTile.AddVisible(visible);
                    visible.tileCache.tile = newTile;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ManSaveGame.StoredTile), "StoreScenery")]
        private static class PleaseSaveMyChunks
        {
            private static void Postfix(ref ManSaveGame.StoredTile __instance, Dictionary<int, Visible>[] visibles)
            {
                ManSaveGame.Storing = true;
                int i = 0;
                foreach (VoxTerrain vox in Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(__instance.coord, false).StaticParent.GetComponentsInChildren<VoxTerrain>(true))//Visible visible in visibles[(int)TerrainGenerator.ObjectTypeVoxelChunk].Values)
                {
                    Visible visible = vox.GetComponent<Visible>();
                    //if (visible.name == "VoxTerrainChunk")
                    //{
                    var store = new VoxTerrain.VoxelSaveData();
                    store.Store(visible);
                    if (store.Cloud64 == null) continue;
                    if (!__instance.m_StoredVisibles.ContainsKey(-8))
                    {
                        __instance.m_StoredVisibles.Add(-8, new List<ManSaveGame.StoredVisible>(100));
                    }
                    __instance.m_StoredVisibles[-8].Add(store);
                    i++;
                    //}
                }
                if (i != 0)
                    DebugVoxel.Log($"({DateTime.Now.ToString()}) {i} unique vox terrain saved");
                ManSaveGame.Storing = false;
            }
        }

        [HarmonyPatch(typeof(ManSaveGame.StoredTile), "RestoreVisibles")]
        private static class PleaseLoadMyChunks
        {
            private static void Prefix(ManSaveGame.StoredTile __instance)
            {
                if (__instance.m_StoredVisibles.TryGetValue(-8, out List<ManSaveGame.StoredVisible> voxlist))
                {
                    //    for (int j = 0; j < voxlist.Count; j++)
                    //    {
                    //        ManSaveGame.StoredVisible storedVisible = voxlist[j];
                    //        ManSaveGame.RestoreOrDeferLoadingVisible(storedVisible, __instance.coord);
                    //    }
                    if (!ManSaveGame.k_RestoreOrder.Contains(-8))
                    {
                        ManSaveGame.k_RestoreOrder.Insert(0, -8);
                        if (!ManSaveGame.k_RestoreOrder.Contains(-8))
                        {
                            DebugVoxel.LogError("gues i'l die");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ManSaveGame), "CreateStoredVisible")]
        private static class SaveVoxChunks
        {
            private static bool Prefix(ref ManSaveGame.StoredVisible __result, Visible visible)
            {
                if (visible.GetComponent<VoxTerrain>())
                {
                    var result = new VoxTerrain.VoxelSaveData();
                    result.Store(visible);
                    __result = result;
                    //DebugVoxel.Log(result.Cloud64);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Visible), "get_rbody")]
        private static class SupressPhysics
        {
            private static bool Prefix(Visible __instance)
            {
                return !__instance.GetComponent<VoxTerrain>();
            }
        }

        [HarmonyPatch(typeof(TileManager), "GetTerrainHeightAtPosition")]
        [HarmonyPriority(9001)]
        private static class ReplaceHeightGet
        {   //Will have to retrofit this to operate with scenery in mind
            private static bool Prefix(ref float __result, ref Vector3 scenePos, out bool onTile, ref bool forceCalculate)
            {
                onTile = true;
                //DebugVoxel.Assert("Heigg=htget");
                int layerMask = Globals.inst.layerTerrain.mask;//VoxTerrain.VoxelTerrainOnlyLayer
                //int layerMask = hitScenery ? (Globals.inst.layerScenery.mask | Globals.inst.layerTerrain.mask) : (int)TerrainGenerator.TerrainOnlyLayer;
                if (Physics.Raycast(scenePos, Vector3.down, out RaycastHit raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                {
                    __result = scenePos.y - raycasthit.distance;
                    return false;
                }
                if (Physics.Raycast(scenePos + Vector3.up * VoxTerrain.voxBlockResolution, Vector3.down, out raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                {
                    __result = scenePos.y + VoxTerrain.voxBlockResolution - raycasthit.distance;
                    return false;
                }
                if (Physics.Raycast(scenePos + Vector3.up * 4096 + Vector3.one * 0.001f, Vector3.down, out raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                {
                    __result = scenePos.y + 4096.001f - raycasthit.distance;
                    return false;
                }
                return true;
            }
        }

        /*
        [HarmonyPatch(typeof(TankCamera), "GroundProjectAllowingForGameMode")]
        private static class ReplaceTankCameraGroundProject//Function missing!
        {
            private static bool Prefix(ref Vector3 __result, Vector3 position)
            {
                __result = Singleton.Manager<ManWorld>.inst.ProjectToGround(position, false) + Vector3.down * 10000;
                return false;
            }
        }
        */

        [HarmonyPatch(typeof(Tank), "HandleCollision")]
        private static class TerrainCollisionBypassPatch
        {
            private static void Prefix(Tank __instance, Collision collisionData, bool stay, ref Visible __state)
            {
                var go = collisionData.GetContact(0).thisCollider;
                if (go.transform.parent.GetComponent<VoxTerrain>())
                {
                    var V = go.GetComponentInParent<Visible>();
                    if (V)
                    {
                        V.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, 0);
                        __state = V;
                        DebugVoxel.Log("oh yea vox");
                        return;
                    }
                }
                __state = null;
            }
            private static void Postfix(ref Visible __state)
            {
                if (__state)
                {
                    __state.m_ItemType = new ItemTypeInfo(VoxGenerator.ObjectTypeVoxelChunk, 0);
                     DebugVoxel.Log("vox yea oh");
                }
            }
        }
    }
}
