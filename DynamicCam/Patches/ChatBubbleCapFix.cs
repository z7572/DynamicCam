using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicCam.Patches;

// Since we removed loading bars, we can use this to adjust chat bubble caps from a fixed rect to margins from the screen edges
// Useful for non-16:9 screens to prevent chat bubbles from being too narrow or too short
public static class ChatBubbleCapFix
{
    public class ChatBubbleConstraint : MonoBehaviour
    {
        private FollowTransform _followTransform;
        private Camera _cam;

        private float _marginX;
        private float _marginTop;
        private float _marginBottom;

        private float _visualCenterOffsetY;

        private bool _initialized = false;

        public void Init(Vector2 originalCap, Vector2 originalCapTop)
        {
            _followTransform = GetComponent<FollowTransform>();
            _cam = Camera.main;

            if (_cam == null) return;

            var startCamPos = _cam.transform.position;

            var localMinX = originalCap.x - startCamPos.z;
            var localMaxX = originalCapTop.x - startCamPos.z;
            var localMinY = originalCap.y - startCamPos.y;
            var localMaxY = originalCapTop.y - startCamPos.y;

            var width = Mathf.Abs(localMaxX - localMinX);
            var centeredHalfWidth = width / 2f;

            var refOrthoSize = 10f;
            var refAspect = 16f / 9f;
            var refScreenHalfHeight = refOrthoSize;
            var refScreenHalfWidth = refOrthoSize * refAspect; // ~17.77

            _marginX = refScreenHalfWidth - centeredHalfWidth;

            _marginTop = refScreenHalfHeight - localMaxY;
            _marginBottom = localMinY - (-refScreenHalfHeight);

            _marginX = Mathf.Max(_marginX, 0f);

            var localCenterY = (localMaxY + localMinY) / 2f;
            _visualCenterOffsetY = localCenterY;

            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || _followTransform == null) return;

            var currentHalfHeight = _cam.orthographicSize;
            var currentHalfWidth = currentHalfHeight * _cam.aspect;
            var camPos = _cam.transform.position;

            var screenLeft = camPos.z - currentHalfWidth;
            var screenRight = camPos.z + currentHalfWidth;
            var screenBottom = camPos.y - currentHalfHeight;
            var screenTop = camPos.y + currentHalfHeight;

            var newMinZ = screenLeft + _marginX;
            var newMaxZ = screenRight - _marginX;

            var newMaxY = screenTop - _marginTop;
            var newMinY = screenBottom + _marginBottom;

            if (newMinZ > newMaxZ)
            {
                newMinZ = camPos.z;
                newMaxZ = camPos.z;
            }

            if (newMinY > newMaxY)
            {
                var targetY = camPos.y + _visualCenterOffsetY;
                newMinY = targetY;
                newMaxY = targetY;
            }

            _followTransform.cap = new Vector2(newMinZ, newMinY);
            _followTransform.capTop = new Vector2(newMaxZ, newMaxY);
        }
    }

    [HarmonyPatch(typeof(FollowTransform))]
    public static class FollowTransformPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(FollowTransform __instance)
        {
            if (Plugin.IsQOLExLoaded) return;

            if (__instance.cap == Vector2.zero || __instance.capTop == Vector2.zero) return;

            var constraint = __instance.gameObject.GetComponent<ChatBubbleConstraint>();
            if (constraint == null)
            {
                constraint = __instance.gameObject.AddComponent<ChatBubbleConstraint>();
                constraint.Init(__instance.cap, __instance.capTop);
            }
        }
    }

    // Win scores UI aspect fix, but not work satisfyingly
    //    [HarmonyPatch(typeof(AspectFix))]
    //    class AspectFixPatch
    //    {
    //        private static float lastAspect = 0f;
    //        private static float lastOrthoSize = 0f;
    //        [HarmonyPatch("Start")]
    //        [HarmonyPostfix]
    //        private static void StartPostfix(Camera ___cam)
    //        {
    //            WinCounterUI scoreController = Object.FindObjectOfType<WinCounterUI>();
    //            if (scoreController == null) return;

    //            Transform scoreTransform = scoreController.transform;

    //            const float RefAspect = 16f / 9f;
    //            const float BaseSize = 10f;

    //            float currentAspect = ___cam.aspect;

    //            float screenRatio = Mathf.Max(currentAspect, RefAspect);
    //            float standardSize = screenRatio * 10f / currentAspect;

    //            float yRatio = standardSize / BaseSize;

    //            float targetWidth = BaseSize * RefAspect;
    //            float currentWidth = standardSize * currentAspect;
    //            float zRatio = currentWidth / targetWidth;

    //            Debug.Log($"[QOL] Fixing ScoreUI Positions. Aspect:{currentAspect:F2} YRatio:{yRatio:F2} ZRatio:{zRatio:F2}");

    //            foreach (Transform child in scoreTransform)
    //            {
    //                Vector3 originalPos = child.position;

    //                child.position = new Vector3(
    //                    originalPos.x,
    //                    originalPos.y * yRatio,
    //                    originalPos.z * zRatio
    //                );
    //            }
    //        }
    //    }
}