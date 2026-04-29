using UnityEngine;
using System;

[Serializable]
public sealed class PlayerMovement
{
    [Header("Movement (Walk & Run)")]
    public float walkSpeed = 6f;
    public float runInitialSpeed = 10f; 
    public float runMaxSpeed = 18f;     
    public float runChargeTime = 3f;    
    
    public float groundAccel = 80f;
    public float airAccel = 30f;
    public float jumpForce = 12f;

    [Header("Descent & Landing")]
    public float minFallSpeedForDescent = -5f;
    public float minAirTimeForRoll = 0.5f;     
    [Tooltip("하강 시 중력 가속도 배율 (기본값 2.5 = 2.5배 빠르게 낙하)")]
    public float fallMultiplier = 2.5f;

    [Header("Camera & Look Hierarchy")]
    [Tooltip("마우스 위아래(Pitch) 회전을 전담하는 피벗입니다.")]
    public Transform cameraPitchPivot;
    public float mouseSensitivity = 0.1f;
    [Tooltip("카메라가 위를 바라볼 수 있는 최대 각도 (음수, 기본값 -90)")]
    public float maxPitchUp = -90f;
    [Tooltip("카메라가 아래를 바라볼 수 있는 최대 각도 (양수, 자신의 목이 보이지 않게 조절)")]
    public float maxPitchDown = 70f;
    private float xRotation = 0f;

    [Header("Camera Recoil")]
    [Tooltip("사격 시 카메라가 위로 들리는 속도 (부드러운 반동 적용)")]
    public float recoilApplySpeed = 30f;
    private float targetRecoilOffset = 0f;

    [Header("Hover Suspension")]
    [Tooltip("캡슐 중심에서 바닥까지 띄울 목표 높이. 캡슐의 extents.y(보통 1)보다 커야 바닥에 닿지 않습니다.")]
    public float rideHeight = 1.2f;
    [Tooltip("서스펜션 레이캐스트 최대 길이")]
    public float raycastLength = 1.5f;
    [Tooltip("스프링 강도. 숫자가 높을수록 딱딱하게 버팁니다.")]
    public float springStiffness = 250f;

    [Header("Ground Check")]
    public LayerMask groundMask;

    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float CurrentRunTime { get; private set; }
    public float CurrentAirTime { get; private set; }
    public float HighestY { get; private set; }

