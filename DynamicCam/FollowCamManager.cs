using UnityEngine;
using LevelEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicCam.Patches;
using BepInEx.Configuration;

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
        StartCoroutine(EnsureCameraRoutine());
    }

    private IEnumerator EnsureCameraRoutine()
    {
        while (_cam == null)
        {
            _cam = Camera.main;
            if (_cam != null)
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
                if (LevelCreator.Instance != null && _cam != null)
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
            if (IsPlayerDead(Helper.controller))
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
        if (c == null) return true;
        var info = c.GetComponent<CharacterInformation>();
        return info != null && info.isDead;
    }

    private void LateUpdate()
    {
        if (!ShouldZoomIn || targetController == null || _cam == null) return;

        if (targetController == null)
        {
            SetFollowDesired(false);
            return;
        }

        var playerPos = CheatHelper.GetPlayerPosition(targetController);
        var targetPos = new Vector3(initialPos.x, playerPos.y, playerPos.z);
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
        if (this != null && gameObject != null)
        {
            transform.position = initialPos;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}