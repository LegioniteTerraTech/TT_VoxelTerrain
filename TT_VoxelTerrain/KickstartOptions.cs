using System;
using UnityEngine;

namespace TT_VoxelTerrain
{
    internal class KickstartOptions
    {
        public static ModHelper.ModConfig _thisMod;

        public static Nuterra.NativeOptions.OptionKey ExcalivatorToggle;
        public static Nuterra.NativeOptions.OptionKey LevelMode;
        public static Nuterra.NativeOptions.OptionKey UpMode;
        public static Nuterra.NativeOptions.OptionKey AddSize;
        public static Nuterra.NativeOptions.OptionKey SubSize;
        public static Nuterra.NativeOptions.OptionToggle CanPlayerDestroyTerrain;
        public static Nuterra.NativeOptions.OptionToggle CanEnemyDestroyTerrain;

        public static void InitHooks()
        {
            if (_thisMod != null)
                return;

            ModHelper.ModConfig thisConfig = new ModHelper.ModConfig();
            thisConfig.BindConfig<MassShifter>(null, "toolHotkeySerial");
            thisConfig.BindConfig<MassShifter>(null, "toolLevelSerial");
            thisConfig.BindConfig<MassShifter>(null, "toolUpSerial");
            thisConfig.BindConfig<MassShifter>(null, "toolAddSerial");
            thisConfig.BindConfig<MassShifter>(null, "toolSubSerial");
            MassShifter.toolHotkey = (KeyCode)MassShifter.toolHotkeySerial;
            MassShifter.toolLevel = (KeyCode)MassShifter.toolLevelSerial;
            MassShifter.toolUp = (KeyCode)MassShifter.toolUpSerial;
            MassShifter.toolAdd = (KeyCode)MassShifter.toolAddSerial;
            MassShifter.toolSub = (KeyCode)MassShifter.toolSubSerial;

            thisConfig.BindConfig<ManVoxelTerrain>(null, "AllowPlayerDamageTerraform");
            thisConfig.BindConfig<ManVoxelTerrain>(null, "AllowEnemyDamageTerraform");

            _thisMod = thisConfig;

            string nameGeneral = ManVoxelTerrain.ModName + " - General";
            ExcalivatorToggle = new Nuterra.NativeOptions.OptionKey("Terrain Tool Toggle Hotkey", nameGeneral, MassShifter.toolHotkey);
            ExcalivatorToggle.onValueSaved.AddListener(() => { 
                MassShifter.toolHotkeySerial = (int)(MassShifter.toolHotkey = ExcalivatorToggle.SavedValue);
            });
            LevelMode = new Nuterra.NativeOptions.OptionKey("Terrain Tool Level Hotkey", nameGeneral, MassShifter.toolLevel);
            LevelMode.onValueSaved.AddListener(() => {
                MassShifter.toolLevelSerial = (int)(MassShifter.toolLevel = LevelMode.SavedValue);
            });
            UpMode = new Nuterra.NativeOptions.OptionKey("Terrain Tool Raise Hotkey", nameGeneral, MassShifter.toolUp);
            UpMode.onValueSaved.AddListener(() => {
                MassShifter.toolUpSerial = (int)(MassShifter.toolUp = UpMode.SavedValue);
            });
            AddSize = new Nuterra.NativeOptions.OptionKey("Terrain Tool Add Size/Type Hotkey", nameGeneral, MassShifter.toolAdd);
            AddSize.onValueSaved.AddListener(() => {
                MassShifter.toolAddSerial = (int)(MassShifter.toolAdd = AddSize.SavedValue);
            });
            SubSize = new Nuterra.NativeOptions.OptionKey("Terrain Tool Subtract Size/Type Hotkey", nameGeneral, MassShifter.toolSub);
            SubSize.onValueSaved.AddListener(() => {
                MassShifter.toolSubSerial = (int)(MassShifter.toolSub = SubSize.SavedValue);
            });

            CanPlayerDestroyTerrain = new Nuterra.NativeOptions.OptionToggle("Allow player to terraform with weapons",
                nameGeneral, ManVoxelTerrain.AllowPlayerDamageTerraform);
            CanPlayerDestroyTerrain.onValueSaved.AddListener(() => {
                ManVoxelTerrain.AllowPlayerDamageTerraform = CanPlayerDestroyTerrain.SavedValue;
            });
            CanEnemyDestroyTerrain = new Nuterra.NativeOptions.OptionToggle("Allow enemy to terraform with weapons (NOT ADVISED)",
                nameGeneral, ManVoxelTerrain.AllowEnemyDamageTerraform);
            CanEnemyDestroyTerrain.onValueSaved.AddListener(() => {
                ManVoxelTerrain.AllowEnemyDamageTerraform = CanEnemyDestroyTerrain.SavedValue;
            });


            Nuterra.NativeOptions.NativeOptionsMod.onOptionsSaved.AddListener(() =>
            {
                try
                {
                    _thisMod.WriteConfigJsonFile();
                }
                catch (Exception e)
                {
                    DebugVoxel.Log(e);
                }
            });
        }
    }
}
