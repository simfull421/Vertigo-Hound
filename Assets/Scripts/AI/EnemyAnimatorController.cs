using UnityEngine;

/// <summary>
/// AI의 Animator 파라미터를 동기화하는 컨트롤러.
/// 
/// [Animator Controller 구조 (Unity 에디터에서 수동 생성)]
/// - Locomotion (BlendTree): Speed 파라미터로 Idle/Walk/Run 블렌딩
/// - Stumble 상태: TriggerStumble로 진입
/// - GetUp 상태: TriggerGetUp으로 진입, 클립 끝에 OnGetUpAnimationEnd 이벤트
/// - Locomotion 복귀: GetUp 클립 종료 시
/// 
/// [파라미터 목록]
/// - Speed (Float): 블렌드 트리 구동 (0~1 정규화)
/// - MoveMultiplier (Float): 애니메이션 재생 속도 배율
/// - AiType (Int): AI 타입 (0: 인간형, 1: 사족보행)
/// - TriggerStumble (Trigger): 넘어짐
/// - TriggerGetUp (Trigger): 일어남
/// 
/// [IK]
/// - Head LookAt: 플레이어를 향해 고개를 돌림 (IK Pass 필수)
/// 
/// [사망 처리]
/// 사망 시 애니메이션 없이 바로 레그돌로 전환됩니다 (DisableAnimator 호출).
/// </summary>
public class EnemyAnimatorController : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("AI 모델에 붙어있는 Animator")]
    public Animator animator;

    [Header("IK Settings")]
    [Tooltip("플레이어를 향해 고개를 돌리는 LookAt IK 활성화")]
    public bool useLookAtIK = true;
    [Tooltip("전체 LookAt 가중치 (0: 끔, 1: 완전)")]
    public float lookAtWeight = 1.0f;
    [Tooltip("몸통 회전 가중치 (낮으면 몸은 거의 안 돌아감)")]
    public float bodyWeight = 0.2f;
    [Tooltip("머리 회전 가중치 (높으면 고개가 많이 돌아감)")]
    public float headWeight = 0.8f;

    // Animator 해시값 (문자열보다 조회 속도가 빠름)
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashMoveMultiplier = Animator.StringToHash("MoveMultiplier");
    private readonly int hashAiType = Animator.StringToHash("AiType");
    private readonly int hashTriggerStumble = Animator.StringToHash("TriggerStumble");
    private readonly int hashStumbleIndex = Animator.StringToHash("StumbleIndex");
    private readonly int hashTriggerGetUp = Animator.StringToHash("TriggerGetUp");
    private readonly int hashTriggerAttack = Animator.StringToHash("TriggerAttack");

    // SmoothDamp용 변수
    private float _currentSpeed;
    private float _speedVelocity;
    private float _currentMultiplier = 1f;
    private float _multiplierVelocity;

    // IK 타겟 (플레이어)
    private Transform _lookAtTarget;

    [Tooltip("애니메이션 파라미터 보간 시간 (SmoothDamp)")]
    public float smoothTime = 0.1f;

    /// <summary>
    /// IK LookAt 대상 설정. EnemyAI.Initialize()에서 호출.
    /// </summary>
    public void SetLookAtTarget(Transform target)
    {
        _lookAtTarget = target;
    }

    /// <summary>
    /// 풀에서 활성화될 때 호출. Animator 리셋 및 타입 설정.
    /// </summary>
    public void Activate(int aiType)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[EnemyAnimatorController] Animator를 찾을 수 없습니다!");
                return;
            }
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);

        // AI 타입 설정 (인간형/사족보행)
        animator.SetInteger(hashAiType, aiType);

        _currentSpeed = 0f;
        _currentMultiplier = 1f;
    }

    /// <summary>
    /// 속도 파라미터 설정 (0~1 정규화된 값).
    /// </summary>
    public void SetSpeed(float normalizedSpeed)
    {
        if (animator == null || !animator.enabled) return;

        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, normalizedSpeed, ref _speedVelocity, smoothTime);
        animator.SetFloat(hashSpeed, _currentSpeed);
    }

    /// <summary>
    /// 애니메이션 배속 설정.
    /// </summary>
    public void SetMoveMultiplier(float multiplier)
    {
        if (animator == null || !animator.enabled) return;

        _currentMultiplier = Mathf.SmoothDamp(_currentMultiplier, multiplier, ref _multiplierVelocity, smoothTime);
        animator.SetFloat(hashMoveMultiplier, _currentMultiplier);
    }



    /// <summary>
    /// 넘어짐 애니메이션 트리거.
    /// variant: 0~N-1 랜덤 인덱스. Animator에서 StumbleIndex로 분기하여
    /// 각각 다른 넘어짐 클립을 재생합니다.
    /// 짧은 넘어짐은 바로 Locomotion으로, 긴 넘어짐은 GetUp을 거칩니다.
    /// </summary>
    public void TriggerStumble(int variant)
    {
        if (animator == null || !animator.enabled) return;
        animator.SetInteger(hashStumbleIndex, variant);
        animator.SetTrigger(hashTriggerStumble);
    }

    /// <summary>
    /// 일어남 애니메이션 트리거.
    /// Stumble 미끄러짐이 끝난 후 EnemyAI.StartGettingUp()에서 호출.
    /// </summary>
    public void TriggerGetUp()
    {
        if (animator == null || !animator.enabled) return;
        animator.SetTrigger(hashTriggerGetUp);
    }

    /// <summary>
    /// 점프 허그 공격 애니메이션 트리거.
    /// </summary>
    public void TriggerAttack()
    {
        if (animator == null || !animator.enabled) return;
        animator.SetTrigger(hashTriggerAttack);
    }

    /// <summary>
    /// Animator를 완전히 비활성화 (레그돌 전환 시 호출).
    /// </summary>
    public void DisableAnimator()
    {
        if (animator != null) animator.enabled = false;
    }

    // ──────────────────────────────────────────────
    //  IK: 플레이어를 향해 고개 돌림
    //  [필수] Animator Controller의 Base Layer → 톱니바퀴 → IK Pass 체크
    // ──────────────────────────────────────────────
    void OnAnimatorIK(int layerIndex)
    {
        if (!useLookAtIK || animator == null || !animator.enabled) return;
        if (_lookAtTarget == null) return;

        // 넘어지거나 일어나는 중에는 LookAt 가중치를 줄임
        // (완전히 끄면 부자연스러우므로 살짝 남겨둠)
        EnemyAI ai = GetComponent<EnemyAI>();
        float currentWeight = lookAtWeight;
        if (ai != null && (ai.CurrentState == EnemyAI.EnemyState.Stumbling
                        || ai.CurrentState == EnemyAI.EnemyState.Dead))
        {
            currentWeight = 0f;
        }

        animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight);
        animator.SetLookAtPosition(_lookAtTarget.position + Vector3.up * 1.5f);
    }
}
