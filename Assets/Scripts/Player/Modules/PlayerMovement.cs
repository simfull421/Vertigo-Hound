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

    [Header("Camera & Look Hierarchy")]
    [Tooltip("마우스 위아래(Pitch) 회전을 전담하는 피벗입니다.")]
    public Transform cameraPitchPivot;
    public float mouseSensitivity = 0.1f;
    private float xRotation = 0f;

    [Header("Ground Check")]
    public LayerMask groundMask;

    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float CurrentRunTime { get; private set; }
    public float CurrentAirTime { get; private set; }
    public float HighestY { get; private set; }

    private PlayerController _hub;
    private bool _wasGrounded;

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
            CheckDescentState();
            
            // 공중일 때 HighestY 최고점 갱신
            if (_hub.transform.position.y > HighestY)
            {
                HighestY = _hub.transform.position.y;
            }
        }
        else
        {
            CurrentAirTime = 0f;
            HighestY = _hub.transform.position.y; // 바닥일 때는 현재 높이로 동기화
            
            if (_hub.cameraActionController != null)
            {
                _hub.cameraActionController.ResetDescentPitch();
            }
        }
    }

    public void FixedUpdateModule()
    {
        // 경사면에서는 PlayerRamp가 중력을 관리하므로 여기서는 비경사면만 켬
        if (!_hub.ramp.IsOnRamp)
            _hub.Rb.useGravity = true;

        MovePlayer();

        // 1. 무중력 탈출 (Heavy Jump) 로직
        // 하강 중일 때 추가적인 하향 가속도를 곱연산하여 묵직하게 떨어지게 만듭니다.
        if (_hub.Rb.linearVelocity.y < 0f && !IsGrounded)
        {
            _hub.Rb.AddForce(Physics.gravity * 1.5f, ForceMode.Acceleration);
        }
    }

    public void HandleJump()
    {
        if (IsGrounded)
        {
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
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        if (cameraPitchPivot != null)
        {
            cameraPitchPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        _hub.transform.Rotate(Vector3.up * mouseX);
    }

    private void MovePlayer()
    {
        Vector2 moveInput = _hub.InputProv.MoveInput;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        float currentTargetSpeed = walkSpeed;
        currentTargetSpeed *= _hub.ramp.GetWalkSpeedMultiplier();

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

        // 느릿한 쭈그려 걷기 페널티 (슬라이드 발동 실패 스팸 벌칙). 이 페널티는 허브/슬라이더 상태를 물어보고 결정합니다.
        if (_hub.InputProv.SlideHeld && !_hub.slider.IsSliding)
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

    private void CheckGrounded()
    {
        _wasGrounded = IsGrounded;

        Vector3 bottom = _hub.Capsule.bounds.center - new Vector3(0, _hub.Capsule.bounds.extents.y, 0);
        bool hitSphere = Physics.CheckSphere(bottom + Vector3.up * 0.3f, 0.45f, groundMask);

        if (hitSphere)
        {
            if (Physics.Raycast(_hub.Capsule.bounds.center, Vector3.down, out RaycastHit hit, _hub.Capsule.bounds.extents.y + 0.5f, groundMask))
            {
                GroundNormal = hit.normal;
            }
            else
            {
                GroundNormal = Vector3.up;
            }

            if (Vector3.Angle(Vector3.up, GroundNormal) <= 50f)
            {
                IsGrounded = true;
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

    private void CheckDescentState()
    {
        if (_hub.Rb.linearVelocity.y < minFallSpeedForDescent)
        {
            if (_hub.juiceController != null)
            {
                _hub.juiceController.UpdateDescentShake(CurrentAirTime, _hub.Rb.linearVelocity.y);
            }
        }
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
