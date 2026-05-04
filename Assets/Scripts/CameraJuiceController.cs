using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Cinemachine;
using DG.Tweening;

public class CameraJuiceController : MonoBehaviour
{
    [Header("Cinemachine QTE Setup")]
    public CinemachineBrain cinemachineBrain; // <--- 인스펙터에서 Main Camera 할당
    [Tooltip("파운딩 당할 때 활성화할 시네머신 QTE 전용 카메라")]
    public CinemachineVirtualCamera qteVirtualCamera;
    public CinemachineVirtualCamera aiFleeZoomCam; // <--- 이거 한 줄 추가

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
    
    private HashSet<IJuiceModule> _activeModules = new HashSet<IJuiceModule>();

    void Awake()
    {
        if (positionPivot != null) _originalLocalPos = positionPivot.localPosition;
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

    public void RegisterModule(IJuiceModule module) { _activeModules.Add(module); }
    public void UnregisterModule(IJuiceModule module) { _activeModules.Remove(module); }

    public void TriggerSlideStart() { sprint.Suppress(true); slide.TriggerSlideStart(); }
    public void TriggerSlideEnd(bool isJumpHop = false) { slide.TriggerSlideEnd(isJumpHop); sprint.Suppress(false); SyncSprintState(); }
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

    // ── QTE Trolling Camera Logic (Cinemachine & DOTween 기반) ──
    private bool _isQTEActive = false;

    public void DisablePlayerCamera()
    {
        _isQTEActive = true;
        if (cinemachineBrain != null) cinemachineBrain.enabled = true; 
        if (actionController != null) actionController.enabled = false; 
    }

    // 컷신 종료 후 복구 시 호출
    public void RestoreCameraAndPlayer()
    {
        _isQTEActive = false;
        if (cinemachineBrain != null) cinemachineBrain.enabled = false; 
        
        // [핵심] Z축 밀림 등 카메라 고장 원천 차단 (하드 리셋)
        positionPivot.localPosition = _originalLocalPos;
        mainCamera.transform.localPosition = Vector3.zero; 
        mainCamera.transform.localRotation = Quaternion.identity;
        
        // [Juice] 땅에 메다꽂힌 뒤 일어나는 무거운 충격 연출
        TriggerLandingDrop(1.5f);
        
        if (actionController != null) actionController.enabled = true;
        if (player != null) player.enabled = true;
    }

    public void OnFootstepTriggered(string side) { sprint.TriggerStep(side); }

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
        // 시네마틱(QTE) 중일 때는 쥬스 컨트롤러가 카메라 트랜스폼을 절대 건드리지 않음
        if (_isQTEActive) return; 

        if (mainCamera == null || positionPivot == null) return;

        Vector3 totalPosOffset = Vector3.zero;
        Vector3 totalRotOffset = Vector3.zero;
        float maxFov = baseFOV;
        float totalFovOffset = 0f;

        foreach (var module in _activeModules)
        {
            totalPosOffset += module.PosOffset;
            totalRotOffset += module.RotOffset;
            if (module.FovOverride > maxFov) maxFov = module.FovOverride;
            totalFovOffset += module.FovOffset;
        }

        Quaternion actionRot = (actionController != null) ? actionController.ActionRotation : Quaternion.identity;

        if (!_isQTEActive)
        {
            // 기본 상태: 오리지널 로컬 베이스 + 쥬스 오프셋
            positionPivot.localPosition = _originalLocalPos + totalPosOffset;
            mainCamera.transform.localRotation = _originalLocalRot * actionRot * Quaternion.Euler(totalRotOffset);
            mainCamera.fieldOfView = maxFov + totalFovOffset;
        }
        // 시네마틱(QTE) 중일 때는 카메라의 위치/회전 제어를 전적으로 시네머신에 맡깁니다.
    }
}