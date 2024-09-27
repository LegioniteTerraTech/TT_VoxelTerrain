using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

internal static class DebugVoxel
{
    private const string modName = "Voxel Terrain";

    internal static bool ShouldLog = true;
    internal const bool LogAll = false;
    private const bool LogDev = true;

    internal static void Info(string message)
    {
        if (!ShouldLog || !LogAll)
            return;
        Debug.Log(message);
    }
    internal static void Log(string message)
    {
        if (!ShouldLog)
            return;
        Debug.Log(message);
    }
    internal static void Log(Exception e)
    {
        if (!ShouldLog)
            return;
        Debug.Log(e);
    }
    internal static void Assert(string message)
    {
        if (!ShouldLog)
            return;
        Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
    }
    internal static void Assert(bool shouldAssert, string message)
    {
        if (!ShouldLog || !shouldAssert)
            return;
        Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
    }
    internal static void LogError(string message)
    {
        if (!ShouldLog)
            return;
        Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
    }
    internal static void LogDevOnly(string message)
    {
        if (!LogDev)
            return;
        Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
    }
    internal static void FatalError(string e)
    {
        try
        {
            ManUI.inst.ShowErrorPopup(modName + ": ENCOUNTERED CRITICAL ERROR: " + e + StackTraceUtility.ExtractStackTrace());
        }
        catch { }
        Debug.Log(modName + ": ENCOUNTERED CRITICAL ERROR: " + e);
        Debug.Log(modName + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        Debug.Log(modName + ": STACKTRACE: " + StackTraceUtility.ExtractStackTrace());
    }
    internal static void Exception(bool shouldAssert, string e)
    {
        if (shouldAssert)
            throw new Exception(e);
    }
    internal static void LogException(Exception e)
    {
        Debug.Log(e);
    }

    private static bool SavedOver = false;
    private static FloatingTextOverlayData OverEdit;
    private static GameObject AllyTextStor;
    //private static CanvasGroup AllyCanGroup;
    internal static void PopupInfo(string text, WorldPosition pos)
    {
        if (!SavedOver)
        {
            AllyTextStor = AltUI.CreateCustomPopupInfo("NewTextVoxel", Color.black, out OverEdit);
            SavedOver = true;
        }
        AltUI.PopupCustomInfo(text, pos, OverEdit);
    }
}
