using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace DynamicCam.Patches;

public static class CameraFollowFix
{
    public static float RealMapSize { get; private set; } = 10f;

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

    [HarmonyPatch(typeof(HealthHandler))]
    public static class HealthHandlerPatch
    {
        [HarmonyPatch("Die")]
        [HarmonyPatch("ForcedDie")]
        [HarmonyPostfix]
        public static void DiePostfix(HealthHandler __instance)
        {
            if (LevelCreator.Instance != null) return;
            if (RealMapSize <= 15f && !DefaultFollowSmallMap) return;

            var controller = __instance.GetComponent<Controller>();
            FollowCamManager.Instance.OnPlayerDied(controller);

        }
    }

    [HarmonyPatch(typeof(GameManager))]
    public static class GameManagerPatch
    {
        [HarmonyPatch("OnMapSizeChanged")]
        [HarmonyPostfix]
        public static void OnMapSizeChangedPostfix(float newSize)
        {
            if (LevelCreator.Instance != null) return;

            RealMapSize = newSize;

            Debug.Log($"[DynamicCam] Map Size Loaded: {RealMapSize}. Resetting to Full View.");

            if (FollowCamManager.Instance != null)
            {
                FollowCamManager.Instance.SetFollowDesired(false);
            }
        }

        [HarmonyPatch("StartCountDown")]
        [HarmonyPostfix]
        public static void StartCountDownPostfix()
        {
            if (LevelCreator.Instance != null) return;

            if (MatchmakingHandler.IsNetworkMatch && (RealMapSize > 15f || DefaultFollowSmallMap))
            {
                //Debug.Log("[QOL] Online Match & Big Map. Starting 1s Delay...");
                FollowCamManager.Instance.SetFollowDelayed(true, 1.0f);
            }
        }

        [HarmonyPatch("PrepareMapForTravel")]
        [HarmonyPostfix]
        public static void PrepareMapForTravelPostfix(bool comingIn)
        {
            if (LevelCreator.Instance != null) return;

            if (comingIn)
            {
                if (!MatchmakingHandler.IsNetworkMatch && (RealMapSize > 15f || DefaultFollowSmallMap))
                {
                    //Debug.Log("[QOL] Local Match & Big Map (Map Moving In). Starting 1s Delay...");
                    FollowCamManager.Instance.SetFollowDelayed(true, 1.0f);
                }
            }
        }

        //[HarmonyPatch("AllButOnePlayersDied")]
        //[HarmonyPatch("NetworkAllPlayersDiedButOne")]
        //[HarmonyPostfix]
        //public static void AllButOnePlayersDiesPostfix()
        //{
        //    if (LevelCreator.Instance != null) return;
        //    FollowCamManager.Instance.SetFollowDelayed(false, 0f);
        //}

        [HarmonyPatch("RevivePlayer")]
        [HarmonyPostfix]
        public static void RevivePlayerPostfix(Controller playerToRevive)
        {
            if (LevelCreator.Instance != null) return;
            if (RealMapSize <= 15f && !DefaultFollowSmallMap) return;

            if (playerToRevive == Helper.controller)
            {
                FollowCamManager.Instance.OnLocalPlayerRevived(playerToRevive);
            }
        }

        [HarmonyPatch("MovePlayer")]
        [HarmonyPrefix]
        public static void MovePlayerPrefix(Rigidbody player)
        {
            if (player == null || LevelCreator.Instance != null) return;
            if (FollowCamManager.Instance == null) return;

            var controller = player.transform.root.GetComponent<Controller>();

            if (controller != null && controller == Helper.controller)
            {
                FollowCamManager.Instance.RegisterController(Helper.controller);
            }
        }
    }

    [HarmonyPatch(typeof(LevelCreator))]
    public static class LevelCreatorPatch
    {
        [HarmonyPatch("OnPlayTestStarted")]
        [HarmonyPostfix]
        public static void OnPlayTestStartedPostfix()
        {
            if (MapSizeHandler.Instance != null)
            {
                var currentSize = MapSizeHandler.Instance.mapSize;
                RealMapSize = currentSize;

                if (currentSize > 15f || DefaultFollowSmallMap)
                {
                    Debug.Log($"[DynamicCam] Editor Big Map ({currentSize}). Enabling Follow.");
                    FollowCamManager.Instance.SetFollowDesired(true);
                }
            }
        }

        [HarmonyPatch("OnPlayTestEnded")]
        [HarmonyPostfix]
        public static void OnPlayTestEndedPostfix()
        {
            FollowCamManager.Instance.SetFollowDesired(false);
        }
    }

    [HarmonyPatch(typeof(AspectFix))]
    public static class AspectFixPatch
    {
        // Rewrite the UpdateSize() method to use the real map size
        [HarmonyPatch("UpdateSize")]
        [HarmonyPrefix]
        public static bool UpdateSizePrefix(
            AspectFix __instance,
            ref float ___mapSize, ref float ___currentMapSize, ref float ___mapSizeVelocity,
            float ___spring, float ___drag, Camera ___cam, bool ___scale
        )
        {
            var scaleMultiplier = 1f;

            if (MapSizeHandler.Instance)
            {
                if (WorkshopStateHandler.IsPlayTestingMode || ___scale)
                {
                    var realEditorSize = MapSizeHandler.Instance.mapSize;

                    if (WorkshopStateHandler.IsPlayTestingMode)
                    {
                        var shouldZoom = (realEditorSize > 15f || DefaultFollowSmallMap) && FollowCamManager.Instance.ShouldZoomIn;

                        if (shouldZoom)
                        {
                            scaleMultiplier = DefaultOrthographicSize / 10f;
                        }
                        else
                        {
                            scaleMultiplier = realEditorSize / 10f;
                        }
                    }
                    else
                    {
                        scaleMultiplier = realEditorSize / 10f;
                    }
                }
            }
            else
            {
                var targetSize = ___mapSize;

                var shouldZoom = (___mapSize > 15f || DefaultFollowSmallMap) && FollowCamManager.Instance.ShouldZoomIn;

                if (shouldZoom)
                {
                    targetSize = DefaultOrthographicSize; // 10f;
                }

                ___mapSizeVelocity += (targetSize - ___currentMapSize) * ___spring;
                ___mapSizeVelocity *= ___drag;
                ___currentMapSize += ___mapSizeVelocity;

                scaleMultiplier = ___currentMapSize / 10f;
            }

            var screenRatio = Mathf.Max((float)Screen.width / (float)Screen.height, 1.78f);

            if (___cam != null)
            {
                ___cam.orthographicSize = screenRatio * 10f / ___cam.aspect * scaleMultiplier;
            }

            return false;
        }
    }
}