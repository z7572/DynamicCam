// QOL-Ex
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using LevelEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DynamicCam.Patches;

[HarmonyPatch]
public static class UIFix
{
    private static GameObject EditorCanvas;

    private static void FixGameCanvas()
    {
        if (GameManager.Instance == null || GameManager.Instance.GameCanvas == null) return;
        if (Camera.main == null) return;

        var canvasObj = GameManager.Instance.GameCanvas;
        var camTransform = Camera.main.transform;

        var rigidCam = camTransform.parent;
        var targetParent = rigidCam != null ? rigidCam : camTransform;

        if (canvasObj.transform.parent != targetParent)
        {
            canvasObj.transform.SetParent(targetParent, true);
            Debug.Log($"GameCanvas reparented to {targetParent.name}.");
        }

        canvasObj.transform.localPosition = new Vector3(10f, 0f, 0f);
        canvasObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        if (canvasObj.GetComponent<UIStabilizer>() == null)
        {
            canvasObj.AddComponent<UIStabilizer>();
        }

        GameManager.Instance.LastAppliedScale = 1f;
    }

    [Obsolete("Chatfield wont be added in LevelEditor, use QOL-Ex instead")]
    private static void FixEditorCanvas()
    {
        if (LevelCreator.Instance == null || ResourcesManager.Instance == null || GameManager.Instance != null) return;
        if (Camera.main == null) return;
        if (EditorCanvas != null) return;

        EditorCanvas = new GameObject("EditorCanvas");

        EditorCanvas.transform.SetParent(Camera.main.transform, false);

        Canvas canvas = EditorCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 12;
        canvas.planeDistance = 50;

        var rect = EditorCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);

        EditorCanvas.AddComponent<GraphicRaycaster>();

        var scaler = EditorCanvas.AddComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.matchWidthOrHeight = 1f;

        EditorCanvas.transform.position = new Vector3(0f, 0f, 0f);
        EditorCanvas.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        EditorCanvas.transform.localScale = new Vector3(0.0148f, 0.0148f, 0.0148f);

        if (EditorCanvas.GetComponent<UIStabilizer>() == null)
        {
            EditorCanvas.AddComponent<UIStabilizer>();
        }
    }

    public class UIStabilizer : MonoBehaviour
    {
        private Camera _cam;

        private readonly Vector3 _baseScale = new Vector3(0.0148f, 0.0148f, 0.0148f);
        private const float TargetAspect = 16f / 9f;

        private void Start()
        {
            _cam = Camera.main;
            UpdateScale();
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }
            UpdateScale();
        }

        private void UpdateScale()
        {
            var currentAspect = _cam.aspect;
            var screenRatio = Mathf.Max(currentAspect, TargetAspect);
            var standardSize = screenRatio * 10f / currentAspect;

            var factor = _cam.orthographicSize / standardSize;

            transform.localScale = new Vector3(
                _baseScale.x * factor,
                _baseScale.y * factor,
                _baseScale.z // 0.0148
            );
        }
    }

    [HarmonyPatch(typeof(AspectFix))]
    public static class AspectFixPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void StartPostfix()
        {
            if (Plugin.IsQOLExLoaded) return;

            if (GameManager.Instance != null)
            {
                FixGameCanvas();
            }
            //if (LevelCreator.Instance != null)
            //{
            //    FixEditorCanvas();
            //}
        }
    }
}