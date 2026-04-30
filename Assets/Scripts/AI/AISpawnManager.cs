using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

/// <summary>
/// AI 오브젝트 풀링 및 스폰 관리자.
/// 
/// [풀링 사이클]
/// 1. 게임 시작 시 poolSize개의 AI를 미리 생성 → 비활성 상태로 컨테이너에 보관
/// 2. spawnInterval마다 풀에서 비활성 AI를 꺼내 플레이어 주변(시야 밖)에 배치
/// 3. AI 사망 시 OnReturnToPool 이벤트 → 상태 초기화 후 풀로 반환
/// 
/// [스폰 규칙]
/// - 플레이어 시야(forward) 밖에서만 스폰
/// - 최소/최대 스폰 거리 제한
/// - 활성 AI 수가 maxActiveAI 미만일 때만 스폰
/// </summary>
public class AISpawnManager : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("AI 프리팹 (EnemyAI, EnemyAnimatorController, EnemyRagdollHandler, FollowerEntity 포함)")]
    public GameObject aiPrefab;
    [Tooltip("풀에 미리 생성할 AI 수")]
    public int poolSize = 20;
    [Tooltip("비활성 AI를 보관할 컨테이너 (빈 GameObject)")]
    public Transform poolContainer;

    [Header("Spawn Settings")]
    [Tooltip("플레이어 Transform")]
    public Transform playerTransform;
    [Tooltip("최대 스폰 거리")]
    public float spawnRadius = 30f;
    [Tooltip("최소 스폰 거리 (너무 가까이 스폰 방지)")]
    public float minSpawnDistance = 15f;
    [Tooltip("스폰 간격 (초)")]
    public float spawnInterval = 5f;
    [Tooltip("동시 활성 AI 최대 수")]
    public int maxActiveAI = 10;

    [Header("Spawn View Culling")]
    [Tooltip("플레이어 시야 방향 내적 임계값 (이 값보다 크면 시야 내로 판정, 스폰 안 함)")]
    public float viewDotThreshold = 0.5f;

    // 내부 풀 관리
    private Queue<EnemyAI> _pool = new Queue<EnemyAI>();
    private List<EnemyAI> _activeAIs = new List<EnemyAI>();
    private float _spawnTimer;

    void Start()
    {
        InitializePool();
        // [중요] 첫 스폰을 spawnInterval만큼 지연시켜 A* 그래프 스캔 완료를 대기
        _spawnTimer = spawnInterval;
    }

    void Update()
    {
        if (playerTransform == null) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = spawnInterval;
            TrySpawnAI();
        }
    }

    /// <summary>
    /// 풀 초기화. poolSize개의 AI를 미리 생성하여 비활성 상태로 보관.
    /// </summary>
    private void InitializePool()
    {
        if (aiPrefab == null)
        {
            Debug.LogError("[AISpawnManager] aiPrefab이 할당되지 않았습니다!");
            return;
        }

        // 풀 컨테이너가 없으면 자동 생성
        if (poolContainer == null)
        {
            GameObject container = new GameObject("AI_Pool_Container");
            poolContainer = container.transform;
        }

        for (int i = 0; i < poolSize; i++)
        {
            GameObject aiObj = Instantiate(aiPrefab, poolContainer);
            aiObj.name = $"AI_{i}";

            EnemyAI ai = aiObj.GetComponent<EnemyAI>();
            if (ai == null)
            {
                Debug.LogError($"[AISpawnManager] AI 프리팹에 EnemyAI 컴포넌트가 없습니다! ({aiObj.name})");
                Destroy(aiObj);
                continue;
            }

            // [순서 중요] Initialize()를 SetActive(false) 이전에 호출
            // → 컴포넌트 초기화는 오브젝트가 활성 상태일 때 가장 안정적으로 작동합니다.
            ai.Initialize(playerTransform);

            // 레그돌 핸들러에 풀 반납 이벤트 구독
            EnemyRagdollHandler ragdoll = aiObj.GetComponent<EnemyRagdollHandler>();
            if (ragdoll != null)
            {
                ragdoll.OnReturnToPool += ReturnToPool;
            }

            // 초기화 완료 후 비활성화하여 풀에 보관
            aiObj.SetActive(false);
            _pool.Enqueue(ai);
        }

        Debug.Log($"[AISpawnManager] 풀 초기화 완료. {poolSize}개의 AI 생성됨.");
    }

    /// <summary>
    /// 스폰 시도. 조건 충족 시 풀에서 AI를 꺼내 배치.
    /// </summary>
    private void TrySpawnAI()
    {
        // 활성 AI 수 확인
        if (_activeAIs.Count >= maxActiveAI) return;

        // 풀에 남은 AI가 없으면 스킵
        if (_pool.Count == 0) return;

        // A* 그래프가 아직 준비되지 않았으면 스킵
        if (AstarPath.active == null || AstarPath.active.data.graphs == null || AstarPath.active.data.graphs.Length == 0)
        {
            Debug.LogWarning("[AISpawnManager] A* 그래프가 아직 준비되지 않았습니다. 스폰을 건너뜁니다.");
            return;
        }

        // 스폰 위치 계산 (시야 밖 보장 + 네비메시 스냅)
        Vector3 spawnPos;
        if (!TryGetSpawnPosition(out spawnPos))
        {
            Debug.LogWarning("[AISpawnManager] 유효한 스폰 위치를 찾지 못했습니다 (10회 시도 실패).");
            return;
        }

        // 풀에서 꺼내기
        EnemyAI ai = _pool.Dequeue();
        ai.transform.SetParent(null); // 풀 컨테이너에서 분리

        // 활성화
        ai.Activate(spawnPos);
        _activeAIs.Add(ai);
    }

    /// <summary>
    /// AI를 풀로 반납. 사망 시 EnemyRagdollHandler에서 호출됨.
    /// </summary>
    private void ReturnToPool(EnemyAI ai)
    {
        if (ai == null) return;

        // 비활성화 및 상태 초기화
        ai.Deactivate();
        ai.transform.SetParent(poolContainer);
        ai.transform.localPosition = Vector3.zero;

        // 리스트에서 제거 및 풀에 반환
        _activeAIs.Remove(ai);
        _pool.Enqueue(ai);
    }

    /// <summary>
    /// 시야 밖 스폰 위치 계산. 최대 10번 시도 후 실패 시 false 반환.
    /// </summary>
    private bool TryGetSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;

        for (int i = 0; i < 10; i++)
        {
            // 랜덤 방향 (수평면)
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y = 0f;
            if (randomDir.sqrMagnitude < 0.01f) continue;
            randomDir.Normalize();

            // 랜덤 거리
            float dist = Random.Range(minSpawnDistance, spawnRadius);
            Vector3 candidate = playerTransform.position + randomDir * dist;

            // 시야 체크: 플레이어 forward 방향이면 재시도
            float dot = Vector3.Dot(
                playerTransform.forward,
                (candidate - playerTransform.position).normalized
            );
            if (dot > viewDotThreshold) continue; // 시야 앞쪽이면 스킵

          // A* 네비메시 상의 가장 가까운 유효 노드로 좌표 보정
            // → 벽 속, 맵 바깥, 허공에 스폰되는 것을 원천 차단
            if (AstarPath.active != null)
            {
                // randomSpawnPos 대신 candidate를 사용하고 최신 문법 적용
                NNInfo nearestNode = AstarPath.active.GetNearest(candidate, NNConstraint.Walkable);
                if (nearestNode.node != null)
                {
                    candidate = (Vector3)nearestNode.position;
                }
                else
                {
                    continue; // 유효한 노드를 찾지 못하면 이 좌표는 버리고 재시도
                }
            }

            position = candidate;
            return true;
        }

        return false; // 10번 시도 모두 시야 내 → 이번 프레임은 스폰 안 함
    }

    /// <summary>
    /// 외부에서 강제 스폰 (테스트용).
    /// </summary>
    public void ForceSpawn(Vector3 position)
    {
        if (_pool.Count == 0)
        {
            Debug.LogWarning("[AISpawnManager] 풀에 남은 AI가 없습니다!");
            return;
        }

        EnemyAI ai = _pool.Dequeue();
        ai.transform.SetParent(null);
        ai.Activate(position);
        _activeAIs.Add(ai);
    }

    /// <summary>
    /// 모든 활성 AI를 강제로 풀에 반납 (씬 전환 등).
    /// </summary>
    public void ReturnAllToPool()
    {
        for (int i = _activeAIs.Count - 1; i >= 0; i--)
        {
            ReturnToPool(_activeAIs[i]);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        foreach (var ai in _activeAIs)
        {
            if (ai == null) continue;
            EnemyRagdollHandler ragdoll = ai.GetComponent<EnemyRagdollHandler>();
            if (ragdoll != null) ragdoll.OnReturnToPool -= ReturnToPool;
        }
    }
}
