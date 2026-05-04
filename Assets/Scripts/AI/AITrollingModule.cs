using UnityEngine;
using System.Collections;
using DG.Tweening; // Local 의도: DOTween 사용
using Cinemachine; // Remote 의도: 카메라 제어 사용

/// <summary>
/// AI가 플레이어를 농락하는 시스템 (키 뺏기 QTE, 패스, 도발 춤추기)
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class AITrollingModule : MonoBehaviour
{
    // QTE UI 및 설정 제거됨 (타임라인으로 대체)
    
    [Header("Key Visuals")]
    public Transform rightHandBone;

    [Header("Pass Settings")]
    public float passTriggerDistance = 5f;
    public float passSearchRadius = 15f;
    public GameObject dummyKeyPrefab;
    [Tooltip("키를 뺏은 직후 패스를 지연할 시간 (초)")]
    public float passDelayAfterSteal = 1.2f;

    [Header("Cinemachine Closeup")]
    [Tooltip("키를 뺏었을 때 얼굴 클로즈업용 카메라")]
    public CinemachineVirtualCamera keyStealCloseupCamera;
    [Tooltip("기본 플레이 카메라")]
    public CinemachineVirtualCamera defaultCamera;
    public float keyStealCloseupDuration = 1.2f;
    public int closeupPriority = 20;

    [Header("Debug")]
    public bool debugForcePlayerHasKey = false;

    private EnemyAI _enemyAI;
    private EnemyAnimatorController _animCtrl;
    private FaceTextureController _faceCtrl;

    private bool _isCinematicTriggered = false;
    private Transform _playerTransform;
    
    // Copilot이 추가한 유효한 변수들 (카메라 및 타이머 제어용)
    private float _passDelayTimer;

    private int _defaultCameraOriginalPriority;
    private int _closeupCameraOriginalPriority;

    void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
        _animCtrl = GetComponent<EnemyAnimatorController>();
        _faceCtrl = GetComponentInChildren<FaceTextureController>();

        if (defaultCamera != null) _defaultCameraOriginalPriority = defaultCamera.Priority;
        if (keyStealCloseupCamera != null) _closeupCameraOriginalPriority = keyStealCloseupCamera.Priority;
    }

    void Update()
    {
        // 1. 패스 유예 타이머 감소
        if (_passDelayTimer > 0f)
        {
            _passDelayTimer -= Time.deltaTime;
        }

        // 2. 키 소지 및 패스 로직 (타임라인 실행 중이 아닐 때만)
        if (!_isCinematicTriggered && DataKeyManager.Instance != null && DataKeyManager.Instance.currentKeyHolder == this.transform)
        {
            if (_playerTransform == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) _playerTransform = pc.transform;
            }

            // 쿨타임이 끝났고 플레이어가 다가오면 패스
            if (_passDelayTimer <= 0f && _playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) < passTriggerDistance)
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

    // Local HEAD의 깔끔한 타임라인 진입 로직 유지
    private void TriggerCinematic(PlayerController pc)
    {
        _isCinematicTriggered = true;
        _playerTransform = pc.transform;

        // [Juice] 역경직 (Hit Stop) 연출
        Time.timeScale = 0.1f;
        DG.Tweening.DOVirtual.DelayedCall(0.15f, () => Time.timeScale = 1f).SetUpdate(true);

        PoundCinematicController cinematicCtrl = pc.GetComponentInChildren<PoundCinematicController>();
        if (cinematicCtrl != null)
        {
            cinematicCtrl.StartPoundCinematic(this.gameObject);
        }
    }

    public void ResetTrigger()
    {
        _isCinematicTriggered = false;
    }

    /// <summary>
    /// [추가] 타임라인(Cinematic) 종료 후, AI가 플레이어의 키를 뺏고 도망갈 때 호출할 퍼블릭 메서드
    /// 타임라인의 Signal이나 이벤트에서 이 함수를 호출해야 함.
    /// </summary>
    public void ExecuteKeyStealAndFlee()
    {
        if (DataKeyManager.Instance != null)
        {
            DataKeyManager.Instance.SetKeyHolder(this.transform, false);
        }
        
        _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        _passDelayTimer = passDelayAfterSteal; // 뺏은 직후 바로 패스하지 못하도록 유예 기간 설정
        TriggerKeyStealCloseup(); // 카메라 클로즈업 연출 시작
    }

    // 시네머신 클로즈업 로직 (동적 타겟팅)
    private void TriggerKeyStealCloseup()
    {
        if (keyStealCloseupCamera == null) return;

        // [핵심] 카메라의 타겟을 현재 이 스크립트가 붙어있는 AI로 동적 할당
        // 더 정밀한 얼굴 클로즈업을 원한다면 this.transform 대신 머리(Head) Bone의 Transform을 캐싱해서 넣을 것.
        keyStealCloseupCamera.Follow = this.transform;
        keyStealCloseupCamera.LookAt = this.transform;

        if (defaultCamera != null)
        {
            int loweredPriority = _defaultCameraOriginalPriority;
            if (loweredPriority >= closeupPriority)
            {
                loweredPriority = closeupPriority - 1;
            }
            defaultCamera.Priority = loweredPriority;
        }
        keyStealCloseupCamera.Priority = closeupPriority;

        DOVirtual.DelayedCall(keyStealCloseupDuration, () =>
        {
            if (keyStealCloseupCamera != null)
            {
                keyStealCloseupCamera.Priority = _closeupCameraOriginalPriority;
            }
            if (defaultCamera != null)
            {
                defaultCamera.Priority = _defaultCameraOriginalPriority;
            }
        }).SetUpdate(true);
    }

    // Local HEAD의 DOTween 기반 패스 로직 유지
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