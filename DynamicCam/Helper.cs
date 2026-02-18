using System;
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
    public static Controller controller; // The controller of the local user (ours)
}