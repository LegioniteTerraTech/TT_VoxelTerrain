using System;
using System.Collections.Generic;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;
using static ModuleMeleeWeapon;


namespace TT_VoxelTerrain
{
    public static class Patches
    {
        [HarmonyPatch(typeof(GameCursor), "GetCursorState")]
        [HarmonyPriority(Priority.VeryLow)]
        private static class CursorPatches
        {
            /// <summary>
            /// See CursorChanger for more information
            /// </summary>
            /// <param name="__result"></param>
            internal static void Postfix(ref GameCursor.CursorState __result)
            {
                if (!CursorChanger.AddedNewCursors)
                    return;
                if (!ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked &&
                    ManModGUI.IsMouseOverModGUI && MassShifter.ARMED)
                {
                    switch (MassShifter.state)
                    {
                        case TerraformerCursorState.None:
                            break;
                        case TerraformerCursorState.Leveling:
                            __result = CursorChanger.CursorIndexCache[0];
                            break;
                        case TerraformerCursorState.Up:
                            __result = CursorChanger.CursorIndexCache[1];
                            break;
                        case TerraformerCursorState.Default:
                            __result = CursorChanger.CursorIndexCache[2];
                            break;
                        case TerraformerCursorState.Down:
                            __result = CursorChanger.CursorIndexCache[3];
                            break;
                        default:
                            break;
                    }
                }
            }

        }

        /// <summary>
        /// Prevent the game from trying to save this as it's using it's own saving system!
        /// </summary>
        [HarmonyPatch(typeof(WorldTile), "AddVisible")]
        private static class EnforceNotActuallyScenery
        {
            internal static bool Prefix(Visible visible)
            {
                return !visible.GetComponent<VoxTerrain>();
            }
        }

        [HarmonyPatch(typeof(ManSaveGame.StoredTile), "SetSceneryAwake")]
        private static class NoResdispBecauseNotActuallyScenery
        {
            internal static bool Prefix(Dictionary<int, Visible>[] visibles, bool awake)
            {
                foreach (Visible visible in visibles[3].Values)
                {
                    if (visible.resdisp != null)
                        visible.resdisp.SetAwake(awake);
                }
                return false;
            }
        }


        [HarmonyPatch(typeof(Visible), "MoveAboveGround")]
        private static class DenySurfTeleports
        {
            internal static void Prefix(ref Visible __instance)
            {
                if (__instance.type == ObjectTypes.Vehicle)
                {
                    return; //DENY THE TELEPORT TO SURFACE WHEN BUILDING!!!
                            // If we fall down into void then let KillVolumeCheck do the dirty work.
                }
            }
        }


        [HarmonyPatch(typeof(Visible), "OnPool")]
        private static class VisibleIsBeingStubborn
        {
            internal static void Prefix(ref Visible __instance)
            {
                if (__instance.GetComponent<VoxTerrain>() is VoxTerrain state21)
                    __instance.m_ItemType = state21.OurType;
            }
        }

