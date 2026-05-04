using UnityEngine;

public class EnemyAnimatorController : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;

    [Header("IK Settings")]
    public bool useLookAtIK = true;
    public float lookAtWeight = 1.0f;
    public float bodyWeight = 0.2f;
    public float headWeight = 0.8f;
    [Tooltip("플레이어와 이 거리 이하일 때만 IK 활성화")]
    public float lookAtMaxDistance = 10f;

    // Animator 해시값
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashMoveMultiplier = Animator.StringToHash("MoveMultiplier");
    private readonly int hashAiType = Animator.StringToHash("AiType");
    private readonly int hashTriggerStumble = Animator.StringToHash("TriggerStumble");
    private readonly int hashStumbleIndex = Animator.StringToHash("StumbleIndex");
    private readonly int hashTriggerGetUp = Animator.StringToHash("TriggerGetUp");
    private readonly int hashTriggerAttack = Animator.StringToHash("TriggerAttack");
    private readonly int hashTriggerDance = Animator.StringToHash("TriggerDance");
    private readonly int hashDanceIndex = Animator.StringToHash("DanceIndex");

    [Header("Hit Animation State Names")]
    [Tooltip("Animator에 띄워둔 피격 State 이름들을 정확히 적어주세요.")]
    public string[] headHitStates = { "Hit_Head_01", "Hit_Head_02" }; // 대가리 여러 개 가능!
    public string[] bodyHitStates = { "Hit_Body" };
    public string[] leftArmHitStates = { "Hit_LeftArm" };
    public string[] rightArmHitStates = { "Hit_RightArm" };

    [Tooltip("피격 애니메이션이 들어있는 레이어 인덱스 (보통 Base가 0, HitLayer가 1)")]
    public int hitLayerIndex = 1;

    private float _currentSpeed;
    private float _speedVelocity;
    private float _currentMultiplier = 1f;
    private float _multiplierVelocity;
    private Transform _lookAtTarget;
    private Transform _overrideLookAtTarget;
    private float _overrideLookAtTimer;
    public float smoothTime = 0.1f;

    // [추가] 피격 시 IK를 잠시 풀어서 모가지가 고정되는 걸 막는 타이머
    private float _ikBlockTimer = 0f;

    public void SetLookAtTarget(Transform target)
    {
        _lookAtTarget = target;
    }

    public void ForceLookAtTarget(Transform target, float duration)
    {
        _overrideLookAtTarget = target;
        _overrideLookAtTimer = Mathf.Max(0f, duration);
    }

    public void Activate(int aiType)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        animator.SetInteger(hashAiType, aiType);

        _currentSpeed = 0f;
        _currentMultiplier = 1f;
        _ikBlockTimer = 0f;
    }

    void Update()
    {
        // 피격 타이머 감소
        if (_ikBlockTimer > 0f)
        {
            _ikBlockTimer -= Time.deltaTime;
        }
        if (_overrideLookAtTimer > 0f)
        {
            _overrideLookAtTimer -= Time.deltaTime;
            if (_overrideLookAtTimer <= 0f)
            {
                _overrideLookAtTimer = 0f;
                _overrideLookAtTarget = null;
            }
        }
    }

    public void SetSpeed(float normalizedSpeed) { /* 기존과 동일 생략 가능하지만 구조유지 */
        if (animator == null || !animator.enabled) return;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, normalizedSpeed, ref _speedVelocity, smoothTime);
        animator.SetFloat(hashSpeed, _currentSpeed);
    }

    public void SetMoveMultiplier(float multiplier) {
        if (animator == null || !animator.enabled) return;
        _currentMultiplier = Mathf.SmoothDamp(_currentMultiplier, multiplier, ref _multiplierVelocity, smoothTime);
        animator.SetFloat(hashMoveMultiplier, _currentMultiplier);
    }

    public void TriggerStumble(int variant) {
        if (animator == null || !animator.enabled) return;
        animator.SetInteger(hashStumbleIndex, variant);
        animator.SetTrigger(hashTriggerStumble);
    }

    public void TriggerGetUp() {
        if (animator == null || !animator.enabled) return;
        animator.SetTrigger(hashTriggerGetUp);
    }

    public void TriggerAttack() {
        if (animator == null || !animator.enabled) return;
        animator.SetTrigger(hashTriggerAttack);
    }

    public void TriggerDance(int variant) {
        if (animator == null || !animator.enabled) return;
        animator.SetInteger(hashDanceIndex, variant);
        animator.SetTrigger(hashTriggerDance);
    }

    // ──────────────────────────────────────────────
    //  [수정] CrossFade를 이용한 강제 전환 & IK 차단
    // ──────────────────────────────────────────────
    public void PlayHitAnimation(string boneName)
    {
        if (animator == null || !animator.enabled) return;

        string[] targetStates = bodyHitStates; // 기본값: 몸통

        if (!string.IsNullOrEmpty(boneName))
        {
            string nameLower = boneName.ToLower();

            if (nameLower.Contains("head") || nameLower.Contains("neck"))
            {
                targetStates = headHitStates;
                _ikBlockTimer = 0.5f; // [핵심] 대가리 맞으면 0.5초 동안 플레이어 안 쳐다봄 (고정 해제)
            }
            else if (nameLower.Contains("left") && (nameLower.Contains("arm") || nameLower.Contains("hand") || nameLower.Contains("shoulder")))
            {
                targetStates = leftArmHitStates;
                _ikBlockTimer = 0.3f; // 팔 맞을 때도 살짝 풀어줌
            }
            else if (nameLower.Contains("right") && (nameLower.Contains("arm") || nameLower.Contains("hand") || nameLower.Contains("shoulder")))
            {
                targetStates = rightArmHitStates;
                _ikBlockTimer = 0.3f;
            }
            else 
            {
                _ikBlockTimer = 0.3f; // 몸통
            }
        }

        // 등록된 상태가 없으면 무시
        if (targetStates == null || targetStates.Length == 0) return;

        // 랜덤하게 하나 뽑아서 강제 전환 (Has Exit Time 이딴 거 다 무시하고 0.1초 만에 덮어버림)
        string stateToPlay = targetStates[Random.Range(0, targetStates.Length)];
        animator.CrossFadeInFixedTime(stateToPlay, 0.1f, hitLayerIndex);
        
        Debug.Log($"[Hit Animation] {boneName} 피격 -> {stateToPlay} (CrossFade 강제 실행)");
    }

    public void DisableAnimator()
    {
        if (animator != null) animator.enabled = false;
    }

    void OnAnimatorIK(int layerIndex)
    {
        Transform lookAtTarget = GetCurrentLookAtTarget();
        if (!useLookAtIK || animator == null || !animator.enabled || lookAtTarget == null) return;

        if (_lookAtTarget == null) return;

        float maxDistanceSqr = lookAtMaxDistance * lookAtMaxDistance;
        float distanceSqr = (transform.position - _lookAtTarget.position).sqrMagnitude;
        if (distanceSqr > maxDistanceSqr)
        {
            animator.SetLookAtWeight(0f, bodyWeight, headWeight);
            return;
        }

        EnemyAI ai = GetComponent<EnemyAI>();
        float currentWeight = lookAtWeight;
        
        // 사망, 넘어짐, 공중 넉다운, 일어나는 중, 또는 [피격 직후]에는 IK 강제 종료!
        if (ai != null && (ai.CurrentState == EnemyAI.EnemyState.Stumbling || 
                           ai.CurrentState == EnemyAI.EnemyState.KnockedDown || 
                           ai.CurrentState == EnemyAI.EnemyState.GettingUp || 
                           ai.CurrentState == EnemyAI.EnemyState.Dead) 
            || _ikBlockTimer > 0f) 
        {
            currentWeight = 0f;
        }

        animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight);
        animator.SetLookAtPosition(lookAtTarget.position + Vector3.up * 1.5f);
    }

    private Transform GetCurrentLookAtTarget()
    {
        if (_overrideLookAtTimer > 0f && _overrideLookAtTarget != null)
        {
            return _overrideLookAtTarget;
        }

        return _lookAtTarget;
    }
}
