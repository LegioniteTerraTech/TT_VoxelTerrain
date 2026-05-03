using System;
using System.Collections.Generic;
using TerraTechETCUtil;
using UnityEngine;

namespace TT_VoxelTerrain
{

    internal class MassShifter : MonoBehaviour
    {

        //private static List<int> tris = new List<int>();
        //private static List<Vector3> vertices = new List<Vector3>();

        public static VoxTerrain GetVoxelTerrain(Ray ray, float dist, out RaycastHit raycastHit)
        {
            if (Physics.Raycast(ray, out raycastHit, dist, VoxelGlobals.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore))
            {
                VoxTerrain vox = raycastHit.transform.gameObject.GetComponentInParent<VoxTerrain>();
                if (vox != null)
                    return vox;
            }
            return null;
        }
        public static bool AlterTerrainTool(Ray ray, float dist, float radius, float change, 
            byte terrain, Vector3 normal = default)
        {
            VoxTerrain vox = GetVoxelTerrain(ray, dist, out RaycastHit hitPoint);
            if (vox)
            {
                vox.SemiSphereDeltaVoxTerrain(hitPoint.point, radius / VoxelGlobals.voxBlockResolution, 
                    change, normal == default ? hitPoint.normal : normal, terrain);
                return true;
            }
            return false;
        }
        public static bool LevelTerrainTool(Ray ray, float dist, float radius, float change,
            byte terrain, Vector3 normal = default)
        {
            VoxTerrain vox = GetVoxelTerrain(ray, dist, out RaycastHit hitPoint);
            if (vox)
            {
                vox.SemiSphereLevelVoxTerrain(hitPoint.point, hitPoint.point, radius / VoxelGlobals.voxBlockResolution,
                    change, normal == default ? hitPoint.normal : normal, terrain);
                return true;
            }
            return false;
        }
        public static bool LevelTerrainTool(Ray ray, float dist, float radius, float change, 
            byte terrain, Vector3 Anchor, Vector3 normal = default)
        {
            VoxTerrain vox = GetVoxelTerrain(ray, dist, out RaycastHit hitPoint);
            if (vox)
            {
                if (normal == default)
                    normal = hitPoint.normal;
                vox.SemiSphereLevelVoxTerrain(Vector3.ProjectOnPlane(hitPoint.point, normal) + Anchor,
                    hitPoint.point, radius / VoxelGlobals.voxBlockResolution, change, normal, terrain);
                return true;
            }
            return false;
        }
        internal static Vector3 IntersectionOnPlane(Vector3 planePos, Vector3 planeNormal, Ray ray)
        {
            Plane plane = new Plane(planeNormal, planePos);
            if (plane.Raycast(ray, out float CollisionMag) && CollisionMag < 128)
                return (ray.direction.normalized * CollisionMag) + ray.origin;
            return plane.ClosestPointOnPlane((ray.direction.normalized * 128) + ray.origin);
            /*
            // approximation
            Vector3 deviance = Vector3.Project(ray.origin - planePos, planeNormal);
            float collsionDist = deviance.magnitude;
            Vector3 rayNorm = Vector3.Project(ray.direction * 128, planeNormal);
            float rayDist = rayNorm.magnitude;
            float correctedRayDistMulti = collsionDist / rayDist;
            return ray.direction * correctedRayDistMulti;
            */
        }

        internal static bool ARMED = false;
        public static TerraformerCursorState state = 0;
        public static KeyCode toolHotkey = KeyCode.H;
        public static int toolHotkeySerial = (int)toolHotkey;
        static Vector3 cachedPoint = Vector3.zero;
        static Vector3 cachedNormal = Vector3.up;
        static byte BrushMat = 0xFF;
        static int brushSize = Mathf.RoundToInt(VoxelGlobals.voxBlockResolution);

        public static KeyCode toolLevel = KeyCode.LeftControl;
        public static int toolLevelSerial = (int)toolLevel;
        public static KeyCode toolUp = KeyCode.LeftShift;
        public static int toolUpSerial = (int)toolUp;
        public static KeyCode toolDebug = KeyCode.Backspace;
        public static int toolDebugSerial = (int)toolDebug;

        public static KeyCode toolAdd = KeyCode.KeypadPlus;
        public static int toolAddSerial = (int)toolAdd;
        static bool AddBool => Input.GetKeyDown(toolAdd); //|| Input.GetKeyDown(KeyCode.RightBracket);
        public static KeyCode toolSub = KeyCode.KeypadMinus;
        public static int toolSubSerial = (int)toolSub;
        static bool SubBool => Input.GetKeyDown(toolSub); //|| Input.GetKeyDown(KeyCode.LeftBracket);

