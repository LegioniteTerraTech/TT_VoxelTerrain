using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine;
using System.Reflection;
using UnityEngine.Networking;

namespace TT_VoxelTerrain
{
    public class Class1
    {
        //Detection variables
        public static bool isBiomeInjectorPresent = false; 


        //Networking
        const TTMsgType VoxBrushMsg = (TTMsgType)4326;



        public static void Init()
        {
            HarmonyInstance.Create("aceba1.betterterrain").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            new GameObject().AddComponent<MassShifter>();
            Nuterra.NetHandler.Subscribe<VoxBrushMessage>(VoxBrushMsg, ReceiveVoxBrush, PromptNewVoxBrush);

            if (LookForMod("Nuterra.Biomes"))
            {
                Debug.Log("VoxelTerrain: Found Biome Injector!  Attempting hookups!");
                isBiomeInjectorPresent = true;
            }
        }

        public static bool LookForMod(string name)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith(name))
                {
                    return true;
                }
            }
            return false;
        }

        public static void SendVoxBrush(VoxBrushMessage message)
        {
            if (ManNetwork.IsHost)
            {
                Nuterra.NetHandler.BroadcastMessageToAllExcept(VoxBrushMsg, message, true);
                return;
            }
            Nuterra.NetHandler.BroadcastMessageToServer(VoxBrushMsg, message);
        }

        private static void PromptNewVoxBrush(VoxBrushMessage obj, NetworkMessage netmsg)
        {
            Nuterra.NetHandler.BroadcastMessageToAllExcept(VoxBrushMsg, obj, true, netmsg.conn.connectionId);
            ReceiveVoxBrush(obj, netmsg);
        }

        private static void ReceiveVoxBrush(VoxBrushMessage obj, NetworkMessage netmsg)
        {
            var Tiles = Physics.OverlapSphere(obj.Position, TerrainGenerator.ChunkSize / 4, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Collide);
            foreach (var Tile in Tiles)
            {
                var Vox = Tile.GetComponent<TerrainGenerator.VoxTerrain>();
                if (Vox != null)
                {
                    Vox.BleedBrushModifyBuffer(obj.Position, obj.Radius, obj.Strength, Vector3.zero, obj.Terrain);
                    return;
                }
            }
        }

        public class VoxBrushMessage : UnityEngine.Networking.MessageBase
        {
            public VoxBrushMessage()
            {
            }

            public VoxBrushMessage(Vector3 Position, float Radius, float Strength, byte Terrain)
            {
                this.Position = Position;
                this.Radius = Radius;
                this.Strength = Strength;
                this.Terrain = Terrain;
            }

            public override void Deserialize(UnityEngine.Networking.NetworkReader reader)
            {
                Position = reader.ReadVector3();
                Radius = reader.ReadSingle();
                Strength = reader.ReadSingle();
                Terrain = reader.ReadByte();
            }

            public override void Serialize(UnityEngine.Networking.NetworkWriter writer)
            {
                writer.Write(Position);
                writer.Write(Radius);
                writer.Write(Strength);
                writer.Write(Terrain);
            }
            public float Radius, Strength;
            public byte Terrain;
            public Vector3 Position;
        }
        
        //private static void SingtonStarted()
        //{
        //    var b = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        //    typeof(CameraManager).GetField("m_UnderGroundTolerance", b).SetValue(CameraManager.inst, 100000f);
        //    TankCamera.inst.groundClearance = -100000f;
        //    ManSaveGame.k_RestoreOrder.Insert(0, -8);
        //}

        public static void AddVoxTerrain(WorldTile tile)
        {
            tile.Terrain.gameObject.AddComponent<TerrainGenerator>().worldTile = tile;
        }

        internal class MassShifter : MonoBehaviour
        {
            byte BrushMat = 0xFF;
            int brushSize = 6;
            void Update()
            {
                if (!Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))) brushSize++;
                if (!Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))) brushSize = Math.Max(brushSize - 1,1);
                if (Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))) BrushMat++;
                if (Input.GetKey(KeyCode.LeftAlt) && (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))) BrushMat--;

                if (Physics.Raycast(Singleton.camera.ScreenPointToRay(Input.mousePosition), out var raycastHit, 10000, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore))
                {
                    TerrainGenerator.VoxTerrain vox = raycastHit.transform.gameObject.GetComponentInParent<TerrainGenerator.VoxTerrain>();
                    if (vox != null)
                    {
                        if (Input.GetKey(KeyCode.Equals))
                        {
                            SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, 0x00));
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, raycastHit.normal, BrushMat);
                        }
                        if (Input.GetKey(KeyCode.Minus))
                        {
                            SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -1f, 0x00));
                            vox.BleedBrushModifyBuffer(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -1f, raycastHit.normal, 0x00);
                        }
                        if (Input.GetKeyDown(KeyCode.Backspace))
                        {
                            Console.WriteLine("Vox ID "+vox.GetComponent<Visible>().ID);
                        }
                    }
                }
            }
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(WorldTile), "AddVisible")]
            private static class EnforceNotActuallyScenery
            {
                private static bool Prefix(Visible visible)
                {
                    return visible.name != "VoxTerrainChunk";
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
                    if (__instance.name == "VoxTerrainChunk")
                    {
                        __instance.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 0);
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
                    if (__instance.name == "VoxTerrainChunk")
                    {
                        __instance.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 0);
                    }
                }
            }

            [HarmonyPatch(typeof(TileManager), "SetTileCache")]
            private static class PleaseStopRemovingMyChunks
            {
                private static bool Prefix(Visible visible, WorldTile newTile, ref bool __result)
                {
                    __result = false;
                    if (visible.name == "VoxTerrainChunk" && newTile != null)
                    {
                        newTile.AddVisible(visible);
                        visible.tileCache.tile = newTile;
                        return false;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(ManSaveGame.StoredTile),"StoreScenery")]
            private static class PleaseSaveMyChunks
            {
                private static void Postfix(ref ManSaveGame.StoredTile __instance, Dictionary<int, Visible>[] visibles)
                {
                    ManSaveGame.Storing = true;
                    int i = 0;
                    foreach (TerrainGenerator.VoxTerrain vox in Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(__instance.coord, false).StaticParent.GetComponentsInChildren<TerrainGenerator.VoxTerrain>(true))//Visible visible in visibles[(int)TerrainGenerator.ObjectTypeVoxelChunk].Values)
                    {
                        Visible visible = vox.GetComponent<Visible>();
                        //if (visible.name == "VoxTerrainChunk")
                        //{
                        var store = new TerrainGenerator.VoxTerrain.VoxelSaveData();
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
                        Console.WriteLine($"({DateTime.Now.ToString()}) {i} unique vox terrain saved");
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
                                Console.WriteLine("gues i'l die");
                            }
                        }
                    }
                }
            }

            //[HarmonyPatch(typeof(ManSaveGame), "CreateStoredVisible")]
            //private static class SaveVoxChunks
            //{
            //    private static bool Prefix(ref ManSaveGame.StoredVisible __result, Visible visible)
            //    {
            //        if (visible.name == "VoxTerrainChunk")
            //        {
            //            var result = new TerrainGenerator.VoxTerrain.VoxelSaveData();
            //            result.Store(visible);
            //            __result = result;
            //            //Console.WriteLine(result.Cloud64);
            //            return false;
            //        }
            //        return true;
            //    }
            //}

            [HarmonyPatch(typeof(Visible), "get_rbody")]
            private static class SupressPhysics
            {
                private static bool Prefix(Visible __instance)
                {
                    return __instance.name != "VoxTerrainChunk";
                }
            }

            [HarmonyPatch(typeof(TileManager), "Init")]
            private static class AttachVoxTerrain
            {
                private static void Postfix()
                {
                    ManWorld.inst.TileManager.TilePopulatedEvent.Subscribe(AddVoxTerrain);
                }
            }

            [HarmonyPatch(typeof(TileManager), "GetTerrainHeightAtPosition")]
            private static class ReplaceHeightGet
            {   //Will have to retrofit this to operate with scenery in mind
                private static bool Prefix(ref float __result, Vector3 scenePos, out bool onTile, bool forceCalculate)
                {
                    onTile = true;
                    //int layerMask = hitScenery ? (Globals.inst.layerScenery.mask | Globals.inst.layerTerrain.mask) : (int)TerrainGenerator.TerrainOnlyLayer;
                    if (Physics.Raycast(scenePos, Vector3.down, out RaycastHit raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    if (Physics.Raycast(scenePos + Vector3.up * TerrainGenerator.voxelSize, Vector3.down, out raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    if (Physics.Raycast(scenePos + Vector3.up * 4096 + Vector3.one * 0.001f, Vector3.down, out raycasthit, 8192, TerrainGenerator.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
                    {
                        __result = raycasthit.point.y;
                        return false;
                    }
                    return true;
                }
            }

            /*
            [HarmonyPatch(typeof(TankCamera), "GroundProjectAllowingForGameMode")]
            private static class ReplaceTankCameraGroundProject//Trouble Magnet...
            {
                private static bool Prefix(ref Vector3 __result, Vector3 position)
                {
                    __result = Singleton.Manager<ManWorld>.inst.ProjectToGround(position, false) + Vector3.down * 10000;
                    return false;
                }
            }
            */

            //[HarmonyPatch(typeof(Tank), "HandleCollision")]
            //private static class TerrainCollisionBypassPatch
            //{
            //    private static void Prefix(Tank __instance, Collision collisionData, bool stay, ref Visible __state)
            //    {
            //        var go = collisionData.GetContact(0).thisCollider;
            //        if (go.transform.parent.name == "VoxTerrainChunk")
            //        {
            //            var V = go.GetComponentInParent<Visible>();
            //            if (V)
            //            {
            //                V.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, 0);
            //                __state = V;
            //                Console.WriteLine("oh yea vox");
            //                return;
            //            }
            //        }
            //        __state = null;
            //    }
            //    private static void Postfix(ref Visible __state)
            //    {
            //        if (__state)
            //        {
            //            __state.m_ItemType = new ItemTypeInfo(TerrainGenerator.ObjectTypeVoxelChunk, 0);
            //            Console.WriteLine("vox yea oh");
            //        }
            //    }
            //}
        }
    }
}
