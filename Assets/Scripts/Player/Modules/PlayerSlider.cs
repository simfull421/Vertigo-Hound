using UnityEngine;
using System;

[Serializable]
public sealed class PlayerSlider
{
    [Header("Sliding (Hardcore)")]
    public float minSpeedForSlide = 5f;     
    public float crouchSpeed = 3f;
    public float slideImpulse = 22f;        // 폭발적 초기 가속
    public float slideDragAccumulation = 25f; // 강한 감속 (짧고 빠르게)
    public float slidingCooldown = 1.2f;    // 슬라이딩 재발동 방지용 쿨타임
    public float slideHeightRatio = 0.5f;

    public bool IsSliding { get; private set; }
    public bool IsCrouching { get; private set; }

    private float _lastSlideEndTime = -10f;

    private float _originalCapsuleHeight;
    private Vector3 _originalCapsuleCenter;
    private float _originalRideHeight;

    private PlayerController _hub;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
        _originalCapsuleHeight = _hub.Capsule.height;
        _originalCapsuleCenter = _hub.Capsule.center;
        _originalRideHeight = _hub.movement.rideHeight;
    }

    public void UpdateModule()
    {
        CheckSlide();
    }

    public void FixedUpdateModule()
    {
        SlideMovement();
    }

    public void HandleJump()
    {
        float checkDist = _originalCapsuleHeight - _hub.Capsule.height;
        Vector3 bottom = _hub.Capsule.bounds.center - new Vector3(0, _hub.Capsule.bounds.extents.y, 0);
        bool hasCeiling = Physics.SphereCast(bottom + Vector3.up * _hub.Capsule.radius, _hub.Capsule.radius, Vector3.up, out _, checkDist, _hub.movement.groundMask);

        if (!hasCeiling)
        {
            StopSlideOrCrouch(true);
        }
        else 
        {
            return; 
        }

        _hub.Rb.linearVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
        Vector3 jumpDir = Vector3.Lerp(Vector3.up, _hub.movement.GroundNormal, 0.6f).normalized;
        
        Vector3 finalForce = jumpDir * _hub.movement.jumpForce;

        Vector3 currentXZDir = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).normalized;
        finalForce += currentXZDir * (slideImpulse * 0.9f);

        _hub.Rb.AddForce(finalForce, ForceMode.Impulse);

        float v0 = _hub.movement.jumpForce / _hub.Rb.mass;
        float g = Physics.gravity.magnitude; 
        float timeToApex = v0 / g;

        if (_hub.juiceController != null)
        {
             _hub.juiceController.TriggerKickJuice();
        }
    }

    private void CheckSlide()
    {
        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z);
        float currentSpeed = currentXZVelocity.magnitude;
        bool crouchInput = _hub.InputProv.SlideHeld || _hub.InputProv.CrouchHeld;

        if (!IsSliding && !IsCrouching)
        {
            if (crouchInput && _hub.movement.IsGrounded)
            {
                // 달리기 상태(가속 중)이거나 경사면일 때 슬라이딩
                bool isJoggingOrSprinting = currentSpeed >= 6.5f && _hub.InputProv.DashHeld;
                bool isOnRamp = _hub.ramp.IsOnRamp;

                if (isJoggingOrSprinting || isOnRamp)
                {
                    if (Time.time >= _lastSlideEndTime + slidingCooldown)
                    {
                        StartSlide(currentSpeed);
                    }
                    else
                    {
                        // 쿨타임 중일 때는 슬라이드 대신 앉기로 대체
                        StartCrouch();
                    }
                }
                else
                {
                    StartCrouch();
                }
            }
        }
        else if (IsSliding)
        {
            bool tooSlow = currentSpeed <= crouchSpeed && !_hub.ramp.IsOnRamp;
            
            if (!crouchInput || !_hub.movement.IsGrounded)
            {
                AttemptStopCrouchOrSlide();
            }
            else if (tooSlow)
            {
                // 속도가 떨어지면 슬라이딩에서 일반 앉기(Crouch) 상태로 전환
                IsSliding = false;
                IsCrouching = true;

                if (_hub.juiceController != null)
                {
                    _hub.juiceController.TriggerSlideEnd(false);
                }
            }
        }
        else if (IsCrouching)
        {
            if (!crouchInput || !_hub.movement.IsGrounded)
            {
                AttemptStopCrouchOrSlide();
            }
            // 앉아 있다가 갑자기 Dash를 눌러도 속도가 안나오므로 굳이 슬라이드로 즉시 안 바꿈. 
            // 일어서고 나서 다시 뛰게 하는 것이 자연스러움.
        }
    }

    private void StartSlide(float entrySpeed)
    {
        IsSliding = true;
        
        _hub.Capsule.height = _originalCapsuleHeight * slideHeightRatio;
        float dipAmount = _originalCapsuleHeight * (1f - slideHeightRatio) / 2f;
        _hub.Capsule.center = new Vector3(_originalCapsuleCenter.x, _originalCapsuleCenter.y - dipAmount, _originalCapsuleCenter.z);
        
        // 추가: 호버링 목표 높이(Ride Height)도 스케일에 맞춰 감소
        _hub.movement.rideHeight = _originalRideHeight * slideHeightRatio;

        Vector3 slideDir = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z).normalized;
        if (slideDir.sqrMagnitude < 0.1f) slideDir = _hub.transform.forward;
        
        // 경사면에서는 초기 Impulse 없이 순수 중력 가속만 적용 (버니합 악용 차단)
        if (!_hub.ramp.IsOnRamp)
        {
            // 속도 비례 슬라이딩 추진력: 최고 달리기 속도(18.0)를 기준으로 비율 계산
            float speedRatio = Mathf.Clamp01(entrySpeed / _hub.movement.runMaxSpeed);
            // 걷기 속도쯤엔 Base Impulse의 절반(0.5) ~ 최고속도일 땐 1.0배
            float proportionalImpulse = slideImpulse * Mathf.Lerp(0.4f, 1.0f, speedRatio);
            
            // 한 번(Impulse)만 부드럽게 밀어 넣음
            _hub.Rb.AddForce(slideDir * proportionalImpulse, ForceMode.Impulse);
        }

        if (_hub.juiceController != null)
        {
            _hub.juiceController.TriggerSlideStart();
        }
    }

    private void StartCrouch()
    {
        IsCrouching = true;
        
        _hub.Capsule.height = _originalCapsuleHeight * slideHeightRatio;
        float dipAmount = _originalCapsuleHeight * (1f - slideHeightRatio) / 2f;
        _hub.Capsule.center = new Vector3(_originalCapsuleCenter.x, _originalCapsuleCenter.y - dipAmount, _originalCapsuleCenter.z);
        
        // 추가: 호버링 목표 높이(Ride Height)도 스케일에 맞춰 감소
        _hub.movement.rideHeight = _originalRideHeight * slideHeightRatio;

        // 앉기는 Impulse 부여나 이펙트가 필요 없이 캡슐만 축소함.
    }

    private void AttemptStopCrouchOrSlide()
    {
        float checkDist = _originalCapsuleHeight - _hub.Capsule.height;
        Vector3 bottom = _hub.Capsule.bounds.center - new Vector3(0, _hub.Capsule.bounds.extents.y, 0);
        bool hasCeiling = Physics.SphereCast(bottom + Vector3.up * _hub.Capsule.radius, _hub.Capsule.radius, Vector3.up, out _, checkDist, _hub.movement.groundMask);

        if (hasCeiling && _hub.movement.IsGrounded) 
        {
            return;
        }

        StopSlideOrCrouch(false);
    }

    public void StopSlideOrCrouch(bool isJumpHop)
    {
        bool wasSliding = IsSliding;

        IsSliding = false;
        IsCrouching = false;
        
        if (wasSliding)
        {
            _lastSlideEndTime = Time.time;
        }

        _hub.Capsule.height = _originalCapsuleHeight;
        _hub.Capsule.center = _originalCapsuleCenter;
        
        // 일어날 때 원래 호버링 높이로 복구
        _hub.movement.rideHeight = _originalRideHeight;

        if (wasSliding && _hub.juiceController != null)
        {
            _hub.juiceController.TriggerSlideEnd(isJumpHop);
        }
    }

    private void SlideMovement()
    {
        // 경사면 슬라이딩은 PlayerRamp.ApplySlidingMode()가 전담
        if (_hub.ramp.IsOnRamp) return;

        Vector2 moveInput = _hub.InputProv.MoveInput;
        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
        
        // Grace Period: 경사→평지 전이 직후 드래그를 줄여 모멘텀 보존
        float effectiveDrag = slideDragAccumulation * _hub.ramp.GraceDragMultiplier;
        Vector3 newXZVelocity = Vector3.MoveTowards(currentXZVelocity, Vector3.zero, effectiveDrag * Time.fixedDeltaTime);
        
        Vector3 steering = (_hub.transform.right * moveInput.x).normalized * (_hub.movement.walkSpeed * 0.3f);
        newXZVelocity += steering * Time.fixedDeltaTime;

        _hub.Rb.linearVelocity = new Vector3(newXZVelocity.x, _hub.Rb.linearVelocity.y, newXZVelocity.z);
    }
}
