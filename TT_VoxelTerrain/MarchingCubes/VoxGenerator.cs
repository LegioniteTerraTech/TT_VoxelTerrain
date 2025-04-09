using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using Nuterra;
using System.IO;
using TT_VoxelTerrain;
using FMOD;
public class VoxGenerator : MonoBehaviour
{
    /// <summary>
    /// Note: -1000 is the default limit 
    /// </summary>
    public static float VisibleMinPermittedHeight => Globals.inst.m_VisibleEmergencyKillHeight;
    /// <summary>
    /// Note: 100000 is the default limit 
    /// </summary>
    public static float VisibleMaxPermittedHeight => Globals.inst.m_VisibleEmergencyKillMaxHeight;

    //internal static List<VoxTerrain> ListOfAllActiveChunks = new List<VoxTerrain>();

    public const ObjectTypes ObjectTypeVoxelChunk = ObjectTypes.Scenery; 
    // - Crate cannot be impacted or drilled,
    // - Scenery causes implementation problems (PATCH:EnforceNotActuallyScenery), out-of-enum causes null problems
    internal static VoxTerrain Prefab;
    public WorldTile worldTile;

    internal const int VoxTerrainPoolSize = 512;//64;//8;


    internal void LateUpdate()
    {
        //if (Dirty && worldTile.m_LoadStep >= WorldTile.LoadStep.PopulatingScenery)
        if (worldTile.m_LoadStep >= WorldTile.LoadStep.Populated)
        {
            GenerateTerrain();//StartCoroutine("GenerateTerrain");
        }
    }

    /*IEnumerator*/
    /// <summary>
    /// This CREATES the one terrain TILE
    /// </summary>
    private void GenerateTerrain()
    {
        try
        {

            Terrain _terrain = worldTile.Terrain;
            TerrainData _terrainData = _terrain.terrainData;


            var tc = _terrain.GetComponent<TerrainCollider>();
            var b = tc.bounds;
            int size = Mathf.RoundToInt(VoxTerrain.voxChunkSize / VoxTerrain.voxBlockResolution);
            float terrainHeight = worldTile.Terrain.transform.position.y;

            for (int z = 0; z < VoxTerrain.voxChunksPerTile; z++)
            {
                for (int x = 0; x < VoxTerrain.voxChunksPerTile; x++)
                {
                    float chWorld;
                    int centerHeight;
                    switch (ManVoxelTerrain.state)
                    {
                        /*
                        case VoxelState.Preparing:
                            break;
                        case VoxelState.Normal:
                            break;*/
                        case VoxelState.RandD:
                            chWorld = worldTile.GetTerrainheight(_terrain.transform.position + Vector3.one);
                            MarchingCubes.DefaultSampleHeight = chWorld;
                            centerHeight = (int)(chWorld / VoxTerrain.voxChunkSize);
                            break;
                        default:
                            /*
                            DebugVoxel.Log("VoxelTerrain: got terrain at coords " + x + " | " + z + " as - " + _terrainData.size.x + " | " + _terrainData.size.z);
                            //chWorld = _terrainData.GetHeight((int)((x + 0.5f) * size), (int)((z + 0.5f) * size));
                            //DebugVoxel.Log("VoxelTerrain: set terrain at coords " + x + " | " + z + " as - " + chWorld);
                            terrainHeight = 200;
                            Vector3 loader = new Vector3((int)((x + 0.5f) * size), terrainHeight, (int)((z + 0.5f) * size));
                            chWorld = Singleton.Manager<ManWorld>.inst.TileManager.GetTerrainHeightAtPosition(_terrain.transform.TransformPoint(loader), out bool landHo);
                            DebugVoxel.Log("VoxelTerrain: setting terrain at coords " + x + " | " + z + " to - " + chWorld);
                            if (!landHo)
                            {
                                DebugVoxel.Log("VoxelTerrain: error - could not find terrain!");
                                chWorld = worldTile.GetTerrainheight(_terrain.transform.position + Vector3.one);
                            }
                            */
                            centerHeight = (int)(terrainHeight / VoxTerrain.voxChunkSize);
                            break;
                    }
                    //int centerHeight = (int)(chWorld / ChunkSize);

                    //for (int y = minY; y < Mathf.CeilToInt(centerHeight / ChunkSize + voxelSize); y++)
                    //  for (int y = Mathf.FloorToInt(b.min.y / ChunkSize); y < Mathf.CeilToInt(b.max.y / ChunkSize); y++) //Change to use buffer of tile, creating chunks where needed
                    //{
                    var offset = new Vector3(x, centerHeight, z) * VoxTerrain.voxChunkSize;
                    var t = CheckAndGenerateVoxPart(offset + transform.position);
                    //t.transform.rotation = Quaternion.identity;
                    //t.transform.position = offset + transform.position;
                    //t.Buffer = MarchingCubes.CreateBufferFromTerrain(worldTile, offset, size, voxelSize);
                    //yield return null;// new WaitForEndOfFrame();
                    //}
                }
            }
            //PlaceBedrockLayer();
            // We are done! No longer need to update this so we shut it off
            enabled = false;
        }
        catch (Exception e)
        {
            DebugVoxel.Log("Voxel Terrain: Generation FAILED - " + e);
        }
    }
    
