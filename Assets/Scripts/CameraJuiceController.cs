using UnityEngine;
using UnityEngine.Rendering;

public class CameraJuiceController : MonoBehaviour
{
    [Header("Camera Transforms (피벗 분리 핵심)")]
    [Tooltip("상하 회전이 없는 최상위 더미 피벗 (위치 이동 전용)")]
    public Transform positionPivot; 
    
    [Tooltip("실제 화면을 비추는 메인 카메라 (회전 및 FOV 전용)")]
    public Camera mainCamera; 

    [Header("Camera & FOV")]
    public float baseFOV = 90f;

    [Header("URP Post Processing")]
    public Volume postProcessingVolume;

    [Header("Player Reference (허브가 직접 물리 상태를 읽음)")]
    public PlayerController player;
    public CameraActionController actionController;

    [Header("Modules (POCO)")]
    public JuiceSprint sprint = new JuiceSprint();
    public JuiceWallRun wallRun = new JuiceWallRun();
    public JuiceSlide slide = new JuiceSlide();
    public JuiceImpact impact = new JuiceImpact();
    public JuiceStrafeTilt strafeTilt = new JuiceStrafeTilt();

    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;
    
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
        wallRun.Initialize(this);
        slide.Initialize(this);
        impact.Initialize(this);
    }

    public void TriggerSlideStart(float dipAmount) => slide.TriggerSlideStart(dipAmount);
    public void TriggerSlideEnd(bool isJumpHop = false) => slide.TriggerSlideEnd(isJumpHop);
    public void TriggerVaultJuice(float duration) => impact.TriggerVaultJuice(duration);
    public void TriggerLandingDrop(float intensity = 1f) => impact.TriggerLandingDrop(intensity);
    public void TriggerPulsingEffect(float duration) => impact.TriggerPulsingEffect(duration);
    public void UpdateDescentShake(float airTime, float yVel) => impact.UpdateDescentShake(airTime, yVel);
    public void TriggerWallAttachJuice(bool isWallRight) => impact.TriggerPulsingEffect(0.15f);
    public void TriggerSlideJumpPunch() => impact.TriggerSlideJumpPunch();
    public void TriggerActiveLandingRoll() => impact.TriggerActiveLandingRoll();
    public void InterruptPulse() => impact.InterruptPulse();

    public void OnFootstepTriggered(string side)
    {
        sprint.TriggerStep(side); 
    }

    void LateUpdate()
    {
        if (mainCamera == null || positionPivot == null || player == null) return;

        // ── Step 1: 물리 상태 읽기 ──
        Rigidbody rb = player.Rb;
        float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        float speedRatio = Mathf.Clamp01(currentSpeed / player.movement.runMaxSpeed);

        // ── Step 2: 각 모듈 업데이트 ──
        if (player.vault.IsVaulting || player.slider.IsSliding)
        {
            sprint.UpdateModule(false, false, 0f, 0f);
            wallRun.UpdateModule(false, false, 0f);
        }
        else if (player.wallRunner.IsWallRunning)
        {
            sprint.UpdateModule(false, false, 0f, 0f);
            wallRun.UpdateModule(true, player.wallRunner.IsWallRight, speedRatio);
        }
        else
        {
            bool isSprinting = player.InputProv.DashHeld && currentSpeed > player.movement.walkSpeed;
            bool isWalking = player.movement.IsGrounded && currentSpeed > 0.1f && !isSprinting;
            bool isRunning = player.movement.IsGrounded && isSprinting;
            sprint.UpdateModule(isWalking, isRunning, currentSpeed, player.movement.runMaxSpeed);
            wallRun.UpdateModule(false, false, 0f);
        }

        slide.UpdateModule();
        impact.UpdateModule();

        bool isStrafeTiltActive = !player.wallRunner.IsWallRunning && !player.vault.IsVaulting; 
        strafeTilt.UpdateModule(player.InputProv.MoveInput.x, isStrafeTiltActive);

        // ── Step 3: 완벽하게 분리된 다이렉트 대입 ──
        // 수학 문서에 따라 이제 sprint.PosOffset에는 X축 진자 이동 수치도 포함되어 정상 적용됩니다.
        Vector3 totalPosOffset = sprint.PosOffset + wallRun.PosOffset + slide.PosOffset + impact.PosOffset;
        positionPivot.localPosition = _originalLocalPos + totalPosOffset;

        // 수학 문서에 따라 sprint.RotOffset.x 에 앞숙임(Pitch) 값이 정상 반영됩니다.
        Quaternion actionRot = (actionController != null) ? actionController.ActionRotation : Quaternion.identity;
        Vector3 totalRotOffset = sprint.RotOffset + wallRun.RotOffset + slide.RotOffset + impact.RotOffset + strafeTilt.RotOffset;
        mainCamera.transform.localRotation = _originalLocalRot * actionRot * Quaternion.Euler(totalRotOffset);

        // [FOV 적용]
        float maxFov = Mathf.Max(sprint.FovOverride, wallRun.FovOverride, slide.FovOverride);
        if (maxFov < baseFOV) maxFov = baseFOV;
        mainCamera.fieldOfView = maxFov + impact.FovOffset;
    }
}