        /*
        [HarmonyPatch(typeof(ManDamage), "DamageableType")]
        private static class ForceAddTerrainType
        {
            internal static void Prefix(ref ManDamage.DamageableType __instance)
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
            internal static void Prefix(ref Visible __instance)
            {
                if (__instance.GetComponent<VoxTerrain>() is VoxTerrain state21)
                    __instance.m_ItemType = state21.OurType;
            }
        }

        [HarmonyPatch(typeof(TileManager), "SetTileCache")]
        private static class PleaseStopRemovingMyChunks
        {
            internal static bool Prefix(Visible visible, WorldTile newTile, ref bool __result)
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

        /// <summary>
        /// Will need to change this so that it no longer crashes on startup!
        /// </summary>
        [HarmonyPatch(typeof(ManSaveGame.StoredTile), "StoreScenery")]
        private static class PleaseSaveMyChunks
        {
            internal static void Postfix(ref ManSaveGame.StoredTile __instance, Dictionary<int, Visible>[] visibles)
            {
                if (!ManVoxelTerrain.IsEnabled)
                    return;
                ManSaveGame.Storing = true;
                int i = 0;
                foreach (VoxTerrain vox in Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(__instance.coord, false).
                    StaticParent.GetComponentsInChildren<VoxTerrain>(true))//Visible visible in visibles[(int)TerrainGenerator.ObjectTypeVoxelChunk].Values)
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
            internal static void Prefix(ManSaveGame.StoredTile __instance)
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
            internal static bool Prefix(ref ManSaveGame.StoredVisible __result, Visible visible)
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
            internal static bool Prefix(Visible __instance)
            {
                return !__instance.GetComponent<VoxTerrain>();
            }
        }

        // Already accounted for in "ReplaceHeightGet"
        /*
        [HarmonyPatch(typeof(ManWorld), "TryProjectToGround",
            new Type[] { typeof(Vector3), typeof(Vector3), typeof(bool) },
            new ArgumentType[] { ArgumentType.Ref, ArgumentType.Out, ArgumentType.Normal })]
        [HarmonyPriority(9001)]
        private static class ReplaceHeightGetWorld
        {   //Will have to retrofit this to operate with scenery in mind
            internal static bool Prefix(ref float __result, ref Vector3 scenePos, ref Vector3 outNormal, out bool onTile, ref bool hitScenery)
            {
                onTile = true;
                int layerMask = Globals.inst.layerTerrain.mask;//VoxTerrain.VoxelTerrainOnlyLayer
                //int layerMask = hitScenery ? (Globals.inst.layerScenery.mask | Globals.inst.layerTerrain.mask) : (int)TerrainGenerator.TerrainOnlyLayer;
                if (Physics.Raycast(scenePos, Vector3.down, out RaycastHit raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)
                {
                    __result = scenePos.y - raycasthit.distance;
                    outNormal = raycasthit.normal;
                    return false;
                }
                if (Physics.Raycast(scenePos + Vector3.up * VoxTerrain.voxBlockResolution, Vector3.down, out raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)
                {
                    __result = scenePos.y + VoxTerrain.voxBlockResolution - raycasthit.distance;
                    outNormal = raycasthit.normal;
                    return false;
                }
                if (Physics.Raycast(scenePos + Vector3.up * 4096 + Vector3.one * 0.001f, Vector3.down, out raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)
                {
                    __result = scenePos.y + 4096.001f - raycasthit.distance;
                    outNormal = raycasthit.normal;
                    return false;
                }
                return true;
            }
        }
        */

        [HarmonyPatch(typeof(TileManager), "GetTerrainHeightAtPosition")]
        [HarmonyPriority(9001)]
        private static class ReplaceHeightGet
        {   //Will have to retrofit this to operate with scenery in mind
            internal static bool Prefix(ref float __result, ref Vector3 scenePos, out bool onTile, ref bool forceCalculate)
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
                if (Physics.Raycast(scenePos + Vector3.up * VoxelGlobals.voxBlockResolution, Vector3.down, out raycasthit, 8192, layerMask, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                {
                    __result = scenePos.y + VoxelGlobals.voxBlockResolution - raycasthit.distance;
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
            internal static bool Prefix(ref Vector3 __result, Vector3 position)
            {
                __result = Singleton.Manager<ManWorld>.inst.ProjectToGround(position, false) + Vector3.down * 10000;
                return false;
            }
        }
        */

        [HarmonyPatch(typeof(Tank), "HandleCollision")]
        private static class TankTerrainCollisionBypassPatch
        {
            private static Visible state1;
            private static VoxTerrain state2;
            //*
            internal static void Prefix(Tank __instance, Collision collisionData, bool stay)
            {
                var go = collisionData.GetContact(0).thisCollider;
                if (go?.transform.GetComponent<VoxTerrain>() is VoxTerrain state21)
                {
                    state1 = go.GetComponentInParent<Visible>();
                    if (state1)
                    {
                        state2 = state21;
                        state1.m_ItemType = VoxelGlobals.GetDamageableObjectType;
                        DebugVoxel.Info("oh yea vox");
                        return;
                    }
                }
                state1 = null;
            }
            internal static void Postfix()
            {
                if (state1 != null && state2 != null)
                {
                    state1.m_ItemType = state2.OurType;
                    DebugVoxel.Info("vox yea oh");
                }
            }//*/
        }
        [HarmonyPatch(typeof(ModuleMeleeWeapon), "HandleCollision")]
        private static class MeleeTerrainMining
        {
            private static Visible state1;
            private static VoxTerrain state2;
            //*
            internal static void Prefix(ModuleMeleeWeapon __instance, FrameCollisionInfo collisionInfo)
            {
                var go = collisionInfo.OtherCol;
                if (go?.transform.GetComponent<VoxTerrain>() is VoxTerrain state21)
                {
                    state1 = go.GetComponentInParent<Visible>();
                    if (state1)
                    {
                        state2 = state21;
                        state1.m_ItemType = VoxelGlobals.GetDamageableObjectType;
                        DebugVoxel.Info("oh yea vox");
                        return;
                    }
                }
                state1 = null;
            }
            internal static void Postfix()
            {
                if (state1 != null && state2 != null)
                {
                    state1.m_ItemType = state2.OurType;
                    DebugVoxel.Info("vox yea oh");
                }
            }//*/
        }
    }
}
