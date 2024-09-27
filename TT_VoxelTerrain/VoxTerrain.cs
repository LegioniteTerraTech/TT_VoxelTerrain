using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using Nuterra;
using System.IO;
using System.Reflection;
using TT_VoxelTerrain;

namespace TT_VoxelTerrain
{
    internal class VoxTile
    {
        private static Dictionary<IntVector2, VoxTile> Tiles = new Dictionary<IntVector2, VoxTile>();

        private static IntVector3 ToInt(Vector3 inPos)
        {
            return new IntVector3(
                Mathf.FloorToInt((inPos.x + 0.01f) / VoxTerrain.voxChunkSize) * VoxTerrain.voxChunkSize,
                Mathf.FloorToInt((inPos.y + 0.01f) / VoxTerrain.voxChunkSize) * VoxTerrain.voxChunkSize,
                Mathf.FloorToInt((inPos.z + 0.01f) / VoxTerrain.voxChunkSize) * VoxTerrain.voxChunkSize
                );
        }

        internal static void Add(Vector3 scenePos, VoxTerrain vox)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            vox.chunkCoord = WP.TileCoord;
            vox.inTilePos = WP.TileRelativePos;
            if (!Tiles.TryGetValue(WP.TileCoord, out var chunk))
            {
                chunk = new VoxTile(WP.TileCoord);
                Tiles.Add(WP.TileCoord, chunk);
            }
            chunk.AddVox(vox.inTilePos, vox);
        }
        internal static void Remove(Vector3 scenePos, VoxTerrain vox)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            if (Tiles.TryGetValue(vox.chunkCoord, out var chunk) &&
                chunk.RemoveVox(vox.inTilePos))
            {
                if (!chunk.blocks.Any())
                    Tiles.Remove(vox.chunkCoord);
                return;
            }
            DebugVoxel.Log("failed to get vox at [" + WP.TileCoord.x + ", " +
                WP.TileCoord.y + "], doing it the long way");
            if (Tiles.TryGetValue(vox.chunkCoord, out chunk))
            {
                chunk.RemoveVox(vox);
                if (!chunk.blocks.Any())
                    Tiles.Remove(vox.chunkCoord);
            }
        }
        internal static VoxTerrain Lookup(Vector3 scenePos)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            if (Tiles.TryGetValue(WP.TileCoord, out var chunk))
                return chunk.LookupVox(WP.TileRelativePos);
            return null;
        }
        internal static VoxTerrain LookupOrCreate(Vector3 scenePos)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(scenePos);
            if (!Tiles.TryGetValue(WP.TileCoord, out var chunk))
            {
                chunk = new VoxTile(WP.TileCoord);
                Tiles.Add(WP.TileCoord, chunk);
            }
            return chunk.LookupOrCreateVox(WP.TileRelativePos);
        }

        public readonly IntVector2 worldCoord;
        public Dictionary<IntVector3, VoxTerrain> blocks = new Dictionary<IntVector3, VoxTerrain>();
        public VoxTile(IntVector2 coordWorld)
        {
            worldCoord = coordWorld;
        }
        private void AddVox(Vector3 inPos, VoxTerrain vox) => blocks.Add(ToInt(inPos), vox);
        private bool RemoveVox(Vector3 inPos) => blocks.Remove(ToInt(inPos));
        private void RemoveVox(VoxTerrain vox)
        {
            try
            {
                blocks.Remove(blocks.ElementAt(blocks.Values.ToList().IndexOf(vox)).Key);
            }
            catch
            {
                DebugVoxel.Assert("fallback failed to remove vox from lookup!  We will now be out of sync!");
            }
        }
        private VoxTerrain LookupVox(Vector3 inPos)
        {
            blocks.TryGetValue(ToInt(inPos), out VoxTerrain vox);
            return vox;
        }
        public VoxTerrain LookupOrCreateVox(Vector3 inPos)
        {
            IntVector3 pos = ToInt(inPos);
            if (blocks.TryGetValue(pos, out VoxTerrain vox))
                return vox;
            vox = VoxGenerator.GenerateVoxPart(new WorldPosition(worldCoord, pos).ScenePosition);
            return vox;
        }
    }
    /// <summary> A Vox Chunk </summary>
    internal class VoxTerrain : MonoBehaviour, IWorldTreadmill
    {
        public static LayerMask VoxelTerrainOnlyLayer = LayerMask.GetMask(LayerMask.LayerToName(Globals.inst.layerTerrain));
        /// <summary>
        /// Resolution of each voxel block
        /// </summary>
        public const float voxBlockResolution = 6; //3.0f; //! Half of a terrain vertex scale
                                                   //public const float voxelSize = 8; //3.0f; //! Half of a terrain vertex scale
        /// <summary>
        /// AUTO-SET in Setup() - Size of each voxel
        /// </summary>
        public static int voxBlockSize = -1;
        /// <summary>
        /// AUTO-SET in Setup() - Size of a chunk in world units
        /// </summary>
        public static int voxChunkSize = -1;
        /// <summary>
        /// AUTO-SET in Setup()
        /// </summary>
        public static Vector3 voxBlockCenterOffset = Vector3.zero;
        public static void Setup()
        {
            voxChunkSize = Mathf.RoundToInt(ManWorld.inst.TileSize) / VoxGenerator.subCount;
            voxBlockSize = Mathf.RoundToInt(voxChunkSize / voxBlockResolution);
            voxBlockCenterOffset = Vector3.one * (voxChunkSize / 2f);
            BleedWrap = Mathf.RoundToInt(voxChunkSize / voxBlockResolution);
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
            Vector3 min = new Vector3(Mathf.Floor(ScenePos.x / voxBlockSize) * voxBlockSize,
                Mathf.Floor(ScenePos.y / voxBlockSize) * voxBlockSize,
                Mathf.Floor(ScenePos.z / voxBlockSize) * voxBlockSize);
            Vector3 max = min + (Vector3.one * voxBlockSize);
            return min.x <= ScenePosTarget.x && max.x >= ScenePosTarget.x &&
                min.y <= ScenePosTarget.y && max.y >= ScenePosTarget.y &&
                min.z <= ScenePosTarget.z && max.z >= ScenePosTarget.z;
        }
        public static bool WithinVoxByOrigin(Vector3 voxOrigin, Vector3 ScenePosTarget)
        {
            Vector3 max = voxOrigin + (Vector3.one * voxBlockSize);
            return voxOrigin.x <= ScenePosTarget.x && max.x >= ScenePosTarget.x &&
                voxOrigin.y <= ScenePosTarget.y && max.y >= ScenePosTarget.y &&
                voxOrigin.z <= ScenePosTarget.z && max.z >= ScenePosTarget.z;
        }


        //internal static Queue<CloudPair[,,]> cloudStorage = new Queue<CloudPair[,,]>();


        internal static PropertyInfo Visible_damageable;


        public WorldTile parent;
        public IntVector2 chunkCoord;
        public IntVector3 inTilePos;
        public bool BufferSet = false;
        public Vector3 SceneOrigin => transform.position;
        public Vector3 SceneCenter => transform.position + voxBlockCenterOffset;


        private void ThrowOnIllegalRecycle(Visible vis)
        {
            if (!ManVoxelTerrain.IsRemoving)
            {
                DebugVoxel.Assert("You dumbbell game! You cannot recycle a voxel tile this way!");
                throw new InvalidOperationException("You dumbbell game! You cannot recycle a voxel tile this way!");
            }
        }
        private void OnPool()//Next failiure point to fix
        {
            DebugVoxel.Info("VoxelTerrain: POOLING TIME!");
            Visible v = GetComponent<Visible>();
            if (v)
            {
                DebugVoxel.Info("VoxelTerrain: Grabbed Visible");
                v.RecycledEvent.Subscribe(ThrowOnIllegalRecycle);
            }
            try
            {
                //Visible_m_VisibleComponent.SetValue(v, this);
            }
            catch (Exception e)
            {
                DebugVoxel.Log("VoxelTerrain: Unhandled error with setting visible");
                DebugVoxel.Log(e);
            }
            //Sets the fetched visible to VoxTerrain
            DebugVoxel.Info("VoxelTerrain: Set Visible");
            d = GetComponent<Damageable>();
            DebugVoxel.Info("VoxelTerrain: Grabbed Damageable");
            d.SetRejectDamageHandler(RejectDamageEvent);
            DebugVoxel.Info("VoxelTerrain: Commanding DamageHandler");
            //            d.rejectDamageEvent += RejectDamageEvent;
            Visible_damageable.SetValue(v, d, null);
            DebugVoxel.Info("VoxelTerrain: Set DamageHandler");
            voxFriendLookup = new Dictionary<IntVector3, VoxTerrain>();
            mr = GetComponentInChildren<MeshRenderer>();
            mf = GetComponentInChildren<MeshFilter>();
            meshCol = GetComponentInChildren<MeshCollider>();
            mesh = new Mesh();
            mesh.MarkDynamic();
            DebugVoxel.Info("VoxelTerrain: Absorbed children");

            mcubes = new MarchingCubes() { interpolate = true };//, sampleProc = Sample };
            DebugVoxel.Info("VoxelTerrain: Shunted Voxel Terrain generation");
            PendingBleedBrushEffects = new List<BrushEffect>();
            PendingBleedBrush = new List<ManDamage.DamageInfo>();
            if (Buffer == null)
            {
                DebugVoxel.Info("VoxelTerrain: Added buffer");
                Buffer = MarchingCubes.CreateNewBuffer(voxBlockSize);
            }
            BufferSet = false;
            transform.localScale = Vector3.one;
            DebugVoxel.Info("VoxelTerrain: Operations complete!");
        }

        private void OnSpawn()
        {
            try
            {
                name = "VoxTerrainChunk";
                mr.enabled = false;
                meshCol.enabled = false;
                if (parent != null)
                    DebugVoxel.Assert("For some reason parent was set - when we are pooling we SHOULDN'T have a parent!");
                if (Buffer == null)
                    Buffer = MarchingCubes.CreateNewBuffer(voxBlockSize);
                Reset();
            }
            catch (Exception E)
            {
                DebugVoxel.Log(E);
                transform.Recycle();
            }
        }
        internal bool AutoSetTile()
        {
            if (parent == null)
            {
                parent = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position, false);
                if (parent == null)
                {
                    DebugVoxel.Assert("Voxel Terrain: Unable to acquire parent for tile at " + WorldPosition.FromScenePosition(transform.position).TileCoord);
                    return false;
                }
                parent.AddVisible(GetComponent<Visible>());
            }
            transform.parent = parent.StaticParent;
            if (transform.parent?.GetComponent<Terrain>() && transform.parent?.GetComponent<Terrain>() != parent.Terrain)
                throw new InvalidOperationException("connected Terrain doesn't match tile's terrain - how");
            if (parent.StaticParent.localPosition != Vector3.zero)
                DebugVoxel.Assert("Voxel Terrain: For some reason StaticParent is not zero");
            return true;
        }
        internal void OnWorldSpawn()
        {
            try
            {
                transform.localScale = Vector3.one;
                if (parent == null)
                {
                    if (!AutoSetTile())
                        throw new NullReferenceException("Voxel Terrain: For some reason parent worldTile is NOT LOADED");
                }
                if (Buffer == null)
                    Buffer = MarchingCubes.CreateNewBuffer(voxBlockSize);
                Singleton.Manager<ManWorldTreadmill>.inst.AddListener(this);
                Reset();
                voxFriendLookup.Clear();
                voxFriendLookup.Add(IntVector3.zero, this);
                if (parent == null)
                {
                    parent = Singleton.Manager<ManWorld>.inst.TileManager.LookupTile(transform.position + Vector3.one, false);
                    transform.parent = parent.StaticParent;
                    DebugVoxel.Assert("Voxel Terrain: For some reason parent worldTile wasn't set - setting NOW");
                    if (parent == null)
                        throw new NullReferenceException("Voxel Terrain: For some reason parent is STILL NULL");
                    if (parent.Terrain == null)
                        throw new NullReferenceException("Voxel Terrain: For some reason parent.Terrain is NULL");
                    if (parent.Terrain.terrainData == null)
                        throw new NullReferenceException("Voxel Terrain: For some reason parent.Terrain.terrainData is NULL");
                    if (parent.StaticParent == null)
                        throw new NullReferenceException("Voxel Terrain: Static parent is NULL, cannot continue");
                }
                /*
                var V = GetComponent<Visible>();
                try
                {
                    parent.Visibles[(int)V.type].Add(V.ID, V);
                }
                catch
                {
                    //DebugVoxel.LogError($"{V.type}-ItemType {V.ID}-ID {V.name}-name - FAILED");
                }
                */
                GetComponent<Visible>().m_ItemType = new ItemTypeInfo(VoxGenerator.ObjectTypeVoxelChunk, 0);
                mr.enabled = false;
                meshCol.enabled = false;
                enabled = true;
                PendingBleedBrush.Clear();
                PendingBleedBrushEffects.Clear();
                VoxTile.Add(transform.position, this);
            }
            catch (Exception E)
            {
                DebugVoxel.Log(E);
                transform.Recycle();
                throw E;
            }
        }

        internal void Reset()
        {
            Dirty = true;
            Modified = false;
            BufferSet = false;
        }
        internal void OnRecycle()
        {
            CancelInvoke();
            if (Modified && !Saved)
            {
                DebugVoxel.Log("Chunk is being removed, but did not save!");
                //if (parent == null || parent.SaveData == null)
                //{
                //    DebugVoxel.Log("The owning tile is null!");
                //}
                //else
                //{ 
                //    var store = new VoxelSaveData();
                //    store.Store(GetComponent<Visible>());
                //    var savedata = parent.SaveData;
                //    if (!savedata.m_StoredVisibles.ContainsKey(-8))
                //    {
                //        savedata.m_StoredVisibles.Add(-8, new List<ManSaveGame.StoredVisible>(100));
                //    }
                //    savedata.m_StoredVisibles[-8].Add(store);
                //}
            }
            Singleton.Manager<ManWorldTreadmill>.inst.RemoveListener(this);
            VoxTile.Remove(transform.position, this);
            transform.parent = null;
            parent = null;
            Reset();
            mr.enabled = false;
            meshCol.enabled = false;
            //DebugVoxel.Log("Recycling Voxel Terrain:"); DebugVoxel.Log(new System.Diagnostics.StackTrace().ToString());
        }

        public void OnMoveWorldOrigin(IntVector3 amountToMove)
        {
            //transform.localPosition -= amountToMove;
        }

        #region Base64 save conversion

        static byte[] GetBytes(CloudPair[,,] value)
        {
            int sizep1 = Mathf.RoundToInt(voxChunkSize / voxBlockResolution) + 1;

            int size = sizep1 * sizep1 * sizep1 * 2;
            byte[] array = new byte[size];

            int c = 0;
            for (int j = 0; j < sizep1; j++)
            {
                for (int k = 0; k < sizep1; k++)
                {
                    for (int i = 0; i < sizep1; i++)
                    {
                        var item = value[i, j, k];
                        array[c++] = (byte)item.Density;
                        array[c++] = item.Terrain;
                    }
                }
            }
            return array;
            //return OcTree.GetByteArrayFromBuffer(value, sizep1);
        }
        CloudPair[,,] FromBytes(byte[] array)
        {
            int sizep1 = Mathf.RoundToInt(voxChunkSize / voxBlockResolution) + 1;

            int size = sizep1 * sizep1 * sizep1 * 2;
            CloudPair[,,] value = new CloudPair[sizep1, sizep1, sizep1];

            int c = 0;
            for (int j = 0; j < sizep1; j++)
            {
                for (int k = 0; k < sizep1; k++)
                {
                    for (int i = 0; i < sizep1; i++)
                    {
                        value[i, j, k] = new CloudPair((sbyte)array[c++], array[c++]);
                    }
                }
            }
            return value;

            //return OcTree.GetBufferFromByteArray(array, sizep1);
        }

        private byte[] BufferToByteArray()
        {
            return GetBytes(Buffer);
        }

        public override string ToString()
        {
            return BufferToString();
        }

        private string BufferToString()
        {
            if (BufferSet && Buffer.Length > 7)
                return System.Convert.ToBase64String(BufferToByteArray());
            return "";
        }

        private void StringToBuffer(string base64buffer)
        {
            Modified = true;
            Dirty = true;
            var newBuffer = FromBytes(System.Convert.FromBase64String(base64buffer));
            if (newBuffer == null)
                BufferSet = false;
            else
            {
                int size = Mathf.RoundToInt(voxChunkSize / voxBlockResolution);
                int sizep1 = size + 1;
                if (newBuffer.GetLength(0) != sizep1 || newBuffer.GetLength(1) != sizep1 || newBuffer.GetLength(2) != sizep1)
                {
                    BufferSet = false;
                    DebugVoxel.Assert("Voxel SaveData entry was corrupted, ignoring");
                }
                else
                {
                    //cloudStorage.Enqueue(Buffer);
                    BufferSet = true;
                    Buffer = newBuffer;
                }
            }
        }
        #endregion

        public Dictionary<IntVector3, VoxTerrain> voxFriendLookup;
        /// <summary>
        /// AUTO-SET in Setup()
        /// </summary>
        public static int BleedWrap = 2;

        public CloudPair[,,] Buffer = null;
        public enum BakeState
        {
            Baking,
            Meshing,
            Finalization,
            Done,
        }
        public bool Dirty = false, DirtyPartial = false, Processing = false, FindVoxels = false;
        public BakeState BakeStatus = BakeState.Done;

        //public VoxDispenser vd;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider meshCol;
        public Mesh mesh;
        public Damageable d;
        public byte terrainType = 0;
        //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        MarchingCubes mcubes;
        List<Vector3> normalcache;
        List<BrushEffect> PendingBleedBrushEffects;
        List<ManDamage.DamageInfo> PendingBleedBrush;


        internal static Dictionary<byte, Material> _matcache = new Dictionary<byte, Material>();
        internal static Dictionary<byte, PhysicMaterial> _phycache = new Dictionary<byte, PhysicMaterial>();
        private bool _modified;
        private bool Modified { get => _modified; set { Saved = Saved && !value; _modified = value; } }
        private bool Saved;

        private static Dictionary<byte, Transform> _impactPrefab = new Dictionary<byte, Transform>();
        private static Transform ImpactPrefab(byte ID)
        {
            Transform prefab;
            if (_impactPrefab.TryGetValue(ID, out prefab))
            {
                return prefab;
            }
            Biome b = ManWorld.inst.CurrentBiomeMap.LookupBiome((byte)(ID / 2));
            if (b != null)
            {
                prefab = b.GetImpactPrefab("BulletMini");
            }
            _impactPrefab.Add(ID, prefab);
            return prefab;
        }


        internal static Material sharedMaterial = MakeMaterial();

        private static Material MakeMaterial()
        {
            var shader = Shader.Find("Standard");
            if (!shader)
            {
                IEnumerable<Shader> shaders = Resources.FindObjectsOfTypeAll<Shader>();
                shaders = shaders.Where(s => s.name == "Standard");
                shader = shaders.ElementAt(1);
            }
            var material = new Material(shader); 
            //*
            material.DisableKeyword("_EMISSION");
            // */ material.EnableKeyword("_EMISSION");
            material.EnableKeyword("_NORMALMAP");
            //material.EnableKeyword("_METALLICGLOSSMAP");
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetColor("_EmissionColor", new Color(0, 0, 0, 0));
            material.SetFloat("_DstBlend", 0f);
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_UVSec", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Parallax", 0.02f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Mode", 0f);
            material.SetFloat("_Glossiness", 0.069f);
            material.SetFloat("_Cutoff", 0.5f);
            return material;
        }
        private static Material MakeMaterial_attempt1()
        {
            /*
            string name = "Standard";//"Nature/Terrain/Diffuse";
            var shader = Shader.Find(name);
            if (!shader)
            {
                IEnumerable<Shader> shaders = Resources.FindObjectsOfTypeAll<Shader>();
                foreach (var item in shaders)
                {
                    DebugVoxel.Log(item.name.NullOrEmpty() ? "<NULL>" : item.name);
                }
                shaders = shaders.Where(s => s.name == name);
                shader = shaders.ElementAt(1);
            }
            var material = new Material(shader)
            {
                globalIlluminationFlags = MaterialGlobalIlluminationFlags.None,
            };
            
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.EnableKeyword("_SPECGLOSSMAP");
            //material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_");
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            */
            string name = "Artifact_Grasslands";
            IEnumerable<Material> mats = Resources.FindObjectsOfTypeAll<Material>();
            Material material = new Material(mats.Where(s => s.name == name).First());
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_DetailNormalMap", null);
            /*
            var material = new Material(shader)
            {
                shaderKeywords = new string[] { "_DETAIL_MULX2" , "_GLOSSYREFLECTIONS_OFF" ,
                    "_METALLICGLOSSMAP", "_NORMALMAP"},
                globalIlluminationFlags = MaterialGlobalIlluminationFlags.None,
            }*/
            return material;
        }
        private static Material MakeMaterial_LEGACY()
        {
            var shader = Shader.Find("Standard");
            if (!shader)
            {
                IEnumerable<Shader> shaders = Resources.FindObjectsOfTypeAll<Shader>();
                shaders = shaders.Where(s => s.name == "Standard");
                shader = shaders.ElementAt(1);
            }
            var material = new Material(shader);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICGLOSSMAP");
            return material;
        }


        private static Material GetMaterialFromBiome(byte ID)
        {
            Material result = null;
            try
            {
                if (!_matcache.TryGetValue(ID, out result))
                {
                    if (ManVoxelTerrain.extraMats.TryGetValue(ID, out result))
                        _matcache.Add(ID, result);
                    else
                    {
                        Biome b = ManWorld.inst.CurrentBiomeMap.LookupBiome((byte)(ID / 2));
                        if (b == null)
                            throw new NullReferenceException("biome null");
                        result = new Material(sharedMaterial);
                        var bmat = (ID % 2 == 0 ? b.MainMaterialLayer : b.AltMaterialLayer);
                        //bmat.metallic;
                        //bmat.smoothness;
                        //bmat.specular;
                        result.SetTexture("_MainTex", bmat.diffuseTexture);
                        result.SetTexture("_BumpMap", bmat.normalMapTexture);
                        result.renderQueue = 1000;
                        _matcache.Add(ID, result);
                    }
                }
            }
            catch (Exception E)
            {
                DebugVoxel.Log(E);
                var nultx = new Texture2D(4096, 4096);
                for (int y = 0; y < 4096; y++)
                {
                    for (int x = 0; x < 4096; x++)
                    {
                        nultx.SetPixel(x, y, new Color(Mathf.PerlinNoise(x / 500f, y / 500f), Mathf.PerlinNoise(x / 500f + 1, y / 500f), Mathf.PerlinNoise(x / 500f, y / 500f + 1)));
                    }
                }
                nultx.Apply();
                result.SetTexture("_MainTex", nultx);
            }
            return result;
        }
        private static PhysicMaterial GetPhyMaterialFromBiome(byte ID)
        {
            PhysicMaterial result;
            try
            {
                if (!_phycache.TryGetValue(ID, out result))
                {
                    if (ManVoxelTerrain.biomeFriction.TryGetValue(ID, out result))
                        _phycache.Add(ID, result);
                    else
                    {
                        Biome b = ManWorld.inst.CurrentBiomeMap.LookupBiome((byte)(ID / 2));
                        if (b == null)
                            throw new NullReferenceException("biome null");
                        result = new PhysicMaterial()
                        {
                            name = b.name + "_Friction",
                            bounciness = 0,
                            bounceCombine = PhysicMaterialCombine.Maximum,
                            dynamicFriction = b.SurfaceFriction,
                            staticFriction = 1f,
                            frictionCombine = PhysicMaterialCombine.Maximum,
                        };
                        _phycache.Add(ID, result);
                    }
                }
            }
            catch (Exception E)
            {
                DebugVoxel.Log(E);
                result = new PhysicMaterial();
            }
            return result;
        }


        public string WriteBoolState()
        {
            return $"Enabled?{enabled}, GameObjectEnabled?{gameObject.activeInHierarchy}{gameObject.activeSelf}, Dirty?{Dirty}, Processing?{Processing}, BakeStatus?{BakeStatus}, BufferExists?{(BufferSet ? Buffer.Length.ToString() : "false")}, Parent?{parent != null}";
        }

        public void HideTerrain()
        {
            parent.Terrain.enabled = false;
            parent.Terrain.GetComponent<TerrainCollider>().enabled = false;
        }
        private void MarchChunks()
        {
            new Task(delegate
            {
                mcubes.MarchChunk(transform.position, voxBlockSize, voxBlockResolution, voxBlockResolution, Buffer);

                normalcache = NormalSolver.RecalculateNormals(mcubes.GetIndices(), mcubes.GetVertices(), 70);

                BakeStatus = BakeState.Meshing;
            }).Start();
        }
        private int VoxelsBelow;
        private int VoxelsAbove;
        internal void Remote_LatePreUpdate()
        {
        }
        internal void LatePreUpdate()
        {
            try
            {
                if (parent == null)
                {
                    if (!AutoSetTile())
                        return;
                    DebugVoxel.Log("VoxTerrain was adopted...");
                }
                if (BakeStatus == BakeState.Finalization || DirtyPartial)
                {
                    if (TryFixNormals())
                    {
                        DirtyPartial = false;
                        BakeStatus = BakeState.Done;
                        mesh.SetNormals(normalcache);
                        //normalcache.Clear();

                        meshCol.sharedMesh = mesh;
                        meshCol.contactOffset = 0.001f;
                        meshCol.enabled = true;
                        mf.sharedMesh = mesh;
                        mr.enabled = true;

                        Invoke("HideTerrain", 0.1f);

                        Processing = false;
                    }
                }
            }
            catch (Exception e)
            {
                DebugVoxel.Log("Voxel Terrain: ERROR (pre) - " + e);
                BakeStatus = BakeState.Done;
                Processing = false;

                throw e;
            }
        }
        /// <summary>
        /// Doesn't work properly at all, something keep screwing up the ordering
        /// </summary>
        /// <returns></returns>
        private bool TryFixNormals()
        {
            upDirty = false;
            downDirty = false;
            leftDirty = false;
            rightDirty = false;
            fwdDirty = false;
            backDirty = false;
            /*
            if (upDirty && !TryFixEdge(Vector3.up))
                return false;
            upDirty = false;
            if (downDirty && !TryFixEdge(Vector3.down))
                return false;
            downDirty = false;
            if (leftDirty && !TryFixEdge(Vector3.left))
                return false;
            leftDirty = false;
            if (rightDirty && !TryFixEdge(Vector3.right))
                return false;
            rightDirty = false;
            if (fwdDirty && !TryFixEdge(Vector3.forward))
                return false;
            fwdDirty = false;
            if (backDirty && !TryFixEdge(Vector3.back))
                return false;
            backDirty = false;
            */
            return true;
        }
        private IEnumerable<int> GetTriVerts(List<int> indices, int triPoint)
        {
            int offset = triPoint * 3;
            yield return indices[offset];
            yield return indices[offset + 1];
            yield return indices[offset + 2];
        }
        private IEnumerable<int> GetTriIndexes(List<int> indices, int index)
        {
            for (int i = 0; indices.Count > i; i++)
            {
                if (indices[i] == index)
                    yield return i / 3;
            }
        }
        private bool rightDirty = false;
        private bool leftDirty = false;
        private bool upDirty = false;
        private bool downDirty = false;
        private bool fwdDirty = false;
        private bool backDirty = false;
        private bool TryFixEdge(Vector3 direction)
        {
            var friend = FindFriend(direction);
            if (friend)
            {
                if (BakeStatus < BakeState.Finalization ||
                    friend.BakeStatus < BakeState.Finalization)
                    return false;
                if (friend.normalcache == null || !friend.normalcache.Any())
                    return true;
                int error = 0;
                try
                {
                    int firstHalf = 0;
                    List<int> tris = new List<int>();
                    List<Vector3> verts = new List<Vector3>();
                    List<int> indices = new List<int>();
                    Dictionary<Vector3, int> pointsCheck = new Dictionary<Vector3, int>();

                    HashSet<int> trisCheck = new HashSet<int>();
                    var ourEdgeIndices = mcubes.edgeIndices[direction];
                    var ourIndices = mcubes.GetIndices();
                    var ourVerts = mcubes._vertices;
                    error = 1;
                    foreach (var item in ourEdgeIndices)
                    {
                        foreach (var triIndex in GetTriIndexes(ourIndices, item))
                        {
                            if (trisCheck.Add(triIndex))
                            {
                                bool created = false;
                                firstHalf++;
                                foreach (var vertI in GetTriVerts(ourIndices, triIndex))
                                {
                                    error = 2;
                                    Vector3 vert = ourVerts[vertI];
                                    Vector3 vertSnap = vert;
                                    for (int i = 0; i < 3; i++)
                                        vertSnap[i] = (int)(vertSnap[i] * 10000) / 10000f;
                                    if (pointsCheck.TryGetValue(vertSnap, out int val))
                                    {
                                        indices.Add(val);

                                    }
                                    else
                                    {
                                        pointsCheck.Add(vertSnap, verts.Count);
                                        indices.Add(verts.Count);
                                        verts.Add(vert);
                                        created = true;
                                    }
                                }
                                if (created)
                                    tris.Add(triIndex);
                            }
                        }
                    }

                    error = 3;
                    trisCheck.Clear();
                    var otherEdgeIndices = friend.mcubes.edgeIndices[-direction];
                    var otherIndices = friend.mcubes.GetIndices();
                    var otherVerts = friend.mcubes._vertices;
                    error = 4;
                    foreach (var item in otherEdgeIndices)
                    {
                        foreach (var triIndex in GetTriIndexes(otherIndices, item))
                        {
                            if (trisCheck.Add(triIndex))
                            {
                                bool created = false;
                                foreach (var vertI in GetTriVerts(otherIndices, triIndex))
                                {
                                    error = 5;
                                    Vector3 vert = otherVerts[vertI] - (direction * voxChunkSize);
                                    Vector3 vertSnap = vert;
                                    for (int i = 0; i < 3; i++)
                                        vertSnap[i] = (int)(vertSnap[i] * 100000) / 100000f;
                                    if (pointsCheck.TryGetValue(vertSnap, out int val))
                                    {
                                        indices.Add(val);
                                    }
                                    else
                                    {
                                        pointsCheck.Add(vertSnap, verts.Count);
                                        indices.Add(verts.Count);
                                        verts.Add(vert);
                                        created = true;
                                    }
                                }
                                if (created)
                                    tris.Add(triIndex);
                            }
                        }
                    }

                    error = 6;
                    if (!((float)tris.Count).Approximately(indices.Count / 3f))
                        throw new Exception("tris [" + tris.Count + "] mismatch indices 1/3 [" + indices.Count + "]");
                    var norms = NormalSolver.RecalculateNormals(indices.ToArray(), verts.ToArray(), 70);
                    if (norms.Count != tris.Count)
                        throw new Exception("tris [" + tris.Count + "] mismatch norms [" + norms.Count + "]");
                    error = 7;
                    if (friend.BakeStatus < BakeState.Finalization ||
                        friend.normalcache == null)
                        throw new Exception("friend unexpected deltas");
                    for (int i = 0; i < firstHalf; i++)
                        normalcache[tris[i]] = norms[i];
                    /*
                    error = 8;
                    if (friend.normalcache.Count != norms.Count) throw new Exception("friend.normalcache [" + friend.normalcache.Count + "] mismatch norms [" + norms.Count + "]");
                    
                    for (int i = firstHalf; i < Mathf.Min(tris.Count, norms.Count); i++)
                    {
                        int triVal = tris[i];
                        if (triVal > friend.normalcache.Count)
                            throw new Exception("triVal [" + triVal + "] out of range of friend.normalcache [" + friend.normalcache.Count + "]");
                        friend.normalcache[triVal] = norms[i];
                    }
                    if (direction == Vector3.up)
                        friend.upDirty = false;
                    else if (direction == Vector3.down)
                        friend.downDirty = false;
                    else if (direction == Vector3.right)
                        friend.rightDirty = false;
                    else if (direction == Vector3.left)
                        friend.leftDirty = false;
                    else if (direction == Vector3.forward)
                        friend.fwdDirty = false;
                    else
                        friend.backDirty = false;
                    */
                }
                catch (Exception e)
                {
                    throw new Exception("ErrorCode " + error, e);
                }
            }
            return true;
        }
        
        internal void Remote_LatePostUpdate()
        {
        }
        internal void LateUpdate()
        {
            try
            {
                LatePreUpdate();
                if (BakeStatus == BakeState.Meshing)
                {
                    if (transform.lossyScale.x <= 0)
                        DebugVoxel.Log("Voxel Terrain: For some reason our x scale was negative or zero");
                    if (transform.lossyScale.y <= 0)
                        DebugVoxel.Log("Voxel Terrain: For some reason our y scale was negative or zero");
                    if (transform.lossyScale.z <= 0)
                        DebugVoxel.Log("Voxel Terrain: For some reason our z scale was negative or zero");

                    mesh = new Mesh();
                    //mesh.vertices = mcubes.GetVertices().ToArray();
                    mesh.SetVertices(mcubes.GetVertices());

                    mesh.SetUVs(0, mcubes._uvs);//new Vector2[mesh.vertices.Length];


                    //for (int i = 0; i < mesh.vertices.Length; i++)
                    //mesh.uv[i] = new Vector2(mesh.vertices[i].x, mesh.vertices[i].y);

                    mesh.RecalculateBounds();

                    Material[] mats = new Material[mcubes._indices.Count];

                    int i = 0;
                    mesh.subMeshCount = mcubes._indices.Count;
                    foreach (var pair in mcubes._indices)
                    {
                        mesh.SetTriangles(pair.Value, i);
                        mats[i] = GetMaterialFromBiome(pair.Key);
                        i++;
                    }

                    mr.sharedMaterials = mats;

                    if (mcubes._indices.Any())
                    {
                        terrainType = mcubes._indices.ElementAt(mcubes._indices.Count / 2).Key;
                        meshCol.sharedMaterial = GetPhyMaterialFromBiome(terrainType);
                    }

                    mcubes.PartialReset();
                    BakeStatus = BakeState.Finalization;
                }

                if (FindVoxels)
                {
                    FindVoxels = false;
                    Processing = true;
                    if (VoxelsBelow != 0)
                    {
                        //DebugVoxel.Log($"<{CountBelow}");
                        for (int i = -1; i >= VoxelsBelow; i--)
                        {
                            FindOrCreateFriend(Vector3.up * i);
                        }
                    }
                    if (VoxelsAbove != 0)
                    {
                        //DebugVoxel.Log($"{CountAbove}->");
                        for (int i = 1; i <= VoxelsAbove; i++)
                        {
                            FindOrCreateFriend(Vector3.up * i);
                        }
                    }
                    MarchChunks();
                    return;
                }

                if (!Processing)
                {
                    if (PendingBleedBrushEffects.Count != 0)
                    {
                        while (0 < PendingBleedBrushEffects.Count)
                        {
                            if (!BufferSet) break;
                            BrushModifyBuffer(PendingBleedBrushEffects[0]);
                            PendingBleedBrushEffects.RemoveAt(0);
                        }
                        TerraTechETCUtil.WorldDeformer.OnTerrainDeformed.Send(parent);
                    }

                    if (Dirty)
                    {
                        DirtyPartial = false;
                        if (transform.rotation != Quaternion.identity)
                            DebugVoxel.Log("dumb worldtile rotated to " + transform.rotation.ToString() + " when it MUST be " + Quaternion.identity.ToString());
                        //stopwatch.Restart();
                        BakeStatus = BakeState.Baking;
                        Processing = true;
                        /*
                        if (Dirty)
                        {
                            upDirty = downDirty = leftDirty = rightDirty = fwdDirty = backDirty = true;
                            var friend = FindFriend(Vector3.up);
                            if (friend)
                            {
                                friend.downDirty = true;
                                friend.DirtyPartial = true;
                            }
                            friend = FindFriend(Vector3.down);
                            if (friend)
                            {
                                friend.upDirty = true;
                                friend.DirtyPartial = true;
                            }
                            friend = FindFriend(Vector3.left);
                            if (friend)
                            {
                                friend.rightDirty = true;
                                friend.DirtyPartial = true;
                            }
                            friend = FindFriend(Vector3.right);
                            if (friend)
                            {
                                friend.leftDirty = true;
                                friend.DirtyPartial = true;
                            }
                            friend = FindFriend(Vector3.forward);
                            if (friend)
                            {
                                friend.backDirty = true;
                                friend.DirtyPartial = true;
                            }
                            friend = FindFriend(Vector3.back);
                            if (friend)
                            {
                                friend.fwdDirty = true;
                                friend.DirtyPartial = true;
                            }
                        }
                        */
                        //DebugVoxel.Log("dumb worldtile is at " + transform.localPosition.ToString());
                        //transform.position = transform.position.SetY(parent.Terrain.transform.position.y);
                        Vector3 voxelOffset = transform.localPosition;

                        ManVoxelTerrain.GatherVoxelData();
                        //cloudStorage.Enqueue(Buffer);
                        //Buffer = cloudStorage.Dequeue();
                        if (!BufferSet)
                        {
                            BufferSet = true;
                            float[,] terrainDataFast;
                            switch (ManVoxelTerrain.state)
                            {
                                case VoxelState.Preparing:
                                    // WAIT
                                    break;
                                case VoxelState.Normal:
                                    // Make a shrinkwrapped plane of voxels 
                                    if (Buffer == null)
                                        DebugVoxel.Assert("Voxel Terrain: Buffer is NULL");
                                    if (parent == null)
                                        DebugVoxel.Assert("Voxel Terrain: parent is NULL");
                                    DebugVoxel.Info("Generating voxels Normal");
                                    try
                                    {
                                        terrainDataFast = parent.BiomeMapData.heightData.heights;
                                        if (MarchingCubes.heightSize == -200)
                                            MarchingCubes.heightSize = parent.Terrain.terrainData.size.y;
                                    }
                                    catch (Exception)
                                    {
                                        terrainDataFast = MarchingCubes.GetRealHeights(parent);
                                        DebugVoxel.Log("Tile FAILED to have BiomeMapData.heightData.heights, doing costly alternative...");
                                        //throw new ArgumentNullException(terrainDataFast == null ? "terrainDataFast" : "terrainData");
                                    }
                                    new Task(delegate
                                    {
                                        MarchingCubes.SetBufferFromTerrain(Buffer, terrainDataFast, parent, voxelOffset,
                                        voxBlockSize, voxBlockResolution,
                                        out VoxelsBelow, out VoxelsAbove);
                                        FindVoxels = true;
                                    }).Start();
                                    Dirty = false;
                                    return;
                                case VoxelState.RandD:
                                    // Make a flat plane of Voxels
                                    if (Buffer == null)
                                        DebugVoxel.Assert("Voxel Terrain: Buffer is NULL");
                                    DebugVoxel.Info("Generating voxels RandD");
                                    mcubes.SetBuffer(Buffer, voxelOffset, voxBlockSize, voxBlockResolution);
                                    break;
                                default:
                                    break;
                            }
                        }
                        Dirty = false;
                        //Buffer = mcubes.CreateBuffer(transform.position, Mathf.RoundToInt(ChunkSize / voxelSize), voxelSize);

                        MarchChunks();
                    }
                }
            }
            catch (Exception e)
            {
                DebugVoxel.Log("Voxel Terrain: ERROR (post) - " + e);

                throw e;
            }
        }

        private void FixedUpdate()
        {
            if (!Processing)
            {
                if (PendingBleedBrush.Count != 0)
                {
                    while (0 < PendingBleedBrush.Count)
                    {
                        if (!BufferSet) break;
                        ProcessDamageEvent(PendingBleedBrush[0], 0x00);
                        PendingBleedBrush.RemoveAt(0);
                    }
                }
            }
        }

        private struct BrushEffect
        {
            public Vector3 LocalPos;
            public float Radius;
            public float Change;
            public byte Terrain;
            public Func<float, float, CloudPair[,,], Vector3, IntVector3, byte, int> ModifyBuffer;

            public BrushEffect(Vector3 LocalPos, float Radius, float Change,
                 Func<float, float, CloudPair[,,], Vector3, IntVector3, byte, int> ModifyBuffer, byte Terrain)
            {
                this.LocalPos = LocalPos;
                this.Radius = Radius;
                this.Change = Change;
                this.Terrain = Terrain;
                this.ModifyBuffer = ModifyBuffer;
            }
        }

        private static float PointInterpolate(float Radius, float Distance)
        {
            return Mathf.Min(Mathf.Max(Radius - Distance, 0), voxBlockResolution) / voxBlockResolution;
        }
        private static int SphericalDeltaAdd(float Radius, float Change, CloudPair[,,] buffer,
            Vector3 LocalPos, IntVector3 inPos, byte terrain)
        {
            return PointAddBuffer(inPos.x, inPos.y, inPos.z, PointInterpolate(Radius,
                Vector3.Distance(inPos, LocalPos)) * Change, buffer, terrain);
        }
        private static int SphericalDeltaSub(float Radius, float Change, CloudPair[,,] buffer,
            Vector3 LocalPos, IntVector3 inPos, byte terrain)
        {
            return PointSubBuffer(inPos.x, inPos.y, inPos.z, PointInterpolate(Radius,
                Vector3.Distance(inPos, LocalPos)) * Change, buffer);
        }
        private static int SphericalLevel(float Radius, float Change, CloudPair[,,] buffer,
            Vector3 LocalPos, IntVector3 inPos, byte terrain)
        {
            CloudPair pre = buffer[inPos.x, inPos.y, inPos.z];
            CloudPair post;
            float Expected = LocalPos.y - inPos.y;
            float Delta = Expected - pre.DensityFloat;
            if (Delta == 0)
                return 0;
            if (Delta > 0)
            {
                Delta = Mathf.Min(Delta, PointInterpolate(Radius, Vector3.Distance(inPos, LocalPos)) * Change);
                post = pre.AddDensityAndSeepTerrain(Delta, terrain);
            }
            else
            {
                Delta = Mathf.Max(Delta, -PointInterpolate(Radius, Vector3.Distance(inPos, LocalPos)) * Change);
                post = pre.SubDensity(Delta);
            }
            buffer[inPos.x, inPos.y, inPos.z] = post;
            return (int)Mathf.Clamp(Delta * 128f + 0.5f, -128, 127);
        }

        /// <summary> Debug feedback for the last DigNormal inputted from DeformVoxelTerrain() </summary>
        private Vector3 DigNormal;

        /// <summary> 
        /// Alter the terrain!
        /// Was BleedBrushModifyBuffer 
        /// </summary>
        /// <param name="ScenePos">Where in the scene we shall do our operation</param>
        /// <param name="Radius">The radius to apply the operation</param>
        /// <param name="Change">The polarity and strength of the radius brush operation</param>
        /// <param name="DigNormal">The normal which we add in relation to our target vox</param>
        /// <param name="Terrain">The material byte used for the terrain visual at said point</param>
        public void SphereDeltaVoxTerrain(Vector3 ScenePos, float Radius, float Change, Vector3 DigNormal, byte Terrain = 0xFF)
        {
            this.DigNormal = DigNormal;
            var LocalPos = (ScenePos - transform.position) / voxBlockResolution;
            var ContactTerrain = Terrain == 0xFF ? Buffer[Mathf.RoundToInt(LocalPos.x), Mathf.RoundToInt(LocalPos.y), Mathf.RoundToInt(LocalPos.z)].Terrain : Terrain;
            if (Change > 0)
            {
                foreach (var item in IterateAndCreateVoxTerrain(ScenePos, Radius))
                    item.BBMB_internal(ScenePos, Radius, Change, SphericalDeltaAdd, ContactTerrain);
            }
            else
            {
                foreach (var item in IterateAndCreateVoxTerrain(ScenePos, Radius))
                    item.BBMB_internal(ScenePos, Radius, Change, SphericalDeltaSub, ContactTerrain);
            }
        }
        public void SphereLevelVoxTerrain(Vector3 ScenePos, float Radius, float Change, Vector3 DigNormal, byte Terrain = 0xFF)
        {
            this.DigNormal = DigNormal;
            var LocalPos = (ScenePos - transform.position) / voxBlockResolution;
            var ContactTerrain = Terrain == 0xFF ? Buffer[Mathf.RoundToInt(LocalPos.x), Mathf.RoundToInt(LocalPos.y), Mathf.RoundToInt(LocalPos.z)].Terrain : Terrain;
            if (Change > 0)
            {
                foreach (var item in IterateAndCreateVoxTerrain(ScenePos, Radius))
                {
                    item.BBMB_internal(ScenePos, Radius, Change, SphericalLevel, ContactTerrain);
                    item.Modified = true;
                }
            }
        }
        public IEnumerable<VoxTerrain> IterateVoxTerrain(Vector3 ScenePos, float Radius)
        {
            var LocalPos = (ScenePos - transform.position) / voxBlockResolution;
            int xmax = Mathf.CeilToInt((LocalPos.x + Radius + 2) / BleedWrap),
                ymax = Mathf.CeilToInt((LocalPos.y + Radius + 2) / BleedWrap),
                zmax = Mathf.CeilToInt((LocalPos.z + Radius + 2) / BleedWrap);
            for (int x = Mathf.FloorToInt((LocalPos.x - Radius - 2) / BleedWrap); x <= xmax; x++)
                for (int y = Mathf.FloorToInt((LocalPos.y - Radius - 2) / BleedWrap); y <= ymax; y++)
                    for (int z = Mathf.FloorToInt((LocalPos.z - Radius - 2) / BleedWrap); z <= zmax; z++)
                    {
                        VoxTerrain vox = FindFriend(new Vector3(x, y, z));
                        if (vox != null)
                            yield return vox;
                    }
        }
        public IEnumerable<VoxTerrain> IterateAndCreateVoxTerrain(Vector3 ScenePos, float Radius)
        {
            var LocalPos = (ScenePos - transform.position) / voxBlockResolution;
            int xmax = Mathf.CeilToInt((LocalPos.x + Radius + 2) / BleedWrap),
                ymax = Mathf.CeilToInt((LocalPos.y + Radius + 2) / BleedWrap),
                zmax = Mathf.CeilToInt((LocalPos.z + Radius + 2) / BleedWrap);
            for (int x = Mathf.FloorToInt((LocalPos.x - Radius - 2) / BleedWrap); x <= xmax; x++)
                for (int y = Mathf.FloorToInt((LocalPos.y - Radius - 2) / BleedWrap); y <= ymax; y++)
                    for (int z = Mathf.FloorToInt((LocalPos.z - Radius - 2) / BleedWrap); z <= zmax; z++)
                        yield return FindOrCreateFriend(new Vector3(x, y, z));
        }


        private static int PointAddBuffer(int x, int y, int z, float Change, CloudPair[,,] Buffer, byte Terrain)
        {
            CloudPair pre = Buffer[x, y, z];
            CloudPair post = pre.AddDensityAndSeepTerrain(Change, Terrain);
            Buffer[x, y, z] = post;
            return post.Density - pre.Density;
        }
        private static int PointSubBuffer(int x, int y, int z, float Change, CloudPair[,,] Buffer)
        {
            CloudPair pre = Buffer[x, y, z];
            CloudPair post = pre.SubDensity(Change);
            /*
            // Doesn't seem to do anything
            if (pre.Density > 0 && post.Density <= 0) 
                SpawnChunk(new Vector3(x, y, z) * voxBlockResolution + transform.position);
            */
            Buffer[x, y, z] = post;
            return post.Density - pre.Density;
        }

        private void BBMB_internal(Vector3 ScenePos, float Radius, float Change,
            Func<float, float, CloudPair[,,], Vector3, IntVector3, byte, int> ModifyBuffer, byte Terrain)
        {
            CheckAndUpdateNearbyScenery();
            var LocalPos = (ScenePos - transform.position) / voxBlockResolution;
            if (Processing || !BufferSet)
                PendingBleedBrushEffects.Add(new BrushEffect(LocalPos, Radius, Change, ModifyBuffer, Terrain));
            else
                BrushModifyBuffer(LocalPos, Radius, Change, ModifyBuffer, Terrain);
        }
        private void CheckAndUpdateNearbyScenery()
        {
            foreach (var item in VoxGenerator.IterateNearbyScenery(this))
            {
                ManVoxelTerrain.resDispThisFrame.Add(item);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ScenePos"></param>
        /// <param name="Radius"></param>
        /// <param name="ModifyBuffer">Radius, ChangeDelta, Buffer, LocalPos, posInBuffer, out Cost</param>
        /// <param name="Terrain"></param>
        /// <returns></returns>
        private int BrushModifyBuffer(Vector3 LocalPos, float Radius, float Change,
            Func<float, float, CloudPair[,,], Vector3, IntVector3, byte, int> ModifyBuffer, byte Terrain)
        {
            int Result = 0;
            int zmax = Mathf.Min(Mathf.CeilToInt(LocalPos.z + Radius), BleedWrap + 1),
                ymax = Mathf.Min(Mathf.CeilToInt(LocalPos.y + Radius), BleedWrap + 1),
                xmax = Mathf.Min(Mathf.CeilToInt(LocalPos.x + Radius), BleedWrap + 1);
            for (int z = Mathf.Max(0, Mathf.FloorToInt(LocalPos.z - Radius)); z < zmax; z++)
                for (int y = Mathf.Max(0, Mathf.FloorToInt(LocalPos.y - Radius)); y < ymax; y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(LocalPos.x - Radius)); x < xmax; x++)
                    {
                        int delta = ModifyBuffer(Radius, Change, Buffer, LocalPos, new IntVector3(x, y, z), Terrain);
                        if (delta != 0)
                            Dirty = true;
                        Result += delta;
                    }
            Modified |= Dirty;
            return Result;
        }
        private void BrushModifyBuffer(BrushEffect b)
        {
            BrushModifyBuffer(b.LocalPos, b.Radius, b.Change, b.ModifyBuffer, b.Terrain);
        }
        private IEnumerable<IntVector3> IterateInBuffer(IntVector3 LocalPos, float Radius)
        {
            int zmax = Mathf.Min(Mathf.CeilToInt(LocalPos.z + Radius), BleedWrap + 1),
                ymax = Mathf.Min(Mathf.CeilToInt(LocalPos.y + Radius), BleedWrap + 1),
                xmax = Mathf.Min(Mathf.CeilToInt(LocalPos.x + Radius), BleedWrap + 1);
            for (int z = Mathf.Max(0, Mathf.FloorToInt(LocalPos.z - Radius)); z < zmax; z++)
                for (int y = Mathf.Max(0, Mathf.FloorToInt(LocalPos.y - Radius)); y < ymax; y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(LocalPos.x - Radius)); x < xmax; x++)
                        yield return new IntVector3(x, y, z);
        }

        private VoxTerrain FindFriend_exp(Vector3 Direction)
        {
            return VoxTile.Lookup(transform.position + (Direction *
                voxChunkSize));
        }
        private VoxTerrain FindFriend(Vector3 Direction)
        {
            VoxTerrain fVox;
            if (voxFriendLookup.TryGetValue(Direction, out fVox))
            {
                if (fVox != null && fVox.enabled && fVox.transform.position - 
                    transform.position == Direction * voxChunkSize)
                    return fVox;
                voxFriendLookup.Remove(Direction);
            }
            /*
            // Might miss things.  Potential point of failiure!
            //   Although it is Grid-aligned, should never miss in nature but
            //    this is after all, a collider collision check..
            fVox = VoxGenerator.OverlapCheckFast(transform.position +
                Direction * voxChunkSize + voxBlockCenterOffset, voxChunkSize / 8f);
            */
            fVox = VoxTile.Lookup(transform.position + (Direction *
                voxChunkSize) + voxBlockCenterOffset);
            if (fVox)
                voxFriendLookup.Add(Direction, fVox);
            return fVox;
        }
        private VoxTerrain FindOrCreateFriend(Vector3 Direction)
        {
            /*
            return VoxChunk.LookupOrCreate(transform.position + (Direction *
                voxChunkSize));
            // */
            //*
            VoxTerrain fVox = FindFriend(Direction);
            if (fVox != null)
                return fVox;
            fVox = CreateFriend(Direction, Direction == Vector3.down ? 1f : -1f);
            voxFriendLookup.Add(Direction, fVox);
            fVox.voxFriendLookup.Add(-Direction, this);
            return fVox;
            // */
        }

        private VoxTerrain CreateFriend(IntVector3 Direction, float Fill)
        {
            var pos = transform.position + Direction * voxChunkSize;
            VoxTerrain newVox = VoxGenerator.GenerateVoxPart(pos);
            int Size = Mathf.RoundToInt(voxChunkSize / voxBlockResolution) + 1;
            return newVox;
        }

        private int SpawnChunk(Vector3 position)
        {
            return 0;
            //if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer())
            //{
            //    return 0;
            //}
            //ChunkTypes chunkTypes = ChunkTypes.SenseOre;
            //Vector3 velocity = Vector3.zero;
            //velocity = DigNormal * 40f;

            //Visible visible = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)chunkTypes), position, Quaternion.identity, false, false, false, true);
            //if (visible)
            //{
            //    velocity += (2f/*Random speed*/) * UnityEngine.Random.insideUnitSphere;
            //    Vector3 angularVelocity = (2f /*Random spinny speed*/) * UnityEngine.Random.insideUnitSphere;
            //    visible.pickup.InitNew(velocity, angularVelocity);
            //    visible.trans.SetParent(Singleton.dynamicContainer);
            //    visible.SetCollidersEnabled(true);
            //    return 1;
            //}
            //DebugVoxel.Log(new object[]
            //{
            //        string.Concat(new string[]
            //        {
            //            "VoxTerrain.SpawnChunk - '",
            //            base.name,
            //            "' Failed to spawn resource: ",
            //            chunkTypes.ToString(),
            //            " ...check ResourceTable"
            //        })
            //});
            //return 0;
        }

        /// <summary>
        /// Called when the voxel terrain is damaged by anything
        /// </summary>
        internal bool RejectDamageEvent(ManDamage.DamageInfo arg, bool DealActualDamage)
        {
            PendingBleedBrush.Add(arg);
            return true;
        }
        private void ProcessDamageEvent(ManDamage.DamageInfo arg, byte Terrain)
        {
            float Radius, Strength;
            float dmg = arg.Damage * 0.01f;
            switch (arg.DamageType)
            {
                case ManDamage.DamageType.Cutting:
                case ManDamage.DamageType.Standard:
                    Radius = voxBlockResolution * 0.6f + dmg * 0.15f;
                    Strength = -1f;//.5f;//-0.01f - dmg * 0.0001f;
                    Radius /= voxBlockResolution;
                    break;
                case ManDamage.DamageType.Blast:
                    Radius = voxBlockResolution * 0.7f + dmg * 0.4f;
                    Strength = -.5f;//-0.01f - dmg * 0.001f;
                    Radius /= voxBlockResolution;
                    break;
                case ManDamage.DamageType.Impact:
                    Radius = voxBlockResolution * 0.5f + dmg * 0.25f;
                    Strength = -.5f;//-0.01f - dmg * 0.0001f;
                    if (arg.Source is Tank tank)
                        Radius = Mathf.Min(Radius / voxBlockResolution, ManVoxelTerrain.MaxTechImpactRadius);
                    else
                        Radius /= voxBlockResolution;
                    break;
                default:
                    return;
            }
            //ImpactPrefab(0)?.Spawn(Singleton.dynamicContainer, arg.HitPosition, Quaternion.Euler(arg.DamageDirection));
            //TT_VoxelTerrain.Class1.SendVoxBrush(new TT_VoxelTerrain.Class1.VoxBrushMessage(arg.HitPosition, Radius, Strength, Terrain));
            SphereDeltaVoxTerrain(arg.HitPosition, Radius, Strength, -arg.DamageDirection, Terrain);
        }


        static System.Reflection.MethodInfo StoredTile_AddStoredVisibleToTile = typeof(ManSaveGame.StoredTile).GetMethod("AddStoredVisibleToTile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);


        internal class VoxelSaveData : ManSaveGame.StoredVisible
        {
            public override bool CanRestore()
            {
                return true;//!string.IsNullOrEmpty(Cloud64);
            }

            public override Visible SpawnAndRestore()
            {
                //DebugVoxel.Log("Loading unique VOX...");
                var tile = ManWorld.inst.TileManager.LookupTile(m_WorldPosition.TileCoord);
                Vector3 pos = GetBackwardsCompatiblePosition();
                pos.y += MarchingCubes.TileYOffsetDelta;
                var vox = VoxGenerator.CheckAndGenerateVoxPart(pos);
                if (!string.IsNullOrEmpty(Cloud64))
                {
                    vox.StringToBuffer(Cloud64);
                }
                return vox.GetComponent<Visible>();
            }

            public override void Store(Visible visible)
            {
                if (false)
                {
                    visible.SaveForStorage(this);
                }
                else
                {
                    m_Position = Vector3.zero;
                    //m_WorldPosition = WorldPosition.FromScenePosition(visible.centrePosition);
                    m_WorldPosition = WorldPosition.FromScenePosition(visible.trans.position); 
                    /*
                    if (visible.centrePosition.IsNaN())
                    {
                        DebugVoxel.LogError("Saving Visible " + visible.name + " for storage - CentrePos is NaN. Using transform pos instead");
                        m_WorldPosition = WorldPosition.FromScenePosition(visible.trans.position);
                    }*/
                    m_ID = visible.ID;
                }
                Store(visible.GetComponent<VoxTerrain>());
            }

            private void Store(VoxTerrain vox)
            {
                if (vox.Modified)
                {
                    vox.Saved = true;
                    Cloud64 = vox.BufferToString();
                    //DebugVoxel.Log("Stored unique VOX...");
                }
                else
                {
                    Cloud64 = null;
                }
            }
            public string Cloud64;
        }

        internal static class OcTree
        {
            static IntVector3 GetExtents(int Extents, int Corner, IntVector3 CurrentExtents)
            {
                int xMin = (Corner % 2) * Extents;
                int yMin = ((Corner / 2) % 2) * Extents;
                int zMin = ((Corner / 4) % 2) * Extents;
                return new IntVector3(xMin + CurrentExtents.x, yMin + CurrentExtents.y, zMin + CurrentExtents.z);
            }

            static CloudPair[,,] CopyBufferFromCorner(int Extents, int Corner, CloudPair[,,] Source)
            {
                var Buffer = new CloudPair[Extents, Extents, Extents];
                int xMin = (Corner % 2) * Extents;
                int yMin = ((Corner / 2) % 2) * Extents;
                int zMin = ((Corner / 4) % 2) * Extents;
                for (int j = 0; j < Extents; j++)
                    for (int k = 0; k < Extents; k++)
                        for (int i = 0; i < Extents; i++)
                            Buffer[i, j, k] = Source[i + xMin, j + yMin, k + zMin];
                return Buffer;
            }

            static void WriteValueToBufferArea(int Extents, IntVector3 MinExtents, ref CloudPair[,,] Buffer, CloudPair Value)
            {
                for (int j = MinExtents.x; j < MinExtents.x + Extents; j++)
                    for (int k = MinExtents.x; k < MinExtents.x + Extents; k++)
                        for (int i = MinExtents.x; i < MinExtents.x + Extents; i++)
                            Buffer[i, j, k] = Value;
            }

            static bool SplitCondition(CloudPair[,,] Buffer)
            {
                var control = Buffer[0, 0, 0];
                foreach (var pair in Buffer)
                {
                    if (!control.Equals(pair)) return true;
                }
                return false;
            }

            static void Split(ref List<byte> Out, CloudPair[,,] Buffer, int Extents)
            {
                if (SplitCondition(Buffer))
                {
                    Console.Write("#");
                    Out.Add(255); // Mark as has children (Terrain should not normally be 0xFF)
                    for (int i = 0; i < 8; i++)
                    {
                        var nExtents = Extents / 2;
                        var nBuffer = CopyBufferFromCorner(nExtents, i, Buffer);
                        Split(ref Out, nBuffer, nExtents);
                    }
                    return;
                }
                Console.Write("+");
                Out.Add(Buffer[0, 0, 0].Terrain); //Terrain first
                Out.Add((byte)Buffer[0, 0, 0].Density); //Density second
            }

            static void Join(ref CloudPair[,,] Buffer, int Extents, IntVector3 MinCorner, byte[] bytes, ref int CurrentStep)
            {
                if (bytes[CurrentStep] == 255)
                {
                    Console.Write("#");
                    CurrentStep++;
                    for (int i = 0; i < 8; i++)
                    {
                        var nExtents = Extents / 2;
                        //var nBuffer = CopyBufferFromCorner(nExtents, i, Buffer);
                        Join(ref Buffer, nExtents, GetExtents(nExtents, i, MinCorner), bytes, ref CurrentStep);
                    }
                    return;
                }
                Console.Write("+");
                WriteValueToBufferArea(Extents, MinCorner, ref Buffer, new CloudPair((sbyte)bytes[CurrentStep + 1], bytes[CurrentStep]));
                CurrentStep += 2;
            }

            public static byte[] GetByteArrayFromBuffer(CloudPair[,,] buffer, int ArraySize)
            {
                var bytes = new List<Byte>();
                Split(ref bytes, buffer, ArraySize);
                return bytes.ToArray();
            }

            public static CloudPair[,,] GetBufferFromByteArray(byte[] bytes, int DimensionSize)
            {
                var result = new CloudPair[DimensionSize, DimensionSize, DimensionSize];
                int iterator = 0;
                Join(ref result, DimensionSize, IntVector3.zero, bytes, ref iterator);
                return result;
            }
        }
    }
}
