using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditor;

namespace DynamicCam;

public static class Helper
{
    public static bool DefaultFollowSmallMap
    {
        get => ConfigHandler.GetEntry<bool>("DefaultFollowSmallMap");
        set
        {
            if (DefaultFollowSmallMap == value) return;
            ConfigHandler.ModifyEntry("DefaultFollowSmallMap", value.ToString());
        }
    }

    public static float DefaultOrthographicSize
    {
        get => ConfigHandler.GetEntry<float>("DefaultOrthographicSize");
        set
        {
            if (Mathf.Approximately(DefaultOrthographicSize, value)) return;
            ConfigHandler.ModifyEntry("DefaultOrthographicSize", value.ToString());
        }
    }

    public static Controller controller; // The controller of the local user (ours)
}