    private PlayerController _hub;
    private bool _wasGrounded;
    private bool _wasWalking;
    private bool _wasSprinting;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;
        _wasGrounded = true;
    }

    public void UpdateModule()
    {
        CheckGrounded();
        HandleLook();

        if (!IsGrounded)
        {
            CurrentAirTime += Time.deltaTime;
            
            // 공중일 때 HighestY 최고점 갱신
            if (_hub.transform.position.y > HighestY)
            {
                HighestY = _hub.transform.position.y;
            }
        }
        else
        {
            CurrentAirTime = 0f;
            HighestY = _hub.transform.position.y; 
            
            if (_hub.cameraActionController != null)
            {
                _hub.cameraActionController.ResetDescentPitch();
            }
        }

        UpdateMovementJuiceTriggers();
    }

    private void UpdateMovementJuiceTriggers()
    {
        if (_hub.juiceController == null) return;

        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z);
        float currentSpeed = currentXZVelocity.magnitude;

        bool isSprinting = _hub.InputProv.DashHeld && currentSpeed > walkSpeed;
        bool isWalking = IsGrounded && currentSpeed > 0.1f && !isSprinting;
        bool isRunningNow = IsGrounded && isSprinting;

        bool strafeActive = !_hub.vault.IsVaulting;
        if (strafeActive)
        {
            _hub.juiceController.TriggerStrafe(_hub.InputProv.MoveInput.x);
        }

        if (isRunningNow && !_wasSprinting)
        {
            _hub.juiceController.TriggerSprintStart();
        }
        else if (!isRunningNow && _wasSprinting)
        {
            _hub.juiceController.TriggerSprintStop();
        }

        if (isWalking && !_wasWalking)
        {
            _hub.juiceController.TriggerWalkStart();
        }
        else if (!isWalking && _wasWalking)
        {
            _hub.juiceController.TriggerWalkStop();
        }

        _wasSprinting = isRunningNow;
        _wasWalking = isWalking;
    }

    public void FixedUpdateModule()
    {
        _hub.Rb.useGravity = true;

        MovePlayer();

        if (IsGrounded && (Time.time - _lastJumpTime > 0.2f))
        {
            ApplySuspension();
        }

        // 1. 무중력 탈출 (Heavy Jump) 로직 - Fall Multiplier
        // 정점을 찍고 하강 중일 때 추가 중력을 더해 묵직하게 꽂히도록 만듭니다.
        if (_hub.Rb.linearVelocity.y < 0f && !IsGrounded)
        {
            _hub.Rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    private float _lastJumpTime;

    public void HandleJump()
    {
        if (IsGrounded)
        {
            _lastJumpTime = Time.time;
            _hub.Rb.linearVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);
            Vector3 jumpDir = Vector3.Lerp(Vector3.up, GroundNormal, 0.6f).normalized;
            
            _hub.Rb.AddForce(jumpDir * jumpForce, ForceMode.Impulse);

            float v0 = jumpForce / _hub.Rb.mass;
            float g = Physics.gravity.magnitude; 
            float timeToApex = v0 / g;

            if (_hub.cameraActionController != null)
            {
                 _hub.cameraActionController.TriggerRandomPattern(timeToApex);
            }
        }
    }

    private void HandleLook()
    {
        Vector2 lookInput = _hub.InputProv.LookInput;
        
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;

        // 반동 누적값을 xRotation에 부드럽게 실제 적용 (영구적용되므로 직접 마우스로 내려서 잡아야 함)
        if (targetRecoilOffset > 0f)
        {
            float applyStep = targetRecoilOffset * recoilApplySpeed * Time.deltaTime;
            applyStep = Mathf.Min(applyStep, targetRecoilOffset); // 오버슛 방지
            
            xRotation -= applyStep;
            targetRecoilOffset -= applyStep;
        }

        xRotation = Mathf.Clamp(xRotation, maxPitchUp, maxPitchDown);
        
        if (cameraPitchPivot != null)
        {
            cameraPitchPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        _hub.transform.Rotate(Vector3.up * mouseX);
    }

    public void AddRecoilPitch(float pitchUp)
    {
        targetRecoilOffset += Mathf.Abs(pitchUp);
    }

    private void MovePlayer()
    {
        Vector2 moveInput = _hub.InputProv.MoveInput;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        float currentTargetSpeed = walkSpeed;

        if (_hub.InputProv.DashHeld && isMoving)
        {
            CurrentRunTime += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(CurrentRunTime / runChargeTime);
            currentTargetSpeed = Mathf.Lerp(runInitialSpeed, runMaxSpeed, progress);
        }
        else
        {
            if (!isMoving) 
            {
                CurrentRunTime = 0f;
                currentTargetSpeed = 0f;
            }
        }

        // 앉기(Crouch) 물리 연산: PlayerSlider가 IsCrouching 상태일 때 속도 제한 적용
        if (_hub.slider.IsCrouching)
        {
            currentTargetSpeed = _hub.slider.crouchSpeed;
            CurrentRunTime = 0f;
        }

        Vector3 targetVelocity = (_hub.transform.forward * moveInput.y + _hub.transform.right * moveInput.x).normalized * currentTargetSpeed;
        Vector3 currentXZVelocity = new Vector3(_hub.Rb.linearVelocity.x, 0f, _hub.Rb.linearVelocity.z);

        float accelRate = IsGrounded ? groundAccel : airAccel;
        Vector3 newXZVelocity = Vector3.MoveTowards(currentXZVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        _hub.Rb.linearVelocity = new Vector3(newXZVelocity.x, _hub.Rb.linearVelocity.y, newXZVelocity.z);
    }

    private float _currentRideHeight;

    private void CheckGrounded()
    {
        _wasGrounded = IsGrounded;

        // 캡슐 반경보다 약간 작게 쏴서 모서리에 걸리는 것 방지
        float radius = _hub.Capsule.radius * 0.9f;

        if (Physics.SphereCast(_hub.Capsule.bounds.center, radius, Vector3.down, out RaycastHit hit, raycastLength, groundMask))
        {
            GroundNormal = hit.normal;
            if (Vector3.Angle(Vector3.up, GroundNormal) <= 50f)
            {
                IsGrounded = true;
                _currentRideHeight = hit.distance;

                if (!_wasGrounded)
                {
                    OnLanded();
                }
            }
            else
            {
                IsGrounded = false;
            }
        }
        else
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
        }
    }

    private void ApplySuspension()
    {
        float mass = _hub.Rb.mass;
        // 임계 감쇠 공식 (Critical Damping)
        float springDamper = 2f * Mathf.Sqrt(springStiffness * mass);

        // 조화 진동자 공식: F = k * (H_target - H_current) - c * v_y
        float springForce = springStiffness * (rideHeight - _currentRideHeight) - springDamper * _hub.Rb.linearVelocity.y;
        
        _hub.Rb.AddForce(Vector3.up * springForce, ForceMode.Force);
    }

    private void OnLanded()
    {
        float fallDistance = HighestY - _hub.transform.position.y;
        
        bool isActiveLanding = fallDistance >= 5f && _hub.InputProv.SlideHeld;

        if (isActiveLanding)
        {
            // 낙법 (Active Landing) 성공 - 낙뎀 무효화 및 Head Dip 발동
            if (_hub.juiceController != null)
            {
                _hub.juiceController.TriggerActiveLandingRoll();
            }
        }
        else if (fallDistance >= 10f)
        {
            // 치명적 낙하 (낙뎀 처리 등)
            Debug.Log($"[Player] Fall Damage taken! Distance: {fallDistance:F1}m");
            if (_hub.juiceController != null)
            {
                _hub.juiceController.TriggerLandingDrop(2.0f);
            }
        }
        else if (fallDistance >= 4f || CurrentAirTime >= minAirTimeForRoll)
        {
            // 평범한 하드 랜딩 (무거운 착지감)
            if (_hub.juiceController != null)
            {
                _hub.juiceController.TriggerLandingDrop(1.5f);
            }
        }
        else
        {
            // 가벼운 착지
            if (_hub.cameraActionController != null)
            {
                _hub.cameraActionController.InterruptAction();
            }
        }

        HighestY = _hub.transform.position.y;
    }

    public void ForceLookDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            // Y축(Yaw) 반사된 방향으로 즉시 동기화
            _hub.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            
            // X축(Pitch) Snap-back 방지를 위해 누적값을 0으로 리셋
            xRotation = 0f;
            if (cameraPitchPivot != null)
            {
                cameraPitchPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }
    }
}
