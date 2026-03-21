using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace DynamicCam;

// From Monky's QOL-Mod
public static class ConfigHandler
{
    private static readonly Dictionary<string, ConfigEntryBase> EntriesDict = new(StringComparer.InvariantCultureIgnoreCase);

    private const string FollowCamSect = "FollowCam";
    private const string KeySect = "Keys";

    public static void InitConfig(ConfigFile config)
    {
        var dynamicCamKeybindEntry = config.Bind(KeySect, "DynamicCamKeybind", new KeyboardShortcut(KeyCode.F5),
            "切换相机跟随快捷键");
        EntriesDict[dynamicCamKeybindEntry.Definition.Key] = dynamicCamKeybindEntry;
        dynamicCamKeybindEntry.SettingChanged += (_, _) =>
        {
            FollowCamManager.Instance.Keybind = dynamicCamKeybindEntry.Value.MainKey;
        };

        var defaultFollowSmallMapEntry = config.Bind(FollowCamSect, "DefaultFollowSmallMap", false, "小地图是否也默认跟随视角");
        EntriesDict[defaultFollowSmallMapEntry.Definition.Key] = defaultFollowSmallMapEntry;

        var defaultOrthographicSizeEntry = config.Bind(FollowCamSect, "DefaultOrthographicSize", 10f, "默认视角大小");
        EntriesDict[defaultOrthographicSizeEntry.Definition.Key] = defaultOrthographicSizeEntry;

        var enableSpecWhenAliveEntry = config.Bind(FollowCamSect, "EnableSpecWhenAlive", false, "是否在玩家存活时启用视角切换");
        EntriesDict[enableSpecWhenAliveEntry.Definition.Key] = enableSpecWhenAliveEntry;
    }

    public static T GetEntry<T>(string entryKey, bool defaultValue = false)
        => defaultValue ? (T)EntriesDict[entryKey].DefaultValue : (T)EntriesDict[entryKey].BoxedValue;

    public static void ModifyEntry(string entryKey, string value)
        => EntriesDict[entryKey].SetSerializedValue(value);

    public static void ResetEntry(string entryKey)
    {
        var configEntry = EntriesDict[entryKey];
        configEntry.BoxedValue = configEntry.DefaultValue;
    }

    public static bool EntryExists(string entryKey)
        => EntriesDict.ContainsKey(entryKey);

    public static string[] GetConfigKeys() => EntriesDict.Keys.ToArray();

}