        internal void OnGUI()
        {
            if (ARMED && Input.GetMouseButton(0) && !ManPointer.inst.IsInteractionBlocked) // LOCK PLAYER CONTROLS
                ManModGUI.IsMouseOverAnyModGUI = 1;
        }
        public static void ToggleState(bool active)
        {
            if (ARMED != active)
            {
                ARMED = active;
                if (active)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Open);
                    UIHelpersExt.BigF5broningBannerSP("Enabled Terraforming", false, 3);
                    showBrushSize = true;
                }
                else
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Close);
                    UIHelpersExt.BigF5broningBannerSP("Disabled Terraforming", false, 3);
                }
                try
                {
                    ManVoxelTerrain.TheTerrainToolButton.SetToggleState(ARMED);
                }
                catch { }
            }
        }
        static bool showBrushSize = false;
        internal void Update()
        {
            bool showBrushMat = false;
            /*
            if (Input.GetKey(KeyCode.LeftAlt) && 
                ((Input.GetKey(KeyCode.Equals) && Input.GetKeyDown(KeyCode.Minus)) ||
                (Input.GetKeyDown(KeyCode.Equals) && Input.GetKey(KeyCode.Minus))))//*/

            if (Input.GetKeyDown(toolHotkey))
                ToggleState(!ARMED);

            if (ARMED)
            {
                if (Input.GetKey(toolUp))
                {
                    if (AddBool)
                    {
                        BrushMat++;
                        showBrushMat = true;
                    }
                    if (SubBool)
                    {
                        BrushMat--;
                        showBrushMat = true;
                    }
                }
                else
                {
                    if (AddBool)
                    {
                        brushSize++;
                        showBrushSize = true;
                    }
                    if (SubBool)
                    {
                        brushSize = Math.Max(brushSize - 1, 1);
                        showBrushSize = true;
                    }
                }

                Ray camRay = Singleton.camera.ScreenPointToRay(Input.mousePosition);
                VoxTerrain vox = GetVoxelTerrain(camRay, 10000, out RaycastHit hitPoint);
                if (vox)
                {
                    if (showBrushMat)
                        DebugVoxel.PopupInfo(BrushMat.ToString(),
                            WorldPosition.FromScenePosition(hitPoint.point));
                    else if (showBrushSize)
                        DebugVoxel.PopupInfo(brushSize.ToString(),
                            WorldPosition.FromScenePosition(hitPoint.point));
                    float transp = 0.6f;
                    float SFXtime = 0.75f;
                    if (Input.GetKeyDown(toolDebug))
                    {
                        state = TerraformerCursorState.Default;
                        DebugVoxel.PopupInfo(("ID " + vox.GetComponent<Visible>().ID).ToString(),
                            WorldPosition.FromScenePosition(hitPoint.point));
                        DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                            Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0f, 0f, 0f, transp));
                    }
                    else
                    {
                        float strength = 1f;
                        if (Input.GetKey(toolLevel))
                        {
                            state = TerraformerCursorState.Leveling;
                            if (Input.GetMouseButton(0))
                            {
                                if (Input.GetMouseButtonDown(0))
                                {
                                    cachedPoint = hitPoint.point;
                                    cachedNormal = hitPoint.normal;
                                }
                                Vector3 normal = Input.GetKey(KeyCode.LeftShift) ? Vector3.up : cachedNormal;
                                Vector3 point = IntersectionOnPlane(cachedPoint, normal, camRay);
                                vox.SemiSphereLevelVoxTerrain(point, hitPoint.point, brushSize / VoxelGlobals.voxBlockResolution, 
                                    strength, normal, BrushMat);
                                //SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, 0x00)); 
                                DebugExtUtilities.DrawDirIndicatorCircle(point, normal,
                                    Vector3.Cross(normal, Vector3.forward).normalized, brushSize, new Color(1f, 1f, 0f, transp));
                                DebugExtUtilities.DrawDirIndicator(point + (normal * 0.5f),
                                    point - (normal * 0.5f), new Color(1f, 1f, 0f, transp));
                                SFXHelpers.TankPlayLooping(Singleton.playerTank, TechAudio.SFXType.GCPlasmaCutter, SFXtime, 1);
                            }
                            else
                            {
                                Vector3 normal = Input.GetKey(KeyCode.LeftShift) ? Vector3.up : hitPoint.normal;
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, normal, 
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0.5f, 1f, 0f, transp));
                            }
                        }
                        else if (Input.GetKey(toolUp))
                        {
                            state = TerraformerCursorState.Up;
                            if (Input.GetMouseButton(0))
                            {
                                vox.SemiSphereDeltaVoxTerrain(hitPoint.point, brushSize / VoxelGlobals.voxBlockResolution,
                                    strength, hitPoint.normal, 0x00);
                                //SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, 0x00)); 
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0f, 1f, 0f, transp));
                                DebugExtUtilities.DrawDirIndicator(hitPoint.point, hitPoint.point + (hitPoint.normal * 2), new Color(0f, 1f, 0f, transp));
                                SFXHelpers.TankPlayLooping(Singleton.playerTank, TechAudio.SFXType.Refinery, SFXtime, 1);
                            }
                            else if (Input.GetMouseButtonDown(2))
                            {
                                BrushMat = vox.terrainType;
                                DebugVoxel.PopupInfo(("Mat " + vox.terrainType).ToString(),
                                    WorldPosition.FromScenePosition(hitPoint.point));
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(1f, 0f, 1f, transp));
                            }
                            else
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(1f, 1f, 1f, transp));
                        }
                        else
                        {
                            state = TerraformerCursorState.Down;
                            if (Input.GetMouseButton(0))
                            {
                                vox.SemiSphereDeltaVoxTerrain(hitPoint.point, brushSize / VoxelGlobals.voxBlockResolution,
                                    -strength, hitPoint.normal, BrushMat);
                                //SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -1f, 0x00));
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(1f, 0f, 0f, transp));
                                DebugExtUtilities.DrawDirIndicator(hitPoint.point, hitPoint.point + hitPoint.normal, new Color(1f, 0f, 0f, transp));
                                SFXHelpers.TankPlayLooping(Singleton.playerTank, TechAudio.SFXType.VENFlameThrower, SFXtime, 1);
                            }
                            else if (Input.GetMouseButtonDown(2))
                            {
                                DebugVoxel.PopupInfo(("ID " + vox.GetComponent<Visible>().ID).ToString(),
                                    WorldPosition.FromScenePosition(hitPoint.point));
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0f, 0f, 0f, transp));
                            }
                            else
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0f, 0f, 0f, transp));
                        }
                    }
                }
                else
                    state = TerraformerCursorState.None;
            }
            showBrushSize = false;
        }
    }
}
