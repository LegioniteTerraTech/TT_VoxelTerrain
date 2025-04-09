using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Server;
using TerraTechETCUtil;
using UnityEngine;

namespace TT_VoxelTerrain
{

    internal class MassShifter : MonoBehaviour
    {
        private static List<int> tris = new List<int>();
        private static List<Vector3> vertices = new List<Vector3>();

        public static VoxTerrain GetVoxelTerrain(Ray ray, float dist, out RaycastHit raycastHit)
        {
            if (Physics.Raycast(ray, out raycastHit, dist, VoxTerrain.VoxelTerrainOnlyLayer, QueryTriggerInteraction.Ignore))
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
                vox.SemiSphereDeltaVoxTerrain(hitPoint.point, radius / VoxTerrain.voxBlockResolution, 
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
                vox.SemiSphereLevelVoxTerrain(hitPoint.point, hitPoint.point, radius / VoxTerrain.voxBlockResolution,
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
                    hitPoint.point, radius / VoxTerrain.voxBlockResolution, change, normal, terrain);
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

        static bool ARMED = false;
        static Vector3 cachedPoint = Vector3.zero;
        static Vector3 cachedNormal = Vector3.up;
        static byte BrushMat = 0xFF;
        static int brushSize = Mathf.RoundToInt(VoxTerrain.voxBlockResolution);
        internal void Update()
        {
            bool showBrushSize = false;
            bool showBrushMat = false;
            if (Input.GetKey(KeyCode.LeftAlt) && 
                ((Input.GetKey(KeyCode.Equals) && Input.GetKeyDown(KeyCode.Minus)) ||
                (Input.GetKeyDown(KeyCode.Equals) && Input.GetKey(KeyCode.Minus))))
            {
                ARMED = !ARMED;
                showBrushSize = true;
            }

            if (ARMED)
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))
                    {
                        BrushMat++;
                        showBrushMat = true;
                    }
                    if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))
                    {
                        BrushMat--;
                        showBrushMat = true;
                    }
                }
                else
                {
                    if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.RightBracket))
                    {
                        brushSize++;
                        showBrushSize = true;
                    }
                    if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.LeftBracket))
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
                    if (Input.GetKeyDown(KeyCode.Backspace))
                    {
                        DebugVoxel.PopupInfo(("ID " + vox.GetComponent<Visible>().ID).ToString(),
                            WorldPosition.FromScenePosition(hitPoint.point));
                        DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                            Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0f, 0f, 0f, transp));
                    }
                    else
                    {
                        float strength = 1f;
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            if (Input.GetMouseButton(0))
                            {
                                if (Input.GetMouseButtonDown(0))
                                {
                                    cachedPoint = hitPoint.point;
                                    cachedNormal = hitPoint.normal;
                                }
                                Vector3 normal = Input.GetKey(KeyCode.LeftShift) ? Vector3.up : cachedNormal;
                                Vector3 point = IntersectionOnPlane(cachedPoint, normal, camRay);
                                vox.SemiSphereLevelVoxTerrain(point, hitPoint.point, brushSize / VoxTerrain.voxBlockResolution, 
                                    strength, normal, BrushMat);
                                //SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, 0x00)); 
                                DebugExtUtilities.DrawDirIndicatorCircle(point, normal,
                                    Vector3.Cross(normal, Vector3.forward).normalized, brushSize, new Color(1f, 1f, 0f, transp));
                                DebugExtUtilities.DrawDirIndicator(point + (normal * 0.5f),
                                    point - (normal * 0.5f), new Color(1f, 1f, 0f, transp));
                            }
                            else
                            {
                                Vector3 normal = Input.GetKey(KeyCode.LeftShift) ? Vector3.up : hitPoint.normal;
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, normal, 
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0.5f, 1f, 0f, transp));
                            }
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            if (Input.GetMouseButton(0))
                            {
                                vox.SemiSphereDeltaVoxTerrain(hitPoint.point, brushSize / VoxTerrain.voxBlockResolution,
                                    strength, hitPoint.normal, 0x00);
                                //SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, 1f, 0x00)); 
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(0f, 1f, 0f, transp));
                                DebugExtUtilities.DrawDirIndicator(hitPoint.point, hitPoint.point + (hitPoint.normal * 2), new Color(0f, 1f, 0f, transp));
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
                            if (Input.GetMouseButton(0))
                            {
                                vox.SemiSphereDeltaVoxTerrain(hitPoint.point, brushSize / VoxTerrain.voxBlockResolution,
                                    -strength, hitPoint.normal, BrushMat);
                                //SendVoxBrush(new VoxBrushMessage(raycastHit.point, brushSize / TerrainGenerator.voxelSize, -1f, 0x00));
                                DebugExtUtilities.DrawDirIndicatorCircle(hitPoint.point, hitPoint.normal,
                                    Vector3.Cross(hitPoint.normal, Vector3.forward).normalized, brushSize, new Color(1f, 0f, 0f, transp));
                                DebugExtUtilities.DrawDirIndicator(hitPoint.point, hitPoint.point + hitPoint.normal, new Color(1f, 0f, 0f, transp));
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
            }
        }
    }
}