    /*
    internal static VoxTerrain OverlapCheckTEST(Vector3 scenePos, float radius)
    {
        int Count;
        while (true)
        {
            Count = Physics.OverlapBoxNonAlloc(scenePos, Vector3.one * radius,
                resultsCache, Quaternion.identity, VoxTerrain.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Collide);
            if (Count > resultsCache.Length)
            {
                Array.Resize(ref resultsCache, Count);
                DebugVoxel.Log("Voxel Terrain: resized resultsCache to " + Count);
            }
            else
                break;
        }
        for (int i = 0; Count > i; i++)
        {
            var vox = resultsCache[i].GetComponent<VoxTerrain>();
            if (vox) return vox;
        }
        return null;
    }
    */
    
    private static Collider[] resultsCache = new Collider[32];
    internal static VoxTerrain OverlapCheckRecentered(Vector3 scenePos)
    {
        return OverlapCheckFast(VoxTerrain.GetVoxCenter(scenePos), VoxTerrain.voxChunkSize / 8f);
    }
    internal static VoxTerrain OverlapCheckFast(Vector3 scenePos, float radius)
    {
        int Count;
        while (true)
        {
            Count = Physics.OverlapSphereNonAlloc(scenePos, radius,
                resultsCache, VoxTerrain.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Collide);
            if (Count > resultsCache.Length)
            {
                Array.Resize(ref resultsCache, Count);
                DebugVoxel.Log("Voxel Terrain: resized resultsCache to " + Count);
            }
            else
                break;
        }
        for (int i = 0; Count > i; i++)
        {
            var vox = resultsCache[i].GetComponent<VoxTerrain>();
            if (vox) return vox;
        }
        return null;
    }
    

