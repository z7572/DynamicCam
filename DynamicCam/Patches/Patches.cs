using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using LevelEditor;
using Object = UnityEngine.Object;

namespace DynamicCam.Patches;

[HarmonyPatch]
public static class Patches
{
    [HarmonyPatch(typeof(Controller))]
    private static class ControllerPatches
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(Controller __instance)
        {
            if (/* !__instance.isAI && */ !__instance.name.ToLower().Contains("snake"))
            {
                if (!Plugin.IsQOLExLoaded)
                {
                    if (__instance.gameObject.GetComponent<EdgeArrowManager>() == null)
                    {
                        __instance.gameObject.AddComponent<EdgeArrowManager>();
                        Debug.Log("Added EdgeArrowManager");
                    }
                }
            }
            if (__instance.HasControl && !__instance.IsAI()) // me
            {
                Helper.controller = __instance;

                try
                {
                    FollowCamManager.Instance.RegisterController(__instance);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error registering controller: {e.Message}");
                }

                if (!Plugin.IsQOLExLoaded)
                {
                    var barsHandler = Object.FindObjectOfType<BarsHandler>();
                    if (barsHandler != null)
                    {
                        barsHandler.gameObject.SetActive(false);
                        Debug.Log("Disabled Loading Bars!");
                    }
                }
            }
        }
    }
    
}
