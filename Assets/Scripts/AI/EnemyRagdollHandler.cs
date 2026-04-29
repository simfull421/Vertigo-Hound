using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// AI의 레그돌 전환 및 피격 처리를 담당합니다.
/// 
/// [설계 핵심]
/// - 샷건 피격 시: 피격 부위의 Rigidbody에 총 방향 힘 적용
/// - 나머지 부위: 달려오던 관성(CurrentVelocity) 보존
/// - 결과: 비대칭적이고 역동적인 레그돌 연출
/// 
/// [풀 반납 사이클]
/// ApplyHit() → 레그돌 활성화 → 일정 시간 대기 → ReturnToPool 이벤트 발동
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

    [Header("Ragdoll Colliders")]
    [Tooltip("비워두면 Awake에서 자식 오브젝트의 모든 Collider를 자동 수집합니다.")]
    public Collider[] ragdollColliders;

    [Header("Partial Ragdoll")]
    [Tooltip("부분 레그돌 유지 시간 (초)")]
    public float partialRagdollDuration = 0.15f;

    [Header("Hit Reaction Layer")]
    [SerializeField] private int hitLayerIndex = 1;
    [SerializeField] private float hitLayerFadeTime = 0.05f;

    private EnemyAI _enemyAI;
    private EnemyAnimatorController _animController;
    private Rigidbody _mainRb; // 루트 Rigidbody (EnemyAI에 붙어있는 것)
    private Coroutine _returnCoroutine;
    private Coroutine _partialCoroutine;
    private Coroutine _hitLayerCoroutine;
    private readonly Dictionary<Rigidbody, Joint> _jointCache = new Dictionary<Rigidbody, Joint>();

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

        if (ragdollColliders == null || ragdollColliders.Length == 0)
        {
            ragdollColliders = GetComponentsInChildren<Collider>();
        }

        CacheJoints();

        // 초기 상태: 레그돌 비활성화 (Kinematic)
        SetRagdollActive(false);
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

        if (_partialCoroutine != null)
        {
            StopCoroutine(_partialCoroutine);
            _partialCoroutine = null;
        }

        if (_hitLayerCoroutine != null)
        {
            StopCoroutine(_hitLayerCoroutine);
            _hitLayerCoroutine = null;
        }

        SetRagdollActive(false);
    }

    /// <summary>
    /// 피격 처리. 샷건/주먹 모두 이 메서드를 통해 레그돌을 활성화합니다.
    /// 
    /// [동작 순서]
    /// 1. AI 이동 완전 정지 (EnemyAI.Disable)
    /// 2. Animator 비활성화
    /// 3. 모든 본의 Rigidbody를 kinematic 해제 (레그돌 활성화)
    /// 4. 모든 본에 기존 추적 관성(달려오던 속도) 적용
    /// 5. 피격 부위에만 추가 물리력(힘) 적용
    /// 6. 일정 시간 후 풀 반납
    /// </summary>
    /// <param name="hitPoint">피격 지점 (월드 좌표)</param>
    /// <param name="hitDirection">피격 방향 (총구 → 타격점)</param>
    /// <param name="force">물리력 크기</param>
    /// <param name="hitBone">피격된 뼈의 Rigidbody (null이면 전체에 힘 적용)</param>
    public void ApplyHit(Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone, bool fullRagdoll = true)
    {
        // 이미 사망 상태면 추가 힘만 적용
        if (_enemyAI.CurrentState == EnemyAI.EnemyState.Dead)
        {
            ApplyForceToBone(hitBone, hitDirection, force, hitPoint);
            return;
        }

        if (!fullRagdoll)
        {
            if (hitBone == null) return;
            ApplyPartialRagdoll(hitPoint, hitDirection, force, hitBone);
            return;
        }

        ApplyFullRagdoll(hitPoint, hitDirection, force, hitBone);
    }

    private void ApplyFullRagdoll(Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone)
    {
        if (_partialCoroutine != null)
        {
            StopCoroutine(_partialCoroutine);
            _partialCoroutine = null;
        }
        if (_hitLayerCoroutine != null)
        {
            StopCoroutine(_hitLayerCoroutine);
            _hitLayerCoroutine = null;
        }

        // 1. AI 이동 완전 정지
        Vector3 inertiaVelocity = _enemyAI.CurrentVelocity;
        _enemyAI.Disable();

        // 2. Animator 비활성화
        if (_animController != null) _animController.DisableAnimator();

        // 3. 레그돌 활성화 + 관성 보존
        SetRagdollActive(true);

        foreach (var rb in ragdollBodies)
        {
            if (rb == null || rb == _mainRb) continue;

            // 기존 추적 관성 보존: 달려오던 방향의 속도를 각 본에 적용
            rb.linearVelocity = inertiaVelocity;
        }

        // 4. 피격 부위에 샷건 충격 추가
        // 질량을 무시하고 즉각적인 속도를 부여해 로켓처럼 날아가는 현상 방지
        if (hitBone != null)
        {
            ApplyForceToBone(hitBone, hitDirection, force, hitPoint);
        }

        // 5. 일정 시간 후 풀 반납
        _returnCoroutine = StartCoroutine(ReturnToPoolAfterDelay(ragdollDuration));
    }

    private void ApplyPartialRagdoll(Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone)
    {
        if (_partialCoroutine != null)
        {
            StopCoroutine(_partialCoroutine);
            _partialCoroutine = null;
        }

        List<Rigidbody> partialBodies = GetPartialBodies(hitBone);
        SetPartialRagdollActive(partialBodies, true);
        ApplyForceToBone(hitBone, hitDirection, force, hitPoint);

        if (_hitLayerCoroutine != null)
        {
            StopCoroutine(_hitLayerCoroutine);
        }
        _hitLayerCoroutine = StartCoroutine(SuppressHitLayer(partialRagdollDuration));

        _partialCoroutine = StartCoroutine(RecoverPartialRagdoll(partialBodies, partialRagdollDuration));
    }

    /// <summary>
    /// 레그돌 본들의 kinematic 상태를 일괄 전환합니다.
    /// </summary>
    private void SetRagdollActive(bool active)
    {
        foreach (var rb in ragdollBodies)
        {
            if (rb == null || rb == _mainRb) continue; // 루트 Rigidbody는 EnemyAI가 관리
            rb.isKinematic = !active;
            rb.useGravity = active;
        }

        // 레그돌 콜라이더 활성/비활성
        if (ragdollColliders == null) return;
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            // 루트의 CapsuleCollider는 건드리지 않음
            if (col.gameObject == gameObject) continue;
            col.enabled = active;
        }
    }

    private void SetPartialRagdollActive(List<Rigidbody> bodies, bool active)
    {
        if (bodies == null) return;
        HashSet<Rigidbody> bodySet = new HashSet<Rigidbody>(bodies);

        foreach (var rb in bodies)
        {
            if (rb == null || rb == _mainRb) continue;
            rb.isKinematic = !active;
            rb.useGravity = active;
        }

        if (ragdollColliders == null) return;

        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            if (col.gameObject == gameObject) continue;
            if (col.attachedRigidbody != null && bodySet.Contains(col.attachedRigidbody))
            {
                col.enabled = active;
            }
        }
    }

    private List<Rigidbody> GetPartialBodies(Rigidbody hitBone)
    {
        HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
        if (hitBone == null) return new List<Rigidbody>();

        bodies.Add(hitBone);

        if (_jointCache.TryGetValue(hitBone, out Joint joint) && joint.connectedBody != null)
        {
            bodies.Add(joint.connectedBody);
        }

        foreach (var rb in ragdollBodies)
        {
            if (rb == null || rb == _mainRb) continue;
            if (_jointCache.TryGetValue(rb, out Joint rbJoint) && rbJoint.connectedBody == hitBone)
            {
                bodies.Add(rb);
            }
        }

        return new List<Rigidbody>(bodies);
    }

    private IEnumerator RecoverPartialRagdoll(List<Rigidbody> bodies, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_enemyAI.CurrentState == EnemyAI.EnemyState.Dead) yield break;

        SetPartialRagdollActive(bodies, false);
        _partialCoroutine = null;
    }

    private IEnumerator SuppressHitLayer(float duration)
    {
        if (animator == null) yield break;
        if (hitLayerIndex < 0 || hitLayerIndex >= animator.layerCount)
        {
            Debug.LogWarning($"[EnemyRagdollHandler] Hit layer index {hitLayerIndex} is out of range for {gameObject.name}.");
            yield break;
        }

        float startWeight = animator.GetLayerWeight(hitLayerIndex);
        float fadeTime = Mathf.Max(0f, hitLayerFadeTime);

        if (fadeTime <= 0f)
        {
            animator.SetLayerWeight(hitLayerIndex, 0f);
            yield return new WaitForSeconds(duration);
            animator.SetLayerWeight(hitLayerIndex, startWeight);
            _hitLayerCoroutine = null;
            yield break;
        }

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            animator.SetLayerWeight(hitLayerIndex, Mathf.Lerp(startWeight, 0f, t / fadeTime));
            yield return null;
        }

        yield return new WaitForSeconds(duration);

        t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            animator.SetLayerWeight(hitLayerIndex, Mathf.Lerp(0f, startWeight, t / fadeTime));
            yield return null;
        }

        _hitLayerCoroutine = null;
    }

    private void ApplyForceToBone(Rigidbody hitBone, Vector3 hitDirection, float force, Vector3 hitPoint)
    {
        if (hitBone == null) return;
        // 질량과 무관한 즉각 속도 부여로 과도한 질량 차이를 완화
        hitBone.AddForceAtPosition(hitDirection * force, hitPoint, ForceMode.VelocityChange);
    }

    private void CacheJoints()
    {
        _jointCache.Clear();
        if (ragdollBodies == null) return;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            Joint joint = rb.GetComponent<Joint>();
            if (joint != null)
            {
                _jointCache[rb] = joint;
            }
        }
    }

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 풀 반납 이벤트 발동
        OnReturnToPool?.Invoke(_enemyAI);
    }
}
