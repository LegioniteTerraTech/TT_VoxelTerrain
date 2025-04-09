using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using UnityEngine.Networking;
using SafeSaves;

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


        [SSManagerInst]
        public static ManVoxelTerrain inst;
        [SSaveField]
        public Dictionary<IntVector2, VoxelSerial> VoxelsByTile;

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
                for (int j = 0; j < ManWorld.inst.CurrentBiomeMap.GetNumBiomes(); j++)
                {
                    b = ManWorld.inst.CurrentBiomeMap.LookupBiome(i);
                    if (b == null) break;
                    BiomeMapInvLookup.Add(b, i);
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
            catch { }
        }

        public const byte fallbackID = 127;
        public static void AddMoreTypes()
        {
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

        public static void Init()
        {
            inst = new GameObject("ManVoxelTerrain").AddComponent<ManVoxelTerrain>();
            VoxTerrain.Setup();
            VoxelRez = Mathf.RoundToInt(VoxTerrain.voxBlockSize * VoxTerrain.voxBlockResolution * VoxTerrain.voxChunksPerTile);

            if (ManWorld.inst.TileSize != VoxelRez)
                throw new InvalidOperationException("Voxel Tile is not equal to Default tile horizontal resolution [" + ManWorld.inst.TileSize + 
                    "],[" + VoxelRez + "].  In order for the voxel terrain to be flush, they must match!");

            _ = VoxTerrain.sharedMaterialDefault;
            AddMoreTypes();

            /*
            for (int i = 0; PreQueuedCloudPairs > i; i++)
                VoxTerrain.cloudStorage.Enqueue(MarchingCubes.CreateNewBuffer(VoxTerrain.voxBlockSize));
            */
            harmonyInst.PatchAll(Assembly.GetExecutingAssembly());
            new GameObject().AddComponent<MassShifter>();
            ManWorld.inst.TileManager.TilePopulatedEvent.Subscribe(AddVoxTile);
            ManWorld.inst.TileManager.TileDepopulatedEvent.Subscribe(RemoveVoxTile);
            ManWorldTreadmill.inst.OnAfterWorldOriginMoved.Subscribe(OnWorldTreadmill);
            ManGameMode.inst.ModeSetupEvent.Subscribe(OnModeSetup);
            //Nuterra.NetHandler.Subscribe<VoxBrushMessage>(VoxBrushMsg, ReceiveVoxBrush, PromptNewVoxBrush);


            if (LookForMod("Nuterra.Biomes"))
            {
                DebugVoxel.Log("VoxelTerrain: Found Biome Injector!  Attempting hookups!");
                isBiomeInjectorPresent = true;
            }
            DebugVoxel.Log("VoxelTerrain: Init!");
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
            var voxGen = tile.Terrain.gameObject.GetComponent<VoxGenerator>();
            if (!voxGen)
                voxGen = tile.Terrain.gameObject.AddComponent<VoxGenerator>();
            voxGen.worldTile = tile;
            voxGen.enabled = true;
        }
        public static void RemoveVoxTile(WorldTile tile)
        {
            if (!IsEnabled)
                return;
            IsRemoving = true;
            foreach (var item in tile.StaticParent.GetComponentsInChildren<VoxTerrain>(true))
            {
                item.Recycle();
            }
            tile.Terrain.enabled = true;
            tile.Terrain.GetComponent<TerrainCollider>().enabled = true;
            IsRemoving = false;
        }

        public static float nextTime = 0;
        internal static HashSet<WorldPosition> VoxAltered = new HashSet<WorldPosition>();
        public void FixedUpdate()
        {
            if (nextTime < Time.time)
            {
                nextTime = Time.time + 0.2f;
            }
            foreach (var item in VoxAltered)
            {
                foreach (var item2 in VoxGenerator.IterateNearbyScenery(item.ScenePosition))
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
                        if (item != null) // Drop the ResourceDispenser down by the mined distance!
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
            VoxAltered.Clear();
        }
        /*
        public void LateUpdate()
        {
            for (int i = 0; i < AllTerrain.Count; i++)
                AllTerrain.ElementAt(i).Remote_LatePreUpdate();

            for (int i = 0; i < AllTerrain.Count; i++)
                AllTerrain.ElementAt(i).Remote_LatePostUpdate();
        }*/

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
                VoxTerrain.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
            {
                height = scenePos.y - raycasthit.distance;
                return true;
            }
            scenePos.y += maxTerrainDetectDelta;
            if (Physics.Raycast(scenePos, Vector3.down, out raycasthit, maxTerrainDetectDelta,
                VoxTerrain.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore)/* && raycasthit.collider.GetComponentInParent<TerrainGenerator.VoxTerrain>()*/)
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
