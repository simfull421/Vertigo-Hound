using UnityEngine;
using System.Collections;
using DG.Tweening; // DOTween 추가

/// <summary>
/// AI가 플레이어를 농락하는 시스템 (키 뺏기 QTE, 패스, 도발 춤추기)
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class AITrollingModule : MonoBehaviour
{
    // QTE UI 및 설정 제거됨
    
    [Header("Key Visuals")]
    public Transform rightHandBone;

    [Header("Pass Settings")]
    public float passTriggerDistance = 5f;
    public float passSearchRadius = 15f;
    public GameObject dummyKeyPrefab;

    [Header("Debug")]
    public bool debugForcePlayerHasKey = false;

    private EnemyAI _enemyAI;
    private EnemyAnimatorController _animCtrl;
    private FaceTextureController _faceCtrl;

    private bool _isCinematicTriggered = false;
    private Transform _playerTransform;

    void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
        _animCtrl = GetComponent<EnemyAnimatorController>();
        _faceCtrl = GetComponentInChildren<FaceTextureController>();
    }

    void Update()
    {
        if (!_isCinematicTriggered && DataKeyManager.Instance != null && DataKeyManager.Instance.currentKeyHolder == this.transform)
        {
            // [버그 수정] 패스받은 AI는 _playerTransform이 null이므로 강제 캐싱
            if (_playerTransform == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) _playerTransform = pc.transform;
            }

            if (_playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) < passTriggerDistance)
            {
                AttemptPass();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayerController pc = collision.gameObject.GetComponentInParent<PlayerController>();
        if (pc != null) CheckCollisionWithPlayer(pc);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) CheckCollisionWithPlayer(pc);
    }

    private void CheckCollisionWithPlayer(PlayerController pc)
    {
        if (_isCinematicTriggered || _enemyAI == null || _enemyAI.CurrentState != EnemyAI.EnemyState.Attacking) return;
        
        // 전역 면역 상태 검사
        if (pc.poundingImmunityTimer > 0f) return;

        TriggerCinematic(pc);
    }

    private void TriggerCinematic(PlayerController pc)
    {
        _isCinematicTriggered = true;
        _playerTransform = pc.transform; // 캐싱 보장

        // [Juice] 역경직 (Hit Stop) 연출
        Time.timeScale = 0.1f;
        DG.Tweening.DOVirtual.DelayedCall(0.15f, () => Time.timeScale = 1f).SetUpdate(true);

        QTECinematicController cinematicCtrl = pc.GetComponentInChildren<QTECinematicController>();
        if (cinematicCtrl != null)
        {
            cinematicCtrl.StartQTEDummyCinematic(this.gameObject);
        }
    }

    // 타임라인 연출이 끝난 후 다시 잡을 수 있도록 초기화
    public void ResetTrigger()
    {
        _isCinematicTriggered = false;
    }

    private void AttemptPass()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, passSearchRadius);
        EnemyAI bestTarget = null;
        float maxDist = 0f;

        foreach (var hit in hits)
        {
            EnemyAI ally = hit.GetComponentInParent<EnemyAI>();
            if (ally != null && ally != _enemyAI && ally.CurrentState != EnemyAI.EnemyState.Dead)
            {
                float distToPlayer = Vector3.Distance(ally.transform.position, _playerTransform.position);
                if (distToPlayer > maxDist)
                {
                    maxDist = distToPlayer;
                    bestTarget = ally;
                }
            }
        }

        if (bestTarget != null)
        {
            if (_animCtrl != null && _animCtrl.animator != null) _animCtrl.animator.SetTrigger("TriggerThrow");

            if (dummyKeyPrefab != null)
            {
                PassKeyToTarget(bestTarget.transform, dummyKeyPrefab);
            }
            else
            {
                DataKeyManager.Instance.SetKeyHolder(bestTarget.transform, false);
                bestTarget.SetState(EnemyAI.EnemyState.Fleeing);
            }

            // 던지는 즉시 도망 상태 유지 (Taunting으로 인한 정지 방지)
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
            if (_faceCtrl != null) _faceCtrl.SetFace(1);
        }
        else
        {
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        }
    }

    private void PassKeyToTarget(Transform targetAI, GameObject dummyKeyPrefab)
    {
        // 1. 던지는 애 달리는 상태(Fleeing) 유지
        _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);

        // 2. 받는 애 캐치 애니메이션 즉시 실행 & 도망 상태 세팅
        if (targetAI != null)
        {
            EnemyAnimatorController targetAnim = targetAI.GetComponent<EnemyAnimatorController>();
            if (targetAnim != null && targetAnim.animator != null)
            {
                // 받는 애 상체 마스크용 트리거
                targetAnim.animator.SetTrigger("TriggerCatch");
            }

            EnemyAI targetEnemyAI = targetAI.GetComponent<EnemyAI>();
            if (targetEnemyAI != null) targetEnemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        }

        // 3. DOTween으로 키 날려주기
        GameObject flyingKey = Instantiate(dummyKeyPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
        Vector3 targetPos = targetAI.position + Vector3.up * 1.5f;

        flyingKey.transform.DOJump(targetPos, jumpPower: 3f, numJumps: 1, duration: 0.5f)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                // 도착 후 처리
                if (flyingKey != null) Destroy(flyingKey);

                if (targetAI != null)
                {
                    if (DataKeyManager.Instance != null)
                    {
                        DataKeyManager.Instance.SetKeyHolder(targetAI, false);
                    }
                }
            });
    }
}