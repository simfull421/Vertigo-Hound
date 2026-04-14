using UnityEngine;
using UnityEngine.Rendering;

public class CameraJuiceController : MonoBehaviour
{
    [Header("Camera & FOV")]
    public Camera targetCamera;
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

    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;

    void Awake()
    {
        if (targetCamera != null)
        {
            _originalLocalPos = targetCamera.transform.localPosition;
            _originalLocalRot = targetCamera.transform.localRotation;
            targetCamera.fieldOfView = baseFOV;
        }

        sprint.Initialize(this);
        wallRun.Initialize(this);
        slide.Initialize(this);
        impact.Initialize(this);
    }

    // ============================================
    // 타 모듈(PlayerController)을 위한 공용 Hook API
    // (이벤트성 트리거만 남김 — 매 프레임 업데이트는 LateUpdate에서 직접 처리)
    // ============================================

    public void TriggerSlideStart(float dipAmount) => slide.TriggerSlideStart(dipAmount);
    public void TriggerSlideEnd(bool isJumpHop = false) => slide.TriggerSlideEnd(isJumpHop);

    // 타격 연출
    public void TriggerVaultJuice(float duration) => impact.TriggerVaultJuice(duration);
    public void TriggerLandingDrop(float intensity = 1f) => impact.TriggerLandingDrop(intensity);
    public void TriggerPulsingEffect(float duration) => impact.TriggerPulsingEffect(duration);
    public void UpdateDescentShake(float airTime, float yVel) => impact.UpdateDescentShake(airTime, yVel);
    
    // 벽타기 시 펄스 연출
    public void TriggerWallAttachJuice(bool isWallRight) => impact.TriggerPulsingEffect(0.15f);

    public void InterruptPulse() => impact.InterruptPulse();

    // ============================================
    // 핵심: 모든 연산을 LateUpdate 한 곳에서 수행
    // FixedUpdate 물리 연산이 완전히 끝난 뒤 실행되므로 Jittering 원천 차단
    // ============================================

    void LateUpdate()
    {
        if (targetCamera == null || player == null) return;

        // ── Step 1: 물리 상태 읽기 ──
        Rigidbody rb = player.Rb;
        float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        float speedRatio = Mathf.Clamp01(currentSpeed / player.movement.runMaxSpeed);
        
        // 공중에 떠있으면 헤드밥 비율을 강제로 0으로 만듦 (공중 꿀렁임 원천 차단)
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
            // 땅에 있을 때의 비율(groundedRatio)을 넘겨줌
            bool isSprinting = player.InputProv.DashHeld && currentSpeed > player.movement.walkSpeed;
            bool isWalking = player.movement.IsGrounded && currentSpeed > 0.1f && !isSprinting;
            bool isRunning = player.movement.IsGrounded && isSprinting;
            sprint.UpdateModule(isWalking, isRunning, currentSpeed, player.movement.runMaxSpeed);
            wallRun.UpdateModule(false, false, 0f);
        }

        slide.UpdateModule();
        impact.UpdateModule();

        // ── Step 3: 단순 합산 & 다이렉트 대입 (Double Lerp 절대 금지) ──

        // 위치: 모든 모듈의 PosOffset을 단순 합산하여 원본에 더함
        Vector3 totalPosOffset = sprint.PosOffset + wallRun.PosOffset + slide.PosOffset + impact.PosOffset;
        targetCamera.transform.localPosition = _originalLocalPos + totalPosOffset;

        // 회전: 액션 회전(360도 플립, 하강 숙임 등) + 주스 회전(헤드밥, 월런, 슬라이드) 를 단일 지점에서 합산
        Quaternion actionRot = (actionController != null) ? actionController.ActionRotation : Quaternion.identity;
        Vector3 totalRotOffset = sprint.RotOffset + wallRun.RotOffset + slide.RotOffset + impact.RotOffset;
        targetCamera.transform.localRotation = _originalLocalRot * actionRot * Quaternion.Euler(totalRotOffset);

        // FOV: 가장 높은 FovOverride를 찾은 후, Impact 펄스 오프셋을 덧붙임
        float maxFov = Mathf.Max(sprint.FovOverride, wallRun.FovOverride, slide.FovOverride);
        if (maxFov < baseFOV) maxFov = baseFOV;
        
        targetCamera.fieldOfView = maxFov + impact.FovOffset;
    }
}
