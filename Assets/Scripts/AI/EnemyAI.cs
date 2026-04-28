using UnityEngine;
using Pathfinding;
using Pathfinding.RVO;

/// <summary>
/// 빙판 추적 AI — 심플 버전.
/// 
/// [아키텍처]
/// - FollowerEntity: 경로 계산만 담당 (isStopped = true → 자체 이동 안 함)
/// - Rigidbody + AddForce: 빙판 물리를 물리 엔진이 직접 처리
/// - Rigidbody.linearDamping: 이 값이 마찰력 → 낮으면 빙판, 높으면 일반 땅
/// 
/// [파라미터 3개만 조절하면 됨]
/// - acceleration: 얼마나 세게 밀어서 가속하는가
/// - maxSpeed: 최대 속도 제한
/// - Rigidbody.linearDamping (Inspector): 빙판 미끄러움 강도
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("가속력 (AddForce 강도). 높으면 빠르게 방향 전환, 낮으면 빙판")]
    public float acceleration = 15f;
    [Tooltip("최대 이동 속도")]
    public float maxSpeed = 14f;
    [Tooltip("회전 속도. 높으면 플레이어를 잘 쫓아감, 낮으면 코너에서 돌지 못함")]
    public float turnSpeed = 8f;

    [Header("Stumble (넘어짐)")]
    [Tooltip("가려는 방향과 실제 미끄러지는 방향의 각도가 이만큼 벌어지면 넘어짐 판정 시작 (도)")]
    public float stumbleAngleThreshold = 100f;
    [Tooltip("이 속도 이상에서만 넘어짐 판정")]
    public float stumbleSpeedThreshold = 10f;
    [Tooltip("넘어짐 판정 조건이 이 시간(초) 동안 유지되면 실제로 넘어짐. 낮을수록 잘 넘어짐.")]
    public float stumbleTriggerTime = 0.5f;
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
    [HideInInspector] public Rigidbody rb;
    private IAstarAI _ai;
    private RVOController _rvo;
    private EnemyAnimatorController _animController;
    private EnemyRagdollHandler _ragdollHandler;
    private Transform _playerTransform;

    // ── 내부 상태 ──
    private float _stumbleAccumulatorTimer;
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
    public Vector3 CurrentVelocity => rb != null ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z) : Vector3.zero;

    /// <summary>현재 수평 속도 스칼라 (애니메이션용)</summary>
    public float CurrentSpeed => CurrentVelocity.magnitude;

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

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (_ai == null) _ai = GetComponent<IAstarAI>();
        if (_rvo == null) _rvo = GetComponent<RVOController>();
        if (_animController == null) _animController = GetComponent<EnemyAnimatorController>();
        if (_ragdollHandler == null) _ragdollHandler = GetComponent<EnemyRagdollHandler>();

        if (_animController != null) _animController.SetLookAtTarget(playerTransform);

        if (_ai == null) Debug.LogError($"[EnemyAI] IAstarAI(FollowerEntity) 없음! ({gameObject.name})");
        if (rb == null) Debug.LogError($"[EnemyAI] Rigidbody 없음! ({gameObject.name})");

        _isInitialized = true;
    }

    public void Activate(Vector3 spawnPosition)
    {
        if (!_isInitialized) { Debug.LogError("[EnemyAI] Initialize() 미호출!"); return; }

        gameObject.SetActive(true);
        transform.position = spawnPosition;

        if (_ai != null)
        {
            _ai.Teleport(spawnPosition, true);
            _ai.canSearch = true;
            // ★ 핵심: FollowerEntity는 경로만 계산. 자체 이동은 하지 않음.
            // steeringTarget에서 방향만 읽고, 실제 이동은 Rigidbody AddForce로 처리.
            _ai.isStopped = true;
        }

        if (_rvo != null) _rvo.locked = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
        if (_ai != null) { _ai.canSearch = false; _ai.isStopped = true; }
        if (_rvo != null) _rvo.locked = true;
    }

    public void Deactivate()
    {
        CurrentState = EnemyState.Inactive;
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════
    //  Update / FixedUpdate
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

    void FixedUpdate()
    {
        if (CurrentState != EnemyState.Chasing) return;
        if (_playerTransform == null) return;

        ApplyIceMovement();
    }

    // ══════════════════════════════════════════════
    //  Chasing — 빙판 물리 (AddForce)
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

    private void ApplyIceMovement()
    {
        if (rb == null) return;

        // 1. A* steeringTarget → 가야 할 방향
        Vector3 desiredDir = Vector3.zero;
        if (_ai != null)
        {
            Vector3 toTarget = _ai.steeringTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
                desiredDir = toTarget.normalized;
        }

        // 2. 빙판 물리: AddForce
        if (desiredDir.sqrMagnitude > 0.1f)
        {
            rb.AddForce(desiredDir * acceleration, ForceMode.Acceleration);
        }

        // 3. 최대 속도 제한
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speed = horizontalVel.magnitude;
        if (speed > maxSpeed)
        {
            Vector3 clampedVel = horizontalVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(clampedVel.x, rb.linearVelocity.y, clampedVel.z);
            speed = maxSpeed;
        }

        // 4. 회전
        if (horizontalVel.sqrMagnitude > 1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalVel.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }

        // 5. 넘어짐 판정 (각도 + 속도 + 유지 시간)
        // 실제 미끄러지는 방향(horizontalVel)과 가야 할 방향(desiredDir)의 각도를 비교
        if (speed > stumbleSpeedThreshold && desiredDir.sqrMagnitude > 0.1f && _stumbleCooldownTimer <= 0f)
        {
            float slipAngle = Vector3.Angle(desiredDir, horizontalVel.normalized);
            if (slipAngle > stumbleAngleThreshold)
            {
                _stumbleAccumulatorTimer += Time.fixedDeltaTime;
                if (_stumbleAccumulatorTimer > stumbleTriggerTime)
                {
                    TriggerStumble();
                    _stumbleAccumulatorTimer = 0f;
                }
            }
            else
            {
                // 각도가 안정권에 들어오면 타이머 서서히 회복
                _stumbleAccumulatorTimer = Mathf.Max(0f, _stumbleAccumulatorTimer - Time.fixedDeltaTime);
            }
        }
        else
        {
            _stumbleAccumulatorTimer = Mathf.Max(0f, _stumbleAccumulatorTimer - Time.fixedDeltaTime);
        }
    }

    // ══════════════════════════════════════════════
    //  Stumbling → GettingUp
    // ══════════════════════════════════════════════

    private void TriggerStumble()
    {
        CurrentState = EnemyState.Stumbling;
        _stumbleTimer = stumbleDuration;

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
        if (rb != null)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

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
        if (_playerTransform == null || _ai == null) return;
        _ai.destination = _playerTransform.position + _currentRandomOffset;
    }

    private Vector3 GenerateRandomOffset()
    {
        Vector3 offset = Random.insideUnitSphere * pathRandomOffset;
        offset.y = 0f;
        return offset;
    }
}
