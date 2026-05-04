using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 오직 '사망 시'에만 작동하는 순수 레그돌 핸들러.
/// </summary>
public class EnemyRagdollHandler : MonoBehaviour
{
    public Animator animator;
    [Header("Ragdoll Settings")]
    public float ragdollDuration = 3f;
    public Rigidbody[] ragdollBodies;

    private EnemyAI _enemyAI;
    private EnemyAnimatorController _animController;
    private Rigidbody _mainRb;
    private Collider _mainCollider;
    private Coroutine _returnCoroutine;

    public Action<EnemyAI> OnReturnToPool;

    void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
        _animController = GetComponent<EnemyAnimatorController>();
        _mainRb = GetComponent<Rigidbody>();
        _mainCollider = GetComponent<Collider>();

        if (animator == null && _animController != null) animator = _animController.animator;
        if (ragdollBodies == null || ragdollBodies.Length == 0) ragdollBodies = GetComponentsInChildren<Rigidbody>();

        DisableRagdoll();
    }

    public void ResetRagdoll()
    {
        if (_returnCoroutine != null) StopCoroutine(_returnCoroutine);
        DisableRagdoll();
    }

    private void DisableRagdoll()
    {
        if (ragdollBodies == null) return;
        foreach (var rb in ragdollBodies)
        {
            if (rb == null || rb == _mainRb) continue;
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        if (_mainCollider != null) _mainCollider.enabled = true;
        if (_mainRb != null) _mainRb.isKinematic = false;
    }

    // 이름도 헷갈리지 않게 변경: 오직 죽었을 때만 호출!
    public void ApplyDeathRagdoll(Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone)
    {
        if (_returnCoroutine != null) StopCoroutine(_returnCoroutine);

        if (DataKeyManager.Instance != null && DataKeyManager.Instance.currentKeyHolder == transform)
        {
            DataKeyManager.Instance.DropKeyAt(transform.position + Vector3.up * 0.5f);
        }

        if (_enemyAI != null) _enemyAI.Disable();
        
        if (_mainRb != null) { _mainRb.isKinematic = true; _mainRb.detectCollisions = false; }
        if (_mainCollider != null) _mainCollider.enabled = false;

        if (_animController != null) _animController.DisableAnimator();
        else if (animator != null) animator.enabled = false;

        if (ragdollBodies != null)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null || rb == _mainRb) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        if (hitBone != null)
        {
            hitBone.WakeUp();
            hitBone.AddForceAtPosition(hitDirection * force, hitPoint, ForceMode.Impulse);
        }

        _returnCoroutine = StartCoroutine(ReturnToPoolAfterDelay(ragdollDuration));
    }

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        OnReturnToPool?.Invoke(_enemyAI);
    }

    // [추가] 공중 피격 시 일시적인 레그돌(넉다운) 적용 (사망 X)
    public void ApplyKnockdown(float duration, Vector3 hitPoint, Vector3 hitDirection, float force, Rigidbody hitBone)
    {
        if (_returnCoroutine != null) StopCoroutine(_returnCoroutine);

        if (_enemyAI != null) _enemyAI.NotifyKnockdown();
        
        if (_mainRb != null) { _mainRb.isKinematic = true; _mainRb.detectCollisions = false; }
        if (_mainCollider != null) _mainCollider.enabled = false;

        if (_animController != null) _animController.DisableAnimator();
        else if (animator != null) animator.enabled = false;

        if (ragdollBodies != null)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null || rb == _mainRb) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        if (hitBone != null)
        {
            hitBone.WakeUp();
            hitBone.AddForceAtPosition(hitDirection * force, hitPoint, ForceMode.Impulse);
        }

        _returnCoroutine = StartCoroutine(RecoverFromKnockdownRoutine(duration));
    }

    private IEnumerator RecoverFromKnockdownRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 골반(Hips) 뼈를 찾아 본체 이동
        Transform hips = null;
        if (animator != null) hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        else if (ragdollBodies != null && ragdollBodies.Length > 0) hips = ragdollBodies[0].transform;

        if (hips != null)
        {
            Vector3 hipsPosition = hips.position;
            hipsPosition.y = transform.position.y; // Y축은 기존 땅 높이 유지
            transform.position = hipsPosition;
        }

        DisableRagdoll();
        
        if (_animController != null)
        {
            _animController.animator.enabled = true;
        }
        else if (animator != null)
        {
            animator.enabled = true;
        }

        if (_enemyAI != null) _enemyAI.RecoverFromKnockdown();
    }
}
