using UnityEngine;
using LevelEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DynamicCam.Patches;

namespace DynamicCam;

public class FollowCamManager : MonoBehaviour
{
    private static FollowCamManager _instance;
    public static FollowCamManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<FollowCamManager>();
                if (_instance == null && Camera.main != null)
                {
                    var target = Camera.main.gameObject; // LevelEditor

                    if (LevelCreator.Instance == null && Camera.main.transform.parent != null) // GameManager
                    {
                        target = Camera.main.transform.parent.gameObject; // RigidCam
                    }

                    _instance = target.AddComponent<FollowCamManager>();
                }
            }
            return _instance;
        }
    }

    private Controller targetController;
    private Vector3 initialPos;
    private Camera _cam;

    private Coroutine _delayedRoutine;

    //public bool IsActive => _wantsToFollow && targetController != null;
    public bool ShouldZoomIn { get; private set; } = false;

    public KeyCode Keybind;
    public KeyCode ResetKeybind;

    // Dynamic viewport
    private Vector3 _customOffset = Vector3.zero;
    private float _keyDownTime = 0f;
    private bool _hasUsedDynamicActions = false;
    private Vector3 _lastMousePos;
    private bool _isDragging = false;
    private const float DragThreshold = 5f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        initialPos = transform.position;
        Keybind = ConfigHandler.GetEntry<KeyboardShortcut>("DynamicCamKeybind").MainKey;
        ResetKeybind = ConfigHandler.GetEntry<KeyboardShortcut>("ResetViewportKeybind").MainKey;
        StartCoroutine(EnsureCameraRoutine());
    }

    private IEnumerator EnsureCameraRoutine()
    {
        while (!_cam)
        {
            _cam = Camera.main;
            if (_cam)
            {
                initialPos = transform.position;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    public void RegisterController(Controller controller)
    {
        this.targetController = controller;
    }

    public void SetFollowDelayed(bool state, float delay)
    {
        if (_delayedRoutine != null) StopCoroutine(_delayedRoutine);

        if (delay <= 0f)
        {
            SetFollowDesired(state);
        }
        else
        {
            _delayedRoutine = StartCoroutine(DoSetFollowDelayed(state, delay));
        }
    }

    private IEnumerator DoSetFollowDelayed(bool state, float delay)
    {
        if (state) Debug.Log($"Waiting {delay}s before enabling follow...");
        yield return new WaitForSeconds(delay);
        SetFollowDesired(state);
        _delayedRoutine = null;
    }

    public void SetFollowDesired(bool state)
    {
        if (ShouldZoomIn != state)
        {
            ShouldZoomIn = state;

            if (!ShouldZoomIn)
            {
                RestorePosition();
                targetController = null;
            }
            else
            {
                if (LevelCreator.Instance && _cam)
                {
                    initialPos = transform.position;
                }
            }
        }
    }

    public void OnLocalPlayerRevived(Controller localController)
    {
        RegisterController(localController);
        SetFollowDesired(true);
    }

    public void OnPlayerDied(Controller deadPlayer)
    {
        var aliveCount = GetAlivePlayers().Count;

        if (aliveCount <= 1)
        {
            RegisterController(deadPlayer);
            SetFollowDesired(true);
            return;
        }

        if (deadPlayer == Helper.controller)
        {
            SwitchToNearestAlive(deadPlayer);
        }
        else if (ShouldZoomIn && deadPlayer == targetController)
        {
            SwitchToNearestAlive(deadPlayer);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(Keybind))
        {
            _keyDownTime = Time.time;
            _hasUsedDynamicActions = false;
            _isDragging = false;
        }

        if (Input.GetKey(Keybind))
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _hasUsedDynamicActions = true;
                var currentSize = Helper.DefaultOrthographicSize;
                var newSize = currentSize - scroll * 10f;
                newSize = Mathf.Max(1f, newSize);
                Helper.DefaultOrthographicSize = newSize;
            }

            if (Input.GetMouseButtonDown(2))
            {
                _lastMousePos = Input.mousePosition;
                _isDragging = false;
            }

            if (Input.GetMouseButton(2))
            {
                if (!_isDragging && Vector3.Distance(Input.mousePosition, _lastMousePos) > DragThreshold)
                {
                    _isDragging = true;
                    _lastMousePos = Input.mousePosition;
                }

                if (_isDragging && _cam != null)
                {
                    _hasUsedDynamicActions = true;

                    var worldCurrent = _cam.ScreenToWorldPoint(Input.mousePosition);
                    var worldLast = _cam.ScreenToWorldPoint(_lastMousePos);

                    var difference = worldLast - worldCurrent;

                    _customOffset.x = 0f;
                    _customOffset.y += difference.y;
                    _customOffset.z += difference.z;

                    _lastMousePos = Input.mousePosition;
                }
            }

            if (Input.GetMouseButtonUp(2))
            {
                _isDragging = false;
            }

            if (Input.GetKeyDown(ResetKeybind))
            {
                _hasUsedDynamicActions = true;
                _customOffset = Vector3.zero;
                Helper.DefaultOrthographicSize = 10f;
            }
        }

        if (Input.GetKeyUp(Keybind))
        {
            _isDragging = false;
            if (!_hasUsedDynamicActions && (Time.time - _keyDownTime < 0.3f))
            {
                ToggleFollow();
            }
        }
    }

    private void ToggleFollow()
    {
        var canSpec = IsPlayerDead(Helper.controller) || ConfigHandler.GetEntry<bool>("EnableSpecWhenAlive");
        if (canSpec)
        {
            CycleSpectateTarget();
        }
        else
        {
            if (ShouldZoomIn)
            {
                SetFollowDelayed(false, 0f);
            }
            else
            {
                RegisterController(Helper.controller);
                SetFollowDelayed(true, 0f);
            }
        }
    }

    private void SwitchToNearestAlive(Controller referenceController)
    {
        var deadPos = CheatHelper.GetPlayerPosition(referenceController);
        var nearest = GetNearestAlivePlayer(deadPos);

        if (nearest != null)
        {
            RegisterController(nearest);
            SetFollowDesired(true);
        }
    }

    private void CycleSpectateTarget()
    {
        var alivePlayers = GetAlivePlayers();

        if (alivePlayers.Count == 0)
        {
            SetFollowDesired(false);
            return;
        }

        if (!ShouldZoomIn || targetController == null)
        {
            SetFollowDesired(true);
            RegisterController(alivePlayers[0]);
        }
        else
        {
            var currentIndex = alivePlayers.IndexOf(targetController);

            if (currentIndex == -1)
            {
                RegisterController(alivePlayers[0]);
            }
            else if (currentIndex >= alivePlayers.Count - 1)
            {
                SetFollowDesired(false);
            }
            else
            {
                RegisterController(alivePlayers[currentIndex + 1]);
            }
        }
    }

    private List<Controller> GetAlivePlayers()
    {
        var list = new List<Controller>();
        if (ControllerHandler.Instance == null) return list;

        foreach (var p in ControllerHandler.Instance.ActivePlayers)
        {
            if (!IsPlayerDead(p))
            {
                list.Add(p);
            }
        }
        return list.OrderBy(c => c.playerID).ToList();
    }

    private Controller GetNearestAlivePlayer(Vector3 referencePos)
    {
        var alive = GetAlivePlayers();
        if (alive.Count == 0) return null;

        Controller nearest = null;
        var minDst = float.MaxValue;

        foreach (var p in alive)
        {
            var dst = Vector3.Distance(referencePos, CheatHelper.GetPlayerPosition(p));
            if (dst < minDst)
            {
                minDst = dst;
                nearest = p;
            }
        }
        return nearest;
    }

    private bool IsPlayerDead(Controller c)
    {
        if (!c) return true;
        var info = c.GetComponent<CharacterInformation>();
        return info && info.isDead;
    }

    private void LateUpdate()
    {
        if (!ShouldZoomIn || !targetController || !_cam) return;

        if (!targetController)
        {
            SetFollowDesired(false);
            return;
        }

        var playerPos = CheatHelper.GetPlayerPosition(targetController);
        var targetPos = new Vector3(initialPos.x, playerPos.y, playerPos.z) + _customOffset;
        targetPos.x = initialPos.x;

        var currentPos = transform.position;
        var dist = Vector3.Distance(currentPos, targetPos);

        if (dist > 50f)
        {
            transform.position = targetPos;
        }
        else if (dist > 0.01f)
        {
            var t = Mathf.Min(Time.deltaTime * 5f, 1f);
            var newPos = Vector3.Lerp(currentPos, targetPos, t);
            newPos.x = initialPos.x;
            transform.position = newPos;
        }
    }

    private void RestorePosition()
    {
        if (this && gameObject)
        {
            transform.position = initialPos;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}