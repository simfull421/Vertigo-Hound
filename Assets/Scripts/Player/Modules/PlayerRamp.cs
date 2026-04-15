using UnityEngine;
using System;

/// <summary>
/// 경사로(Ramp) 듀얼모드 시스템.
/// - 걷기 모드 (!isSliding): 중력 보정 + 부착력 + 경사면 투영으로 계단처럼 안정 보행.
/// - 슬라이딩 모드 (isSliding): 법선벡터 기반 내리막 가속(AddForce) + 최대 속도 캡.
/// 
/// FixedUpdate에서 메인 모듈(Movement/Slider) 실행 후 "후처리"로 호출됩니다.
/// </summary>
[Serializable]
public sealed class PlayerRamp
{
    [Header("Ramp Detection")]
    [Tooltip("이 각도(°) 이상이면 '경사로'로 판정합니다.")]
    public float minSlopeAngle = 5f;

    [Tooltip("이 각도(°) 이상이면 '급경사'로 판정하여 보행 패널티를 적용합니다.")]
    public float steepSlopeAngle = 45f;

    [Header("Walking Mode — 경사면 안정 보행")]
    [Tooltip("경사면에 캐릭터를 밀착시키는 하향 부착력 (Acceleration).")]
    public float slopeStickForce = 20f;

    [Tooltip("급경사(steepSlopeAngle 이상) 보행 시 속도 배율. 0.5 = 절반 속도.")]
    public float steepWalkPenalty = 0.5f;

    [Header("Sliding Mode — 경사면 내리막 가속")]
    [Tooltip("경사면 내리막 방향 가속력 (Acceleration).")]
    public float rampSlideAcceleration = 30f;

    [Tooltip("경사면 슬라이딩 절대 최대 속도.")]
    public float rampSlideMaxSpeed = 35f;

    [Header("Grace Period — 경사→평지 전이 가속 유지")]
    [Tooltip("경사로를 벗어난 뒤 드래그 감소가 유지되는 시간(초).")]
    public float gracePeriodDuration = 0.5f;

    [Tooltip("Grace Period 동안 평지 슬라이딩 드래그에 곱해지는 배율. 0.3 = 30% 드래그만 적용.")]
    public float gracePeriodDragMult = 0.3f;

    // ── 퍼블릭 상태 ──

    /// <summary>현재 지면이 경사로(minSlopeAngle 이상)인지 여부.</summary>
    public bool IsOnRamp { get; private set; }

    /// <summary>현재 경사면의 기울기 각도(°). 평지=0, 수직=90.</summary>
    public float SlopeAngle { get; private set; }

    /// <summary>경사→평지 전이 직후 Grace Period가 활성 중인지 여부.</summary>
    public bool IsInGracePeriod => _graceTimer > 0f;

    /// <summary>
    /// PlayerSlider.SlideMovement()에서 드래그 계산 시 곱할 배율.
    /// Grace Period 중이면 gracePeriodDragMult, 아니면 1.0.
    /// </summary>
    public float GraceDragMultiplier => IsInGracePeriod ? gracePeriodDragMult : 1f;

