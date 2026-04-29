using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class CameraJuiceController : MonoBehaviour
{
    [Header("Camera Transforms (피벗 분리 핵심)")]
    [Tooltip("상하 회전이 없는 최상위 더미 피벗 (위치 이동 전용)")]
    public Transform positionPivot; 
    
    [Tooltip("실제 화면을 비추는 메인 카메라 (회전 및 FOV 전용)")]
    public Camera mainCamera; 

    [Header("Camera & FOV")]
    public float baseFOV = 68f;

    [Header("URP Post Processing")]
    public Volume postProcessingVolume;

    [Header("Player Reference (허브가 직접 물리 상태를 읽음)")]
    public PlayerController player;
    public CameraActionController actionController;

    [Header("Modules (POCO)")]
    public JuiceSprint sprint = new JuiceSprint();
    public JuiceWallKick wallKick = new JuiceWallKick();
    public JuiceSlide slide = new JuiceSlide();
    public JuiceImpact impact = new JuiceImpact();
    public JuiceStrafeTilt strafeTilt = new JuiceStrafeTilt();
    public JuiceHighVault highVault = new JuiceHighVault();

    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;
    
    // 🚀 핵심: 현재 작동 중인 모듈만 모아두는 대기열
    private HashSet<IJuiceModule> _activeModules = new HashSet<IJuiceModule>();

    void Awake()
    {
        if (positionPivot != null)
        {
            _originalLocalPos = positionPivot.localPosition;
        }

        if (mainCamera != null)
        {
            _originalLocalRot = mainCamera.transform.localRotation;
            mainCamera.fieldOfView = baseFOV;
        }
            
        sprint.Initialize(this);
        wallKick.Initialize(this);
        slide.Initialize(this);
        impact.Initialize(this);
        strafeTilt.Initialize(this);
    }

    public void RegisterModule(IJuiceModule module)
    {
        _activeModules.Add(module);
    }

    public void UnregisterModule(IJuiceModule module)
    {
        _activeModules.Remove(module);
    }

    // ── 단발성 트리거 (이벤트-드리븐 방식으로 플레이어가 찌름) ──
    public void TriggerSlideStart()
    {
        sprint.Suppress(true);
        slide.TriggerSlideStart();
    }

    public void TriggerSlideEnd(bool isJumpHop = false)
    {
        slide.TriggerSlideEnd(isJumpHop);
        sprint.Suppress(false);
        SyncSprintState();
    }
    
    public void TriggerWallKickJuice(float duration) => wallKick.TriggerWallKick(duration);

    public void TriggerSprintStart() => sprint.TriggerSprint(true);
    public void TriggerSprintStop() => sprint.TriggerSprint(false);
    public void TriggerWalkStart() => sprint.TriggerWalk(true);
    public void TriggerWalkStop() => sprint.TriggerWalk(false);
    
    public void TriggerStrafe(float inputX) => strafeTilt.TriggerStrafe(inputX);

    public void TriggerVaultJuice(float duration) => impact.TriggerVaultJuice(duration);
    public void TriggerLandingDrop(float intensity = 1f) => impact.TriggerLandingDrop(intensity);
    public void TriggerPulsingEffect(float duration) => impact.TriggerPulsingEffect(duration);
    public void TriggerBreachJuice(float duration) => impact.TriggerBreachJuice(duration);
    public void TriggerWallAttachJuice(bool isWallRight) => impact.TriggerPulsingEffect(0.15f);
    public void TriggerKickJuice() => impact.TriggerKickJuice();
    public void TriggerActiveLandingRoll() => impact.TriggerActiveLandingRoll();
    public void InterruptPulse() => impact.InterruptPulse();
    
    public void TriggerHighVaultJuice(float duration) => highVault.Trigger(this, duration);

    public void OnFootstepTriggered(string side)
    {
        sprint.TriggerStep(side); 
    }

    private void SyncSprintState()
    {
        if (player == null || player.Rb == null || player.movement == null || player.InputProv == null) return;

        Vector3 currentXZVelocity = new Vector3(player.Rb.linearVelocity.x, 0f, player.Rb.linearVelocity.z);
        float currentSpeed = currentXZVelocity.magnitude;

        bool isSprinting = player.InputProv.DashHeld && currentSpeed > player.movement.walkSpeed;
        bool isWalking = player.movement.IsGrounded && currentSpeed > 0.1f && !isSprinting;

        sprint.TriggerSprint(isSprinting);
        sprint.TriggerWalk(isWalking);
    }

    void LateUpdate()
    {
        if (mainCamera == null || positionPivot == null) return;

        Vector3 totalPosOffset = Vector3.zero;
        Vector3 totalRotOffset = Vector3.zero;
        float maxFov = baseFOV;
        float totalFovOffset = 0f;

        // 🚀 업그레이드 포인트: 쉬고 있는 모듈은 쳐다보지도 않음!
        foreach (var module in _activeModules)
        {
            totalPosOffset += module.PosOffset;
            totalRotOffset += module.RotOffset;
            
            if (module.FovOverride > maxFov)
            {
                maxFov = module.FovOverride;
            }
            totalFovOffset += module.FovOffset;
        }

        positionPivot.localPosition = _originalLocalPos + totalPosOffset;

        Quaternion actionRot = (actionController != null) ? actionController.ActionRotation : Quaternion.identity;
        mainCamera.transform.localRotation = _originalLocalRot * actionRot * Quaternion.Euler(totalRotOffset);

        mainCamera.fieldOfView = maxFov + totalFovOffset;
    }
}
