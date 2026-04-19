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
    
    [Header("Animation Sync")]
    public float baseMoveSpeed = 4f; // 걷기 애니메이션이 정상적으로 보일 때의 실제 이동 속도 기준점
    // 모노비헤비어가 아니므로 직접 참조할 수 없습니다. 대신 허브를 통해 가져옵니다.
    // [Header("Viewmodel")] ... (삭제됨, PlayerController에서 관리)
    [Header("World Model Hiding")]
    [Tooltip("플레이어의 진짜 몸뚱이 렌더러들 (SkinMeshRenderer 등)")]
    public SkinnedMeshRenderer[] worldModelRenderers;
    private PlayerController _hub;

    // [추가] 어깨 뼈 자동 할당용 변수
    private Transform _leftShoulder;
    private Transform _rightShoulder;

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
    
    public void Initialize(PlayerController hub)
    {
        _hub = hub;

        if (animator != null)
        {
            // [핵심] 귀찮은 컴포넌트 할당 없이 자동으로 어깨 뼈를 찾아옵니다.
            _leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);

            // 초기 무기 타입 및 뷰모델 상태 설정
            animator.SetInteger(hashWeaponType, currentWeaponType);
            if (_hub.viewmodelGun != null) _hub.viewmodelGun.SetActive(currentWeaponType == 1);
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
    }

    // [추가] 애니메이터가 스케일을 1로 되돌리는 것을 막기 위한 LateUpdate용 함수
    public void LateUpdateModule()
    {
        if (animator == null) return;

        if (currentWeaponType == 1) // 총기 들었을 때 (스케일 0)
        {// 몸통을 투명하게 하되, 그림자는 남깁니다.
            foreach (var renderer in worldModelRenderers)
            {
                if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            if (_leftShoulder != null) _leftShoulder.localScale = Vector3.zero;
            if (_rightShoulder != null) _rightShoulder.localScale = Vector3.zero;
        }
        else // 맨손일 때 (스케일 1)
        {// 몸통을 다시 정상적으로 보이게 합니다.
            foreach (var renderer in worldModelRenderers)
            {
                if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (_leftShoulder != null) _leftShoulder.localScale = Vector3.one;
            if (_rightShoulder != null) _rightShoulder.localScale = Vector3.one;
        }
    }

    private void HandleMovementAnimation()
    {
        // ... (기존 코드 완벽하게 동일) ...
        Vector2 input = _hub.InputProv.MoveInput;
        bool isMoving = input.sqrMagnitude > 0.01f;
        
        float targetMoveX = isMoving ? Mathf.Clamp(input.x, -1f, 1f) : 0f;
        float targetMoveY = isMoving ? Mathf.Clamp(input.y, -1f, 1f) : 0f;

        if (isMoving)
        {
            if (targetMoveY > 0f && _hub.InputProv.DashHeld)
            {
                targetMoveY = 2.0f;
            }
        }

        if (_previousTargetMoveY > 1.5f && targetMoveY < 0.1f)
        {
            animator.SetTrigger(hashTriggerStop);
        }
        _previousTargetMoveY = targetMoveY;

        _currentMoveX = Mathf.SmoothDamp(_currentMoveX, targetMoveX, ref _moveXVelocity, moveSmoothTime);
        _currentMoveY = Mathf.SmoothDamp(_currentMoveY, targetMoveY, ref _moveYVelocity, moveSmoothTime);

        float targetSpeed = new Vector2(targetMoveX, targetMoveY).magnitude;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, moveSmoothTime);

        animator.SetFloat(hashSpeed, _currentSpeed);
        animator.SetFloat(hashMoveX, _currentMoveX);
        animator.SetFloat(hashMoveY, _currentMoveY);

        bool crouchInput = _hub.InputProv.CrouchHeld || _hub.InputProv.SlideHeld;
        bool isSprintIntent = targetMoveY >= 1.5f && _hub.InputProv.DashHeld;
    // [추가] 발 미끄러짐 방지: 실제 이동 속도에 맞춰 애니메이션 배속 조절
        float actualSpeed = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).magnitude;
        
        if (actualSpeed > 0.1f)
        {
            // 배속 계산
            float targetAnimSpeed = actualSpeed / baseMoveSpeed;
            
            // [핵심] 아무리 빨라도 0.8배 ~ 1.3배 사이에서만 놀도록 제한 (Clamp)
            animator.speed = Mathf.Clamp(targetAnimSpeed, 0.8f, 1.3f); 
        }
        else
        {
            animator.speed = 1f;
        }
        if (_wasSliding)
        {
            if (!_hub.slider.IsSliding)
            {
                animator.SetBool(hashIsSliding, false);
                _wasSliding = false;
                
                if (_hub.slider.IsCrouching) 
                {
                    animator.SetBool(hashIsCrouching, true);
                    _wasCrouching = true;
                }
            }
        }
        else if (_wasCrouching)
        {
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
                
                if (_currentMoveY >= 1.5f || isOnSlope)
                {
                    animator.SetTrigger(hashTriggerSlideEnter);
                    animator.SetBool(hashIsSliding, true);
                    _wasSliding = true;
                }
                else
                {
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
            
            // 허브에 있는 뷰모델 활성화/비활성화
            if (_hub.viewmodelGun != null)
            {
                _hub.viewmodelGun.SetActive(currentWeaponType == 1);
            }
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

    public void TriggerJump()
    {
        if (animator != null)
        {
            animator.SetTrigger(hashTriggerJump);
        }
    }
}