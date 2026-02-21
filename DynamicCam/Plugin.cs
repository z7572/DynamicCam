using BepInEx;
using BepInEx.Bootstrap;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;

namespace DynamicCam
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogInfo("DynamicCam is loaded!");

            var qolVersion = GetTargetPluginVersion(QOL_GUID);
            if (qolVersion != null && qolVersion >= new Version(1, 22, 2)) IsQOLExLoaded = true;

            try
            {
                Logger.LogInfo("Loading configuration options from config file...");
                ConfigHandler.InitConfig(Config);
            }
            catch (Exception e)
            {
                Logger.LogError("Exception on loading configuration: " + e.StackTrace + e.Message + e.Source + e.InnerException);
            }            
            try
            {
                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private static Version GetTargetPluginVersion(string targetPluginGuid)
        {
            var targetPlugin = Chainloader.PluginInfos.FirstOrDefault(p => p.Key == targetPluginGuid).Value;
            if (targetPlugin == null) return null;

            return targetPlugin.Metadata.Version;
        }

        public static bool IsQOLExLoaded { get; private set; }
        public const string QOL_GUID = "monky.plugins.QOL";

        public const string PLUGIN_GUID = "z7572.DynamicCam";
        public const string PLUGIN_NAME = "DynamicCam";
        public const string PLUGIN_VERSION = "1.1";
    }
}