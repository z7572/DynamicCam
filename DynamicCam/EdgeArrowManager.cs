// QOL-Ex
using System.Linq;
using UnityEngine;

namespace DynamicCam;

public class EdgeArrowManager : MonoBehaviour
{
    private const float horizonalPadding = 2f;
    private const float verticalPadding = 1f;
    private const float visibilityThreshold = 0.1f;

    private const float baseArrowSize = 0.3f;
    private const float minScale = 0.6f;
    private const float maxScale = 1.4f;
    private const float maxDistance = 15f;

    private int playerID;
    private Controller controller;
    private Camera mainCamera;
    private Rigidbody[] rigs;

    private GameObject parentObj;
    private GameObject spriteObj;

    private void Awake()
    {
        controller = gameObject.GetComponent<Controller>();
        playerID = controller.playerID;
        mainCamera = Camera.main;
    }

    private void Start()
    {
        rigs = gameObject.GetComponentsInChildren<Rigidbody>();

        parentObj = new GameObject("EdgeArrow");
        parentObj.transform.SetParent(transform);
        parentObj.SetActive(false);

        spriteObj = new GameObject("Sprite");
        spriteObj.transform.SetParent(parentObj.transform);
        spriteObj.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
        spriteObj.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        spriteObj.transform.localScale = new Vector3(baseArrowSize, baseArrowSize * Mathf.Sqrt(1f / 3f), baseArrowSize);

        var spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
        var arrowSprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == "triangle-xxl");
        var materials = MultiplayerManagerAssets.Instance.Colors;

        if (arrowSprite == null || materials == null) return;
        spriteRenderer.sprite = arrowSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.material = materials[playerID];
    }

    private void LateUpdate()
    {
        if (parentObj == null)
        {
            Debug.LogError("parentObject is null!!!");
            var found = transform.Find("EdgeArrow");
            if (found) parentObj = found.gameObject;
            else return;
        }

        var isPlayerVisible = AreAnyRigidbodiesOnScreen();

        if (!isPlayerVisible)
        {
            PositionArrowAtScreenEdge();
        }

        parentObj.SetActive(!isPlayerVisible);
    }

    private bool AreAnyRigidbodiesOnScreen()
    {
        var visibleRigidbodies = 0;

        foreach (var rb in rigs)
        {
            var screenPos = mainCamera.WorldToScreenPoint(rb.position);

            if (screenPos.z > 0 &&
                screenPos.x > 0 && screenPos.x < Screen.width &&
                screenPos.y > 0 && screenPos.y < Screen.height)
            {
                visibleRigidbodies++;
            }
        }

        var visibleRatio = (float)visibleRigidbodies / rigs.Length;
        return visibleRatio >= visibilityThreshold;
    }

    private void PositionArrowAtScreenEdge()
    {
        var playerWorldPos = Vector3.zero;
        foreach (var rb in rigs) playerWorldPos += rb.position;
        playerWorldPos /= rigs.Length;

        var playerScreenPos = mainCamera.WorldToScreenPoint(playerWorldPos);

        var arrowX = Mathf.Clamp(playerScreenPos.x, horizonalPadding, Screen.width - horizonalPadding);
        var arrowY = Mathf.Clamp(playerScreenPos.y, verticalPadding, Screen.height - verticalPadding);

        var arrowWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(arrowX, arrowY, mainCamera.nearClipPlane + 5f));

        var arrowScreenPos = new Vector2(arrowX, arrowY);
        var direction = ((Vector2)playerScreenPos - arrowScreenPos).normalized;

        var arrowPosYZ = new Vector2(arrowWorldPos.z, arrowWorldPos.y);
        var playerPosYZ = new Vector2(playerWorldPos.z, playerWorldPos.y);

        var distance = Vector2.Distance(arrowPosYZ, playerPosYZ);
        var scaleRatio = 1f - Mathf.Clamp01(distance / maxDistance);
        var currentScale = Mathf.Lerp(minScale, maxScale, scaleRatio);

        parentObj.transform.position = arrowWorldPos;
        parentObj.transform.rotation = Quaternion.Euler(0f, 90f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        parentObj.transform.localScale = Vector3.one * currentScale;
    }
}