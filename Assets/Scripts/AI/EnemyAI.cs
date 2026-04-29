using UnityEngine;
using Pathfinding;
using Pathfinding.RVO;

/// <summary>
/// A* Pathfinding Project 기반 추적 AI.
/// 
/// [아키텍처]
/// - IAstarAI(AIPath/RichAI 등): 경로 계산 + 이동 전담
/// - AILocomotionController: 이동 제어, 정지/재개 제어
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Stumble (넘어짐)")]
    [Tooltip("현재 바라보는 방향과 목표 방향의 각도 차이(도)가 이 값을 넘으면 넘어짐 확률 체크")]
    public float stumbleAngleThreshold = 60f;
    [Range(0f, 1f)]
    [Tooltip("급격한 방향 전환 시 넘어짐 확률")]
    public float stumbleChance = 0.3f;
    [Tooltip("넘어져서 미끄러지는 시간 (초)")]
    public float stumbleDuration = 1.5f;
    [Tooltip("넘어짐 애니메이션 종류 수 (0번부터 N-1번까지 랜덤 선택)")]
    public int stumbleVariantCount = 3;
    [Tooltip("넘어짐 후 쿨다운 (연속 넘어짐 방지)")]
    public float stumbleCooldown = 4f;

    [Header("Attack (점프 허그)")]
    [Tooltip("이 거리 이내에서 공격 발동")]
    public float attackRange = 2.5f;
    [Tooltip("공격 쿨다운")]
    public float attackCooldown = 3f;

    [Header("Path")]
    [Tooltip("목적지에 추가되는 랜덤 오프셋 (경로 다양성)")]
    public float pathRandomOffset = 2f;
    [Tooltip("목적지 갱신 간격 (초)")]
    public float destinationUpdateInterval = 0.5f;

    [Header("AI Type")]
    [Tooltip("0: 인간형 이족보행, 1: 사족보행")]
    public int aiType = 0;

    // ── 컴포넌트 ──
    private IAstarAI _ai;
    private RVOController _rvo;
    private AILocomotionController _locomotion;
    private EnemyAnimatorController _animController;
    private EnemyRagdollHandler _ragdollHandler;
    private Transform _playerTransform;

    // ── 내부 상태 ──
    private float _stumbleTimer;
    private float _stumbleCooldownTimer;
    private float _attackCooldownTimer;
    private float _attackStateTimer; // 어택 상태 무한 대기 방지용(Failsafe)
    private float _destinationTimer;
    private Vector3 _currentRandomOffset;
    private bool _isInitialized;

    /// <summary>현재 AI 상태</summary>
    public EnemyState CurrentState { get; private set; } = EnemyState.Inactive;

    /// <summary>현재 이동 속도 벡터 (레그돌 관성 전달용)</summary>
    public Vector3 CurrentVelocity => _locomotion != null ? _locomotion.CurrentVelocity : Vector3.zero;

    /// <summary>현재 수평 속도 스칼라 (애니메이션용)</summary>
    public float CurrentSpeed => _locomotion != null ? _locomotion.CurrentSpeed : 0f;

    public enum EnemyState
    {
        Inactive,
        Chasing,
        Stumbling,   // 넘어져서 미끄러지는 중
        GettingUp,   // 일어나는 중 (애니메이션 대기)
        Attacking,   // 점프 허그
        Dead
    }

    // ══════════════════════════════════════════════
    //  초기화 / 활성화 / 비활성화
    // ══════════════════════════════════════════════

    public void Initialize(Transform playerTransform)
    {
        _playerTransform = playerTransform;

        if (_ai == null) _ai = GetComponent<IAstarAI>();
        if (_rvo == null) _rvo = GetComponent<RVOController>();
        if (_locomotion == null && _ai != null)
        {
            _locomotion = new AILocomotionController(_ai, _rvo, transform);
        }
        if (_animController == null) _animController = GetComponent<EnemyAnimatorController>();
        if (_ragdollHandler == null) _ragdollHandler = GetComponent<EnemyRagdollHandler>();

        if (_animController != null) _animController.SetLookAtTarget(playerTransform);

        if (_ai == null) Debug.LogError($"[EnemyAI] IAstarAI(AIPath/RichAI) 없음! ({gameObject.name})");

        _isInitialized = true;
    }

    public void Activate(Vector3 spawnPosition)
    {
        if (!_isInitialized) { Debug.LogError("[EnemyAI] Initialize() 미호출!"); return; }

        gameObject.SetActive(true);
        transform.position = spawnPosition;

        if (_locomotion != null)
        {
            _locomotion.Teleport(spawnPosition, true);
            _locomotion.ResumeMovement();
        }

        CurrentState = EnemyState.Chasing;
        _stumbleCooldownTimer = stumbleCooldown; // 스폰 직후 넘어짐 방지
        _attackCooldownTimer = 1f; // 스폰 직후 즉시 공격 방지
        _destinationTimer = 0f;
        _currentRandomOffset = GenerateRandomOffset();

        if (_animController != null) _animController.Activate(aiType);
        if (_ragdollHandler != null) _ragdollHandler.ResetRagdoll();
    }

    public void Disable()
    {
        CurrentState = EnemyState.Dead;
        if (_locomotion != null) _locomotion.PauseMovement();
    }

    public void Deactivate()
    {
        CurrentState = EnemyState.Inactive;
        if (_locomotion != null) _locomotion.PauseMovement();
        gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════
    //  Update
    // ══════════════════════════════════════════════

    void Update()
    {
        if (CurrentState == EnemyState.Inactive || CurrentState == EnemyState.Dead) return;
        if (_playerTransform == null) return;

        // 쿨다운 감소
        if (_stumbleCooldownTimer > 0f) _stumbleCooldownTimer -= Time.deltaTime;
        if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;

        switch (CurrentState)
        {
            case EnemyState.Chasing:
                UpdateChasing();
                TryTriggerStumble();
                break;
            case EnemyState.Stumbling:
                UpdateStumbling();
                break;
            case EnemyState.GettingUp:
                // 애니메이션 이벤트를 대기
                break;
            case EnemyState.Attacking:
                // 애니메이션 이벤트(OnAttackAnimationEnd) 대기. 
                // 단, 애니메이션 이벤트가 누락됐을 경우를 대비한 자동 복귀(Failsafe)
                _attackStateTimer -= Time.deltaTime;
                if (_attackStateTimer <= 0f)
                {
                    OnAttackAnimationEnd();
                }
                break;
        }

        // 애니메이션 동기화
        if (_animController != null)
        {
            float speed = CurrentSpeed;
            
            // 블렌드 트리 이동 전환용 (Idle -> Run 판단)
            _animController.SetSpeed(speed);
            
            // 애니메이션 배속 제한 (선풍기 현상 방지)
            // 기본 달리기 모션(약 8m/s 기준)을 기준으로 배속하되 최대 3배로 묶어둠
            float animMultiplier = Mathf.Clamp(speed / 8f, 0.8f, 1.8f);
            _animController.SetMoveMultiplier(animMultiplier);
        }
    }

    // ══════════════════════════════════════════════
    //  Chasing
    // ══════════════════════════════════════════════

    private void UpdateChasing()
    {
        // 목적지 갱신
        _destinationTimer -= Time.deltaTime;
        if (_destinationTimer <= 0f)
        {
            _destinationTimer = destinationUpdateInterval;
            _currentRandomOffset = GenerateRandomOffset();
            UpdateDestination();
        }

        // 공격 판정: 플레이어와 충분히 가까우면 점프허그
        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        if (dist <= attackRange && _attackCooldownTimer <= 0f)
        {
            TriggerAttack();
        }
    }

    private void TryTriggerStumble()
    {
        if (_stumbleCooldownTimer > 0f) return;
        if (_locomotion == null) return;

        Vector3 desiredDir = _locomotion.GetDesiredDirection();
        if (desiredDir.sqrMagnitude < 0.01f) return;

        float turnAngle = Vector3.Angle(transform.forward, desiredDir);
        if (turnAngle < stumbleAngleThreshold) return;

        if (Random.value <= stumbleChance)
        {
            TriggerStumble();
        }
    }

    // ══════════════════════════════════════════════
    //  Stumbling → GettingUp
    // ══════════════════════════════════════════════

    private void TriggerStumble()
    {
        CurrentState = EnemyState.Stumbling;
        _stumbleTimer = stumbleDuration;
        if (_locomotion != null) _locomotion.PauseMovement();

        // 랜덤 넘어짐 변형 선택 (0 ~ stumbleVariantCount-1)
        int variant = Random.Range(0, Mathf.Max(1, stumbleVariantCount));
        if (_animController != null) _animController.TriggerStumble(variant);
    }

    private void UpdateStumbling()
    {
        _stumbleTimer -= Time.deltaTime;
        if (_stumbleTimer <= 0f)
        {
            StartGettingUp();
        }
    }

    private void StartGettingUp()
    {
        CurrentState = EnemyState.GettingUp;

        // 완전 정지
        if (_locomotion != null) _locomotion.PauseMovement();

        if (_animController != null) _animController.TriggerGetUp();
    }

    /// <summary>
    /// [애니메이션 이벤트] GetUp 클립 마지막 프레임에서 호출.
    /// Unity 에디터: GetUp 클립 → Animation 창 → Add Event → Function: OnGetUpAnimationEnd
    /// </summary>
    public void OnGetUpAnimationEnd()
    {
        if (CurrentState != EnemyState.GettingUp) return;
        CurrentState = EnemyState.Chasing;
        _stumbleCooldownTimer = stumbleCooldown;
        if (_locomotion != null) _locomotion.ResumeMovement();
        UpdateDestination();
    }

    // ══════════════════════════════════════════════
    //  Attack (점프 허그)
    // ══════════════════════════════════════════════

    private void TriggerAttack()
    {
        CurrentState = EnemyState.Attacking;
        _attackCooldownTimer = attackCooldown;
        _attackStateTimer = 1.5f; // 1.5초 후 강제 추적 재개 (애니메이션 이벤트 누락 대비)

        // 현재 이동 방향 그대로 돌진 (이동 멈추지 않음)
        if (_animController != null) _animController.TriggerAttack();
    }

    /// <summary>
    /// [애니메이션 이벤트] 점프허그 클립 끝에서 호출.
    /// Unity 에디터: Attack 클립 → Add Event → Function: OnAttackAnimationEnd
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        if (CurrentState != EnemyState.Attacking) return;
        CurrentState = EnemyState.Chasing;
        UpdateDestination();
    }

    // ══════════════════════════════════════════════
    //  유틸리티
    // ══════════════════════════════════════════════

    private void UpdateDestination()
    {
        if (_playerTransform == null || _locomotion == null) return;
        _locomotion.SetDestination(_playerTransform.position + _currentRandomOffset);
    }

    private Vector3 GenerateRandomOffset()
    {
        Vector3 offset = Random.insideUnitSphere * pathRandomOffset;
        offset.y = 0f;
        return offset;
    }
}
