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
        // 1. 위치(Position)의 기준점은 Pivot에서 가져옵니다.
        if (positionPivot != null)
        {
            _originalLocalPos = positionPivot.localPosition;
        }

        // 2. 회전(Rotation)과 FOV의 기준점은 MainCamera에서 가져옵니다.
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

    // ============================================
    // (중략) Trigger 메서드들은 기존과 동일하게 유지하셔도 됩니다.
    // ============================================

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

    /// <summary>
    /// 플레이어 발소리 이벤트를 스프린트(Headbob) 모듈로 전달합니다.
    /// </summary>
    public void OnFootstepTriggered(string side)
    {
        sprint.TriggerStep(side);
    }

    // ============================================
    // 핵심: LateUpdate 구조 분리 적용
    // ============================================

    void LateUpdate()
    {
        if (mainCamera == null || positionPivot == null || player == null) return;

        // ── Step 1: 물리 상태 읽기 ──
        Rigidbody rb = player.Rb;
        float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        float speedRatio = Mathf.Clamp01(currentSpeed / player.movement.runMaxSpeed);
        float groundedRatio = player.movement.IsGrounded ? speedRatio : 0f;

        // ── Step 2: 각 모듈 업데이트 ──
        if (player.vault.IsVaulting)
        {
            sprint.UpdateModule(false, false, 0f, 0f);
            wallRun.UpdateModule(false, false, 0f);
        }
        else if (player.slider.IsSliding)
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

        // [위치 적용]: 회전이 없는 더미 피벗(positionPivot)에만 쥬스 오프셋 적용!
        // 마우스를 어디로 돌리든 무조건 월드 공간의 수직 아래(-Y)로 깔끔하게 떨어짐
        Vector3 totalPosOffset = sprint.PosOffset + wallRun.PosOffset + slide.PosOffset + impact.PosOffset;
        positionPivot.localPosition = _originalLocalPos + totalPosOffset;

        // [회전 적용]: 메인 카메라(mainCamera) 자체에 회전 쥬스 적용!
        Quaternion actionRot = (actionController != null) ? actionController.ActionRotation : Quaternion.identity;
        Vector3 totalRotOffset = sprint.RotOffset + wallRun.RotOffset + slide.RotOffset + impact.RotOffset + strafeTilt.RotOffset;
        mainCamera.transform.localRotation = _originalLocalRot * actionRot * Quaternion.Euler(totalRotOffset);

        // [FOV 적용]
        float maxFov = Mathf.Max(sprint.FovOverride, wallRun.FovOverride, slide.FovOverride);
        if (maxFov < baseFOV) maxFov = baseFOV;
        mainCamera.fieldOfView = maxFov + impact.FovOffset;
    }
}