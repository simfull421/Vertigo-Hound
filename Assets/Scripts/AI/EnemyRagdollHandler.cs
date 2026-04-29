using UnityEngine;
using System.Collections;

/// <summary>
/// AI의 레그돌 전환 및 피격 처리를 담당합니다.
/// 
/// [설계 핵심]
/// - 피격 부위에 즉시 힘(Impulse)을 적용
/// - 사망 시 이동/애니메이션 비활성화 후 물리 관성만 전달
/// 
/// [풀 반납 사이클]
/// ApplyHit() → 일정 시간 대기 → ReturnToPool 이벤트 발동
/// </summary>
public class EnemyRagdollHandler : MonoBehaviour
{
    public Animator animator;
    [Header("Ragdoll Settings")]
    [Tooltip("레그돌 유지 시간 (이후 풀 반납)")]
    public float ragdollDuration = 3f;

    [Header("Ragdoll Bodies (자동 수집 또는 수동 할당)")]
    [Tooltip("비워두면 Awake에서 자식 오브젝트의 모든 Rigidbody를 자동 수집합니다.")]
    public Rigidbody[] ragdollBodies;

    private EnemyAI _enemyAI;
    private EnemyAnimatorController _animController;
    private Rigidbody _mainRb; // 루트 Rigidbody (EnemyAI에 붙어있는 것)
    private Coroutine _returnCoroutine;

    /// <summary>풀 반납 요청 이벤트. AISpawnManager가 구독합니다.</summary>
    public System.Action<EnemyAI> OnReturnToPool;

    void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
        _animController = GetComponent<EnemyAnimatorController>();
        _mainRb = GetComponent<Rigidbody>();

        if (animator == null && _animController != null)
        {
            animator = _animController.animator;
        }

        // 레그돌 바디 자동 수집 (비어있을 경우)
        if (ragdollBodies == null || ragdollBodies.Length == 0)
        {
            ragdollBodies = GetComponentsInChildren<Rigidbody>();
        }
    }

    /// <summary>
    /// 레그돌 상태 초기화. 풀에서 재활성화될 때 호출.
    /// </summary>
    public void ResetRagdoll()
    {
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }
    }

    /// <summary>
    /// 피격 처리. 샷건/주먹 모두 이 메서드를 통해 레그돌을 활성화합니다.
    /// 
    /// [동작 순서]
    /// 1. AI 이동 완전 정지 (EnemyAI.Disable)
    /// 2. Animator 비활성화
    /// 3. 모든 본에 기존 추적 관성(달려오던 속도) 적용
    /// 4. 피격 부위에만 추가 물리력(힘) 적용
    /// 5. 일정 시간 후 풀 반납
    /// </summary>
    /// <param name="hitPoint">피격 지점 (월드 좌표)</param>
    /// <param name="hitDirection">피격 방향 (총구 → 타격점)</param>
    /// <param name="force">물리력 크기</param>
    /// <param name="hitBone">피격된 뼈의 Rigidbody (null이면 전체에 힘 적용)</param>
    public void ApplyHit(Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone, bool fullRagdoll = true)
    {
        // 이미 사망 상태면 추가 힘만 적용
        if (_enemyAI != null && _enemyAI.CurrentState == EnemyAI.EnemyState.Dead)
        {
            ApplyForceToBone(hitBone, hitDirection, force, hitPoint);
            return;
        }

        if (!fullRagdoll)
        {
            ApplyForceToBone(hitBone, hitDirection, force, hitPoint);
            return;
        }

        ApplyFullRagdoll(hitPoint, hitDirection, force, hitBone);
    }

    private void ApplyFullRagdoll(Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone)
    {
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        // 1. AI 이동 완전 정지
        Vector3 inertiaVelocity = _enemyAI != null ? _enemyAI.CurrentVelocity : Vector3.zero;
        if (_enemyAI != null)
        {
            _enemyAI.Disable();
        }

        // 2. Animator 비활성화
        if (_animController != null) _animController.DisableAnimator();

        // 3. 기존 추적 관성 보존: 달려오던 방향의 속도를 각 본에 적용
        if (ragdollBodies != null)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null || rb == _mainRb) continue;
                if (rb.isKinematic)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
                rb.linearVelocity = inertiaVelocity;
            }
        }

        // 4. 피격 부위에 충격 추가
        // 질량을 무시하고 즉각적인 속도를 부여해 로켓처럼 날아가는 현상 방지
        if (hitBone != null)
        {
            ApplyForceToBone(hitBone, hitDirection, force, hitPoint);
        }

        // 5. 일정 시간 후 풀 반납
        _returnCoroutine = StartCoroutine(ReturnToPoolAfterDelay(ragdollDuration));
    }

    private void ApplyForceToBone(Rigidbody hitBone, Vector3 hitDirection, float force, Vector3 hitPoint)
    {
        if (hitBone == null) return;
        // 질량과 무관한 즉각 속도 부여로 과도한 질량 차이를 완화
        if (hitBone.isKinematic)
        {
            hitBone.isKinematic = false;
            hitBone.useGravity = true;
        }
        hitBone.WakeUp();
        hitBone.AddForceAtPosition(hitDirection * force, hitPoint, ForceMode.VelocityChange);
    }

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 풀 반납 이벤트 발동
        OnReturnToPool?.Invoke(_enemyAI);
    }
}