    internal static IEnumerable<ResourceDispenser> IterateNearbyScenery(Vector3 scenePos)
    {
        return SceneryOverlapCheckFast(VoxTerrain.GetVoxCenter(scenePos));
    }
    internal static IEnumerable<ResourceDispenser> IterateNearbyScenery(VoxTerrain terra)
    {
        return SceneryOverlapCheckFast(terra.SceneCenter);
    }
    private static bool GetAllScenery = true;
    internal static IEnumerable<ResourceDispenser> SceneryOverlapCheckFast(Vector3 scenePos)
    {
        Vector3 min = scenePos - (Vector3.one * VoxTerrain.voxChunkSize);
        float size = VoxTerrain.voxChunkSize * 2f;
        foreach (var vis in ManVisible.inst.VisiblesTouchingRadius(scenePos, VoxTerrain.voxChunkSize * 1.43f,
            new Bitfield<ObjectTypes>(new ObjectTypes[] { ObjectTypes.Scenery })))
        {
            if (vis?.resdisp && (GetAllScenery || vis.damageable) && 
                VoxTerrain.Within(min, vis.centrePosition, size) &&
                !vis.GetComponent<VoxTerrain>())
                yield return vis.resdisp;
        }
    }
    internal static IEnumerable<Visible> SceneryOverlapCheckFastAlt(Vector3 scenePos)
    {
        int Count;
        while (true)
        {
            Count = Physics.OverlapSphereNonAlloc(scenePos, VoxTerrain.voxChunkSize * 1.41f,
                resultsCache, Globals.inst.layerScenery, QueryTriggerInteraction.Collide);
            if (Count > resultsCache.Length)
            {
                Array.Resize(ref resultsCache, Count);
                DebugVoxel.Log("Voxel Terrain: resized resultsCache to " + Count);
            }
            else
                break;
        }
        Vector3 min = scenePos - (Vector3.one * VoxTerrain.voxChunkSize);
        float size = VoxTerrain.voxChunkSize * 2f;
        for (int i = 0; Count > i; i++)
        {
            Collider result = resultsCache[i];
            if (result)
            {
                Visible vis = ManVisible.inst.FindVisible(result);
                if (vis?.resdisp && vis.damageable && 
                    VoxTerrain.Within(min, vis.centrePosition, size))
                    yield return vis;
            }
        }
    }

    internal static VoxTerrain CheckAndGenerateVoxPart(Vector3 scenePos)
    {
        return VoxTile.LookupOrCreate(scenePos);
        /*
        var vox = OverlapCheckFast(scenePos + VoxTerrain.voxBlockCenterOffset,
            VoxTerrain.voxChunkSize / 8f);
        if (vox)
            return vox;
        return GenerateVoxPart(scenePos);//(worldTile);
        */
    }

    internal static VoxTerrain GenerateVoxPart(Vector3 scenePos)
    {
        if (Prefab == null)
        {
            Prefab = GeneratePrefab();
            //var b = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            //var toT = typeof(TerrainObject);
            //toT.GetField("m_PersistentObjectGUID", b).SetValue(Prefab as TerrainObject, TT_VoxelTerrain.Class1.VoxTerrainGUID);
            //(typeof(TerrainObjectTable).GetField("m_GUIDToPrefabLookup", b).GetValue(typeof(ManSpawn).GetField("m_TerrainObjectTable", b).GetValue(ManSpawn.inst) as TerrainObjectTable) as Dictionary<string, TerrainObject>).Add(TT_VoxelTerrain.Class1.VoxTerrainGUID, Prefab);
            //TerrainObject_AddToTileData = toT.GetMethod("AddToTileData", b);
        }

        var vox = Prefab.Spawn(scenePos, Quaternion.identity);
        //TerrainObject_AddToTileData.Invoke(vinst, null);

        var vinst = vox.transform;
        vinst.name = "VoxTerrainChunk";
        vinst.gameObject.SetActive(true);
        vox.OnWorldSpawn();
        //vinst.position = pos;
        //ListOfAllActiveChunks.Add(vox);
        return vox;
    }
    private static VoxTerrain GeneratePrefab()
    {
        if (VoxTerrain.Visible_damageable == null)
        {
            Type vis = typeof(Visible);
            var bind = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            VoxTerrain.Visible_damageable = vis.GetProperty("damageable", bind);
            //Visible_m_VisibleComponent = vis.GetField("m_VisibleComponent", bind);
        }

        //if (Prefab == null)
        //{
        //    Prefab = (typeof(ManSpawn).GetField("spawnableScenery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ManSpawn.inst) as List<Visible>)[0].GetComponent<TerrainObject>();
        //}
        //var so = Prefab.SpawnFromPrefab(ParentTile, Vector3.zero, Quaternion.identity);
        //var go = so.gameObject;

        var go = new GameObject("VoxTerrainChunk");

        string visTag = "_V";

        //go.AddComponent<ChunkBounds>();
        go.layer = Globals.inst.layerTerrain;
        go.tag = visTag;

        /*
        var bc = go.AddComponent<BoxCollider>();
        bc.size = Vector3.one * VoxTerrain.voxChunkSize;
        bc.center = Vector3.one * (VoxTerrain.voxChunkSize / 2);
        bc.isTrigger = true;
        bc.tag = visTag;
        */

        var cgo = new GameObject("Terrain");
        cgo.layer = Globals.inst.layerTerrain;
        cgo.transform.parent = go.transform;
        cgo.transform.localPosition = Vector3.zero;
        cgo.transform.localRotation = Quaternion.identity;
        cgo.transform.localScale = Vector3.one;
        cgo.tag = visTag;

        var mf = cgo.AddComponent<MeshFilter>();
        mf.tag = visTag;

        var mr = cgo.AddComponent<MeshRenderer>();
        mr.tag = visTag;
        //mr.sharedMaterial = VoxTerrain.sharedMaterialDefault;
        mr.sharedMaterial = VoxTerrain.mainTerrainMaterial;

        var mc = cgo.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMaterial = new PhysicMaterial();
        mc.tag = visTag;

        //This is likely the terrain adding tool arm
        //var voxdisp = go.AddComponent<VoxDispenser>();

        //Future component much like Astroneer's vehicle paver
        //var voxlev = go.AddComponent<VoxLeveler>();

        var vox = go.AddComponent<VoxTerrain>();
        vox.tag = visTag;

        //This is what determines the MaxHealth the Terrain Has
        var d = go.AddComponent<Damageable>();
        d.destroyOnDeath = false;
        d.SetMaxHealth(7500);
        //changed from 1000 to make it far less paper-y, this is the freaking floor after all
        d.InitHealth(7500);
        d.m_DamageableType = ManDamage.DamageableType.Rock;//Make it hard to destroy
        d.tag = visTag;

        var v = go.AddComponent<Visible>();
        v.m_ItemType = new ItemTypeInfo(ObjectTypeVoxelChunk, 0);
        v.tag = visTag;

        vox.CreatePool(VoxTerrainPoolSize);
        go.SetActive(false);

        //TO-DO: Add the bedrock layer so people don't end up in the void.
        return vox;
    }


