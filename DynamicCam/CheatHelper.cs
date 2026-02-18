using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicCam;

public static class CheatHelper
{
    private static readonly Dictionary<Controller, Rigidbody[]> _cachedPlayerRigs = new();
    private const float _rigsCleanUpInterval = 10f;
    private static float _lastRigsCacheTime;
    public static Vector3 GetPlayerPosition(Controller controller)
    {
        if (Time.time - _lastRigsCacheTime > _rigsCleanUpInterval)
        {
            _lastRigsCacheTime = Time.time;
            var deadKeys = _cachedPlayerRigs.Keys.Where(c => c == null).ToList();
            foreach (var deadKey in deadKeys) _cachedPlayerRigs.Remove(deadKey);
        }
        if (!_cachedPlayerRigs.TryGetValue(controller, out var rigs))
        {
            rigs = controller.GetComponentsInChildren<Rigidbody>();
            _cachedPlayerRigs[controller] = rigs;
        }

        var position = Vector3.zero;
        for (var i = 0; i < rigs.Length; i++)
        {
            position += rigs[i].transform.position;
        }
        position /= rigs.Length;
        return position;
    }
}
