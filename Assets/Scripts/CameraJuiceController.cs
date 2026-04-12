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

        // ── Step 1: 물리 상태 읽기 (FixedUpdate 완료 후이므로 안전) ──
        Rigidbody rb = player.Rb;
        float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        float speedRatio = Mathf.Clamp01(currentSpeed / player.movement.runMaxSpeed);

        // ── Step 2: 각 모듈 업데이트 (오프셋 계산) ──
        if (player.vault.IsVaulting)
        {
            sprint.UpdateModule(0f, 0f);
            wallRun.UpdateModule(false, false, 0f);
        }
        else if (player.slider.IsSliding)
        {
            sprint.UpdateModule(0f, 0f);
            wallRun.UpdateModule(false, false, 0f);
        }
        else if (player.wallRunner.IsWallRunning)
        {
            sprint.UpdateModule(0f, 0f);
            wallRun.UpdateModule(true, player.wallRunner.IsWallRight, speedRatio);
        }
        else
        {
            sprint.UpdateModule(speedRatio, currentSpeed);
            wallRun.UpdateModule(false, false, 0f);
        }

        slide.UpdateModule();
        impact.UpdateModule();

        // ── Step 3: 단순 합산 & 다이렉트 대입 (Double Lerp 절대 금지) ──

        // 위치: 모든 모듈의 PosOffset을 단순 합산하여 원본에 더함
        Vector3 totalPosOffset = sprint.PosOffset + wallRun.PosOffset + slide.PosOffset + impact.PosOffset;
        targetCamera.transform.localPosition = _originalLocalPos + totalPosOffset;

        // 회전: 모든 모듈의 RotOffset(Euler)을 단순 합산하여 원본 회전에 곱함
        Vector3 totalRotOffset = sprint.RotOffset + wallRun.RotOffset + slide.RotOffset + impact.RotOffset;
        targetCamera.transform.localRotation = _originalLocalRot * Quaternion.Euler(totalRotOffset);

        // FOV: 가장 높은 FovOverride를 찾은 후, Impact 펄스 오프셋을 덧붙임
        float maxFov = Mathf.Max(sprint.FovOverride, wallRun.FovOverride, slide.FovOverride);
        if (maxFov < baseFOV) maxFov = baseFOV;
        
        targetCamera.fieldOfView = maxFov + impact.FovOffset;
    }
}
