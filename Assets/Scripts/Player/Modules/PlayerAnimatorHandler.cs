using UnityEngine;
using System;

[Serializable]
public sealed class PlayerAnimatorHandler
{
    [Header("World Models (Inspector에서 할당)")]
    [Tooltip("여기에 부모(다리)와 자식(상체/전체) 등 동일한 컨트롤러를 쓰는 월드 모델 애니메이터들을 배열로 넣으세요.")]
    public Animator[] bodyAnimators; 

    [Header("Animation Smooth Damp")]
    [Tooltip("입력 및 이동 애니메이션 보간의 쫀득함을 조정합니다 (값이 클수록 부드럽지만 느리게 반응).")]
    public float moveSmoothTime = 0.1f;

    [Header("Weapon Settings")]
    [Tooltip("현재 활성화된 무기 타입 (0: 맨손, 1: 권총)")]
    public int currentWeaponType = 0;

    [Header("Melee Combat (Kick)")]
    [Tooltip("발차기 타격 범위(거리)")]
    public float kickRange = 2.5f;
    [Tooltip("발차기 판정 반경(두께)")]
    public float kickRadius = 0.8f;
    [Tooltip("발차기 타격력 (높을수록 공처럼 멀리 날아감)")]
    public float kickForce = 60f;
    [Tooltip("연속 발차기 쿨타임")]
    public float kickCooldown = 0.6f;
    
    [Header("Animation Sync")]
    public float baseMoveSpeed = 4f; // 걷기 애니메이션이 정상적으로 보일 때의 실제 이동 속도 기준점
    // 모노비헤비어가 아니므로 직접 참조할 수 없습니다. 대신 허브를 통해 가져옵니다.
    // [Header("Viewmodel")] ... (삭제됨, PlayerController에서 관리)
    [Header("World Model Hiding")]
    [Tooltip("플레이어의 진짜 몸뚱이 렌더러들 (SkinMeshRenderer 등)")]
    public SkinnedMeshRenderer[] worldModelRenderers;
    private PlayerController _hub;
    
    public float CurrentMoveMultiplier { get; private set; } = 1f;

    // [Dual-Model] RunUpper 뷰모델의 Animator — Initialize 시 Hub에서 캐싱. (본체와 완전히 다른 컨트롤러 사용)
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
    private readonly int hashMoveMultiplier = Animator.StringToHash("MoveMultiplier");
    private readonly int hashIsCrouching = Animator.StringToHash("IsCrouching");
    private readonly int hashWeaponType = Animator.StringToHash("WeaponType");
    
    private readonly int hashTriggerKick = Animator.StringToHash("TriggerKick");
    private readonly int hashTriggerJump = Animator.StringToHash("TriggerJump");
    private readonly int hashTriggerStop = Animator.StringToHash("TriggerStop");
    private readonly int hashIsSliding = Animator.StringToHash("IsSliding");
    private readonly int hashTriggerSlideEnter = Animator.StringToHash("Slide_Enter");
    private readonly int hashKickIndex = Animator.StringToHash("KickIndex");

    private bool _wasSliding;
    private bool _wasCrouching;
    private float _nextKickTime;
    
    public void Initialize(PlayerController hub)
    {
        _hub = hub;

        // [Dual-Model] RunUpper Animator는 PlayerController.Awake에서 캐싱한 값을 참조
        _runUpperAnimator = _hub.runUpperAnimator;
        if (_runUpperAnimator == null && _hub.runUpper != null)
            Debug.LogWarning("[PlayerAnimatorHandler] runUpperAnimator가 null입니다. PlayerController Awake 초기화 순서를 확인하세요.");

        if (bodyAnimators != null && bodyAnimators.Length > 0)
        {
            // 초기 무기 타입 세팅
            SyncSetInteger(hashWeaponType, currentWeaponType);
            
            // 초기 뷰모델 활성 상태 세팅
            if (_hub.viewmodelGun != null) _hub.viewmodelGun.SetActive(currentWeaponType == 1);
            if (_hub.runUpper != null) _hub.runUpper.SetActive(currentWeaponType == 0);
        }
        else
        {
            Debug.LogWarning("[PlayerAnimatorHandler] World Model Animators array is empty!");
        }
    }

    public void UpdateModule()
    {
        if (bodyAnimators == null || bodyAnimators.Length == 0) return;

        HandleMovementAnimation();
        HandleWeaponSwitch();
        HandleActionTriggers();
    }

    // [추가] 애니메이터가 스케일을 1로 되돌리는 것을 막기 위한 LateUpdate용 함수
    public void LateUpdateModule()
    {
        if (bodyAnimators == null || bodyAnimators.Length == 0) return;

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
        Vector2 input = _hub.InputProv.MoveInput;
        bool isMoving = input.sqrMagnitude > 0.01f;
        
        // 1. 파쿠르 중이거나 공중에 있을 경우 (WallRun, Vault, Slide) 강제로 팔/이동 인풋 차단
        bool isParkour = _hub.slider.IsSliding || _hub.vault.IsVaulting;

        float targetMoveX = (isMoving && !isParkour) ? Mathf.Clamp(input.x, -1f, 1f) : 0f;
        float targetMoveY = (isMoving && !isParkour) ? Mathf.Clamp(input.y, -1f, 1f) : 0f;

        float actualSpeedXZ = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).magnitude;