    private static Transform bedrockLevel = null;
    /// <summary>
    /// This is determined by the "killHeight"
    /// </summary>
    private static void PlaceBedrockLayer()
    { 
        if (bedrockLevel == null)
            GenerateBedrockLayerPrefab();
    }

    private static void GenerateBedrockLayerPrefab()
    {
        var go = new GameObject("VoxTerrainBedrock");

        string visTag = "_V";

        //go.AddComponent<ChunkBounds>();
        go.layer = Globals.inst.layerTerrain;
        go.tag = visTag;

        var bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(ManWorld.inst.TileSize,10f, ManWorld.inst.TileSize);
        bc.center = new Vector3(ManWorld.inst.TileSize / 2f, -5f, ManWorld.inst.TileSize / 2f);
        bc.isTrigger = false;
        bc.tag = visTag;

        var cgo = new GameObject("Terrain");
        cgo.layer = Globals.inst.layerTerrain;
        cgo.transform.parent = go.transform;
        cgo.transform.localPosition = Vector3.zero;
        cgo.transform.localRotation = Quaternion.identity;
        cgo.transform.localScale = Vector3.one;
        cgo.tag = visTag;

        var mf = cgo.AddComponent<MeshFilter>();
        mf.tag = visTag;
        mf.mesh = PrismMeshGenerator.GenerateMesh(new Vector3[]
            {
            new Vector3(0, 0, 0),
            new Vector3(ManWorld.inst.TileSize, 0, 0),
            new Vector3(ManWorld.inst.TileSize, 0, ManWorld.inst.TileSize),
            new Vector3(0, 0, ManWorld.inst.TileSize),
        }, new Vector3(0, -10, 0), 1f);

        var mr = cgo.AddComponent<MeshRenderer>();
        mr.tag = visTag;
        //mr.sharedMaterial = VoxTerrain.sharedMaterialDefault;
        mr.sharedMaterial = VoxTerrain.mainTerrainMaterial;

        var mc = cgo.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMaterial = new PhysicMaterial();
        mc.tag = visTag;
    }
}
