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
public class VoxGenerator : MonoBehaviour
{
    //internal static List<VoxTerrain> ListOfAllActiveChunks = new List<VoxTerrain>();

    public const ObjectTypes ObjectTypeVoxelChunk = ObjectTypes.Scenery; 
    // - Crate cannot be impacted or drilled,
    // - Scenery causes implementation problems (PATCH:EnforceNotActuallyScenery), out-of-enum causes null problems
    internal static VoxTerrain Prefab;
    public WorldTile worldTile;

    /// <summary>
    /// Number of chunks to break the terrain in to
    /// </summary>
    internal const int subCount = 4;
    internal const int PrefabPoolSize = 64;//8;


    void LateUpdate()
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
    void GenerateTerrain()
    {
        try
        {

            Terrain _terrain = worldTile.Terrain;
            TerrainData _terrainData = _terrain.terrainData;


            var tc = _terrain.GetComponent<TerrainCollider>();
            var b = tc.bounds;
            int size = Mathf.RoundToInt(VoxTerrain.voxChunkSize / VoxTerrain.voxBlockResolution);
            float terrainHeight = worldTile.Terrain.transform.position.y;

            for (int z = 0; z < subCount; z++)
            {
                for (int x = 0; x < subCount; x++)
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
            enabled = false;
            //Destroy(this);
            //_terrain.enabled = false;
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


        //go.AddComponent<ChunkBounds>();
        go.layer = Globals.inst.layerTerrain;
        go.tag = "_V";

        var bc = go.AddComponent<BoxCollider>();
        bc.size = Vector3.one * VoxTerrain.voxChunkSize;
        bc.center = Vector3.one * (VoxTerrain.voxChunkSize / 2);
        bc.isTrigger = true;

        var cgo = new GameObject("Terrain");
        cgo.layer = Globals.inst.layerTerrain;
        cgo.transform.parent = go.transform;
        cgo.transform.localPosition = Vector3.zero;

        var mf = cgo.AddComponent<MeshFilter>();

        var mr = cgo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = VoxTerrain.sharedMaterial;

        var mc = cgo.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMaterial = new PhysicMaterial();

        //This is likely the terrain adding tool arm
        //var voxdisp = go.AddComponent<VoxDispenser>();

        //Future component much like Astroneer's vehicle paver
        //var voxdisp = go.AddComponent<VoxLeveler>();

        var vox = go.AddComponent<VoxTerrain>();

        //This is what determines the MaxHealth the Terrain Has
        var d = go.AddComponent<Damageable>();
        d.destroyOnDeath = false;
        d.SetMaxHealth(7500);
        //changed from 1000 to make it far less paper-y, this is the freaking floor after all
        d.InitHealth(7500);
        d.m_DamageableType = ManDamage.DamageableType.Rock;//Make it hard to destroy

        var v = go.AddComponent<Visible>();
        v.m_ItemType = new ItemTypeInfo(ObjectTypeVoxelChunk, 0);
        v.tag = "_V";

        go.layer = Globals.inst.layerTerrain;
        vox.CreatePool(PrefabPoolSize);
        go.SetActive(false);

        //TO-DO: Add the bedrock layer so people don't end up in the void.
        return vox;
    }

}