        if (isMoving && !isParkour)
        {
            // 2. 제자리 달리기 및 공중 달리기 봉쇄: Shift를 눌렀어도 지면에 닿아있고 실제 걷기 속도 이상으로 나아갈 때만 달리기로 인정
            if (targetMoveY > 0f && _hub.InputProv.DashHeld && actualSpeedXZ > _hub.movement.walkSpeed * 0.5f && _hub.movement.IsGrounded)
            {
                targetMoveY = 2.0f;
            }
        }

        // 3. 파쿠르(Slide, Vault) 진행 중엔 상체 흔들기 레이어(Layer 1) 가중치를 0으로 부드럽게 감소
        foreach (var anim in bodyAnimators)
        {
            if (anim != null && anim.layerCount > 1)
            {
                float currentWeight = anim.GetLayerWeight(1);
                float targetWeight = isParkour ? 0f : 1f;
                anim.SetLayerWeight(1, Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * 6f));
            }
        }

        // RunUpper 뷰모델의 경우 자체적인 레이어가 존재한다면 흔들기 강도 감소
        if (_runUpperAnimator != null && _runUpperAnimator.layerCount > 1)
        {
            float currentViewWeight = _runUpperAnimator.GetLayerWeight(1);
            float targetViewWeight = isParkour ? 0f : 1f;
            _runUpperAnimator.SetLayerWeight(1, Mathf.MoveTowards(currentViewWeight, targetViewWeight, Time.deltaTime * 6f));
        }

        if (_previousTargetMoveY > 1.5f && targetMoveY < 0.1f)
        {
            SyncSetTrigger(hashTriggerStop);
        }
        _previousTargetMoveY = targetMoveY;

        _currentMoveX = Mathf.SmoothDamp(_currentMoveX, targetMoveX, ref _moveXVelocity, moveSmoothTime);
        _currentMoveY = Mathf.SmoothDamp(_currentMoveY, targetMoveY, ref _moveYVelocity, moveSmoothTime);

        float targetSpeed = new Vector2(targetMoveX, targetMoveY).magnitude;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, moveSmoothTime);

        // 오직 배열에 들어있는 부모/자식 모델에게만 파라미터를 쏩니다.
        SyncSetFloat(hashSpeed, _currentSpeed);
        SyncSetFloat(hashMoveX, _currentMoveX);
        SyncSetFloat(hashMoveY, _currentMoveY);

        // 런어퍼(뷰모델)는 철저하게 분리해서 따로 속도만 호출!
        if (currentWeaponType == 0 && _runUpperAnimator != null && _hub.runUpper.activeInHierarchy)
        {
            _runUpperAnimator.SetFloat(hashSpeed, _currentSpeed);
            _runUpperAnimator.SetFloat(hashMoveX, _currentMoveX);
            _runUpperAnimator.SetFloat(hashMoveY, _currentMoveY);
        }

        bool crouchInput = _hub.InputProv.CrouchHeld || _hub.InputProv.SlideHeld;
        bool isSprintIntent = targetMoveY >= 1.5f && _hub.InputProv.DashHeld;
        
        // 애니메이션 배속 퍼포먼스 (스피드 Multiplier 파라미터로 이관)
        float actualSpeed = new Vector3(_hub.Rb.linearVelocity.x, 0, _hub.Rb.linearVelocity.z).magnitude;

        // 1. 글로벌 배속(anim.speed)은 무조건 1f로 고정 (펀치, 파쿠르 애니메이션 속도 보존)
        foreach (var anim in bodyAnimators) { if (anim != null) anim.speed = 1f; }
        if (currentWeaponType == 0 && _runUpperAnimator != null && _hub.runUpper.activeInHierarchy)
            _runUpperAnimator.speed = 1f;

        // 2. 실제 속도에 비례한 클램핑 (다리의 시각적 발작 방지 마지노선 1.6배)
        float clampedSpeed = (actualSpeed > 0.1f) ? Mathf.Clamp(actualSpeed / baseMoveSpeed, 0.8f, 1.6f) : 1f;
        CurrentMoveMultiplier = clampedSpeed;

        // 3. Blend Tree의 Multiplier로 사용될 파라미터 전송
        SyncSetFloat(hashMoveMultiplier, clampedSpeed);
        if (currentWeaponType == 0 && _runUpperAnimator != null && _hub.runUpper.activeInHierarchy)
        {
            _runUpperAnimator.SetFloat(hashMoveMultiplier, clampedSpeed);
        }

        if (_wasSliding)
        {
            if (!_hub.slider.IsSliding)
            {
                SyncSetBool(hashIsSliding, false);
                _wasSliding = false;
                
                if (_hub.slider.IsCrouching) 
                {
                    SyncSetBool(hashIsCrouching, true);
                    _wasCrouching = true;
                }
            }
        }
        else if (_wasCrouching)
        {
            if (!crouchInput || isSprintIntent)
            {
                SyncSetBool(hashIsCrouching, false);
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
                    SyncSetTrigger(hashTriggerSlideEnter);
                    SyncSetBool(hashIsSliding, true);
                    _wasSliding = true;
                }
                else
                {
                    SyncSetBool(hashIsCrouching, true);
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
            ApplyWeaponChange();
        }
    }

    public void ForceSetWeaponType(int type)
    {
        if (currentWeaponType == type) return;
        currentWeaponType = type;
        ApplyWeaponChange();
    }

    private void ApplyWeaponChange()
    {
        SyncSetInteger(hashWeaponType, currentWeaponType);
        
        if (currentWeaponType == 0) // 맨손(Run Upper) 장착
        {
            // 총→맨손: Sway 잔존값 초기화
            if (_hub.gunController != null) _hub.gunController.ResetSway();
            if (_hub.viewmodelGun != null) _hub.viewmodelGun.SetActive(false);
            if (_hub.runUpper != null) _hub.runUpper.SetActive(true);
        }
        else if (currentWeaponType == 1) // 총기(Gun Upper) 장착
        {
            if (_hub.runUpper != null) _hub.runUpper.SetActive(false);
            if (_hub.viewmodelGun != null)
            {
                _hub.viewmodelGun.SetActive(true);
                if (_hub.gunController != null)
                {
                    _hub.gunController.Initialize(_hub);
                    _hub.gunController.ResetSway(); // 맨손→총: 깨끗한 초기 상태 보장
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
                if (Time.time >= _nextKickTime)
                {
                    int kickIndex = UnityEngine.Random.Range(0, 2);
                    SyncSetInteger(hashKickIndex, kickIndex);
                    SyncSetTrigger(hashTriggerKick);
                    
                    _nextKickTime = Time.time + kickCooldown; 
                    
                    // 실제 타격 로직은 더 이상 여기서 즉시 발생하지 않습니다.
                    // 애니메이션 에디터에서 발이 뻗어지는 프레임에 OnKickHitEvent() 이벤트를 달아주세요.
                }
            }
        }
    }

    /// <summary>
    /// [Animation Event] 발차기 애니메이션에서 발이 뻗어지는 정확한 타이밍에 호출할 메서드.
    /// 애니메이션 모델의 AnimationEventForwarder 등을 통해 호출되어야 합니다.
    /// </summary>
    public void OnKickHitEvent()
    {
        Vector3 origin = _hub.transform.position + Vector3.up;
        if (Physics.SphereCast(origin, kickRadius, _hub.transform.forward, out RaycastHit hit, kickRange))
        {
            var health = hit.collider.GetComponentInParent<EnemyHealth>();
            var ragdoll = hit.collider.GetComponentInParent<EnemyRagdollHandler>();

            if (ragdoll != null)
            {
                Rigidbody hitBone = hit.collider.attachedRigidbody;
                bool isDead = false;

                // 발차기 데미지 적용 (예: 20)
                if (health != null)
                {
                    isDead = !health.TakeHit(20f, hit.point, _hub.transform.forward, hitBone);
                }

                // 사망 시에만 레그돌이 되도록 분기 처리 및 최신 API 적용
                if (isDead)
                {
                    ragdoll.ApplyDeathRagdoll(hit.point, _hub.transform.forward, kickForce, hitBone);
                }
                
                // 타격 성공 시 카메라 킥 주스 발동
                if (_hub.juiceController != null)
                {
                    _hub.juiceController.TriggerKickJuice();
                }
            }
        }
    }

    public void TriggerJump()
    {
        SyncSetTrigger(hashTriggerJump);
    }

    // ── 몸체 다중 모델(부모/자식 World Model) Animation Sync Helpers ──
    private void SyncSetTrigger(int hash)
    {
        if (bodyAnimators == null) return;
        foreach (var anim in bodyAnimators)
        {
            if (anim != null && anim.isActiveAndEnabled) anim.SetTrigger(hash);
        }
    }
    
    private void SyncSetBool(int hash, bool value)
    {
        if (bodyAnimators == null) return;
        foreach (var anim in bodyAnimators)
        {
            if (anim != null && anim.isActiveAndEnabled) anim.SetBool(hash, value);
        }
    }

    private void SyncSetInteger(int hash, int value)
    {
        if (bodyAnimators == null) return;
        foreach (var anim in bodyAnimators)
        {
            if (anim != null && anim.isActiveAndEnabled) anim.SetInteger(hash, value);
        }
    }

    private void SyncSetFloat(int hash, float value)
    {
        if (bodyAnimators == null) return;
        foreach (var anim in bodyAnimators)
        {
            if (anim != null && anim.isActiveAndEnabled) anim.SetFloat(hash, value);
        }
    }
}