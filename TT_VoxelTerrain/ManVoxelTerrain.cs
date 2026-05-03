using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using UnityEngine.Networking;
using SafeSaves;
using TerraTechETCUtil;

namespace TT_VoxelTerrain
{
    /// <summary>
    /// Stores data for voxels in here
    /// </summary>
    public class VoxelSerial
    { 
    }
    [AutoSaveManager]
    public class ManVoxelTerrain : MonoBehaviour
    {
        public const int TextureMixLayers = 2;//4; - OG was 2
        public const string ModName = "Voxel Terrain";


        [SSManagerInst]
        public static ManVoxelTerrain inst;
        [SSaveField]
        public Dictionary<IntVector2, VoxelSerial> VoxelsByTile;

        //internal static HashSet<VoxTerrain> AllTerrain = new HashSet<VoxTerrain>();
        internal static EventNoParams TerrainPreLateUpdateEvent = new EventNoParams();
        internal static EventNoParams TerrainPostLateUpdateEvent = new EventNoParams();
        internal static EventNoParams ProcessBrushPendingEvent = new EventNoParams();

        private static MassShifter TerrainShifter = null;

        //Globals
        internal static float MaxTechImpactRadius = 32f;
        internal static float resDispTerrainDeltaDmgMulti = 240f;
        /// <summary>
        /// Disabled state doesn't work properly yet!
        /// </summary>
        public static bool IsEnabled = true;

        public static bool AllowPlayerDamageTerraform = true;
        public static bool AllowEnemyDamageTerraform = true;


        //Detection variables
        public static bool isNativeOptionsPresent = false;
        public static bool isConfigHelperPresent = false;
        public static bool isBiomeInjectorPresent = false; 


        //Networking
        const TTMsgType VoxBrushMsg = (TTMsgType)4326;
        private static Harmony harmonyInst = new Harmony("aceba1.betterterrain");
        internal static VoxelState state = VoxelState.Preparing;
        /// <summary>
        /// Size of each voxel
        /// - AUTO-SET in Setup()
        /// </summary>
        internal static int VoxelRez = 8;
        internal static int PreQueuedCloudPairs = 32;
        internal static bool IsRemoving = false;

        internal static Dictionary<Biome, byte> BiomeMapInvLookup = new Dictionary<Biome, byte>();
        internal static Dictionary<byte, Material> extraMats = new Dictionary<byte, Material>();
        // Note to self - finish implementing this to work with PhysicMaterial
        //  Only DynamicFriction does anything to the wheels btw
        internal static Dictionary<byte, PhysicMaterial> biomeFriction = new Dictionary<byte, PhysicMaterial>();

        internal struct MineFXLookup
        {
            public SceneryTypes ST;
            public string Biome;
        }
        internal static Dictionary<byte, MineFXLookup> biomeMineEffects = new Dictionary<byte, MineFXLookup>();

        public static ManToolbar.ToolbarToggle TheTerrainToolButton = null;
        internal static void Init()
        {
            if (inst != null)
                return;

            isConfigHelperPresent = ModStatusChecker.IsConfigHelperPresent;
            isNativeOptionsPresent = ModStatusChecker.IsNativeOptionsPresent;

            if (ModStatusChecker.LookForMod("Nuterra.Biomes"))
            {
                DebugVoxel.Log("VoxelTerrain: Found Biome Injector!  Attempting hookups!");
                isBiomeInjectorPresent = true;
            }

            inst = new GameObject("ManVoxelTerrain").AddComponent<ManVoxelTerrain>();
            VoxelGlobals.FirstSetup();
            VoxelRez = Mathf.RoundToInt(VoxelGlobals.voxBlockSize * VoxelGlobals.voxBlockResolution * VoxelGlobals.voxChunksPerTile);

            if (ManWorld.inst.TileSize != VoxelRez)
                throw new InvalidOperationException("Voxel Tile is not equal to Default tile horizontal resolution [" + ManWorld.inst.TileSize +
                    "],[" + VoxelRez + "].  In order for the voxel terrain to be flush, they must match! NOTIFY LEGIONITE");

            _ = VoxTerrain.sharedMaterialDefault;
            AddMoreTypes();
            CursorChanger.AddNewCursors();
            if (TheTerrainToolButton == null)
            {
                TheTerrainToolButton = new ManToolbar.ToolbarToggle(
                new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
                {
                {LocalisationEnums.Languages.US_English, "Terrain Tool"},
                }), ResourcesHelper.FetchTexture(ResourcesHelper.GetModContainer(ModName),
                    "TerrainToolDown").ConvertToSprite(), MassShifter.ToggleState);
            }

