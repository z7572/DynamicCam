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
    private Camera _cam;

    private Coroutine _delayedRoutine;

    //public bool IsActive => _wantsToFollow && targetController != null;
    public bool ShouldZoomIn { get; private set; } = false;
    public bool IsCustomZooming { get; private set; } = false;
    public float CurrentOrthographicSize { get; set; } = 10f;

    public KeyCode Keybind;
    public KeyCode ResetKeybind;

    // Dynamic viewport
    private Transform _proxyCam;
    private Vector3 _followOffset = Vector3.zero;  // 动态跟随产生的坐标补偿
    private Vector3 _customOffset = Vector3.zero;
    private Vector3 _lastAppliedCamOffset = Vector3.zero;
    private bool _isRestoring = false;

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
        Keybind = ConfigHandler.GetEntry<KeyboardShortcut>("DynamicCamKeybind").MainKey;
        ResetKeybind = ConfigHandler.GetEntry<KeyboardShortcut>("ResetViewportKeybind").MainKey;
        CurrentOrthographicSize = Helper.DefaultOrthographicSize;
        StartCoroutine(EnsureCameraRoutine());
    }

    private IEnumerator EnsureCameraRoutine()
    {
        while (!_cam)
        {
            _cam = Camera.main;
            if (_cam && LevelCreator.Instance == null)
            {
                if (_cam.transform.parent != null && _cam.transform.parent.name != "DynamicCamProxy")
                {
                    GameObject proxyObj = new GameObject("DynamicCamProxy");
                    _proxyCam = proxyObj.transform;
                    _proxyCam.SetParent(_cam.transform.parent);
                    _proxyCam.localPosition = Vector3.zero;
                    _cam.transform.SetParent(_proxyCam);
                }
                else if (_cam.transform.parent != null && _cam.transform.parent.name == "DynamicCamProxy")
                {
                    _proxyCam = _cam.transform.parent;
                }
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
            _hasUsedDynamicActions = false;

            if (!ShouldZoomIn)
            {
                CurrentOrthographicSize = CameraFollowFix.RealMapSize;
                targetController = null;
            }
            else
            {
                CurrentOrthographicSize = Helper.DefaultOrthographicSize;
            }

            _isRestoring = true;
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
                if (!IsCustomZooming)
                {
                    IsCustomZooming = true;
                    // If scrolling for the first time in fixed view (not following player), use the current map's zoom size as the starting point
                    if (!ShouldZoomIn)
                    {
                        CurrentOrthographicSize = CameraFollowFix.RealMapSize;
                    }
                }

                _hasUsedDynamicActions = true;
                var newSize = CurrentOrthographicSize - scroll * 10f;
                newSize = Mathf.Max(1f, newSize);
                CurrentOrthographicSize = newSize;
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
                    _isRestoring = false; // 拖拽时强制打断任何自动归位

                    Vector3 screenDelta = _lastMousePos - Input.mousePosition;
                    Vector3 originWorld = _cam.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, 10f));
                    Vector3 movedWorld = _cam.ScreenToWorldPoint(new Vector3(Screen.width / 2f + screenDelta.x, Screen.height / 2f + screenDelta.y, 10f));

                    Vector3 difference = movedWorld - originWorld;
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
                CurrentOrthographicSize = ShouldZoomIn ? Helper.DefaultOrthographicSize : CameraFollowFix.RealMapSize;
                IsCustomZooming = false;

                _isRestoring = true;
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
        if (!_cam) return;

        var isEditor = _proxyCam == null;

        if (isEditor && _lastAppliedCamOffset != Vector3.zero)
        {
            _cam.transform.position -= _lastAppliedCamOffset;
            _lastAppliedCamOffset = Vector3.zero;
        }

        if (ShouldZoomIn)
        {
            if (!targetController)
            {
                SetFollowDesired(false);
                return;
            }

            // 智能获取原生相机的底层意图坐标：
            // 编辑器(无Proxy)直接取自身坐标；游戏场景(有Proxy)取Proxy父级坐标+相机局部坐标
            Vector3 nativeWorld = isEditor
                ? _cam.transform.position
                : (_proxyCam.parent != null ? _proxyCam.parent.position : Vector3.zero) + _cam.transform.localPosition;

            Vector3 playerPos = CheatHelper.GetPlayerPosition(targetController);
            Vector3 targetFollowOffset = new Vector3(0, playerPos.y - nativeWorld.y, playerPos.z - nativeWorld.z);

            var verticalBound = _cam.orthographicSize;
            var horizontalBound = _cam.orthographicSize * _cam.aspect;

            var diffY = Mathf.Abs(_followOffset.y - targetFollowOffset.y);
            var diffZ = Mathf.Abs(_followOffset.z - targetFollowOffset.z);

            if (diffY > verticalBound || diffZ > horizontalBound)
            {
                _followOffset = targetFollowOffset; // 出屏瞬间吸附
            }
            else
            {
                var dist = Vector3.Distance(_followOffset, targetFollowOffset);
                if (dist > 0.01f)
                {
                    _followOffset = Vector3.Lerp(_followOffset, targetFollowOffset, Time.deltaTime * 5f);
                }
            }
        }
        else if (_isRestoring)
        {
            // 固定模式下的归位：将跟随补偿缓缓抽离
            _followOffset = Vector3.Lerp(_followOffset, Vector3.zero, Time.deltaTime * 5f);
        }

        // 处理玩家手拖偏移的缓动归位
        if (_isRestoring)
        {
            _customOffset = Vector3.Lerp(_customOffset, Vector3.zero, Time.deltaTime * 5f);

            // 当足够接近零点时，停息归位状态
            if (_customOffset.magnitude < 0.05f && _followOffset.magnitude < 0.05f)
            {
                _customOffset = Vector3.zero;
                _followOffset = Vector3.zero;
                _isRestoring = false;
            }
        }

        // 将计算好的总偏移应用到对应的目标上
        Vector3 totalOffset = _followOffset + _customOffset;

        if (isEditor)
        {
            // 编辑器场景：直接叠加给相机，并记录下来供下一帧剥离
            if (totalOffset != Vector3.zero)
            {
                _cam.transform.position += totalOffset;
                _lastAppliedCamOffset = totalOffset;
            }
        }
        else
        {
            // 游戏场景：只修改代理节点的局部坐标！与底层彻底隔离！
            _proxyCam.localPosition = totalOffset;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}