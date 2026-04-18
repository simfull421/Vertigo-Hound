using UnityEngine;
using System;

[Serializable]
public sealed class PlayerAnimatorHandler
{
    [Header("Animator Components")]
    [Tooltip("플레이어 프리팹 내부의 Animator 객체를 연결하세요.")]
    public Animator animator;

    [Header("Animation Smooth Damp")]
    [Tooltip("입력 및 이동 애니메이션 보간의 쫀득함을 조정합니다 (값이 클수록 부드럽지만 느리게 반응).")]
    public float moveSmoothTime = 0.1f;

    [Header("Weapon Settings")]
    [Tooltip("현재 활성화된 무기 타입 (0: 맨손, 1: 권총)")]
    public int currentWeaponType = 0;

    private PlayerController _hub;

    // SmoothDamp를 위한 레퍼런스 속도 변수
    private float _moveXVelocity;
    private float _moveYVelocity;

    // 현재 보간 중인 X, Y 값
    private float _currentMoveX;
    private float _currentMoveY;
    private float _currentSpeed;
    private float _speedVelocity;
    private float _previousTargetMoveY;

    // Animator 해시값 (문자열보다 조회 속도가 빠름)
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashMoveX = Animator.StringToHash("MoveX");
    private readonly int hashMoveY = Animator.StringToHash("MoveY");
    private readonly int hashIsCrouching = Animator.StringToHash("IsCrouching");
    private readonly int hashWeaponType = Animator.StringToHash("WeaponType");
    
    private readonly int hashTriggerPunch = Animator.StringToHash("TriggerPunch");
    private readonly int hashTriggerJump = Animator.StringToHash("TriggerJump");
    private readonly int hashTriggerStop = Animator.StringToHash("TriggerStop");
    private readonly int hashIsSliding = Animator.StringToHash("IsSliding");
    private readonly int hashTriggerSlideEnter = Animator.StringToHash("Slide_Enter");
    private readonly int hashPunchIndex = Animator.StringToHash("PunchIndex");

    private bool _wasSliding;
    private bool _wasCrouching;
    private float _nextPunchTime;
    
    private float _defaultCameraHeight;
    private bool _hasDefaultHeight;

    public void Initialize(PlayerController hub)
    {
        _hub = hub;

        if (animator != null)
        {
            // 초기 무기 타입 설정
            animator.SetInteger(hashWeaponType, currentWeaponType);
        }
        else
        {
            Debug.LogWarning("[PlayerAnimatorHandler] Animator is not assigned!");
        }
    }

    public void UpdateModule()
    {
        if (animator == null) return;

        HandleMovementAnimation();
        HandleWeaponSwitch();
        HandleActionTriggers();
        HandleCameraHeight();
    }

    private void HandleMovementAnimation()
    {
        Vector2 input = _hub.InputProv.MoveInput;
        bool isMoving = input.sqrMagnitude > 0.01f;
        
        float targetMoveX = isMoving ? Mathf.Clamp(input.x, -1f, 1f) : 0f;
        float targetMoveY = isMoving ? Mathf.Clamp(input.y, -1f, 1f) : 0f;

        if (isMoving)
        {
            // 달리기 (Sprint) 로직: 앞으로 이동 중(W키)이면서 Shift키를 누르고 있을 때만 2.0
            if (targetMoveY > 0f && _hub.InputProv.DashHeld)
            {
                targetMoveY = 2.0f;
                // Debug.Log($"[Sprint] targetMoveY Reached: {targetMoveY:F1}");
            }
        }

        // Run To Stop 트리거 발동 로직 (루프 방지)
        if (_previousTargetMoveY > 1.5f && targetMoveY < 0.1f)
        {
            animator.SetTrigger(hashTriggerStop);
        }
        _previousTargetMoveY = targetMoveY;

        // SmoothDamp 연산으로 쫀득한 보간
        _currentMoveX = Mathf.SmoothDamp(_currentMoveX, targetMoveX, ref _moveXVelocity, moveSmoothTime);
        _currentMoveY = Mathf.SmoothDamp(_currentMoveY, targetMoveY, ref _moveYVelocity, moveSmoothTime);

        // Speed 파라미터 (앞뒤좌우 상관없이 실질적인 이동 강도)
        float targetSpeed = new Vector2(targetMoveX, targetMoveY).magnitude;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, moveSmoothTime);