            /*
            for (int i = 0; PreQueuedCloudPairs > i; i++)
                VoxTerrain.cloudStorage.Enqueue(MarchingCubes.CreateNewBuffer(VoxTerrain.voxBlockSize));
            */
            harmonyInst.PatchAll(Assembly.GetExecutingAssembly());
            TerrainShifter = new GameObject().AddComponent<MassShifter>();
            ManWorld.inst.TileManager.TilePopulatedEvent.Subscribe(AddVoxTile);
            ManWorld.inst.TileManager.TileDepopulatedEvent.Subscribe(RemoveVoxTile);
            ManWorldTreadmill.inst.OnAfterWorldOriginMoved.Subscribe(OnWorldTreadmill);
            ManGameMode.inst.ModeSetupEvent.Subscribe(OnModeSetup);

            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, inst.OnFixedUpdate, 356);
            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, inst.OnLateUpdate, 356);
            //Nuterra.NetHandler.Subscribe<VoxBrushMessage>(VoxBrushMsg, ReceiveVoxBrush, PromptNewVoxBrush);

            if (isNativeOptionsPresent && isConfigHelperPresent)
            {
                try
                {
                    SafeInitOptions();
                }
                catch { }
            }

            DebugVoxel.Log("VoxelTerrain: Init!");
        }
        private static void SafeInitOptions()
        {
            try
            {
                KickstartOptions.InitHooks();
            }
            catch { }
        }
        public static void DeInit()
        {
            if (inst == null)
                return;
            TheTerrainToolButton.Remove();
            TheTerrainToolButton = null;

            //Nuterra.NetHandler.Unsubscribe<VoxBrushMessage>(VoxBrushMsg, ReceiveVoxBrush, PromptNewVoxBrush);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, inst.OnLateUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, inst.OnFixedUpdate);

            ManGameMode.inst.ModeSetupEvent.Unsubscribe(OnModeSetup);
            ManWorldTreadmill.inst.OnAfterWorldOriginMoved.Unsubscribe(OnWorldTreadmill);
            ManWorld.inst.TileManager.TileDepopulatedEvent.Unsubscribe(RemoveVoxTile);
            ManWorld.inst.TileManager.TilePopulatedEvent.Unsubscribe(AddVoxTile);
            Destroy(TerrainShifter);
            TerrainShifter = null;
            harmonyInst.UnpatchAll(harmonyInst.Id);

            Destroy(inst);
            inst = null;
        }

        public static bool DoMiningFX = true;

        public static float MiningFXLastTime = 0;

        public static void OnWorldTreadmill(IntVector3 worldDelta)
        {
            DebugVoxel.Log("VoxelTerrain: World treadmill by " + worldDelta.ToString());
        }
        public static void OnModeSetup(Mode mode)
        {
            BiomeMapInvLookup.Clear();
            VoxTerrain._matcache.Clear();
            VoxTerrain._phycache.Clear();
            /*
            if (Singleton.Manager<ManGameMode>.inst.GetCurrentGameType() == ManGameMode.GameType.RaD)
                state = VoxelState.RandD;
            else
                state = VoxelState.Normal;
            */
            state = VoxelState.Normal;
            DebugVoxel.Log("VoxelTerrain: mode " + state.ToString());
        }
        public static void GatherVoxelData()
        {
            if (BiomeMapInvLookup.Any())
                return;
            DebugVoxel.Log("VoxelTerrain: Reaquired Biome lookup");
            BiomeMapInvLookup.Clear();
            byte i = 0;
            Biome b;
            try
            {
                SpawnHelper.GrabInitList();
                foreach (var item in new List<Biome>(SpawnHelper.BiomesByName.Values))
                {
                    b = item;
                    if (b == null) break;
                    //DebugVoxel.Log("VoxelTerrain: " + b.name);
                    //*
                    if (!BiomeMapInvLookup.ContainsKey(b))
                    {
                        BiomeMapInvLookup.Add(b, i);
                        switch (b.BiomeType)
                        {
                            case BiomeTypes.Grassland:
                            case BiomeTypes.SaltFlats:
                            case BiomeTypes.Ice:
                                if (!biomeMineEffects.ContainsKey(i))
                                    biomeMineEffects.Add(i, new MineFXLookup()
                                    {
                                        Biome = "RockyRidgeBiome",
                                        ST = SceneryTypes.GrasslandRock,
                                    });
                                break;
                            case BiomeTypes.Desert:
                                if (!biomeMineEffects.ContainsKey(i))
                                    biomeMineEffects.Add(i, new MineFXLookup()
                                    {
                                        Biome = "DesertBiome",
                                        ST = SceneryTypes.DesertRock,
                                    });
                                break;
                            case BiomeTypes.Mountains:
                                if (!biomeMineEffects.ContainsKey(i))
                                    biomeMineEffects.Add(i, new MineFXLookup()
                                    {
                                        Biome = "MountainsBiome",
                                        ST = SceneryTypes.MountainRock,
                                    });
                                break;
                            case BiomeTypes.Pillars:
                                if (!biomeMineEffects.ContainsKey(i))
                                    biomeMineEffects.Add(i, new MineFXLookup()
                                    {
                                        Biome = "MountainsBiome",
                                        ST = SceneryTypes.MountainRock,
                                    });
                                break;
                            default:
                                break;
                        }
                    }//*/
                    i++;
                }
                /*
                while (true)
                {
                    b = ManWorld.inst.CurrentBiomeMap.LookupBiome(i);
                    if (b == null) break;
                    BiomeMapInvLookup.Add(b, i);
                    i++;
                }*/
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public const byte fallbackID = 127;
        private static bool AddedMoreTypes = false;
        private static void AddMoreTypes()
        {
            if (AddedMoreTypes)
                return;
            AddedMoreTypes = true;
            Texture2D tex = new Texture2D(0, 0);
            Material result = new Material(VoxTerrain.sharedMaterialDefault);
            tex.LoadRawTextureData(Properties.Resources.neontile_png);
            tex.Apply();
            result.mainTexture = tex;
            extraMats.Add(126, result);
            biomeFriction.Add(126, new PhysicMaterial()
            {
                name = 126.ToString(),
                bounciness = 0,
                bounceCombine = PhysicMaterialCombine.Maximum,
                dynamicFriction = 0.9f,
                staticFriction = 1f,
                frictionCombine = PhysicMaterialCombine.Maximum,
            });

            result = new Material(VoxTerrain.sharedMaterialDefault);
            //result.SetTexture("_MainTex",  BiomeTextures["TERRAIN_EXP_01"]);
            //result.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/EnvironmentTextures/Terrain/TERRAIN_EXP_01.png"));
            tex.LoadRawTextureData(Properties.Resources.neontile_png);
            tex.Apply();
            result.mainTexture = tex;
            extraMats.Add(127, result);
            biomeFriction.Add(127, new PhysicMaterial()
            {
                name = 127.ToString(),
                bounciness = 0,
                bounceCombine = PhysicMaterialCombine.Maximum,
                dynamicFriction = 0.9f,
                staticFriction = 1f,
                frictionCombine = PhysicMaterialCombine.Maximum,
            });

            result = new Material(VoxTerrain.sharedMaterialDefault);
            ////result.SetTexture("_MainTex", BiomeTextures["TERRAIN_EXP_01"]);
            //var bmp = new Texture2D(result.mainTexture.width, result.mainTexture.height);
            //for (int y = 0; y < result.mainTexture.width; y++)
            //{
            //    for (int x = 0; x < result.mainTexture.height; x++)
            //    {
            //        var direction = UnityEngine.Random.onUnitSphere;
            //        direction = direction.SetY(Mathf.Abs(direction.y)*2f) * 0.5f;
            //        bmp.SetPixel(x, y, new Color(direction.x + 0.5f, direction.y + 0.5f, direction.z));
            //    }
            //}
            //bmp.Apply();
            //result.SetTexture("_BumpMap", bmp);
            //result.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/EnvironmentTextures/Terrain/TERRAIN_EXP_01.png"));
            tex.LoadRawTextureData(Properties.Resources.neontile_png);
            tex.Apply();
            result.mainTexture = tex;
            extraMats.Add(128, result);
            biomeFriction.Add(128, new PhysicMaterial()
            {
                name = 128.ToString(),
                bounciness = 0,
                bounceCombine = PhysicMaterialCombine.Maximum,
                dynamicFriction = 0.9f,
                staticFriction = 1f,
                frictionCombine = PhysicMaterialCombine.Maximum,
            });
        }


        //private static void SingtonStarted()
        //{
        //    var b = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        //    typeof(CameraManager).GetField("m_UnderGroundTolerance", b).SetValue(CameraManager.inst, 100000f);
        //    TankCamera.inst.groundClearance = -100000f;
        //    ManSaveGame.k_RestoreOrder.Insert(0, -8);
        //}

        public static void AddVoxTile(WorldTile tile)
        {
            if (!IsEnabled)
                return;
            var voxGen = tile.Terrain.GetComponent<VoxGenerator>();
            if (!voxGen)
                voxGen = tile.Terrain.gameObject.AddComponent<VoxGenerator>();
            voxGen.worldTile = tile;
            voxGen.enabled = true;
        }
        private static List<VoxTerrain> removeList = new List<VoxTerrain>();
        public static void RemoveVoxTile(WorldTile tile)
        {
            if (tile?.Terrain == null)
            {
                DebugVoxel.Assert("TILE OR TERRAIN IS NULL");
                return;
            }
            IsRemoving = true;
            try
            {
                tile.StaticParent?.GetComponentsInChildren(true, removeList);
                foreach (var item in removeList)
                    item?.Recycle();
            }
            finally
            {
                removeList.Clear();
            }
            tile.Terrain.enabled = true;
            tile.Terrain.GetComponent<TerrainCollider>().enabled = true;
            IsRemoving = false;
        }

        public static float nextTime = 0;
        internal static HashSet<WorldPosition> VoxAltered = new HashSet<WorldPosition>();
        private void OnFixedUpdate()
        {
            ProcessBrushPendingEvent.Send();
            if (nextTime < Time.time)
                nextTime = Time.time + 0.2f;
            try
            {
                foreach (var item in VoxAltered)
                {
                    foreach (var item2 in VoxGenerator.SceneryOverlapCheckFast(item.ScenePosition))//VoxGenerator.SceneryOverlapCheckFastest(item))
                    {
                        Visible vis = item2.visible;
                        Vector3 centerPos = item2.transform.position;
                        if (CheckTerrainExistsFast(centerPos, out float height))
                        {   // In range => Move!
                            if (vis.damageable)
                            {
                                float Delta = Mathf.Abs(height - centerPos.y);
                                if (Delta > 0.1f)
                                {
                                    ManDamage.inst.DealImpactDamage(vis.damageable,
                                        Delta * resDispTerrainDeltaDmgMulti,
                                        null, null, centerPos + Vector3.down);
                                }
                            }
                            if (item2 != null) // Drop the ResourceDispenser down by the mined distance!
                                vis.centrePosition = vis.centrePosition.SetY(height);
                        }
                        else
                        {   // No Terrain => uhh ignore
                            /* // Originally was remove
                            item.RemoveFromWorld(true);
                            if (item != null)
                                vis.centrePosition = vis.centrePosition.SetY(height);
                            */
                        }
                    }
                }
                foreach (var item in VoxAltered)
                    VoxGenerator.AwakenAndRepositionAffectedRigidbodies(item);
            }
            finally
            {
                VoxAltered.Clear();
            }
        }
        //*
        private void OnLateUpdate()
        {
            TerrainPreLateUpdateEvent.Send();
            TerrainPostLateUpdateEvent.Send();
        }//*/

        /*
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
        */

        // Utilities
        public const float maxTerrainDetectDelta = 256f;
        public static bool CheckTerrainExistsFast(Vector3 scenePos, out float height)
        {
            if (Physics.Raycast(scenePos, Vector3.down, out RaycastHit raycasthit, maxTerrainDetectDelta,
                VoxelGlobals.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
            {
                height = scenePos.y - raycasthit.distance;
                return true;
            }
            scenePos.y += maxTerrainDetectDelta;
            if (Physics.Raycast(scenePos, Vector3.down, out raycasthit, maxTerrainDetectDelta,
                VoxelGlobals.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
            {
                height = scenePos.y - raycasthit.distance;
                return true;
            }
            height = scenePos.y;
            //DebugVoxel.Assert("IsTerrainCloseFast exceeded maxTerrainDetectDelta or terrain nil");
            return false;
        }
    }
}
