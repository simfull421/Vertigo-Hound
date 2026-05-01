using UnityEngine;
using System.Collections;

/// <summary>
/// AI가 플레이어를 농락하는 시스템 (키 뺏기 QTE, 패스, 도발 춤추기)
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class AITrollingModule : MonoBehaviour
{
    [Header("QTE Settings")]
    public float qteTimeLimit = 3f;
    public int requiredSpaceMashes = 8;
    [Tooltip("QTE 동안 화면이 느려지는 정도")]
    public float timeScaleDuringQTE = 0.5f;
    
    [Header("QTE UI")]
    [Tooltip("스페이스바 연타 안내 이미지와 게이지를 포함하는 캔버스 그룹")]
    public CanvasGroup qteUIGroup;
    [Tooltip("스페이스바 연타 진행도를 보여줄 게이지 이미지 (Image Type: Filled)")]
    public UnityEngine.UI.Image qteGaugeImage;
    
    [Header("Pass Settings")]
    [Tooltip("패스를 시도할 플레이어와의 거리")]
    public float passTriggerDistance = 5f;
    [Tooltip("패스 대상 동료를 찾을 최대 반경")]
    public float passSearchRadius = 15f;
    [Tooltip("날아갈 키 프리팹 (가짜 시각 연출용)")]
    public GameObject dummyKeyPrefab;

    [Header("Debug")]
    [Tooltip("체크 시, 플레이어가 키를 가지고 있지 않아도 QTE(파운딩) 연출을 강제로 실행합니다.")]
    public bool debugForcePlayerHasKey = false;

    private EnemyAI _enemyAI;
    private EnemyAnimatorController _animCtrl;
    private FaceTextureController _faceCtrl;

    private bool _isQTEActive = false;
    private float _qteTimer;
    private int _mashCount;
    private Transform _playerTransform;
    private PlayerController _playerController;
    private Transform _playerCameraTransform;
    private float _originalCameraHeight;

    void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
        _animCtrl = GetComponent<EnemyAnimatorController>();
        _faceCtrl = GetComponentInChildren<FaceTextureController>();
    }

    void Update()
    {
        // 1. QTE 로직 (Time.unscaledDeltaTime 사용)
        if (_isQTEActive)
        {
            _qteTimer -= Time.unscaledDeltaTime;

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _mashCount++;
            }

            // UI 게이지 업데이트
            if (qteGaugeImage != null)
            {
                qteGaugeImage.fillAmount = (float)_mashCount / requiredSpaceMashes;
            }

            if (_mashCount >= requiredSpaceMashes)
            {
                EndQTE(true); // 플레이어 방어 성공
            }
            else if (_qteTimer <= 0f)
            {
                EndQTE(false); // 플레이어 방어 실패 -> 키 강탈
            }
        }

        // 2. 패스 로직 (키를 들고 있고, 플레이어가 가까이 오면 패스)
        if (!_isQTEActive && DataKeyManager.Instance != null && DataKeyManager.Instance.currentKeyHolder == this.transform)
        {
            if (_playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) < passTriggerDistance)
            {
                AttemptPass();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayerController pc = collision.gameObject.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            Debug.Log($"[AITrolling] 물리 충돌(OnCollisionEnter)로 플레이어 감지됨! (현재 상태: {(_enemyAI != null ? _enemyAI.CurrentState.ToString() : "Null")})");
            CheckCollisionWithPlayer(pc);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            Debug.Log($"[AITrolling] 트리거(OnTriggerEnter)로 플레이어 감지됨! (현재 상태: {(_enemyAI != null ? _enemyAI.CurrentState.ToString() : "Null")})");
            CheckCollisionWithPlayer(pc);
        }
    }

    private void CheckCollisionWithPlayer(PlayerController pc)
    {
        if (_isQTEActive) return;

        if (_enemyAI == null || _enemyAI.CurrentState != EnemyAI.EnemyState.Attacking)
        {
            return;
        }

        Debug.Log($"[AITrolling] 플레이어 타격 성공! 파운딩 QTE를 시작합니다.");
        TriggerStealQTE(pc.transform);
    }

    /// <summary>
    /// 플레이어와 접촉했을 때 QTE 트리거 (PlayerCollider 등에 충돌 시 호출됨)
    /// </summary>
    public void TriggerStealQTE(Transform playerTransform)
    {
        if (_isQTEActive) return;
        
        bool hasKey = DataKeyManager.Instance != null && DataKeyManager.Instance.isKeyHeldByPlayer;
        if (!hasKey && !debugForcePlayerHasKey)
        {
            Debug.Log($"[AITrolling] 플레이어가 키를 가지고 있지 않아 QTE가 취소되었습니다. (인스펙터의 Debug 옵션으로 강제 실행 가능)");
            return;
        }

        _playerTransform = playerTransform;
        _playerController = playerTransform.GetComponent<PlayerController>();
        
        _isQTEActive = true;
        _qteTimer = qteTimeLimit;
        _mashCount = 0;

        // UI 켜기
        if (qteUIGroup != null) qteUIGroup.alpha = 1f;
        if (qteGaugeImage != null) qteGaugeImage.fillAmount = 0f;

        // 플레이어 조작 임시 비활성화 및 CameraJuiceController 연출 호출
        if (_playerController != null)
        {
            _playerController.enabled = false;
            if (_playerController.juiceController != null)
            {
                _playerController.juiceController.TriggerQTEPounded(this.transform);
            }
        }

        // 슬로우 모션 발동
        Time.timeScale = timeScaleDuringQTE;
        
        // AI 파운딩 애니메이션 재생
        if (_animCtrl != null && _animCtrl.animator != null)
        {
            _animCtrl.animator.SetTrigger("TriggerPounding");
        }
    }

    /// <summary>
    /// 파운딩 애니메이션의 Animation Event에서 호출됩니다. (매개변수로 "Left" 또는 "Right" 전달)
    /// </summary>
    public void OnPoundingHit(string side)
    {
        if (!_isQTEActive) return;

        float rollAmount = side == "Left" ? 10f : -10f;
        if (_playerController != null && _playerController.juiceController != null)
        {
            _playerController.juiceController.TriggerQTEHitShake(rollAmount);
        }
    }

    private void EndQTE(bool playerWon)
    {
        _isQTEActive = false;
        Time.timeScale = 1f; // 시간 원상복구

        if (qteUIGroup != null) qteUIGroup.alpha = 0f;

        // CameraJuiceController에 종료 시그널 전달 (기상 연출 및 도망가는 연출 위임)
        if (_playerController != null && _playerController.juiceController != null)
        {
            _playerController.juiceController.EndQTEPounded(this.transform, playerWon);
        }

        if (playerWon)
        {
            // 방어 성공: AI를 뻥 차서 날림 (넉다운)
            _enemyAI.NotifyKnockdown();
            Rigidbody aiRb = GetComponent<Rigidbody>();
            if (aiRb != null)
            {
                Vector3 pushDir = (transform.position - _playerTransform.position).normalized;
                aiRb.AddForce(pushDir * 20f + Vector3.up * 5f, ForceMode.Impulse);
            }
        }
        else
        {
            // 방어 실패: AI가 키를 뺏고 도망 (Fleeing)
            DataKeyManager.Instance.SetKeyHolder(this.transform, false);
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        }
    }

    private void AttemptPass()
    {
        // 주변 동료 탐색
        Collider[] hits = Physics.OverlapSphere(transform.position, passSearchRadius);
        EnemyAI bestTarget = null;
        float maxDist = 0f;

        foreach (var hit in hits)
        {
            EnemyAI ally = hit.GetComponentInParent<EnemyAI>();
            if (ally != null && ally != _enemyAI && ally.CurrentState != EnemyAI.EnemyState.Dead)
            {
                // 플레이어로부터 가급적 멀리 있는 동료에게 패스
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
            // 던지기 애니메이션
            if (_animCtrl != null && _animCtrl.animator != null)
            {
                _animCtrl.animator.SetTrigger("TriggerThrow");
            }

            if (dummyKeyPrefab != null)
            {
                PassKeyToTarget(bestTarget.transform, dummyKeyPrefab);
            }
            else
            {
                // 프리팹이 없으면 즉시 이전
                DataKeyManager.Instance.SetKeyHolder(bestTarget.transform, false);
                bestTarget.SetState(EnemyAI.EnemyState.Fleeing);
            }

            // 자신은 패스 후 제자리에 서서 조롱(Taunting)
            _enemyAI.SetState(EnemyAI.EnemyState.Taunting);
            if (_faceCtrl != null)
            {
                _faceCtrl.SetFace(1); // 1번: 비웃는 표정 인덱스
            }
        }
        else
        {
            // 동료가 없으면 계속 도망
            _enemyAI.SetState(EnemyAI.EnemyState.Fleeing);
        }
    }

    /// <summary>
    /// 키를 동료에게 패스하는 연출을 시작합니다.
    /// </summary>
    private void PassKeyToTarget(Transform targetAI, GameObject dummyKeyPrefab)
    {
        // 1. 던질 가짜 키(더미) 생성
        GameObject flyingKey = Instantiate(dummyKeyPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
        
        // 2. 타겟 위치 설정 (동료 AI의 가슴/손 높이)
        Vector3 targetPos = targetAI.position + Vector3.up * 1.5f;

        // 3. DOTween 대신 순수 코루틴으로 포물선 이동 실행 (높이 3m, 체공시간 0.5초)
        StartCoroutine(ParabolaMoveRoutine(flyingKey.transform, flyingKey.transform.position, targetPos, targetAI, 3f, 0.5f));
    }

    /// <summary>
    /// 순수 C# 코루틴을 이용한 포물선 구현
    /// </summary>
    private System.Collections.IEnumerator ParabolaMoveRoutine(Transform obj, Vector3 start, Vector3 end, Transform targetAI, float height, float duration)
    {
        float time = 0f;

        while (time < duration)
        {
            if (obj == null) yield break;

            time += Time.deltaTime;
            // t는 0에서 시작해서 duration이 되면 1이 됨
            float t = time / duration; 

            // 1. 시작점과 끝점을 직선으로 이어주는 기본 이동 (Lerp)
            Vector3 linearPos = Vector3.Lerp(start, end, t);

            // 2. Sin 곡선을 이용한 포물선 높이 계산 (t가 0.5일 때 가장 높이 솟음)
            float heightOffset = Mathf.Sin(t * Mathf.PI) * height;

            // 3. 직선 이동 좌표에 높이 좌표를 더해서 최종 위치 적용
            obj.position = linearPos + Vector3.up * heightOffset;

            yield return null; // 다음 프레임까지 대기
        }

        // 도착 처리
        if (obj != null)
        {
            obj.position = end; // 끝점에 정확히 안착
            Destroy(obj.gameObject); // 연출용 더미 키 삭제
            
            // 패스 완료 시 소유권 이전 및 타겟은 도망치기 시작
            if (targetAI != null)
            {
                EnemyAI allyAI = targetAI.GetComponent<EnemyAI>();
                DataKeyManager.Instance.SetKeyHolder(targetAI, false);
                if (allyAI != null) allyAI.SetState(EnemyAI.EnemyState.Fleeing);
            }
        }
    }
}