        // 파라미터 업데이트
        animator.SetFloat(hashSpeed, _currentSpeed);
        animator.SetFloat(hashMoveX, _currentMoveX);
        animator.SetFloat(hashMoveY, _currentMoveY);

      

        // 앉기 vs 슬라이딩 분기 로직 (핵심) 및 중복 방지 방어 코드
        bool crouchInput = _hub.InputProv.CrouchHeld || _hub.InputProv.SlideHeld;
        bool isSprintIntent = targetMoveY >= 1.5f && _hub.InputProv.DashHeld;

        if (_wasSliding)
        {
            // 슬라이딩이 진행 중일 때는 Ctrl키를 떼더라도 무시하고 Physics(PlayerSlider)가 완전히 슬라이딩을 끝낼 때까지 대기
            if (!_hub.slider.IsSliding)
            {
                animator.SetBool(hashIsSliding, false);
                _wasSliding = false;
                
                // 속도 다이 다운으로 인하여 자연스럽게 앉기로 전환되었을 경우
                if (_hub.slider.IsCrouching) 
                {
                    animator.SetBool(hashIsCrouching, true);
                    _wasCrouching = true;
                }
            }
        }
        else if (_wasCrouching)
        {
            // 앉은 상태에서 일어서거나, 앞으로 이동(W)하며 Dash(Shift)를 누를 경우 즉시 일어남(Sprinting 대응)
            if (!crouchInput || isSprintIntent)
            {
                animator.SetBool(hashIsCrouching, false);
                _wasCrouching = false;
            }
        }
        else
        {
            if (crouchInput)
            {
                bool isOnSlope = _hub.movement.IsGrounded && Vector3.Angle(Vector3.up, _hub.movement.GroundNormal) > 5f;
                
                // 조건 A (슬라이딩): MoveY 가 1.5f 이상이거나 경사면일 때만 
                if (_currentMoveY >= 1.5f || isOnSlope)
                {
                    animator.SetTrigger(hashTriggerSlideEnter);
                    animator.SetBool(hashIsSliding, true);
                    _wasSliding = true;
                }
                else
                {
                    // 조건 B (일반 앉기): 평지 + 보행속도 이하일 때
                    animator.SetBool(hashIsCrouching, true);
                    _wasCrouching = true;
                }
            }
        }
    }

    private void HandleWeaponSwitch()
    {
        bool changed = false;

        // 키보드 1번 -> WeaponType 0 (맨손)
        if (_hub.InputProv.Weapon1Triggered && currentWeaponType != 0)
        {
            currentWeaponType = 0;
            changed = true;
        }
        // 키보드 2번 -> WeaponType 1 (권총)
        else if (_hub.InputProv.Weapon2Triggered && currentWeaponType != 1)
        {
            currentWeaponType = 1;
            changed = true;
        }

        if (changed)
        {
            animator.SetInteger(hashWeaponType, currentWeaponType);
            // Debug.Log($"[Animator] Weapon Switched to: {currentWeaponType}");
        }
    }

    private void HandleActionTriggers()
    {
        if (_hub.InputProv.FireTriggered)
        {
            if (currentWeaponType == 0) // 맨손일 때
            {
                if (Time.time >= _nextPunchTime)
                {
                    int punchIndex = UnityEngine.Random.Range(0, 2);
                    animator.SetInteger(hashPunchIndex, punchIndex);
                    animator.SetTrigger(hashTriggerPunch);
                    
                    _nextPunchTime = Time.time + 0.6f; // 쿨타임
                }
            }
        }
    }

    private void HandleCameraHeight()
    {
        if (_hub.movement.cameraPitchPivot != null)
        {
            if (!_hasDefaultHeight)
            {
                 _defaultCameraHeight = _hub.movement.cameraPitchPivot.localPosition.y;
                 _hasDefaultHeight = true;
            }

            float targetHeight = (_wasSliding || _wasCrouching) ? _defaultCameraHeight * 0.5f : _defaultCameraHeight;
            Vector3 pos = _hub.movement.cameraPitchPivot.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetHeight, Time.deltaTime * 12f);
            
            _hub.movement.cameraPitchPivot.localPosition = pos;
        }
    }

    public void TriggerJump()
    {
        if (animator != null)
        {
            animator.SetTrigger(hashTriggerJump);
        }
    }
}