    // ── 내부 상태 ──
    private PlayerController _hub;
    private float _graceTimer;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
    }

    /// <summary>
    /// Update 루프에서 호출. 경사로 감지 + Grace Period 타이머 관리.
    /// </summary>
    public void UpdateModule()
    {
        bool wasOnRamp = IsOnRamp;

        // PlayerMovement.CheckGrounded()가 이미 계산한 GroundNormal 활용 (중복 레이캐스트 없음)
        if (_hub.movement.IsGrounded)
        {
            SlopeAngle = Vector3.Angle(Vector3.up, _hub.movement.GroundNormal);
            IsOnRamp = SlopeAngle >= minSlopeAngle;
        }
        else
        {
            IsOnRamp = false;
            SlopeAngle = 0f;
        }

        // Grace Period: 경사로를 벗어난 순간부터 카운트다운 시작
        if (wasOnRamp && !IsOnRamp)
        {
            _graceTimer = gracePeriodDuration;
        }

        if (_graceTimer > 0f)
        {
            _graceTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// FixedUpdate에서 메인 모듈(Movement/Slider) 실행 후 호출.
    /// 경사면 위가 아니면 즉시 리턴합니다.
    /// </summary>
    /// <param name="jumpIntended">이번 프레임에 점프가 예정되어 있는지. true이면 부착력을 비활성화합니다.</param>
    public void FixedUpdateModule(bool jumpIntended)
    {
        if (!IsOnRamp) return;

        if (!_hub.slider.IsSliding)
        {
            ApplyWalkingMode(jumpIntended);
        }
        else
        {
            ApplySlidingMode();
        }
    }

    /// <summary>
    /// PlayerMovement가 targetSpeed 계산 시 곱할 배율.
    /// 급경사(steepSlopeAngle 이상)이면 steepWalkPenalty, 아니면 1.0.
    /// </summary>
    public float GetWalkSpeedMultiplier()
    {
        if (IsOnRamp && SlopeAngle >= steepSlopeAngle)
            return steepWalkPenalty;
        return 1f;
    }

    /// <summary>
    /// 걷기 모드: 중력 비활성화 + 부착력 + 속도 경사면 투영.
    /// "투명 경사로지만 미끄러지지 않고 일반 계단처럼 걷기."
    /// </summary>
    private void ApplyWalkingMode(bool jumpIntended)
    {
        // 1. 중력 OFF → 경사면에서 미끄러지지 않음
        _hub.Rb.useGravity = false;

        // 2. 부착력: 점프 의도가 없을 때만 적용 (무거운 점프 방지)
        if (!jumpIntended)
        {
            _hub.Rb.AddForce(-_hub.movement.GroundNormal * slopeStickForce,
                             ForceMode.Acceleration);
        }

        // 3. 현재 속도를 경사면 평면에 투영 (계단처럼 걷기)
        Vector3 vel = _hub.Rb.linearVelocity;
        Vector3 projectedVel = Vector3.ProjectOnPlane(vel, _hub.movement.GroundNormal);

        // 원래 XZ 평면 속력을 유지하면서 경사면 방향으로 정렬
        float originalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
        if (projectedVel.sqrMagnitude > 0.001f)
        {
            projectedVel = projectedVel.normalized * originalSpeed;
        }

        _hub.Rb.linearVelocity = projectedVel;
    }

    /// <summary>
    /// 슬라이딩 모드: 중력 활성화 + 내리막 AddForce 가속 + 최대 속도 캡.
    /// "경사로의 법선 벡터(Normal Vector)를 계산해 가속도(AddForce) 때려 박기."
    /// </summary>
    private void ApplySlidingMode()
    {
        // 1. 중력 ON (자연스러운 물리 기반 가속)
        _hub.Rb.useGravity = true;

        // 2. 경사면 내리막 방향으로 가속
        //    중력 벡터를 경사면에 투영 → "경사면을 따라 아래로 미끄러지는" 방향
        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity,
                           _hub.movement.GroundNormal).normalized;
        _hub.Rb.AddForce(downhill * rampSlideAcceleration, ForceMode.Acceleration);

        // 3. 최대 속도 캡
        if (_hub.Rb.linearVelocity.magnitude > rampSlideMaxSpeed)
        {
            _hub.Rb.linearVelocity = _hub.Rb.linearVelocity.normalized * rampSlideMaxSpeed;
        }

        // 4. 좌우 조향만 허용 (전후 입력 무시)
        Vector2 moveInput = _hub.InputProv.MoveInput;
        Vector3 steering = (_hub.transform.right * moveInput.x).normalized
                           * (_hub.movement.walkSpeed * 0.3f);
        _hub.Rb.AddForce(steering, ForceMode.Acceleration);
    }
}
