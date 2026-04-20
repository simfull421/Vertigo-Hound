using UnityEngine;
using System;

[Serializable]
public sealed class PlayerAnimatorHandler
{
    [Header("Animator Components")]
    [Tooltip("반드시 World Model(본체 캐릭터)의 Animator를 연결하세요. Gun Upper / Run Upper 등 뷰모델 Animator를 연결하면 Walk Blend Tree가 뷰모델에 재생되는 버그가 발생합니다!")]
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
    // [Dual-Model] RunUpper 뷰모델의 Animator — Initialize 시 Hub에서 캐싱
    private Animator _runUpperAnimator;



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

        // [Dual-Model] RunUpper Animator는 PlayerController.Awake에서 캐싱한 값을 참조
        // (PlayerController.Awake에서 animatorHandler.Initialize 전에 runUpperAnimator 캐싱이 완료됨)
        _runUpperAnimator = _hub.runUpperAnimator;
        if (_runUpperAnimator == null && _hub.runUpper != null)
            Debug.LogWarning("[PlayerAnimatorHandler] runUpperAnimator가 null입니다. PlayerController Awake 초기화 순서를 확인하세요.");

        if (animator != null)
        {
            // 초기 무기 타입 및 뷰모델 상태 설정
            animator.SetInteger(hashWeaponType, currentWeaponType);
            // 초기 뷰모델 활성 상태 세팅
            if (_hub.viewmodelGun != null) _hub.viewmodelGun.SetActive(currentWeaponType == 1);
            if (_hub.runUpper != null) _hub.runUpper.SetActive(currentWeaponType == 0);
        }
        else
        {
            Debug.LogWarning("[PlayerAnimatorHandler] World Model Animator is not assigned!");
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

        if (currentWeaponType == 1) // 총기 들었을 때
        {// 몸통을 투명하게 하되, 그림자는 남깁니다.
            foreach (var renderer in worldModelRenderers)
            {
                if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
        else // 맨손일 때
        {// 몸통을 다시 정상적으로 보이게 합니다.
            foreach (var renderer in worldModelRenderers)
            {
                if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
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

        // World Model Animator에 파라미터 전달 (본체 그림자 애니메이션용)
        animator.SetFloat(hashSpeed, _currentSpeed);
        animator.SetFloat(hashMoveX, _currentMoveX);
        animator.SetFloat(hashMoveY, _currentMoveY);

        // [Dual-Model 브로드캐스트] 맨손 상태이고 RunUpper가 활성화되어 있을 때만 동기화
        // RunUpper는 speed 파라미터로 Walk/Run Blend Tree를 구동하므로 동일한 값을 쏴준다.
        // animator.speed(배속 조절)는 World Model에만 적용하고, RunUpper에는 SetFloat만 사용.
        if (currentWeaponType == 0 && _runUpperAnimator != null && _hub.runUpper.activeInHierarchy)
        {
            _runUpperAnimator.SetFloat(hashSpeed, _currentSpeed);
            _runUpperAnimator.SetFloat(hashMoveX, _currentMoveX);
            _runUpperAnimator.SetFloat(hashMoveY, _currentMoveY);
        }

        bool crouchInput = _hub.InputProv.CrouchHeld || _hub.InputProv.SlideHeld;
        bool isSprintIntent = targetMoveY >= 1.5f && _hub.InputProv.DashHeld;
    // [수정] 애니메이션 배속 제한 (블렌드 트리와 이중 가속 방지)
        // 블렌드 트리가 Speed 파라미터로 이미 질주 모션을 틀어주므로,
        // animator.speed는 0.85 ~ 1.1 사이로 꽉 묶어 '발 미끄러짐 보정'만 담당하게 함.
        // RunUpper도 동일 배속으로 동기화해서 뷰모델 팔과 월드 모델 다리 속도를 통일.
        float actualSpeed = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).magnitude;

        if (actualSpeed > 0.1f)
        {
            float clampedSpeed = Mathf.Clamp(actualSpeed / baseMoveSpeed, 0.85f, 1.1f);
            animator.speed = clampedSpeed;

            // RunUpper(뷰모델)에도 동일 배속 적용
            if (currentWeaponType == 0 && _runUpperAnimator != null && _hub.runUpper.activeInHierarchy)
                _runUpperAnimator.speed = clampedSpeed;
        }
        else
        {
            animator.speed = 1f;
            if (currentWeaponType == 0 && _runUpperAnimator != null && _hub.runUpper.activeInHierarchy)
                _runUpperAnimator.speed = 1f;
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

        if (_hub.InputProv.Weapon1Triggered && currentWeaponType != 0)
        {
            currentWeaponType = 0; // 맨손
            changed = true;
        }
        else if (_hub.InputProv.Weapon2Triggered && currentWeaponType != 1)
        {
            currentWeaponType = 1; // 총기
            changed = true;
        }

        if (changed)
        {
            animator.SetInteger(hashWeaponType, currentWeaponType);
            
            if (currentWeaponType == 0) // 맨손(Run Upper) 장착
            {
                if (_hub.viewmodelGun != null) _hub.viewmodelGun.SetActive(false);
                if (_hub.runUpper != null) _hub.runUpper.SetActive(true);
            }
            else if (currentWeaponType == 1) // 총기(Gun Upper) 장착
            {
                if (_hub.runUpper != null) _hub.runUpper.SetActive(false);
                if (_hub.viewmodelGun != null)
                {
                    _hub.viewmodelGun.SetActive(true);

                    // [수정] gunAnim.Update(0f) 제거.
                    // 이 호출은 Unity Animator의 내부 업데이트 사이클을 desync시켜
                    // SetActive 직후 Unity가 Animator 상태를 잘못된 시점에서 재평가하게 만드는
                    // 부작용이 있습니다. GunUpper의 Animator는 자체 컨트롤러(Pistol Idle)를
                    // SetActive 직후 자동으로 첫 State부터 재생하므로 수동 Update가 불필요합니다.

                    // [방어] gunController.Initialize는 총기 오브젝트가 완전히
                    // 활성화된 다음 프레임에 weaponRoot 로컬 좌표가 확정되므로 여기서 호출해도 안전.
                    // 단, Update(0f) 없이 Initialize하면 T-Pose 좌표가 저장될 수 있으므로
                    // gunController 측에서 첫 LateUpdate에서 _hipLocalPos를 갱신하도록 처리.
                    _hub.gunController.Initialize(_hub);
                }
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