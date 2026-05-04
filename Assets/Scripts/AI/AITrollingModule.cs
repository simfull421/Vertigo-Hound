using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("키를 뺏은 직후 패스를 지연할 시간 (초)")]
    public float passDelayAfterSteal = 1.2f;
    [Tooltip("랜덤 패스 최소 간격 (초)")]
    public float randomPassMinInterval = 2.5f;
    [Tooltip("랜덤 패스 최대 간격 (초)")]
    public float randomPassMaxInterval = 5f;
    [Tooltip("패스 시 IK LookAt을 오버라이드할 시간 (초)")]
    public float passLookAtOverrideDuration = 1f;

    [Header("Pass VFX")]
    public LineRenderer passLinePrefab;
    public int passLinePoolSize = 3;
    public float passLineDurationMin = 0.1f;
    public float passLineDurationMax = 0.2f;

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
    private Coroutine _closeupCoroutine;
    private int _defaultCameraOriginalPriority;
    private int _closeupCameraOriginalPriority;
    private float _randomPassTimer;
    private readonly Queue<LineRenderer> _passLinePool = new Queue<LineRenderer>();

    void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
        _animCtrl = GetComponent<EnemyAnimatorController>();
        _faceCtrl = GetComponentInChildren<FaceTextureController>();

        if (defaultCamera != null) _defaultCameraOriginalPriority = defaultCamera.Priority;
        if (keyStealCloseupCamera != null) _closeupCameraOriginalPriority = keyStealCloseupCamera.Priority;
        InitializePassLinePool();
        ResetRandomPassTimer();
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

            if (_playerTransform == null) return;

            if (_passDelayTimer <= 0f)
            {
                _randomPassTimer -= Time.deltaTime;
                bool shouldPass = Vector3.Distance(transform.position, _playerTransform.position) < passTriggerDistance;

                if (!shouldPass && _randomPassTimer <= 0f)
                {
                    shouldPass = true;
                }

                if (shouldPass)
                {
                    AttemptPass();
                    ResetRandomPassTimer();
                }
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

        QTECinematicController cinematicCtrl = pc.GetComponentInChildren<QTECinematicController>();
        if (cinematicCtrl != null)
        {
            cinematicCtrl.StartQTEDummyCinematic(this.gameObject);
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
        if (DataKeyManager.Instance != null && DataKeyManager.Instance.isKeyHeldByPlayer)
        {
            DataKeyManager.Instance.SetKeyHolder(this.transform, false);
        }
        else
        {
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
            return;
        }
        
        _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        _passDelayTimer = passDelayAfterSteal; // 뺏은 직후 바로 패스하지 못하도록 유예 기간 설정
        TriggerKeyStealCloseup(); // 카메라 클로즈업 연출 시작
    }

    // Remote에서 가져온 유효한 시네머신 클로즈업 로직
    private void TriggerKeyStealCloseup()
    {
        if (keyStealCloseupCamera == null) return;

        if (_closeupCoroutine != null) StopCoroutine(_closeupCoroutine);

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

        _closeupCoroutine = StartCoroutine(ResetCloseupAfterDelay());
    }

    private IEnumerator ResetCloseupAfterDelay()
    {
        yield return new WaitForSeconds(keyStealCloseupDuration);

        if (keyStealCloseupCamera != null)
        {
            keyStealCloseupCamera.Priority = _closeupCameraOriginalPriority;
        }
        if (defaultCamera != null)
        {
            defaultCamera.Priority = _defaultCameraOriginalPriority;
        }
        _closeupCoroutine = null;
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
            _animCtrl?.ForceLookAtTarget(bestTarget.transform, passLookAtOverrideDuration);
            PassKeyToTarget(bestTarget.transform);

            // 던지는 즉시 도망 상태 유지 (Taunting으로 인한 정지 방지)
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
            if (_faceCtrl != null) _faceCtrl.SetFace(1);
        }
        else
        {
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        }
    }

    private void PassKeyToTarget(Transform targetAI)
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
            targetAnim?.ForceLookAtTarget(transform, passLookAtOverrideDuration);

            EnemyAI targetEnemyAI = targetAI.GetComponent<EnemyAI>();
            if (targetEnemyAI != null) targetEnemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        }

        // 3. 라인 렌더러로 빠르게 패스 연출
        if (targetAI != null)
        {
            LineRenderer passLine = GetPassLineRenderer();
            Vector3 startPos = transform.position + Vector3.up * 1.5f;
            Vector3 endPos = targetAI.position + Vector3.up * 1.5f;
            float duration = Random.Range(passLineDurationMin, passLineDurationMax);

            if (passLine != null)
            {
                StartCoroutine(AnimatePassLine(passLine, startPos, endPos, duration, targetAI));
            }
            else if (DataKeyManager.Instance != null)
            {
                DataKeyManager.Instance.SetKeyHolder(targetAI, false);
            }
        }
    }

    private void InitializePassLinePool()
    {
        if (passLinePrefab == null || passLinePoolSize <= 0) return;

        for (int i = 0; i < passLinePoolSize; i++)
        {
            LineRenderer line = Instantiate(passLinePrefab, transform);
            line.gameObject.SetActive(false);
            _passLinePool.Enqueue(line);
        }
    }

    private LineRenderer GetPassLineRenderer()
    {
        if (_passLinePool.Count == 0) return null;
        LineRenderer line = _passLinePool.Dequeue();
        line.gameObject.SetActive(true);
        return line;
    }

    private void ReleasePassLineRenderer(LineRenderer line)
    {
        if (line == null) return;
        line.gameObject.SetActive(false);
        _passLinePool.Enqueue(line);
    }

    private IEnumerator AnimatePassLine(LineRenderer line, Vector3 startPos, Vector3 endPos, float duration, Transform targetAI)
    {
        if (line == null)
        {
            yield break;
        }

        line.positionCount = 2;
        line.SetPosition(0, startPos);
        line.SetPosition(1, endPos);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float t = elapsed / safeDuration;
            Vector3 headPos = Vector3.Lerp(startPos, endPos, t);
            line.SetPosition(0, headPos);
            line.SetPosition(1, endPos);
            elapsed += Time.deltaTime;
            yield return null;
        }

        line.SetPosition(0, endPos);
        line.SetPosition(1, endPos);
        yield return null;

        ReleasePassLineRenderer(line);

        if (targetAI != null && DataKeyManager.Instance != null)
        {
            DataKeyManager.Instance.SetKeyHolder(targetAI, false);
        }
    }

    private void ResetRandomPassTimer()
    {
        float min = Mathf.Max(0f, randomPassMinInterval);
        float max = Mathf.Max(min, randomPassMaxInterval);
        _randomPassTimer = Random.Range(min, max);
    }
}
