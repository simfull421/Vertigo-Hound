using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

public class AISpawnManager : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject aiPrefab;
    public int poolSize = 20;
    public Transform poolContainer;

    [Header("Spawn Settings")]
    public Transform playerTransform;
    public float spawnRadius = 30f;
    public float minSpawnDistance = 15f;
    public float spawnInterval = 5f;
    public int maxActiveAI = 10;

    [Header("Spawn View Culling")]
    public float viewDotThreshold = 0.5f;

    private Queue<EnemyAI> _pool = new Queue<EnemyAI>();
    private List<EnemyAI> _activeAIs = new List<EnemyAI>();
    private float _spawnTimer;

    void Start()
    {
        InitializePool();
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

    private void InitializePool()
    {
        if (aiPrefab == null) return;

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
            if (ai == null) continue;

            ai.Initialize(playerTransform);

            EnemyRagdollHandler ragdoll = aiObj.GetComponent<EnemyRagdollHandler>();
            if (ragdoll != null) ragdoll.OnReturnToPool += ReturnToPool;

            aiObj.SetActive(false);
            _pool.Enqueue(ai);
        }
    }

    private void TrySpawnAI()
    {
        if (_activeAIs.Count >= maxActiveAI || _pool.Count == 0) return;

        if (AstarPath.active == null || AstarPath.active.data.graphs == null || AstarPath.active.data.graphs.Length == 0) return;

        if (!TryGetSpawnPosition(out Vector3 spawnPos))
        {
            Debug.LogWarning("[AISpawnManager] 스폰 위치 탐색 실패! (시야에 가득 차 있거나 네비메시 범위를 벗어남)");
            return;
        }

        EnemyAI ai = _pool.Dequeue();
        ai.transform.SetParent(null);
        ai.Activate(spawnPos);
        _activeAIs.Add(ai);
    }

    private void ReturnToPool(EnemyAI ai)
    {
        if (ai == null) return;

        ai.Deactivate();
        ai.transform.SetParent(poolContainer);
        ai.transform.localPosition = Vector3.zero;

        _activeAIs.Remove(ai);
        _pool.Enqueue(ai);
    }

    private bool TryGetSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;

        // [수정] 10번은 너무 적을 수 있으므로 30번으로 넉넉하게 변경
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y = 0f;
            if (randomDir.sqrMagnitude < 0.01f) continue;
            randomDir.Normalize();

            float dist = Random.Range(minSpawnDistance, spawnRadius);
            Vector3 candidate = playerTransform.position + randomDir * dist;

            float dot = Vector3.Dot(playerTransform.forward, (candidate - playerTransform.position).normalized);
            if (dot > viewDotThreshold) continue;

            if (AstarPath.active != null)
            {
                // [핵심 수정] 빡빡한 제약 대신 무조건 가장 가까운 네비메시 위를 찾도록 Default 사용
                NNInfo nearestNode = AstarPath.active.GetNearest(candidate, NNConstraint.Default);
                if (nearestNode.node != null)
                {
                    candidate = (Vector3)nearestNode.position;
                }
                else
                {
                    continue; 
                }
            }

            position = candidate;
            return true;
        }

        return false; 
    }

    public void ForceSpawn(Vector3 position)
    {
        if (_pool.Count == 0) return;
        EnemyAI ai = _pool.Dequeue();
        ai.transform.SetParent(null);
        ai.Activate(position);
        _activeAIs.Add(ai);
    }

    public void ReturnAllToPool()
    {
        for (int i = _activeAIs.Count - 1; i >= 0; i--) ReturnToPool(_activeAIs[i]);
    }

    void OnDestroy()
    {
        foreach (var ai in _activeAIs)
        {
            if (ai == null) continue;
            EnemyRagdollHandler ragdoll = ai.GetComponent<EnemyRagdollHandler>();
            if (ragdoll != null) ragdoll.OnReturnToPool -= ReturnToPool;
        }
    }
}